import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Wrench } from 'lucide-react';
import { timeEntriesApi } from '../lib/timeEntries/api';

/**
 * Sprint 360: banner global quando há uma reparação em curso (timer automático,
 * ligado ao estado "Em reparação"). Mostra desde quando — hora fixa, sem cronómetro
 * ao segundo (imune a clock drift). Pára sozinho quando a reparação sai de "Em reparação".
 */
export default function ActiveTimerBanner() {
  const active = useQuery({
    queryKey: ['time-entries-active'],
    queryFn: () => timeEntriesApi.active(),
    refetchInterval: 30_000,
  });

  if (!active.data) return null;

  const numero = `#${active.data.reparacaoNumero.toString().padStart(5, '0')}`;
  const desde = new Date(active.data.startedAt).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' });

  return (
    <div className="flex items-center gap-2 border-b border-emerald-300/40 bg-emerald-50 px-4 py-1.5 text-xs text-emerald-900 dark:border-emerald-800/40 dark:bg-emerald-950/40 dark:text-emerald-100">
      <Wrench size={14} className="animate-pulse" />
      <span>
        Reparação{' '}
        <Link to={`/reparacoes/${active.data.reparacaoId}`} className="font-semibold underline-offset-2 hover:underline">
          {numero}
        </Link>{' '}
        em reparação desde {desde}
      </span>
    </div>
  );
}
