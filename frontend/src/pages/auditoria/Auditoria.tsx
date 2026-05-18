import { useState } from 'react';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { Search, ScrollText } from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonRow, StatusBadge } from '../../components/ui';
import { auditApi } from '../../lib/audit/api';
import { AUDIT_ACTION_LABEL } from '../../lib/audit/types';
import { formatDate } from '../../lib/money';

const PAGE_SIZE = 50;

export default function Auditoria() {
  const [entityType, setEntityType] = useState('');
  const [entityId, setEntityId] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);

  const list = useQuery({
    queryKey: ['audit', entityType, entityId, from, to, page],
    queryFn: () => auditApi.list({ entityType, entityId, from, to, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / PAGE_SIZE));

  const hasFilters = Boolean(entityType || entityId || from || to);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Auditoria"
        description="Registo imutável de todas as operações de escrita — quem fez o quê e quando. Útil para RGPD e investigação de incidentes."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'evento registado' : 'eventos registados'}</span>}
      />

      <div className="grid gap-2 md:grid-cols-[160px_1fr_150px_150px_auto]">
        <input value={entityType} onChange={(e) => { setEntityType(e.target.value); setPage(1); }} placeholder="Entidade ex: Cliente" className={inputCls} />
        <input value={entityId} onChange={(e) => { setEntityId(e.target.value); setPage(1); }} placeholder="EntityId (Guid)" className={inputCls} />
        <input type="date" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} className={inputCls} />
        <input type="date" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} className={inputCls} />
        <Button type="button" variant="secondary" leftIcon={<Search size={15} />} onClick={() => list.refetch()}>Filtrar</Button>
      </div>

      <section className="overflow-x-auto rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <table className="min-w-full text-sm">
          <thead className="bg-zinc-50 text-left text-xs text-zinc-500 dark:bg-zinc-950">
            <tr>
              <th className="px-3 py-2">Data</th>
              <th className="px-3 py-2">Acção</th>
              <th className="px-3 py-2">Entidade</th>
              <th className="px-3 py-2">Utilizador</th>
              <th className="px-3 py-2">IP</th>
              <th className="px-3 py-2">Alterações</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {items.map((entry) => (
              <tr key={entry.id} className="align-top hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                <td className="whitespace-nowrap px-3 py-2 text-xs text-zinc-500">{formatDate(entry.createdAt)}</td>
                <td className="px-3 py-2"><StatusBadge tone={entry.action === 3 ? 'rose' : entry.action === 5 ? 'blue' : 'zinc'}>{AUDIT_ACTION_LABEL[entry.action] ?? entry.action}</StatusBadge></td>
                <td className="px-3 py-2">
                  <div className="font-medium">{entry.entityType}</div>
                  <div className="font-mono text-[11px] text-zinc-500">{entry.entityId ?? '—'}</div>
                </td>
                <td className="px-3 py-2 font-mono text-[11px] text-zinc-500">{entry.appUserId ?? '—'}</td>
                <td className="px-3 py-2 text-xs text-zinc-500">{entry.ipAddress ?? '—'}</td>
                <td className="max-w-xl px-3 py-2">
                  {entry.changesJson ? (
                    <details>
                      <summary className="cursor-pointer text-xs text-brand-600">ver JSON</summary>
                      <pre className="mt-2 max-h-48 overflow-auto rounded-lg bg-zinc-950 p-2 text-[11px] text-zinc-100">{prettyJson(entry.changesJson)}</pre>
                    </details>
                  ) : (
                    <span className="text-xs text-zinc-400">—</span>
                  )}
                </td>
              </tr>
            ))}
            {items.length === 0 && !list.isLoading && (
              <tr>
                <td colSpan={6} className="px-3 py-2">
                  <EmptyState
                    icon={hasFilters ? Search : ScrollText}
                    title={hasFilters ? 'Sem eventos para estes filtros' : 'Nenhum evento registado'}
                    description={hasFilters
                      ? 'Ajusta os filtros — entidade, data ou ID — ou limpa-os para ver tudo.'
                      : 'O audit log captura automaticamente operações de escrita (criar, editar, apagar). Vai aparecer aqui assim que houver actividade.'}
                    compact
                  />
                </td>
              </tr>
            )}
            {list.isLoading && (
              <>
                {Array.from({ length: 3 }).map((_, i) => (
                  <tr key={`skeleton-${i}`}>
                    <td colSpan={6} className="px-3 py-1">
                      <SkeletonRow columns={6} />
                    </td>
                  </tr>
                ))}
              </>
            )}
          </tbody>
        </table>
      </section>

      {lastPage > 1 && (
        <div className="flex items-center justify-between text-xs text-zinc-500">
          <Button type="button" variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
          <span>{page} / {lastPage}</span>
          <Button type="button" variant="ghost" size="sm" disabled={page >= lastPage} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
        </div>
      )}
    </div>
  );
}

function prettyJson(raw: string) {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

const inputCls = 'block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';
