import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { timeEntriesApi, type TimeStatsRow } from '../../lib/timeEntries/api';

/**
 * Sprint 350 (Doc 83 Pillar 6): relatório produtividade Admin.
 * Tabela de tempo trackeado por técnico no intervalo escolhido.
 */
export default function RelatorioProdutividade() {
  // Default: mês corrente até agora.
  const today = useMemo(() => new Date(), []);
  const startOfMonth = useMemo(() => {
    const d = new Date(today.getFullYear(), today.getMonth(), 1);
    return d.toISOString().slice(0, 10);
  }, [today]);
  const endOfToday = useMemo(() => {
    const d = new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1);
    return d.toISOString().slice(0, 10);
  }, [today]);

  const [from, setFrom] = useState(startOfMonth);
  const [to, setTo] = useState(endOfToday);

  const stats = useQuery({
    queryKey: ['time-stats', from, to],
    queryFn: () => timeEntriesApi.stats(new Date(from).toISOString(), new Date(to).toISOString()),
  });

  const totalMinutos = (stats.data ?? []).reduce((s, r) => s + r.totalMinutos, 0);
  const totalSessoes = (stats.data ?? []).reduce((s, r) => s + r.sessoes, 0);

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-xl font-semibold">Produtividade</h1>
        <p className="text-sm text-zinc-500">Tempo trackeado por técnico (apenas entries fechadas).</p>
      </header>

      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-700 dark:bg-zinc-900">
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-zinc-600 dark:text-zinc-400">De</span>
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)}
            className="rounded border border-zinc-300 px-2 py-1 dark:border-zinc-700 dark:bg-zinc-800" />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-zinc-600 dark:text-zinc-400">Até (exclusive)</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)}
            className="rounded border border-zinc-300 px-2 py-1 dark:border-zinc-700 dark:bg-zinc-800" />
        </label>
        <div className="ml-auto text-right text-xs text-zinc-500">
          <div><strong>{formatMinutes(totalMinutos)}</strong> totais</div>
          <div>{totalSessoes} sessões</div>
        </div>
      </div>

      <div className="overflow-x-auto rounded-lg border border-zinc-200 dark:border-zinc-700">
        <table className="w-full text-sm">
          <thead className="bg-zinc-50 dark:bg-zinc-900">
            <tr className="text-left">
              <th className="px-3 py-2 font-medium">Técnico</th>
              <th className="px-3 py-2 font-medium text-right">Tempo</th>
              <th className="px-3 py-2 font-medium text-right">Sessões</th>
              <th className="px-3 py-2 font-medium text-right">Reparações</th>
              <th className="px-3 py-2 font-medium text-right">Média/sessão</th>
            </tr>
          </thead>
          <tbody>
            {stats.isLoading && <tr><td colSpan={5} className="px-3 py-6 text-center text-zinc-500">A carregar…</td></tr>}
            {stats.data?.length === 0 && <tr><td colSpan={5} className="px-3 py-6 text-center text-zinc-500">Sem registos no intervalo.</td></tr>}
            {(stats.data ?? []).map((row: TimeStatsRow & { displayName: string }) => (
              <tr key={row.userId} className="border-t border-zinc-100 dark:border-zinc-800">
                <td className="px-3 py-2">{row.displayName}</td>
                <td className="px-3 py-2 text-right tabular-nums">{formatMinutes(row.totalMinutos)}</td>
                <td className="px-3 py-2 text-right tabular-nums">{row.sessoes}</td>
                <td className="px-3 py-2 text-right tabular-nums">{row.reparacoes}</td>
                <td className="px-3 py-2 text-right tabular-nums">
                  {row.sessoes > 0 ? `${Math.round(row.totalMinutos / row.sessoes)} min` : '—'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function formatMinutes(m: number): string {
  const h = Math.floor(m / 60);
  const min = m % 60;
  if (h === 0) return `${min}m`;
  if (min === 0) return `${h}h`;
  return `${h}h ${min.toString().padStart(2, '0')}m`;
}
