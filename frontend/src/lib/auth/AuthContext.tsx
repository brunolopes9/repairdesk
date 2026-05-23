import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import axios from 'axios';
import { api } from '../api';
import { clearAccessToken, setAccessToken } from './token';
import type { AuthResponse, ChangePasswordRequest, LoginRequest, UserInfo } from './types';

type Status = 'loading' | 'authenticated' | 'anonymous';

interface AuthState {
  status: Status;
  user: UserInfo | null;
}

interface AuthContextValue extends AuthState {
  login: (req: LoginRequest) => Promise<void>;
  changePassword: (req: ChangePasswordRequest) => Promise<void>;
  logout: () => Promise<void>;
  hasRole: (role: string) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading', user: null });

  const setAuth = useCallback((data: AuthResponse) => {
    setAccessToken(data.accessToken, data.accessTokenExpiresAt);
    setState({ status: 'authenticated', user: data.user });
  }, []);

  const setAnon = useCallback(() => {
    clearAccessToken();
    setState({ status: 'anonymous', user: null });
  }, []);

  const bootstrap = useCallback(async () => {
    try {
      const { data } = await axios.post<AuthResponse>('/api/auth/refresh', null, {
        withCredentials: true,
        timeout: 15000,
      });
      setAuth(data);
    } catch {
      setAnon();
    }
  }, [setAuth, setAnon]);

  useEffect(() => {
    bootstrap();
  }, [bootstrap]);

  useEffect(() => {
    const onUnauthorized = () => setAnon();
    window.addEventListener('auth:unauthorized', onUnauthorized);
    return () => window.removeEventListener('auth:unauthorized', onUnauthorized);
  }, [setAnon]);

  const login = useCallback(
    async (req: LoginRequest) => {
      const { data } = await api.post<AuthResponse>('/auth/login', req);
      setAuth(data);
    },
    [setAuth],
  );

  const changePassword = useCallback(
    async (req: ChangePasswordRequest) => {
      const { data } = await api.post<AuthResponse>('/auth/change-password', req);
      setAuth(data);
    },
    [setAuth],
  );

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout');
    } catch {
      /* ignore */
    } finally {
      setAnon();
    }
  }, [setAnon]);

  const hasRole = useCallback(
    (role: string) => state.user?.roles.includes(role) ?? false,
    [state.user],
  );

  const value = useMemo<AuthContextValue>(
    () => ({ ...state, login, changePassword, logout, hasRole }),
    [state, login, changePassword, logout, hasRole],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}
