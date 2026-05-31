import { useState, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import {
  AlertTriangle,
  AtSign,
  Ban,
  ChevronRight,
  Download,
  FileText,
  FolderUp,
  Mail,
  Megaphone,
  MessageCircle,
  Pencil,
  Phone,
  Search,
  ShoppingBag,
  UserPlus,
  Users,
  Wrench,
} from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { isAxiosError } from 'axios';
import { clientesApi, type ImportClientesResponse } from '../../lib/clientes/api';
import { reparacoesApi } from '../../lib/reparacoes/api';
import { vendasApi } from '../../lib/vendas/api';
import { STATUS_LABEL } from '../../lib/reparacoes/types';
import { VENDA_STATUS } from '../../lib/vendas/types';
import { downloadFile } from '../../lib/downloadPdf';
import { displayPhone } from '../../lib/phone/formatter';
import { validateNif } from '../../lib/nif/validator';
import { formatCents, formatDateOnly } from '../../lib/money';
import type { Cliente, ClienteForm } from '../../lib/clientes/types';
import ClienteFormView from './ClienteForm';

/** Iniciais para o avatar (até 2 letras). */
function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** Cor determinística do avatar a partir do nome. */
const AVATAR_TONES = [
  'bg-sky-100 text-sky-700 dark:bg-sky-950/50 dark:text-sky-300',
  'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300',
  'bg-amber-100 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300',
  'bg-violet-100 text-violet-700 dark:bg-violet-950/50 dark:text-violet-300',
  'bg-rose-100 text-rose-700 dark:bg-rose-950/50 dark:text-rose-300',
  'bg-teal-100 text-teal-700 dark:bg-teal-950/50 dark:text-teal-300',
];
function avatarTone(name: string): string {
  let h = 0;
  for (let i = 0; i < name.length; i++) h = (h * 31 + name.charCodeAt(i)) >>> 0;
  return AVATAR_TONES[h % AVATAR_TONES.length];
}

type FilterKey = 'all' | 'nif' | 'email' | 'nophone' | 'marketing' | 'blocked';
const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'Todos' },
  { key: 'nif', label: 'Com NIF' },
  { key: 'email', label: 'Com email' },
  { key: 'nophone', label: 'Sem telefone' },
  { key: 'marketing', label: 'Marketing OK' },
  { key: 'blocked', label: 'Não contactar' },
];

export default function Clientes() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [editing, setEditing] = useState<Cliente | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<Cliente | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [filter, setFilter] = useState<FilterKey>('all');

  const list = useQuery({
    queryKey: ['clientes', search, page],
    queryFn: () => clientesApi.list(search, page, pageSize),
    placeholderData: keepPreviousData,
  });

  const upsert = useMutation({
    mutationFn: async (form: ClienteForm) => {
      if (editing) return clientesApi.update(editing.id, form);
      return clientesApi.create(form);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clientes'] });
      setModalOpen(false);
      setEditing(null);
    },
  });

  const remove = useMutation({
    mutationFn: (c: Cliente) => clientesApi.remove(c.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clientes'] });
      setConfirmDelete(null);
    },
  });

  function openCreate() {
    setEditing(null);
    setModalOpen(true);
  }

  function openEdit(c: Cliente) {
    setEditing(c);
    setModalOpen(true);
  }

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / pageSize));

  const filtered = items.filter((c) => {
    if (filter === 'nif') return !!c.nif;
    if (filter === 'email') return !!c.email;
    if (filter === 'nophone') return !c.telefone;
    if (filter === 'marketing') return !!c.aceitaMarketing;
    if (filter === 'blocked') return !!c.naoContactar;
    return true;
  });

  const selected = filtered.find((c) => c.id === selectedId) ?? filtered[0] ?? null;

  // KPIs (sobre o conjunto carregado; Total clientes é global)
  const now = new Date();
  const novosEsteMes = items.filter((c) => {
    const d = new Date(c.createdAt);
    return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
  }).length;
  const comNif = items.filter((c) => !!c.nif).length;
  const comEmail = items.filter((c) => !!c.email).length;
  const marketingOk = items.filter((c) => !!c.aceitaMarketing).length;
  const naoContactarCount = items.filter((c) => !!c.naoContactar).length;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Clientes"
        description="Contactos, histórico e dados de faturação das pessoas que entram na loja."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'cliente' : 'clientes'}</span>}
        actions={
          <>
            <Button
              type="button"
              variant="secondary"
              onClick={() => downloadFile('/clientes/export.csv', `clientes_${new Date().toISOString().slice(0, 10)}.csv`)}
              leftIcon={<Download size={15} />}
              title="Exportar todos os clientes para CSV"
            >
              Exportar
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => setImportOpen(true)}
              leftIcon={<FolderUp size={15} />}
              title="Importar clientes em massa de CSV (Excel/Google Sheets)"
            >
              Importar CSV
            </Button>
            <Button type="button" onClick={openCreate} leftIcon={<UserPlus size={15} />}>
              Novo cliente
            </Button>
          </>
        }
      />

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-6">
        <KpiTile icon={<Users size={18} />} tone="sky" label="Total clientes" value={String(total)} />
        <KpiTile icon={<UserPlus size={18} />} tone="emerald" label="Novos este mês" value={String(novosEsteMes)} />
        <KpiTile icon={<FileText size={18} />} tone="violet" label="Com NIF" value={String(comNif)} />
        <KpiTile icon={<AtSign size={18} />} tone="amber" label="Com email" value={String(comEmail)} />
        <KpiTile icon={<Megaphone size={18} />} tone="emerald" label="Marketing OK" value={String(marketingOk)} />
        <KpiTile icon={<Ban size={18} />} tone="amber" label="Não contactar" value={String(naoContactarCount)} />
      </div>

      {/* Pesquisa + chips de filtro */}
      <div className="space-y-3 rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="relative">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input
            type="search"
            inputMode="search"
            placeholder="Pesquisar nome, telefone, email ou NIF..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </div>
        <div className="flex flex-wrap gap-2">
          {FILTERS.map((f) => (
            <button
              key={f.key}
              type="button"
              onClick={() => setFilter(f.key)}
              className={`rounded-full px-3 py-1.5 text-xs font-medium transition ${
                filter === f.key
                  ? 'bg-brand-600 text-white'
                  : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700'
              }`}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {list.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
          Não foi possível carregar a lista.
        </div>
      )}

      {/* 2 colunas: tabela + inspector de perfil */}
      <div className="grid gap-4 xl:grid-cols-[1fr_380px]">
        {/* Tabela */}
        <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex items-center justify-between border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
            <h2 className="text-sm font-semibold">Lista de clientes</h2>
            <span className="text-xs text-zinc-500">{filtered.length} {filtered.length === 1 ? 'resultado' : 'resultados'}</span>
          </div>

          {list.isLoading ? (
            <div className="space-y-2 p-3">
              {Array.from({ length: 5 }).map((_, i) => <SkeletonCard key={i} />)}
            </div>
          ) : filtered.length === 0 ? (
            <div className="p-4">
              <EmptyState
                icon={search ? Search : Users}
                title={search ? 'Nenhum cliente encontrado' : 'Ainda não há clientes'}
                description={search ? 'Ajusta a pesquisa ou limpa o campo para voltar a ver todos os clientes.' : 'Cria o primeiro cliente para associar reparações, trabalhos e histórico.'}
                action={!search ? <Button type="button" onClick={openCreate} leftIcon={<UserPlus size={15} />}>Criar cliente</Button> : undefined}
              />
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-zinc-200 text-left text-[11px] font-semibold uppercase tracking-wide text-zinc-500 dark:border-zinc-800">
                    <th className="px-4 py-2.5">Cliente</th>
                    <th className="px-4 py-2.5">Contactos</th>
                    <th className="px-4 py-2.5">NIF</th>
                    <th className="px-4 py-2.5 text-right">Ações</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((c) => {
                    const isSel = selected?.id === c.id;
                    return (
                      <tr
                        key={c.id}
                        onClick={() => setSelectedId(c.id)}
                        className={`cursor-pointer border-b border-zinc-100 transition last:border-0 dark:border-zinc-800/60 ${
                          isSel ? 'bg-sky-50 dark:bg-sky-950/30' : 'hover:bg-zinc-50 dark:hover:bg-zinc-800/50'
                        }`}
                      >
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-3">
                            <span className={`grid h-9 w-9 shrink-0 place-items-center rounded-full text-xs font-bold ${avatarTone(c.nome)}`}>
                              {initials(c.nome)}
                            </span>
                            <div className="min-w-0">
                              <div className="truncate font-medium">{c.nome}</div>
                              <div className="text-[11px] text-zinc-400">Desde {formatDateOnly(c.createdAt)}</div>
                            </div>
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          <div className="text-zinc-600 dark:text-zinc-300">
                            {c.telefone ? displayPhone(c.telefone) : <em className="opacity-60">sem telefone</em>}
                          </div>
                          {c.email && <div className="truncate text-[11px] text-zinc-400">{c.email}</div>}
                          <div className="mt-1 flex flex-wrap gap-1">
                            {c.contactoPreferido && (
                              <span className="rounded-full bg-sky-50 px-2 py-0.5 text-[10px] font-medium text-sky-700 dark:bg-sky-950/40 dark:text-sky-300">
                                Pref. {c.contactoPreferido}
                              </span>
                            )}
                            {c.aceitaMarketing && (
                              <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-medium text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
                                Marketing OK
                              </span>
                            )}
                            {c.naoContactar && (
                              <span className="rounded-full bg-red-50 px-2 py-0.5 text-[10px] font-medium text-red-700 dark:bg-red-950/40 dark:text-red-300">
                                Não contactar
                              </span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          {c.nif ? (
                            <span className="inline-flex items-center gap-1 text-zinc-600 dark:text-zinc-300">
                              {c.nif}
                              {!validateNif(c.nif).isValid && (
                                <AlertTriangle size={12} className="text-amber-500" />
                              )}
                            </span>
                          ) : (
                            <span className="text-zinc-300 dark:text-zinc-600">—</span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex items-center justify-end gap-1">
                            <button
                              type="button"
                              onClick={(e) => { e.stopPropagation(); openEdit(c); }}
                              className="grid h-8 w-8 place-items-center rounded-md text-zinc-500 transition hover:bg-zinc-100 dark:hover:bg-zinc-800"
                              title="Editar"
                              aria-label="Editar"
                            >
                              <Pencil size={13} />
                            </button>
                            <ChevronRight size={15} className="text-zinc-300 dark:text-zinc-600" />
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {lastPage > 1 && (
            <div className="flex items-center justify-between gap-3 border-t border-zinc-200 px-4 py-2 text-xs text-zinc-500 dark:border-zinc-800">
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="min-h-9 rounded-md px-3 py-1.5 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40"
              >
                ← Anterior
              </button>
              <span>{page} / {lastPage}</span>
              <button
                disabled={page >= lastPage}
                onClick={() => setPage((p) => Math.min(lastPage, p + 1))}
                className="min-h-9 rounded-md px-3 py-1.5 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40"
              >
                Seguinte →
              </button>
            </div>
          )}
        </div>

        {/* Inspector de perfil */}
        <ClienteInspector
          cliente={selected}
          onEdit={openEdit}
          onOpen={(id) => navigate(`/clientes/${id}`)}
        />
      </div>

      <Modal
        open={modalOpen}
        title={editing ? 'Editar cliente' : 'Novo cliente'}
        onClose={() => setModalOpen(false)}
        footer={
          <>
            <button
              type="button"
              onClick={() => setModalOpen(false)}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Cancelar
            </button>
            <button
              type="submit"
              form="cliente-form"
              disabled={upsert.isPending}
              className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-60"
            >
              {upsert.isPending ? 'A guardar…' : 'Guardar'}
            </button>
          </>
        }
      >
        <ClienteFormView
          initial={editing}
          submitting={upsert.isPending}
          onCancel={() => setModalOpen(false)}
          onSubmit={async (form) => {
            await upsert.mutateAsync(form);
          }}
        />
      </Modal>

      <Modal
        open={!!confirmDelete}
        title="Apagar cliente"
        onClose={() => setConfirmDelete(null)}
        footer={
          <>
            <button
              type="button"
              onClick={() => setConfirmDelete(null)}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={remove.isPending}
              onClick={() => confirmDelete && remove.mutate(confirmDelete)}
              className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-red-700 disabled:opacity-60"
            >
              {remove.isPending ? 'A apagar…' : 'Apagar'}
            </button>
          </>
        }
      >
        <p className="text-sm">
          Tens a certeza que queres apagar <strong>{confirmDelete?.nome}</strong>? Esta ação pode
          ser revertida pelo admin (soft delete).
        </p>
      </Modal>

      <ImportCsvModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        onDone={() => { qc.invalidateQueries({ queryKey: ['clientes'] }); }}
      />
    </div>
  );
}

type Tone = 'sky' | 'emerald' | 'violet' | 'amber';
const TONE_CLASS: Record<Tone, string> = {
  sky: 'bg-sky-50 text-sky-600 dark:bg-sky-950/40 dark:text-sky-300',
  emerald: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-950/40 dark:text-emerald-300',
  violet: 'bg-violet-50 text-violet-600 dark:bg-violet-950/40 dark:text-violet-300',
  amber: 'bg-amber-50 text-amber-600 dark:bg-amber-950/40 dark:text-amber-300',
};

function KpiTile({ icon, tone, label, value }: { icon: ReactNode; tone: Tone; label: string; value: string }) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-zinc-200 bg-white px-4 py-3 dark:border-zinc-800 dark:bg-zinc-900">
      <span className={`grid h-10 w-10 shrink-0 place-items-center rounded-lg ${TONE_CLASS[tone]}`}>{icon}</span>
      <div className="min-w-0">
        <div className="truncate text-[11px] font-semibold uppercase tracking-wide text-zinc-500">{label}</div>
        <div className="text-xl font-bold tabular-nums">{value}</div>
      </div>
    </div>
  );
}

function ClienteInspector({
  cliente,
  onEdit,
  onOpen,
}: {
  cliente: Cliente | null;
  onEdit: (c: Cliente) => void;
  onOpen: (id: string) => void;
}) {
  const reparacoes = useQuery({
    queryKey: ['cliente-inspector-reps', cliente?.id],
    queryFn: () => reparacoesApi.list({ clienteId: cliente!.id, pageSize: 100 }),
    enabled: !!cliente,
  });
  const vendas = useQuery({
    queryKey: ['cliente-inspector-vendas', cliente?.id],
    queryFn: () => vendasApi.list({ clienteId: cliente!.id, pageSize: 100 }),
    enabled: !!cliente,
  });

  if (!cliente) {
    return (
      <aside className="hidden rounded-xl border border-dashed border-zinc-300 bg-white p-8 text-center text-sm text-zinc-400 dark:border-zinc-700 dark:bg-zinc-900 xl:flex xl:flex-col xl:items-center xl:justify-center">
        <Users size={28} className="mb-2 opacity-40" />
        Seleciona um cliente para ver o perfil.
      </aside>
    );
  }

  const reps = reparacoes.data?.items ?? [];
  const vds = vendas.data?.items ?? [];
  const repsPagas = reps.filter((r) => r.estado === 5);
  const vendasPagas = vds.filter((v) => v.status === VENDA_STATUS.Paga);
  const totalGasto =
    repsPagas.reduce((s, r) => s + (r.precoFinalCents ?? r.orcamentoCents ?? 0), 0) +
    vendasPagas.reduce((s, v) => s + v.totalCents, 0);
  const abertos = reps.filter((r) => r.estado !== 5 && r.estado !== 6).length;

  type Activity = { key: string; date: string; icon: ReactNode; title: string; sub: string; value: number; href: string };
  const activity: Activity[] = [
    ...reps.map((r) => ({
      key: `r-${r.id}`,
      date: r.recebidoEm,
      icon: <Wrench size={14} className="text-sky-600 dark:text-sky-300" />,
      title: `Reparação #${r.numero}`,
      sub: `${r.equipamento} · ${STATUS_LABEL[r.estado]}`,
      value: r.precoFinalCents ?? r.orcamentoCents ?? 0,
      href: `/reparacoes/${r.id}`,
    })),
    ...vds.map((v) => ({
      key: `v-${v.id}`,
      date: v.data,
      icon: <ShoppingBag size={14} className="text-emerald-600 dark:text-emerald-300" />,
      title: `Venda #${v.numero}`,
      sub: 'Balcão',
      value: v.totalCents,
      href: `/vendas/${v.id}`,
    })),
  ]
    .sort((a, b) => (a.date < b.date ? 1 : -1))
    .slice(0, 6);

  const phone = cliente.telefone?.replace(/\s/g, '') ?? '';
  const loadingHist = reparacoes.isLoading || vendas.isLoading;

  return (
    <aside className="space-y-4 rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900 xl:sticky xl:top-4 xl:self-start">
      {/* Cabeçalho do perfil */}
      <div className="rounded-t-xl bg-gradient-to-br from-sky-50 to-white px-4 py-4 dark:from-sky-950/30 dark:to-zinc-900">
        <div className="flex items-center gap-3">
          <span className={`grid h-14 w-14 shrink-0 place-items-center rounded-full text-lg font-bold ${avatarTone(cliente.nome)}`}>
            {initials(cliente.nome)}
          </span>
          <div className="min-w-0">
            <h3 className="truncate text-base font-bold leading-tight">{cliente.nome}</h3>
            <div className="mt-1 flex flex-wrap gap-1 text-[11px]">
              {cliente.nif && <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">NIF {cliente.nif}</span>}
              {abertos > 0 && <span className="rounded-full bg-amber-100 px-2 py-0.5 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300">{abertos} em curso</span>}
              {cliente.contactoPreferido && <span className="rounded-full bg-sky-100 px-2 py-0.5 text-sky-700 dark:bg-sky-950/50 dark:text-sky-300">Pref. {cliente.contactoPreferido}</span>}
              {cliente.aceitaMarketing && <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300">Marketing OK</span>}
            </div>
          </div>
        </div>
        {cliente.naoContactar && (
          <div className="mt-3 flex items-start gap-2 rounded-lg bg-red-50 px-3 py-2 text-xs text-red-800 dark:bg-red-950/40 dark:text-red-200">
            <Ban size={13} className="mt-0.5 shrink-0" />
            <span>Cliente marcado como Não contactar. Evita mensagens não essenciais.</span>
          </div>
        )}
        {cliente.notaImportante && (
          <div className="mt-3 flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:bg-amber-950/40 dark:text-amber-200">
            <AlertTriangle size={13} className="mt-0.5 shrink-0" />
            <span>{cliente.notaImportante}</span>
          </div>
        )}
      </div>

      {/* Ações de contacto */}
      <div className="grid grid-cols-2 gap-2 px-4">
        <a
          href={phone ? `https://wa.me/${phone.replace(/^\+/, '')}` : undefined}
          target="_blank"
          rel="noreferrer"
          className={`inline-flex items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium text-white transition ${
            phone ? 'bg-emerald-600 hover:bg-emerald-700' : 'pointer-events-none bg-zinc-200 text-zinc-400 dark:bg-zinc-800'
          }`}
        >
          <MessageCircle size={14} /> WhatsApp
        </a>
        <a
          href={phone ? `tel:${phone}` : undefined}
          className={`inline-flex items-center justify-center gap-1.5 rounded-lg border px-3 py-2 text-xs font-medium transition ${
            phone
              ? 'border-zinc-300 text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800'
              : 'pointer-events-none border-zinc-200 text-zinc-400 dark:border-zinc-800'
          }`}
        >
          <Phone size={14} /> Ligar
        </a>
        <a
          href={cliente.email ? `mailto:${cliente.email}` : undefined}
          className={`inline-flex items-center justify-center gap-1.5 rounded-lg border px-3 py-2 text-xs font-medium transition ${
            cliente.email
              ? 'border-zinc-300 text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800'
              : 'pointer-events-none border-zinc-200 text-zinc-400 dark:border-zinc-800'
          }`}
        >
          <Mail size={14} /> Email
        </a>
        <button
          type="button"
          onClick={() => onEdit(cliente)}
          className="inline-flex items-center justify-center gap-1.5 rounded-lg border border-zinc-300 px-3 py-2 text-xs font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800"
        >
          <Pencil size={13} /> Editar
        </button>
      </div>

      {/* KPIs do cliente */}
      <div className="grid grid-cols-2 gap-2 px-4">
        <div className="rounded-lg bg-zinc-50 px-3 py-2.5 dark:bg-zinc-800/50">
          <div className="text-[10px] font-semibold uppercase tracking-wide text-zinc-500">Total gasto</div>
          <div className="text-lg font-bold tabular-nums">{loadingHist ? '…' : formatCents(totalGasto)}</div>
        </div>
        <div className="rounded-lg bg-zinc-50 px-3 py-2.5 dark:bg-zinc-800/50">
          <div className="text-[10px] font-semibold uppercase tracking-wide text-zinc-500">Reparações</div>
          <div className="text-lg font-bold tabular-nums">{loadingHist ? '…' : reps.length}</div>
        </div>
      </div>

      {/* Atividade recente */}
      <div className="px-4 pb-4">
        <h4 className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">Atividade recente</h4>
        {loadingHist ? (
          <p className="text-xs text-zinc-400">A carregar…</p>
        ) : activity.length === 0 ? (
          <p className="text-xs text-zinc-400">Sem reparações ou vendas registadas.</p>
        ) : (
          <ul className="space-y-1">
            {activity.map((a) => (
              <li key={a.key}>
                <Link
                  to={a.href}
                  className="flex items-center gap-2.5 rounded-lg px-2 py-2 text-sm transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50"
                >
                  <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-zinc-100 dark:bg-zinc-800">{a.icon}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium">{a.title}</span>
                    <span className="block truncate text-[11px] text-zinc-400">{a.sub} · {formatDateOnly(a.date)}</span>
                  </span>
                  <span className="shrink-0 text-xs font-semibold tabular-nums">{formatCents(a.value)}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}

        <button
          type="button"
          onClick={() => onOpen(cliente.id)}
          className="mt-3 inline-flex w-full items-center justify-center gap-1 rounded-lg bg-brand-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-brand-700"
        >
          Abrir ficha completa <ChevronRight size={15} />
        </button>
      </div>
    </aside>
  );
}

function ImportCsvModal({
  open,
  onClose,
  onDone,
}: {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
}) {
  const [csv, setCsv] = useState('');
  const [fileName, setFileName] = useState<string | null>(null);
  const [result, setResult] = useState<ImportClientesResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  const imp = useMutation({
    mutationFn: () => clientesApi.importCsv(csv),
    onSuccess: (r) => { setResult(r); onDone(); },
    onError: (err) => {
      if (isAxiosError(err)) {
        const d = err.response?.data as { detail?: string; title?: string } | undefined;
        setError(d?.detail ?? d?.title ?? 'Erro ao importar.');
      } else setError('Erro ao importar.');
    },
  });

  function reset() {
    setCsv('');
    setFileName(null);
    setResult(null);
    setError(null);
  }

  function handleFile(file: File) {
    if (file.size > 5 * 1024 * 1024) {
      setError('Ficheiro demasiado grande (máximo 5 MB).');
      return;
    }
    setFileName(file.name);
    setError(null);
    file.text().then((t) => setCsv(t));
  }

  // Preview: primeiras 5 linhas com vírgula/ponto-e-vírgula como separadores comuns
  const previewLines = csv ? csv.split(/\r?\n/).filter((l) => l.trim()).slice(0, 6) : [];

  return (
    <Modal
      open={open}
      title="Importar clientes de CSV"
      onClose={() => { reset(); onClose(); }}
      footer={
        result ? (
          <button
            type="button"
            onClick={() => { reset(); onClose(); }}
            className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white"
          >
            Fechar
          </button>
        ) : (
          <>
            <button
              type="button"
              onClick={() => { reset(); onClose(); }}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={!csv || imp.isPending}
              onClick={() => imp.mutate()}
              className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
            >
              {imp.isPending ? 'A importar…' : 'Importar'}
            </button>
          </>
        )
      }
    >
      <div className="space-y-3">
        {error && (
          <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300">
            {error}
          </div>
        )}

        {result ? (
          <div className="space-y-3 text-sm">
            <div className="grid grid-cols-3 gap-2">
              <div className="rounded-lg border border-emerald-300 bg-emerald-50 p-3 text-center dark:border-emerald-800/60 dark:bg-emerald-950/30">
                <div className="text-2xl font-semibold text-emerald-700 dark:text-emerald-300">{result.criados}</div>
                <div className="text-[11px] uppercase text-emerald-700/80 dark:text-emerald-300/80">Criados</div>
              </div>
              <div className="rounded-lg border border-zinc-300 bg-zinc-50 p-3 text-center dark:border-zinc-700 dark:bg-zinc-900">
                <div className="text-2xl font-semibold text-zinc-600 dark:text-zinc-300">{result.ignorados}</div>
                <div className="text-[11px] uppercase text-zinc-500">Ignorados (dup. NIF)</div>
              </div>
              <div className="rounded-lg border border-rose-300 bg-rose-50 p-3 text-center dark:border-rose-800/60 dark:bg-rose-950/30">
                <div className="text-2xl font-semibold text-rose-700 dark:text-rose-300">{result.comErro}</div>
                <div className="text-[11px] uppercase text-rose-700/80 dark:text-rose-300/80">Com erro</div>
              </div>
            </div>
            {result.erros.length > 0 && (
              <div>
                <h4 className="mb-1 text-xs font-semibold text-zinc-600 dark:text-zinc-400">Erros por linha:</h4>
                <ul className="max-h-48 space-y-1 overflow-y-auto rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
                  {result.erros.map((e, i) => (
                    <li key={i} className="text-xs">
                      <span className="font-mono text-zinc-500">L{e.linha}</span>{' '}
                      <span className="font-medium">{e.campo}:</span>{' '}
                      <span className="text-rose-700 dark:text-rose-300">{e.mensagem}</span>
                      {e.valorOriginal && <span className="text-zinc-500"> ({e.valorOriginal})</span>}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        ) : (
          <>
            <div className="rounded-lg bg-zinc-50 p-3 text-xs text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
              <p className="font-medium text-zinc-700 dark:text-zinc-300">Formato esperado:</p>
              <p className="mt-1">Header obrigatório: <code className="font-mono">nome,telefone,email,nif,notas</code></p>
              <p className="mt-1">Opcionais: <code className="font-mono">contactopreferido,aceitamarketing,naocontactar</code></p>
              <p className="mt-1">Aceito separador <code>,</code>, <code>;</code> ou tab. Vindo de Excel? Guarda como <strong>CSV UTF-8</strong>. Dedupe automático por NIF.</p>
            </div>

            <div
              onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
              onDragLeave={() => setDragging(false)}
              onDrop={(e) => {
                e.preventDefault();
                setDragging(false);
                const f = e.dataTransfer.files[0];
                if (f) handleFile(f);
              }}
              className={`rounded-xl border-2 border-dashed p-6 text-center text-sm transition ${
                dragging
                  ? 'border-brand-500 bg-brand-50 dark:border-brand-400 dark:bg-brand-950/30'
                  : 'border-zinc-300 bg-white dark:border-zinc-700 dark:bg-zinc-950'
              }`}
            >
              {fileName ? (
                <>
                  <div className="font-medium">📄 {fileName}</div>
                  <button
                    type="button"
                    onClick={() => { setCsv(''); setFileName(null); }}
                    className="mt-1 text-xs text-zinc-500 underline"
                  >Escolher outro</button>
                </>
              ) : (
                <>
                  <div className="text-zinc-500">Arrasta o ficheiro CSV para aqui</div>
                  <div className="mt-1 text-xs text-zinc-400">ou</div>
                  <label className="mt-2 inline-flex min-h-11 cursor-pointer items-center justify-center rounded-md bg-brand-600 px-3 py-2 text-xs font-medium text-white hover:bg-brand-700">
                    Selecionar ficheiro
                    <input
                      type="file"
                      accept=".csv,text/csv,text/plain"
                      className="hidden"
                      onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f); }}
                    />
                  </label>
                </>
              )}
            </div>

            {previewLines.length > 0 && (
              <div>
                <h4 className="mb-1 text-xs font-semibold text-zinc-600 dark:text-zinc-400">
                  Preview (primeiras {previewLines.length - 1} {previewLines.length - 1 === 1 ? 'linha' : 'linhas'}):
                </h4>
                <pre className="max-h-32 overflow-auto rounded-lg border border-zinc-200 bg-zinc-50 p-2 text-[11px] dark:border-zinc-800 dark:bg-zinc-950">
                  {previewLines.join('\n')}
                </pre>
              </div>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}
