import { useQuery } from '@tanstack/react-query';
import { Clock } from 'lucide-react';
import { timeEntriesApi, type TimeEntryDto } from '../../lib/timeEntries/api';

interface Props {
  reparacaoId: string;
}

/**
 * Sprint 360: tempo de bancada. O timer é AUTOMÁTICO — arranca quando a reparação
 * entra em "Em reparação" e pára quando sai (Reparado/Aguarda peça/Cancelado/Entregue).
 * Não há botões manuais. Mostramos o total acumulado + "em curso desde HH:MM" quando
 * activo (hora fixa, sem cronómetro ao segundo → imune a clock drift entre browser/servidor).
 */
export default function ReparacaoTimer({ reparacaoId }: Props) {
  const entriesQuery = useQuery({
    queryKey: ['time-entries', reparacaoId],
    queryFn: () => timeEntriesApi.byReparacao(reparacaoId),
    refetchInterval: 30_000,
  });

  const entries: TimeEntryDto[] = entriesQuery.data ?? [];
  const activa = entries.find((e) => e.endedAt === null);
  const totalMinutosFechados = entries
    .filter((e) => e.duracaoMinutos != null)
    .reduce((sum, e) => sum + (e.duracaoMinutos ?? 0), 0);

  return (
    <section className="rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-700 dark:bg-zinc-900">
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-1.5 text-[10px] uppercase tracking-wide text-zinc-500">
            <Clock size={11} /> Tempo de bancada
          </div>
          <div className="mt-0.5 text-lg font-semibold tabular-nums">
            {formatMinutes(totalMinutosFechados)}
            {activa && (
              <span className="ml-2 text-xs font-normal text-emerald-600 dark:text-emerald-400">
                ● em reparação desde {formatHora(activa.startedAt)}
              </span>
            )}
          </div>
        </div>
      </div>
      {!activa && totalMinutosFechados === 0 && (
        <p className="mt-1 text-[11px] text-zinc-400">
          Conta automaticamente enquanto a reparação estiver em "Em reparação".
        </p>
      )}
      {entries.length > 0 && (
        <details className="mt-2">
          <summary className="cursor-pointer text-[11px] text-zinc-500">Sessões ({entries.length})</summary>
          <ul className="mt-1 divide-y divide-zinc-100 text-[11px] dark:divide-zinc-800">
            {entries.slice(0, 8).map((e) => (
              <li key={e.id} className="flex items-center justify-between py-1">
                <span>{new Date(e.startedAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}</span>
                <span className="tabular-nums text-zinc-600 dark:text-zinc-400">
                  {e.duracaoMinutos != null ? `${e.duracaoMinutos} min` : 'em curso'}
                </span>
              </li>
            ))}
          </ul>
        </details>
      )}
    </section>
  );
}

function formatMinutes(m: number): string {
  if (m <= 0) return '0m';
  const h = Math.floor(m / 60);
  const min = m % 60;
  if (h === 0) return `${min}m`;
  if (min === 0) return `${h}h`;
  return `${h}h ${min.toString().padStart(2, '0')}m`;
}

function formatHora(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });
}
