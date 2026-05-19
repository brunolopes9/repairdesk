import { useMemo, useState } from 'react';
import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { Download, FileText, Search, ScrollText, X } from 'lucide-react';
import Modal from '../../components/Modal';
import JsonViewer from '../../components/JsonViewer';
import { Button, EmptyState, PageHeader, SkeletonRow, StatusBadge } from '../../components/ui';
import { auditApi } from '../../lib/audit/api';
import { AUDIT_ACTION_LABEL, type AuditEntry, type AuditFilters } from '../../lib/audit/types';
import { downloadFile, openPdfInNewTab } from '../../lib/downloadPdf';
import { formatDate } from '../../lib/money';

const PAGE_SIZE = 50;
type Preset = 'today' | 'yesterday' | '7d' | '30d' | 'custom';

export default function Auditoria() {
  const [preset, setPreset] = useState<Preset>('7d');
  const [entityTypes, setEntityTypes] = useState<string[]>([]);
  const [userIds, setUserIds] = useState<string[]>([]);
  const [serviceApiKeyIds, setServiceApiKeyIds] = useState<string[]>([]);
  const [actions, setActions] = useState<number[]>([]);
  const [search, setSearch] = useState('');
  const [from, setFrom] = useState(() => presetRange('7d').from);
  const [to, setTo] = useState(() => presetRange('7d').to);
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<AuditEntry | null>(null);

  const options = useQuery({ queryKey: ['audit-filters'], queryFn: () => auditApi.filters(), staleTime: 60_000 });
  const filters = useMemo<AuditFilters>(() => ({ entityTypes, userIds, serviceApiKeyIds, actions, search, from, to, page, pageSize: PAGE_SIZE }), [actions, entityTypes, from, page, search, serviceApiKeyIds, to, userIds]);
  const exportFilters = useMemo<AuditFilters>(() => ({ entityTypes, userIds, serviceApiKeyIds, actions, search, from, to, page: 1, pageSize: PAGE_SIZE }), [actions, entityTypes, from, search, serviceApiKeyIds, to, userIds]);
  const list = useQuery({ queryKey: ['audit', filters], queryFn: () => auditApi.list(filters), placeholderData: keepPreviousData });

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const allActions = options.data?.actions.length ? options.data.actions : [0, 1, 2, 3, 4, 5];
  const hasFilters = Boolean(entityTypes.length || userIds.length || serviceApiKeyIds.length || actions.length || search || from || to);

  function applyPreset(next: Preset) {
    setPreset(next);
    setPage(1);
    if (next === 'custom') return;
    const range = presetRange(next);
    setFrom(range.from);
    setTo(range.to);
  }

  function clearFilters() {
    setPreset('30d');
    const range = presetRange('30d');
    setFrom(range.from);
    setTo(range.to);
    setEntityTypes([]);
    setUserIds([]);
    setServiceApiKeyIds([]);
    setActions([]);
    setSearch('');
    setPage(1);
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Auditoria"
        description="Registo imutavel de operacoes, acessos e exportacoes para investigacao operacional e RGPD."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'evento' : 'eventos'}</span>}
        actions={
          <>
            <Button type="button" variant="secondary" leftIcon={<Download size={15} />} onClick={() => downloadFile(auditApi.exportCsvPath(exportFilters), 'auditoria.csv')}>Exportar CSV</Button>
            <Button type="button" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(auditApi.exportPdfPath(exportFilters))}>Exportar PDF</Button>
          </>
        }
      />

      <section className="grid grid-cols-1 gap-4 lg:grid-cols-[280px_1fr]">
        <aside className="space-y-4 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">Filtros</h2>
            {hasFilters && (
              <button type="button" onClick={clearFilters} className="rounded-md p-1 text-zinc-500 hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800" aria-label="Limpar filtros">
                <X size={16} />
              </button>
            )}
          </div>

          <fieldset className="space-y-2">
            <legend className="text-xs font-medium text-zinc-500">Periodo</legend>
            <div className="grid grid-cols-2 gap-1">
              {(['today', 'yesterday', '7d', '30d', 'custom'] as Preset[]).map((p) => (
                <button key={p} type="button" onClick={() => applyPreset(p)} className={chipClass(preset === p)}>{presetLabel(p)}</button>
              ))}
            </div>
            <div className="grid grid-cols-2 gap-2">
              <input type="date" value={from} onChange={(e) => { setPreset('custom'); setFrom(e.target.value); setPage(1); }} className={inputCls} />
              <input type="date" value={to} onChange={(e) => { setPreset('custom'); setTo(e.target.value); setPage(1); }} className={inputCls} />
            </div>
          </fieldset>

          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-500">Pesquisa</span>
            <div className="relative">
              <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
              <input value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} placeholder="cliente, descricao, IP..." className={`${inputCls} pl-9`} />
            </div>
          </label>

          <MultiChecks title="Entidade" values={entityTypes} options={options.data?.entityTypes ?? []} onChange={(v) => { setEntityTypes(v); setPage(1); }} />
          <MultiChecks title="Utilizador" values={userIds} options={(options.data?.users ?? []).map((u) => ({ value: u.id, label: u.displayName || u.email || u.id }))} onChange={(v) => { setUserIds(v); setPage(1); }} />
          {(options.data?.apiKeys?.length ?? 0) > 0 && (
            <MultiChecks
              title="Integração externa"
              values={serviceApiKeyIds}
              options={(options.data?.apiKeys ?? []).map((k) => ({
                value: k.id,
                label: k.revoked ? `${k.name} (revogada)` : k.name,
              }))}
              onChange={(v) => { setServiceApiKeyIds(v); setPage(1); }}
            />
          )}
          <MultiChecks title="Acao" values={actions.map(String)} options={allActions.map((a) => ({ value: String(a), label: AUDIT_ACTION_LABEL[a] ?? String(a) }))} onChange={(v) => { setActions(v.map(Number)); setPage(1); }} />
        </aside>

        <section className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
          <div className="overflow-x-auto">
            <table className="min-w-[760px] text-sm">
              <thead className="bg-zinc-50 text-left text-xs text-zinc-500 dark:bg-zinc-950">
                <tr>
                  <th className="px-3 py-2">Quando</th>
                  <th className="px-3 py-2">Quem</th>
                  <th className="px-3 py-2">O que</th>
                  <th className="px-3 py-2">Detalhe</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {list.isLoading && Array.from({ length: 5 }).map((_, i) => <tr key={i}><td colSpan={4}><SkeletonRow columns={4} /></td></tr>)}
                {!list.isLoading && items.map((entry) => (
                  <tr key={entry.id} onClick={() => setSelected(entry)} className="cursor-pointer align-top hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                    <td className="whitespace-nowrap px-3 py-3 text-xs text-zinc-500">{formatDate(entry.createdAt)}</td>
                    <td className="px-3 py-3">
                      <button type="button" onClick={(e) => { e.stopPropagation(); if (entry.appUserId) { setUserIds([entry.appUserId]); setPage(1); } }} className="rounded-sm text-left font-medium hover:text-brand-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">
                        {entry.appUserDisplayName ?? entry.appUserEmail ?? (
                          <span className="inline-flex items-center gap-1">
                            {entry.serviceApiKeyName ?? 'Integração externa'}
                            <span
                              title={entry.serviceApiKeyPrefix
                                ? `API key: ${entry.serviceApiKeyPrefix}`
                                : 'Operação realizada via API key (loja online, importador ou outra integração servidor-a-servidor)'}
                              className="rounded-full bg-blue-100 px-1.5 py-0.5 text-[9px] font-medium text-blue-700 dark:bg-blue-900/40 dark:text-blue-300"
                            >
                              API
                            </span>
                          </span>
                        )}
                      </button>
                      <div className="text-[11px] text-zinc-500">
                        {entry.appUserEmail ?? (entry.appUserId
                          ? entry.appUserId
                          : entry.serviceApiKeyPrefix ?? 'sem utilizador (chave de API)')}
                      </div>
                    </td>
                    <td className="px-3 py-3">
                      <StatusBadge tone={toneFor(entry.action)}>{AUDIT_ACTION_LABEL[entry.action] ?? entry.action}</StatusBadge>
                      <div className="mt-1 font-medium">{entry.entityType}</div>
                      <div className="font-mono text-[11px] text-zinc-500">{entry.entityId ?? '-'}</div>
                    </td>
                    <td className="max-w-xl px-3 py-3 text-xs text-zinc-600 dark:text-zinc-300">{truncate(summary(entry), 180)}</td>
                  </tr>
                ))}
                {items.length === 0 && !list.isLoading && (
                  <tr><td colSpan={4} className="p-4"><EmptyState compact icon={hasFilters ? Search : ScrollText} title={hasFilters ? 'Sem eventos para estes filtros' : 'Nenhum evento registado'} description={hasFilters ? 'Ajusta o periodo, entidade, utilizador, acao ou pesquisa.' : 'O audit log captura operacoes de escrita e eventos sensiveis automaticamente.'} /></td></tr>
                )}
              </tbody>
            </table>
          </div>
          {lastPage > 1 && (
            <div className="flex items-center justify-between gap-3 border-t border-zinc-100 px-3 py-2 text-xs text-zinc-500 dark:border-zinc-800">
              <Button type="button" variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
              <span>{page} / {lastPage}</span>
              <Button type="button" variant="ghost" size="sm" disabled={page >= lastPage} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
            </div>
          )}
        </section>
      </section>

      <Modal open={!!selected} title="Detalhe do evento" onClose={() => setSelected(null)}>
        {selected && (
          <div className="space-y-3">
            <div className="grid gap-2 text-sm sm:grid-cols-2">
              <Info label="Quando" value={formatDate(selected.createdAt)} />
              <Info
                label="Quem"
                value={selected.appUserDisplayName
                  ?? selected.appUserEmail
                  ?? (selected.appUserId
                    ? selected.appUserId
                    : selected.serviceApiKeyName
                      ? `${selected.serviceApiKeyName}${selected.serviceApiKeyPrefix ? ` (${selected.serviceApiKeyPrefix})` : ''}`
                      : 'Integração externa (API key)')}
              />
              <Info label="Acao" value={AUDIT_ACTION_LABEL[selected.action] ?? String(selected.action)} />
              <Info label="Entidade" value={`${selected.entityType}${selected.entityId ? ` #${selected.entityId}` : ''}`} />
              <Info label="IP" value={selected.ipAddress ?? '-'} />
              <Info label="User-Agent" value={selected.userAgent ?? '-'} />
            </div>
            <JsonViewer value={selected.changesJson} />
          </div>
        )}
      </Modal>
    </div>
  );
}

function MultiChecks({ title, values, options, onChange }: { title: string; values: string[]; options: Array<string | { value: string; label: string }>; onChange: (values: string[]) => void }) {
  const normalized = options.map((o) => typeof o === 'string' ? { value: o, label: o } : o);
  return (
    <fieldset className="space-y-2">
      <legend className="text-xs font-medium text-zinc-500">{title}</legend>
      <div className="max-h-40 space-y-1 overflow-auto rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
        {normalized.length === 0 ? <div className="text-xs text-zinc-400">Sem opcoes</div> : normalized.map((o) => (
          <label key={o.value} className="flex min-h-10 items-center gap-2 rounded-md px-2 py-1 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
            <input type="checkbox" checked={values.includes(o.value)} onChange={(e) => onChange(e.target.checked ? [...values, o.value] : values.filter((v) => v !== o.value))} className="h-4 w-4 scale-125 rounded border-zinc-300 text-brand-600 focus:ring-brand-400 sm:scale-100" />
            <span className="truncate">{o.label}</span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return <div><div className="text-xs text-zinc-500">{label}</div><div className="break-words font-medium">{value}</div></div>;
}

function presetRange(preset: Preset) {
  const today = new Date();
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  if (preset === 'today') return { from: iso(start), to: iso(addDays(start, 1)) };
  if (preset === 'yesterday') return { from: iso(addDays(start, -1)), to: iso(start) };
  if (preset === '7d') return { from: iso(addDays(start, -7)), to: iso(addDays(start, 1)) };
  return { from: iso(addDays(start, -30)), to: iso(addDays(start, 1)) };
}

function presetLabel(preset: Preset) {
  return { today: 'Hoje', yesterday: 'Ontem', '7d': '7 dias', '30d': '30 dias', custom: 'Custom' }[preset];
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

function chipClass(active: boolean) {
  return `min-h-10 rounded-md px-2 py-1 text-xs font-medium transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${active ? 'bg-brand-600 text-white' : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300'}`;
}

function toneFor(action: number) {
  if (action === 0) return 'emerald';
  if (action === 1) return 'blue';
  if (action === 2 || action === 3) return 'rose';
  if (action === 5) return 'violet';
  return 'amber';
}

function summary(entry: AuditEntry) {
  if (!entry.changesJson) return entry.entityId ?? '-';
  try {
    const json = JSON.parse(entry.changesJson) as Record<string, unknown>;
    return Object.entries(json).slice(0, 4).map(([k, v]) => `${k}: ${formatValue(v)}`).join(' | ');
  } catch {
    return entry.changesJson;
  }
}

function formatValue(value: unknown): string {
  if (value == null) return 'null';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}

function truncate(value: string, max: number) {
  return value.length <= max ? value : `${value.slice(0, max)}...`;
}

const inputCls = 'block min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950';
