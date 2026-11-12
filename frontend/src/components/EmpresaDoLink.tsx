import { useEffect, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import { setAccessToken } from '../lib/auth/token';
import { avisarTrocaDeEmpresa, empresaDoSeparador } from '../lib/auth/empresaAtiva';
import type { AuthResponse } from '../lib/auth/types';
import { toast } from '../lib/toast';

/**
 * Sprint 630: os avisos no telemóvel trazem `?empresa=<id>`. Se o separador está noutra empresa (ex.: aviso da
 * Cortex com o Mender em LopesTech), troca de empresa e abre a página pedida — em vez de «not found».
 */
export function EmpresaDoLink() {
  const location = useLocation();
  const navigate = useNavigate();
  const aTrocar = useRef(false);

  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const empresa = params.get('empresa');
    if (!empresa || aTrocar.current) return;
    params.delete('empresa');
    const destino = `${location.pathname}${params.toString() ? `?${params}` : ''}`;
    if (empresa === empresaDoSeparador()) {
      navigate(destino, { replace: true });
      return;
    }
    aTrocar.current = true;
    api.post<AuthResponse>(`/auth/empresas/${empresa}/ativar`)
      .then(({ data }) => {
        setAccessToken(data.accessToken, data.accessTokenExpiresAt);
        avisarTrocaDeEmpresa(data.user.tenantId);
        window.location.assign(destino);
      })
      .catch(() => {
        aTrocar.current = false;
        toast.error('Este aviso é de outra empresa a que não tens acesso.');
        navigate(destino, { replace: true });
      });
  }, [location.pathname, location.search, navigate]);

  return null;
}
