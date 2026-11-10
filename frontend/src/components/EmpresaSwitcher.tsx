import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Building2, Check, ChevronDown } from 'lucide-react';
import { api } from '../lib/api';
import { setAccessToken } from '../lib/auth/token';
import { apiErrorMessage } from '../lib/errors';
import { toast } from '../lib/toast';
import type { AuthResponse } from '../lib/auth/types';

interface EmpresaDisponivel {
  id: string;
  nome: string;
  ativa: boolean;
  principal: boolean;
}

/**
 * Sprint 606: empresa ativa + troca (a mesma conta trabalha na LopesTech e na Cortex Automation).
 * Trocar cria uma sessão nova nessa empresa e recarrega a app — assim nenhum dado em cache da
 * empresa anterior fica no ecrã.
 */
export function EmpresaSwitcher({ className = '', soComVarias = false }: { className?: string; soComVarias?: boolean }) {
  const [aberto, setAberto] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const empresas = useQuery({
    queryKey: ['auth', 'empresas'],
    queryFn: () => api.get<EmpresaDisponivel[]>('/auth/empresas').then((r) => r.data),
    staleTime: 300_000,
  });
  const trocar = useMutation({
    mutationFn: (id: string) => api.post<AuthResponse>(`/auth/empresas/${id}/ativar`).then((r) => r.data),
    onSuccess: (data) => {
      setAccessToken(data.accessToken, data.accessTokenExpiresAt);
      window.location.assign('/');
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });

  useEffect(() => {
    if (!aberto) return;
    const fora = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setAberto(false);
    };
    document.addEventListener('mousedown', fora);
    return () => document.removeEventListener('mousedown', fora);
  }, [aberto]);

  const lista = empresas.data ?? [];
  const ativa = lista.find((e) => e.ativa);
  const varias = lista.length > 1;
  if (soComVarias && !varias) return null;

  return (
    <div className={`relative ${className}`} ref={ref}>
      <button
        type="button"
        onClick={() => varias && setAberto((v) => !v)}
        aria-haspopup={varias ? 'menu' : undefined}
        aria-expanded={varias ? aberto : undefined}
        className="flex h-9 max-w-[14rem] items-center gap-2 rounded-lg border border-zinc-200 bg-white px-3 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-200"
        title={varias ? 'Trocar de empresa' : 'Empresa'}
      >
        <Building2 size={15} className="shrink-0 text-zinc-400" />
        <span className="truncate">{ativa?.nome ?? 'Empresa'}</span>
        {varias && <ChevronDown size={14} className={`shrink-0 text-zinc-400 transition ${aberto ? 'rotate-180' : ''}`} />}
      </button>
      {aberto && (
        <div role="menu" className="absolute left-0 z-30 mt-1 w-64 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
          <p className="border-b border-zinc-100 px-3 py-2 text-[11px] uppercase tracking-wide text-zinc-500 dark:border-zinc-800">Trabalhar em</p>
          {lista.map((e) => (
            <button
              key={e.id}
              type="button"
              role="menuitem"
              disabled={e.ativa || trocar.isPending}
              onClick={() => trocar.mutate(e.id)}
              className="flex min-h-11 w-full items-center justify-between gap-2 px-3 text-left text-sm hover:bg-zinc-50 disabled:cursor-default dark:hover:bg-zinc-800"
            >
              <span className="truncate">{e.nome}</span>
              {e.ativa && <Check size={15} className="shrink-0 text-brand-600" />}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
