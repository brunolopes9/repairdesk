import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pause, Timer } from 'lucide-react';
import { timeEntriesApi } from '../lib/timeEntries/api';
import { toast } from '../lib/toast';

/**
 * Sprint 351 (Doc 83 Pillar 6): banner global mostrado em todas as páginas
 * quando o utilizador tem um timer activo. Evita esquecer que está a contar
 * tempo enquanto se distrai noutra parte da app.
 *
 * Sondagem leve (30s) — não usar refetchInterval mais agressivo porque o
 * timer só muda quando o user carrega Start/Stop.
 */
export default function ActiveTimerBanner() {
  const qc = useQueryClient();
  const [now, setNow] = useState<Date>(new Date());

  const active = useQuery({
    queryKey: ['time-entries-active'],
    queryFn: () => timeEntriesApi.active(),
    refetchInterval: 30_000,
  });

  // Tick por segundo só quando há activo, evita re-render desnecessário.
  useEffect(() => {
    if (!active.data) return;
    const id = window.setInterval(() => setNow(new Date()), 1_000);
    return () => window.clearInterval(id);
  }, [active.data]);

  const stopMut = useMutation({
    mutationFn: (id: string) => timeEntriesApi.stop(id),
    onSuccess: (_, id) => {
      qc.invalidateQueries({ queryKey: ['time-entries-active'] });
      // Refetch da lista da reparação que tinha o timer activo (se estiver aberta).
      const repId = active.data?.reparacaoId;
      if (repId) qc.invalidateQueries({ queryKey: ['time-entries', repId] });
      toast.success('Timer parado.');
      void id;
    },
    onError: (err) => toast.fromError(err, 'Não foi possível parar timer.'),
  });

  if (!active.data) return null;

  const elapsedSec = Math.max(0, Math.floor((now.getTime() - new Date(active.data.startedAt).getTime()) / 1000));
  const numero = `#${active.data.reparacaoNumero.toString().padStart(5, '0')}`;

  return (
    <div className="flex items-center gap-3 border-b border-emerald-300/40 bg-emerald-50 px-4 py-1.5 text-xs text-emerald-900 dark:border-emerald-800/40 dark:bg-emerald-950/40 dark:text-emerald-100">
      <Timer size={14} className="animate-pulse" />
      <span>
        Timer em curso na reparação{' '}
        <Link to={`/reparacoes/${active.data.reparacaoId}`} className="font-semibold underline-offset-2 hover:underline">
          {numero}
        </Link>
      </span>
      <span className="tabular-nums font-mono">{formatElapsed(elapsedSec)}</span>
      <button
        type="button"
        onClick={() => stopMut.mutate(active.data!.id)}
        disabled={stopMut.isPending}
        className="ml-auto inline-flex items-center gap-1 rounded bg-emerald-700 px-2 py-0.5 text-[11px] font-medium text-white hover:bg-emerald-800 disabled:opacity-50"
      >
        <Pause size={11} /> Parar
      </button>
    </div>
  );
}

function formatElapsed(totalSec: number): string {
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;
  if (h > 0) return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
}
