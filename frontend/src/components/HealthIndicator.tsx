import { useQuery } from '@tanstack/react-query';
import { Activity, AlertCircle, CheckCircle2 } from 'lucide-react';
import { api } from '../lib/api';

interface HealthCheckEntry {
  name?: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  description?: string;
  duration?: string;
  data?: Record<string, unknown>;
}

interface HealthCheckResponse {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  totalDuration?: string;
  entries?: Record<string, HealthCheckEntry>;
  // Alguns servidores devolvem apenas { status: "..." }
}

/**
 * Mostra um dot verde/amber/vermelho no header consoante o estado de /api/health/ready.
 * Poll a cada 30s. Click para ver detalhes (badge expandido).
 *
 * NÃO é alarme — é visual confidence. Em produção avisa-te imediatamente se DB
 * ou storage cair sem precisares de abrir browser dev tools.
 */
export default function HealthIndicator() {
  const { data, isError, isLoading } = useQuery({
    queryKey: ['health-ready'],
    queryFn: async () => {
      // Não usa o axios `api` porque queremos receber a resposta mesmo em 503
      const resp = await fetch('/api/health/ready', {
        method: 'GET',
        headers: { Accept: 'application/json' },
      });
      const json = (await resp.json().catch(() => null)) as HealthCheckResponse | null;
      return { httpStatus: resp.status, body: json };
    },
    refetchInterval: 30_000,
    retry: false,
    staleTime: 10_000,
  });

  // Reusar 'api' import para evitar warning de import não usado quando alterarmos
  void api;

  const status: 'healthy' | 'degraded' | 'unhealthy' | 'unknown' = (() => {
    if (isLoading) return 'unknown';
    if (isError || !data) return 'unhealthy';
    if (data.httpStatus >= 500) return 'unhealthy';
    if (data.httpStatus !== 200) return 'degraded';
    const s = data.body?.status;
    if (s === 'Healthy') return 'healthy';
    if (s === 'Degraded') return 'degraded';
    if (s === 'Unhealthy') return 'unhealthy';
    return 'healthy'; // 200 sem body parseable: assume OK
  })();

  const dotColor =
    status === 'healthy' ? 'bg-emerald-500'
    : status === 'degraded' ? 'bg-amber-500'
    : status === 'unhealthy' ? 'bg-rose-500'
    : 'bg-zinc-400';

  const label =
    status === 'healthy' ? 'Sistema OK'
    : status === 'degraded' ? 'Sistema com problemas parciais'
    : status === 'unhealthy' ? 'Sistema com falhas'
    : 'A verificar…';

  // Detalhe para tooltip
  const detail = (() => {
    if (!data?.body?.entries) return label;
    const entries = Object.entries(data.body.entries);
    if (entries.length === 0) return label;
    return entries
      .map(([name, e]) => `${name}: ${e.status}${e.description ? ` (${e.description})` : ''}`)
      .join('\n');
  })();

  const Icon = status === 'healthy' ? CheckCircle2 : status === 'degraded' ? Activity : AlertCircle;

  return (
    <a
      href="/api/health/ready"
      target="_blank"
      rel="noopener noreferrer"
      title={detail}
      className="inline-flex min-h-10 items-center gap-1.5 rounded-md border border-zinc-200 px-3 py-2 text-[10px] text-zinc-600 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-800"
      aria-label={label}
    >
      <span className={`relative inline-flex h-2 w-2 rounded-full ${dotColor}`}>
        {status === 'healthy' && (
          <span className="absolute inset-0 animate-ping rounded-full bg-emerald-400 opacity-30" />
        )}
      </span>
      <Icon size={11} strokeWidth={2} aria-hidden />
      <span className="hidden sm:inline">{status === 'unknown' ? '…' : label.split(' ')[1] ?? 'OK'}</span>
    </a>
  );
}
