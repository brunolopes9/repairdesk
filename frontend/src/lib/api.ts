import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { clearAccessToken, getAccessToken, setAccessToken } from './auth/token';
import type { AuthResponse } from './auth/types';
import { empresaDoSeparador, recarregarNaEmpresaAtual } from './auth/empresaAtiva';

export const api = axios.create({
  baseURL: '/api',
  withCredentials: true,
  timeout: 15000,
});

api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});

let refreshPromise: Promise<AuthResponse> | null = null;

async function refresh(): Promise<AuthResponse> {
  const { data } = await axios.post<AuthResponse>('/api/auth/refresh', null, {
    withCredentials: true,
    timeout: 15000,
  });
  const anterior = empresaDoSeparador();
  if (anterior && data.user.tenantId !== anterior) {
    // Sprint 607: a sessão mudou de empresa noutro separador. Não repetir o pedido (podia gravar
    // dados na empresa errada) — recarregar este separador na empresa atual da sessão.
    recarregarNaEmpresaAtual();
    throw new Error('empresa_mudou');
  }
  setAccessToken(data.accessToken, data.accessTokenExpiresAt);
  return data;
}

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
  _skipAuthRefresh?: boolean;
}

api.interceptors.response.use(
  (r) => r,
  async (err: AxiosError) => {
    const original = err.config as RetriableConfig | undefined;
    const status = err.response?.status;

    if (status !== 401 || !original || original._retry || original._skipAuthRefresh) {
      if (status === 401) {
        clearAccessToken();
        window.dispatchEvent(new CustomEvent('auth:unauthorized'));
      }
      return Promise.reject(err);
    }

    original._retry = true;
    try {
      refreshPromise ??= refresh().finally(() => {
        refreshPromise = null;
      });
      await refreshPromise;
      return api(original);
    } catch (refreshErr) {
      if (refreshErr instanceof Error && refreshErr.message === 'empresa_mudou') return Promise.reject(refreshErr);
      clearAccessToken();
      window.dispatchEvent(new CustomEvent('auth:unauthorized'));
      return Promise.reject(refreshErr);
    }
  },
);
