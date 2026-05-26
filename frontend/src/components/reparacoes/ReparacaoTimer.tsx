import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pause, Play } from 'lucide-react';
import { timeEntriesApi, type TimeEntryDto } from '../../lib/timeEntries/api';
import { toast } from '../../lib/toast';

interface Props {
  reparacaoId: string;
}

/**
 * Sprint 349 (Doc 83 Pillar 6): mini-widget timer numa reparação. Mostra
 * total já trackeado + botão Play/Pause + lista das últimas sessões.
 */
export default function ReparacaoTimer({ reparacaoId }: Props) {
  const qc = useQueryClient();
  const [now, setNow] = useState<Date>(new Date());

  const entriesQuery = useQuery({
    queryKey: ['time-entries', reparacaoId],
    queryFn: () => timeEntriesApi.byReparacao(reparacaoId),
    refetchInterval: 60_000,
  });

  const activeQuery = useQuery({
    queryKey: ['time-entries-active'],
    queryFn: () => timeEntriesApi.active(),
    refetchInterval: 30_000,
  });

  const entries: TimeEntryDto[] = entriesQuery.data ?? [];
  const active = activeQuery.data;
  const isActiveOnThis = active && active.reparacaoId === reparacaoId;
  const isActiveOnOther = active && active.reparacaoId !== reparacaoId;

  // Re-render por segundo enquanto está activo nesta reparação para mostrar contador.
  useEffect(() => {
    if (!isActiveOnThis) return;
    const id = window.setInterval(() => setNow(new Date()), 1_000);
    return () => window.clearInterval(id);
  }, [isActiveOnThis]);

  const startMut = useMutation({
    mutationFn: () => timeEntriesApi.start(reparacaoId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['time-entries', reparacaoId] });
      qc.invalidateQueries({ queryKey: ['time-entries-active'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível iniciar timer.'),
  });

  const stopMut = useMutation({
    mutationFn: (id: string) => timeEntriesApi.stop(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['time-entries', reparacaoId] });
      qc.invalidateQueries({ queryKey: ['time-entries-active'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível parar timer.'),
  });

  const totalMinutosFechados = entries
    .filter((e) => e.duracaoMinutos != null)
    .reduce((sum, e) => sum + (e.duracaoMinutos ?? 0), 0);

  let runningSeconds = 0;
  if (isActiveOnThis) {
    runningSeconds = Math.max(0, Math.floor((now.getTime() - new Date(active!.startedAt).getTime()) / 1000));
  }
  const totalSegundos = totalMinutosFechados * 60 + runningSeconds;

  return (
    <section className="rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-700 dark:bg-zinc-900">
      <div className="flex items-center justify-between">
        <div>
          <div className="text-[10px] uppercase tracking-wide text-zinc-500">Tempo trackeado</div>
          <div className="text-lg font-semibold tabular-nums">
            {formatDuration(totalSegundos)}
            {isActiveOnThis && <span className="ml-2 text-xs font-normal text-emerald-600 dark:text-emerald-400">● em curso</span>}
          </div>
        </div>
        <div>
          {isActiveOnThis ? (
            <button
              type="button"
              onClick={() => stopMut.mutate(active!.id)}
              disabled={stopMut.isPending}
              className="inline-flex items-center gap-1 rounded-lg bg-rose-600 px-3 py-1.5 text-xs font-medium text-white transition hover:bg-rose-700 disabled:opacity-50"
            >
              <Pause size={12} /> Parar
            </button>
          ) : (
            <button
              type="button"
              onClick={() => startMut.mutate()}
              disabled={startMut.isPending}
              className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white transition hover:bg-emerald-700 disabled:opacity-50"
              title={isActiveOnOther ? `Vai parar o timer activo na rep. #${active!.reparacaoNumero.toString().padStart(5, '0')}` : 'Iniciar timer'}
            >
              <Play size={12} /> {isActiveOnOther ? 'Iniciar (parar outro)' : 'Iniciar'}
            </button>
          )}
        </div>
      </div>
      {entries.length > 0 && (
        <details className="mt-2">
          <summary className="cursor-pointer text-[11px] text-zinc-500">Histórico ({entries.length})</summary>
          <ul className="mt-1 divide-y divide-zinc-100 text-[11px] dark:divide-zinc-800">
            {entries.slice(0, 5).map((e) => (
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

function formatDuration(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = totalSeconds % 60;
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m ${s.toString().padStart(2, '0')}s`;
  return `${s}s`;
}
