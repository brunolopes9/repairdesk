import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  Download,
  FolderUp,
  Inbox,
  LayoutGrid,
  List,
  PackageSearch,
  PartyPopper,
  Plus,
  Search,
  Stethoscope,
  Tags,
  Timer,
  Wrench,
  type LucideIcon,
} from 'lucide-react';
import { isAxiosError } from 'axios';
import EquipmentFieldsForm, {
  buildEquipmentFieldValues,
  initEquipmentFieldValues,
  missingRequiredEquipmentFields,
  type EquipmentFieldValuesMap,
} from '../../components/EquipmentFieldsForm';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { clientesApi } from '../../lib/clientes/api';
import { equipmentFieldTemplatesApi } from '../../lib/equipmentFields/api';
import { reparacoesApi } from '../../lib/reparacoes/api';
import { precosApi, type PriceTableEntry } from '../../lib/precos/api';
import { downloadFile } from '../../lib/downloadPdf';
import {
  STATUS_LABEL,
  STATUS_COLOR,
  type Reparacao,
  type RepairStatus,
} from '../../lib/reparacoes/types';
import { formatCents, formatDate } from '../../lib/money';
import { displayPhone } from '../../lib/phone/formatter';

const TABS: Array<{ value: RepairStatus | null; label: string }> = [
  { value: null, label: 'Todas' },
  { value: 7, label: 'Orçamentos' },
  { value: 0, label: 'Recebidas' },
  { value: 1, label: 'Diagnóstico' },
  { value: 2, label: 'Aguarda peça' },
  { value: 3, label: 'Em reparação' },
  { value: 4, label: 'Reparadas' },
  { value: 5, label: 'Entregues' },
];

const KANBAN_COLUMNS: Array<{ estado: RepairStatus; label: string; icon: LucideIcon }> = [
  { estado: 0, label: 'Recebido', icon: Inbox },
  { estado: 1, label: 'Diagnóstico', icon: Stethoscope },
  { estado: 2, label: 'Aguarda peça', icon: PackageSearch },
  { estado: 3, label: 'Em reparação', icon: Wrench },
  { estado: 4, label: 'Reparado', icon: CheckCircle2 },
  { estado: 5, label: 'Entregue', icon: PartyPopper },
];

type ViewMode = 'list' | 'kanban';
const VIEW_KEY = 'rd.reparacoes.view';

export default function Reparacoes() {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [view, setView] = useState<ViewMode>(() => {
    try { return (localStorage.getItem(VIEW_KEY) as ViewMode) || 'list'; } catch { return 'list'; }
  });
  const [estado, setEstado] = useState<RepairStatus | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<Reparacao | null>(null);
  const [importOpen, setImportOpen] = useState(false);

  function setViewMode(v: ViewMode) {
    setView(v);
    try { localStorage.setItem(VIEW_KEY, v); } catch { /* ignore */ }
  }

  const remove = useMutation({
    mutationFn: (r: Reparacao) => reparacoesApi.remove(r.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setConfirmDelete(null);
    },
  });

  const changeEstado = useMutation({
    mutationFn: ({ id, estado }: { id: string; estado: RepairStatus }) =>
      reparacoesApi.changeEstado(id, estado),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['reparacoes'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['dashboard-alertas'] });
    },
  });

  // List view: paginação. Kanban: queries por coluna (até 50 cada).
  const list = useQuery({
    queryKey: ['reparacoes', estado, search, page],
    queryFn: () => reparacoesApi.list({ q: search, estado, page, pageSize: 20 }),
    placeholderData: keepPreviousData,
    enabled: view === 'list',
  });

  const kanban = useQuery({
    queryKey: ['reparacoes-kanban', search],
    queryFn: async () => {
      const pages = await Promise.all(
        KANBAN_COLUMNS.map((c) =>
          reparacoesApi.list({ q: search, estado: c.estado, pageSize: 50 }).then((p) => ({ estado: c.estado, items: p.items })),
        ),
      );
      const map = new Map<RepairStatus, Reparacao[]>();
      pages.forEach((p) => map.set(p.estado, p.items));
      return map;
    },
    enabled: view === 'kanban',
    placeholderData: keepPreviousData,
    staleTime: 15_000,
  });

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / 20));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Reparacoes"
        description="Entrada, diagnostico, estados e entrega dos equipamentos em loja."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'reparacao' : 'reparacoes'}</span>}
        actions={
          <>
            <div className="inline-flex rounded-lg border border-zinc-200 bg-white p-0.5 dark:border-zinc-800 dark:bg-zinc-900">
              <button
                type="button"
                onClick={() => setViewMode('list')}
                className={`inline-flex items-center gap-1 rounded-md px-2.5 py-1 text-xs font-medium transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${view === 'list' ? 'bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100' : 'text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300'}`}
                title="Vista lista"
              >
                <List size={14} /> Lista
              </button>
              <button
                type="button"
                onClick={() => setViewMode('kanban')}
                className={`inline-flex items-center gap-1 rounded-md px-2.5 py-1 text-xs font-medium transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${view === 'kanban' ? 'bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100' : 'text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300'}`}
                title="Vista Kanban"
              >
                <LayoutGrid size={14} /> Kanban
              </button>
            </div>
            <Button
              type="button"
              variant="secondary"
              onClick={() => downloadFile('/reparacoes/export.csv', `reparacoes_${new Date().toISOString().slice(0,10)}.csv`)}
              leftIcon={<Download size={15} />}
              title="Exportar todas as reparacoes para CSV"
            >
              Exportar
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => setImportOpen(true)}
              leftIcon={<FolderUp size={15} />}
              title="Importar reparacoes em massa de CSV"
            >
              Importar
            </Button>
            <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>
              Nova
            </Button>
          </>
        }
      />

      {view === 'list' && (
        <div className="-mx-4 overflow-x-auto px-4 pb-1">
          <div className="flex gap-2">
            {TABS.map((t) => {
              const active = t.value === estado;
              return (
                <button
                  key={t.label}
                  type="button"
                  onClick={() => {
                    setEstado(t.value);
                    setPage(1);
                  }}
                  className={`whitespace-nowrap rounded-full px-3 py-1.5 text-xs font-medium transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${
                    active
                      ? 'bg-brand-600 text-white'
                      : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-700'
                  }`}
                >
                  {t.label}
                </button>
              );
            })}
          </div>
        </div>
      )}

      <div className="relative">
        <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input
          type="search"
          placeholder="Pesquisar equipamento, IMEI, cliente..."
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
          className="w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
        />
      </div>

      {view === 'list' ? (
        <>
          <ul className="space-y-2">
            {list.isLoading && Array.from({ length: 4 }).map((_, index) => <SkeletonCard key={index} />)}
            {items.map((r) => (
              <ReparacaoCard
                key={r.id}
                r={r}
                onClick={() => navigate(`/reparacoes/${r.id}`)}
                onDelete={() => setConfirmDelete(r)}
              />
            ))}
            {items.length === 0 && !list.isLoading && (
              <li>
                <EmptyState
                  icon={search ? Search : Wrench}
                  title={search ? 'Nenhuma reparacao encontrada' : 'Ainda nao ha reparacoes'}
                  description={search ? 'Ajusta a pesquisa ou muda o filtro de estado.' : 'Cria a primeira ficha de entrada para acompanhar o equipamento ate a entrega.'}
                  action={!search ? <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>Criar reparacao</Button> : undefined}
                />
              </li>
            )}
          </ul>

          {lastPage > 1 && (
            <div className="flex items-center justify-between text-xs text-zinc-500">
              <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="rounded-md px-2 py-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">
                ← Anterior
              </button>
              <span>{page} / {lastPage}</span>
              <button disabled={page >= lastPage} onClick={() => setPage((p) => p + 1)} className="rounded-md px-2 py-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">
                Seguinte →
              </button>
            </div>
          )}
        </>
      ) : (
        <KanbanBoard
          data={kanban.data}
          loading={kanban.isLoading}
          onMove={(id, to) => changeEstado.mutate({ id, estado: to })}
          onCardClick={(id) => navigate(`/reparacoes/${id}`)}
          pending={changeEstado.isPending}
        />
      )}

      <CreateReparacaoModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={(rep) => {
          qc.invalidateQueries({ queryKey: ['reparacoes'] });
          setCreateOpen(false);
          navigate(`/reparacoes/${rep.id}`);
        }}
      />

      <ImportReparacoesModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        onDone={() => {
          qc.invalidateQueries({ queryKey: ['reparacoes'] });
          qc.invalidateQueries({ queryKey: ['reparacoes-kanban'] });
          qc.invalidateQueries({ queryKey: ['clientes'] });
          qc.invalidateQueries({ queryKey: ['dashboard'] });
        }}
      />

      <Modal
        open={!!confirmDelete}
        title="Apagar reparação"
        onClose={() => setConfirmDelete(null)}
        footer={<>
          <button type="button" onClick={() => setConfirmDelete(null)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={remove.isPending}
            onClick={() => confirmDelete && remove.mutate(confirmDelete)}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {remove.isPending ? 'A apagar…' : 'Apagar'}
          </button>
        </>}
      >
        {confirmDelete && (
          <p className="text-sm">Apagar <strong>#{confirmDelete.numero} {confirmDelete.equipamento}</strong>? Vai ser ocultada (soft delete) mas pode ser recuperada.</p>
        )}
      </Modal>
    </div>
  );
}

function ReparacaoCard({ r, onClick, onDelete }: { r: Reparacao; onClick: () => void; onDelete: () => void }) {
  return (
    <li className="group relative rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      <button type="button" onClick={onClick} className="flex w-full flex-col gap-1 p-4 pr-12 text-left">
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs font-mono text-zinc-500">#{r.numero}</span>
          <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${STATUS_COLOR[r.estado]}`}>
            {STATUS_LABEL[r.estado]}
          </span>
        </div>
        <div className="font-medium">{r.equipamento}</div>
        <div className="text-xs text-zinc-500">
          {r.cliente.nome}{r.cliente.telefone && ` · ${displayPhone(r.cliente.telefone)}`}
        </div>
        <div className="text-xs text-zinc-500 line-clamp-1">{r.avaria}</div>
        <div className="mt-1 flex items-center justify-between text-xs">
          <span className="text-zinc-500">{formatDate(r.recebidoEm)}</span>
          <span className="font-medium">{formatCents(r.precoFinalCents ?? r.orcamentoCents)}</span>
        </div>
      </button>
      <button
        type="button"
        onClick={(e) => { e.stopPropagation(); onDelete(); }}
        className="absolute right-2 top-2 rounded-md p-1.5 text-zinc-400 transition hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40"
        aria-label="Apagar reparação"
        title="Apagar"
      >
        ✕
      </button>
    </li>
  );
}

function CreateReparacaoModal({
  open,
  onClose,
  onCreated,
}: {
  open: boolean;
  onClose: () => void;
  onCreated: (rep: Reparacao) => void;
}) {
  const [clienteSearch, setClienteSearch] = useState('');
  const [clienteId, setClienteId] = useState<string | null>(null);
  const [novoClienteOpen, setNovoClienteOpen] = useState(false);
  const [equipamento, setEquipamento] = useState('');
  const [avaria, setAvaria] = useState('');
  const [imei, setImei] = useState('');
  const [orcamento, setOrcamento] = useState('');
  const [comoOrcamento, setComoOrcamento] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [precoSugerirOpen, setPrecoSugerirOpen] = useState(false);
  const [templateId, setTemplateId] = useState<string | null>(null);
  const [fieldValues, setFieldValues] = useState<EquipmentFieldValuesMap>({});

  const imeiNormalizado = imei.replace(/\D/g, '');
  const imeiSearchEnabled = imeiNormalizado.length >= 6;

  const historicoImei = useQuery({
    queryKey: ['historico-imei', imeiNormalizado],
    queryFn: () => reparacoesApi.historicoImei(imeiNormalizado),
    enabled: open && imeiSearchEnabled,
    staleTime: 30_000,
  });

  const clientes = useQuery({
    queryKey: ['clientes-lookup', clienteSearch],
    queryFn: () => clientesApi.list(clienteSearch, 1, 10),
    enabled: open,
    placeholderData: keepPreviousData,
  });

  const templates = useQuery({
    queryKey: ['equipment-field-templates-active'],
    queryFn: () => equipmentFieldTemplatesApi.active(),
    enabled: open,
    staleTime: 60_000,
  });

  const selectedTemplate = templates.data?.find((template) => template.id === templateId) ?? null;
  const requiredMissing = missingRequiredEquipmentFields(selectedTemplate, fieldValues);

  const create = useMutation({
    mutationFn: () =>
      reparacoesApi.create({
        clienteId: clienteId!,
        equipamento: equipamento.trim(),
        avaria: avaria.trim(),
        imei: imeiNormalizado || null,
        orcamentoCents: orcamento ? Math.round(parseFloat(orcamento.replace(',', '.')) * 100) : null,
        notas: null,
        estadoInicial: comoOrcamento ? 7 : null,
        equipmentFieldTemplateId: selectedTemplate?.id ?? null,
        fields: selectedTemplate ? buildEquipmentFieldValues(selectedTemplate, fieldValues) : null,
      }),
    onSuccess: (rep) => {
      reset();
      onCreated(rep);
    },
    onError: (err) => {
      if (isAxiosError(err)) {
        const data = err.response?.data as { detail?: string; title?: string } | undefined;
        setError(data?.detail ?? data?.title ?? 'Erro ao criar.');
      } else setError('Erro ao criar.');
    },
  });

  function reset() {
    setClienteSearch('');
    setClienteId(null);
    setEquipamento('');
    setAvaria('');
    setImei('');
    setOrcamento('');
    setComoOrcamento(false);
    setTemplateId(null);
    setFieldValues({});
    setError(null);
  }

  function handleTemplateChange(id: string) {
    const nextTemplate = templates.data?.find((template) => template.id === id) ?? null;
    setTemplateId(nextTemplate?.id ?? null);
    setFieldValues(nextTemplate ? initEquipmentFieldValues(nextTemplate) : {});
  }

  return (
    <Modal
      open={open}
      title="Nova reparação"
      onClose={() => {
        reset();
        onClose();
      }}
      footer={
        <>
          <button
            type="button"
            onClick={() => {
              reset();
              onClose();
            }}
            className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
          >
            Cancelar
          </button>
          <button
            type="button"
            disabled={!clienteId || !equipamento || !avaria || requiredMissing || create.isPending}
            onClick={() => create.mutate()}
            className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-60"
          >
            {create.isPending ? 'A criar…' : 'Criar'}
          </button>
        </>
      }
    >
      <div className="space-y-3">
        {error && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
            {error}
          </div>
        )}
        <div className="space-y-1">
          <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">Cliente *</label>
          {clienteId ? (
            <div className="flex items-center justify-between rounded-lg border border-zinc-300 bg-zinc-50 px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950">
              {clientes.data?.items.find((c) => c.id === clienteId)?.nome ?? 'Selecionado'}
              <button type="button" onClick={() => setClienteId(null)} className="text-xs text-zinc-500">trocar</button>
            </div>
          ) : (
            <>
              <div className="flex gap-2">
                <input
                  placeholder="Pesquisar cliente…"
                  value={clienteSearch}
                  onChange={(e) => setClienteSearch(e.target.value)}
                  className="flex-1 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
                />
                <button
                  type="button"
                  onClick={() => setNovoClienteOpen(true)}
                  className="rounded-lg bg-zinc-100 px-3 py-2 text-xs font-medium text-zinc-700 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300"
                >
                  + Novo
                </button>
              </div>
              {clientes.data && clientes.data.items.length > 0 && (
                <ul className="max-h-48 overflow-y-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
                  {clientes.data.items.map((c) => (
                    <li key={c.id}>
                      <button
                        type="button"
                        onClick={() => setClienteId(c.id)}
                        className="block w-full px-3 py-2 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800"
                      >
                        <div className="font-medium">{c.nome}</div>
                        {c.telefone && <div className="text-xs text-zinc-500">{displayPhone(c.telefone)}</div>}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </div>

        <Field label="Equipamento" required>
          <input
            value={equipamento}
            onChange={(e) => setEquipamento(e.target.value)}
            placeholder="ex: iPhone 13 Pro Max"
            className={inputCls}
          />
          <button
            type="button"
            onClick={() => setPrecoSugerirOpen(true)}
            className="mt-1 inline-flex items-center gap-1 text-xs text-brand-600 hover:underline dark:text-brand-400"
          >
            <Tags size={12} strokeWidth={2} /> Sugerir da tabela de preços
          </button>
        </Field>
        <Field label="Avaria reportada" required>
          <textarea
            rows={3}
            value={avaria}
            onChange={(e) => setAvaria(e.target.value)}
            className={inputCls + ' resize-none'}
          />
        </Field>
        <Field label="IMEI / Serial (recomendado)">
          <input
            value={imei}
            onChange={(e) => setImei(e.target.value)}
            inputMode="numeric"
            placeholder="ex: 359123456789012 ou *#06# no telemóvel"
            className={inputCls + ' font-mono'}
          />
          {imeiSearchEnabled && historicoImei.data && (
            <div className="mt-1 space-y-1 text-xs">
              {!historicoImei.data.luhnValido && imeiNormalizado.length === 15 && (
                <div className="flex items-start gap-1.5 text-amber-700 dark:text-amber-400">
                  <AlertTriangle size={13} strokeWidth={2} className="mt-0.5 flex-none" />
                  <span>IMEI com 15 dígitos mas check-digit Luhn inválido. Confirma se está bem escrito.</span>
                </div>
              )}
              {historicoImei.data.luhnValido && (
                <div className="text-emerald-700 dark:text-emerald-400">✓ IMEI válido</div>
              )}
              {historicoImei.data.total > 0 && (
                <div className="rounded-lg border border-amber-300 bg-amber-50 p-2 text-amber-800 dark:border-amber-800/60 dark:bg-amber-950/30 dark:text-amber-200">
                  <div className="flex items-start gap-1.5">
                    <AlertTriangle size={13} strokeWidth={2} className="mt-0.5 flex-none" />
                    <span>Este IMEI já entrou cá <strong>{historicoImei.data.total}</strong> {historicoImei.data.total === 1 ? 'vez' : 'vezes'}:</span>
                  </div>
                  <ul className="mt-1 space-y-0.5 pl-5">
                    {historicoImei.data.items.slice(0, 3).map((it) => (
                      <li key={it.id}>
                        · #{it.numero} {it.equipamento} ({new Date(it.recebidoEm).toLocaleDateString('pt-PT')}) — {it.cliente.nome}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </Field>
        <Field label="Categoria equipamento">
          <select
            value={templateId ?? ''}
            onChange={(e) => handleTemplateChange(e.target.value)}
            className={inputCls}
          >
            <option value="">Sem template personalizado</option>
            {templates.data?.map((template) => (
              <option key={template.id} value={template.id}>{template.nome}</option>
            ))}
          </select>
          <p className="mt-1 text-xs text-zinc-500">
            Usa templates para portateis/desktops com CPU, RAM, storage e outros dados tecnicos.
          </p>
        </Field>
        <EquipmentFieldsForm
          template={selectedTemplate}
          values={fieldValues}
          onChange={(fieldId, value) => setFieldValues((current) => ({ ...current, [fieldId]: value }))}
        />
        <Field label="Orçamento (€)">
          <input
            inputMode="decimal"
            value={orcamento}
            onChange={(e) => setOrcamento(e.target.value)}
            placeholder="0,00"
            className={inputCls}
          />
        </Field>
        <label className="flex cursor-pointer items-start gap-2 rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-sm dark:border-zinc-800 dark:bg-zinc-950">
          <input
            type="checkbox"
            checked={comoOrcamento}
            onChange={(e) => setComoOrcamento(e.target.checked)}
            className="mt-0.5"
          />
          <span>
            <span className="font-medium">Apenas orçamento</span>
            <span className="block text-xs text-zinc-500">Cliente pediu preço por mensagem mas ainda não trouxe o equipamento.</span>
          </span>
        </label>
      </div>
      <NovoClienteModal
        open={novoClienteOpen}
        onClose={() => setNovoClienteOpen(false)}
        onCreated={(c) => {
          setNovoClienteOpen(false);
          setClienteId(c.id);
          setClienteSearch(c.nome);
        }}
      />
      <SugerirPrecoModal
        open={precoSugerirOpen}
        onClose={() => setPrecoSugerirOpen(false)}
        onPicked={(picked) => {
          setPrecoSugerirOpen(false);
          // Preencher equipamento (mantém o existente se já estiver preenchido)
          if (!equipamento.trim()) setEquipamento(`${picked.marca} ${picked.modelo}`);
          if (!avaria.trim()) setAvaria(picked.servico);
          // Sempre overrida orçamento (intenção clara)
          setOrcamento((picked.pvpCents / 100).toFixed(2));
        }}
      />
    </Modal>
  );
}

function NovoClienteModal({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: (c: { id: string; nome: string }) => void }) {
  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => clientesApi.create({
      nome: nome.trim(),
      telefone: telefone.trim() || null,
      email: null,
      nif: null,
      notas: null,
    }),
    onSuccess: (c) => { setNome(''); setTelefone(''); setError(null); onCreated(c); },
    onError: (err) => {
      if (isAxiosError(err)) {
        const data = err.response?.data as { detail?: string; errors?: Record<string, string[]> } | undefined;
        if (data?.errors) setError(Object.values(data.errors).flat().join(' '));
        else setError(data?.detail ?? 'Erro');
      }
    },
  });

  return (
    <Modal open={open} title="Novo cliente" onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button type="button" disabled={!nome || create.isPending}
          onClick={() => create.mutate()}
          className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
          {create.isPending ? 'A criar…' : 'Criar e selecionar'}
        </button>
      </>}
    >
      <div className="space-y-3">
        {error && <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950/40 dark:text-red-300">{error}</div>}
        <Field label="Nome" required>
          <input value={nome} onChange={e => setNome(e.target.value)} className={inputCls} autoFocus />
        </Field>
        <Field label="Telefone (opcional)">
          <input value={telefone} onChange={e => setTelefone(e.target.value)} className={inputCls} placeholder="ou vazio se for via Messenger" />
        </Field>
      </div>
    </Modal>
  );
}

const inputCls =
  'w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';

function Field({ label, required, children }: { label: string; required?: boolean; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {children}
    </div>
  );
}

// ============ Kanban ============

function KanbanBoard({
  data,
  loading,
  onMove,
  onCardClick,
  pending,
}: {
  data: Map<RepairStatus, Reparacao[]> | undefined;
  loading: boolean;
  onMove: (id: string, to: RepairStatus) => void;
  onCardClick: (id: string) => void;
  pending: boolean;
}) {
  const [dragId, setDragId] = useState<string | null>(null);
  const [dragFrom, setDragFrom] = useState<RepairStatus | null>(null);
  const [dropTarget, setDropTarget] = useState<RepairStatus | null>(null);

  if (loading) {
    return (
      <div className="grid grid-cols-1 gap-3 lg:grid-cols-3 xl:grid-cols-6">
        {Array.from({ length: 6 }).map((_, index) => <SkeletonCard key={index} />)}
      </div>
    );
  }

  return (
    <div className="-mx-4 overflow-x-auto px-4 pb-4">
      <div className="flex min-w-max gap-3">
        {KANBAN_COLUMNS.map((col) => {
          const items = data?.get(col.estado) ?? [];
          const isDropAllowed = dragFrom !== null && dragFrom !== col.estado && (KANBAN_VALID_DROPS[dragFrom]?.includes(col.estado) ?? false);
          const isDropTarget = dropTarget === col.estado && isDropAllowed;
          return (
            <div
              key={col.estado}
              onDragOver={(e) => { if (isDropAllowed) { e.preventDefault(); setDropTarget(col.estado); } }}
              onDragLeave={() => setDropTarget((t) => (t === col.estado ? null : t))}
              onDrop={(e) => {
                e.preventDefault();
                setDropTarget(null);
                if (dragId && isDropAllowed) onMove(dragId, col.estado);
                setDragId(null);
                setDragFrom(null);
              }}
              className={`w-72 flex-shrink-0 rounded-xl border bg-zinc-50 transition dark:bg-zinc-950 ${
                isDropTarget
                  ? 'border-brand-500 bg-brand-50 dark:border-brand-400 dark:bg-brand-950/30'
                  : isDropAllowed
                    ? 'border-dashed border-zinc-300 dark:border-zinc-700'
                    : 'border-zinc-200 dark:border-zinc-800'
              }`}
            >
              <div className="flex items-center justify-between px-3 py-2 text-xs font-semibold">
                <span className="flex items-center gap-1.5">
                  <col.icon size={14} strokeWidth={2} className="text-zinc-500 dark:text-zinc-400" aria-hidden />
                  {col.label}
                </span>
                <span className="rounded-full bg-zinc-200 px-2 py-0.5 text-[10px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">{items.length}</span>
              </div>
              <ul className="min-h-[100px] space-y-2 px-2 pb-2">
                {items.map((r) => {
                  const diasParado = Math.floor((Date.now() - new Date(r.estadoSince).getTime()) / (1000 * 60 * 60 * 24));
                  const stale = diasParado >= 7;
                  const dragging = dragId === r.id;
                  return (
                    <li
                      key={r.id}
                      draggable={!pending}
                      onDragStart={() => { setDragId(r.id); setDragFrom(col.estado); }}
                      onDragEnd={() => { setDragId(null); setDragFrom(null); setDropTarget(null); }}
                      onClick={() => onCardClick(r.id)}
                      className={`cursor-move rounded-lg border border-zinc-200 bg-white p-2.5 text-xs shadow-sm transition hover:border-brand-300 dark:border-zinc-800 dark:bg-zinc-900 ${dragging ? 'opacity-40' : ''}`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <span className="font-mono text-[10px] text-zinc-500">#{r.numero}</span>
                        {stale && (
                          <span className="inline-flex items-center gap-0.5 rounded-full bg-red-100 px-1.5 py-0.5 text-[9px] font-semibold text-red-700 dark:bg-red-950/40 dark:text-red-300">
                            <Clock size={9} strokeWidth={2.5} /> {diasParado}d
                          </span>
                        )}
                      </div>
                      <div className="mt-1 truncate font-medium">{r.equipamento}</div>
                      <div className="truncate text-[11px] text-zinc-500">{r.cliente.nome}</div>
                      {(r.precoFinalCents ?? r.orcamentoCents) != null && (
                        <div className="mt-1 flex flex-wrap items-center justify-between gap-1">
                          <span className="text-[11px] font-medium tabular-nums">{formatCents(r.precoFinalCents ?? r.orcamentoCents)}</span>
                          <div className="flex items-center gap-1">
                            {r.estadoPagamento === 2 ? (
                              <span className="rounded-full bg-emerald-100 px-1.5 py-0.5 text-[9px] font-medium text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300">✓ Pago</span>
                            ) : r.estado === 5 ? (
                              <span className="rounded-full bg-rose-100 px-1.5 py-0.5 text-[9px] font-medium text-rose-700 dark:bg-rose-900/40 dark:text-rose-300">Por cobrar</span>
                            ) : null}
                            {r.invoiceNumber ? (
                              <span title={`Fatura ${r.invoiceNumber}`} className="rounded-full bg-brand-100 px-1.5 py-0.5 text-[9px] font-medium text-brand-700 dark:bg-brand-900/40 dark:text-brand-300">
                                📄 Faturada
                              </span>
                            ) : r.estadoPagamento === 2 ? (
                              <span title="Pago sem fatura — emite via Moloni" className="rounded-full bg-amber-100 px-1.5 py-0.5 text-[9px] font-medium text-amber-700 dark:bg-amber-900/40 dark:text-amber-300">
                                Sem fatura
                              </span>
                            ) : null}
                          </div>
                        </div>
                      )}
                    </li>
                  );
                })}
                {items.length === 0 && (
                  <li className="rounded-lg border border-dashed border-zinc-300 p-3 text-center text-[11px] text-zinc-400 dark:border-zinc-800">
                    {isDropAllowed ? 'Larga aqui →' : 'Sem reparações'}
                  </li>
                )}
              </ul>
            </div>
          );
        })}
      </div>
      {pending && (
        <div className="mt-2 text-xs text-zinc-500">A guardar mudança…</div>
      )}
    </div>
  );
}

// ============ Sugerir Preço Modal ============

function SugerirPrecoModal({
  open,
  onClose,
  onPicked,
}: {
  open: boolean;
  onClose: () => void;
  onPicked: (entry: PriceTableEntry) => void;
}) {
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 200);

  const list = useQuery({
    queryKey: ['precos-sugerir', debouncedSearch],
    queryFn: () => precosApi.list({ q: debouncedSearch || undefined, pageSize: 30 }),
    enabled: open && debouncedSearch.length >= 2,
  });

  return (
    <Modal
      open={open}
      title="Sugerir da tabela de preços"
      onClose={() => { setSearch(''); onClose(); }}
    >
      <div className="space-y-3">
        <input
          autoFocus
          type="search"
          placeholder="Pesquisar marca, modelo ou serviço… (ex: iPhone 13 ecrã)"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
        />
        {debouncedSearch.length < 2 && (
          <p className="text-xs text-zinc-500">Escreve pelo menos 2 caracteres para procurar.</p>
        )}
        {debouncedSearch.length >= 2 && list.data && list.data.items.length === 0 && (
          <div className="rounded-lg border border-dashed border-zinc-300 p-4 text-center text-xs text-zinc-500 dark:border-zinc-700">
            Nenhuma combinacao encontrada. Adiciona esta combinacao em <a href="/precos" target="_blank" className="rounded-sm text-brand-600 hover:underline focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">/precos</a>.
          </div>
        )}
        {list.data && list.data.items.length > 0 && (
          <ul className="max-h-80 overflow-y-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
            {list.data.items.map((e) => (
              <li key={e.id}>
                <button
                  type="button"
                  onClick={() => onPicked(e)}
                  className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800"
                >
                  <div className="min-w-0 flex-1">
                    <div className="font-medium">{e.marca} {e.modelo}</div>
                    <div className="text-xs text-zinc-500">
                      {e.servico}
                      {e.tempoEstimadoMin && <span className="ml-2 inline-flex items-center gap-0.5"><Timer size={11} strokeWidth={2} /> {e.tempoEstimadoMin}m</span>}
                      {e.margemPct != null && <span className="ml-2">{e.margemPct}% margem</span>}
                    </div>
                  </div>
                  <span className="font-semibold tabular-nums text-brand-600 dark:text-brand-400">
                    {formatCents(e.pvpCents)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}

function useDebouncedValue<T>(value: T, ms: number): T {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

// Transições válidas para drag-drop (subset relaxado das VALID_TRANSITIONS,
// só para os 6 estados Kanban). Espelha lógica do backend IsValidTransition.
const KANBAN_VALID_DROPS: Partial<Record<RepairStatus, RepairStatus[]>> = {
  0: [1],                // Recebido → Diagnóstico
  1: [2, 3, 4],          // Diagnóstico → Aguarda Peça / Em Reparação / Reparado
  2: [3, 1],             // Aguarda Peça → Em Reparação / Diagnóstico
  3: [4, 2],             // Em Reparação → Reparado / Aguarda Peça
  4: [5, 1],             // Reparado → Entregue / Diagnóstico (reabrir)
};

// ============ Import Reparações Modal ============

function ImportReparacoesModal({
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
  const [result, setResult] = useState<import('../../lib/reparacoes/api').ImportReparacoesResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  const imp = useMutation({
    mutationFn: () => reparacoesApi.importCsv(csv),
    onSuccess: (r) => { setResult(r); onDone(); },
    onError: (err) => {
      if (isAxiosError(err)) {
        const d = err.response?.data as { detail?: string; title?: string } | undefined;
        setError(d?.detail ?? d?.title ?? 'Erro ao importar.');
      } else setError('Erro ao importar.');
    },
  });

  function reset() {
    setCsv(''); setFileName(null); setResult(null); setError(null);
  }

  function handleFile(file: File) {
    if (file.size > 10 * 1024 * 1024) { setError('Ficheiro demasiado grande (máx 10 MB).'); return; }
    setFileName(file.name); setError(null);
    file.text().then((t) => setCsv(t));
  }

  const previewLines = csv ? csv.split(/\r?\n/).filter((l) => l.trim()).slice(0, 6) : [];

  return (
    <Modal
      open={open}
      title="Importar reparações de CSV"
      onClose={() => { reset(); onClose(); }}
      footer={
        result ? (
          <button type="button" onClick={() => { reset(); onClose(); }} className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white">Fechar</button>
        ) : (
          <>
            <button type="button" onClick={() => { reset(); onClose(); }} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
            <button type="button" disabled={!csv || imp.isPending} onClick={() => imp.mutate()} className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
              {imp.isPending ? 'A importar…' : 'Importar'}
            </button>
          </>
        )
      }
    >
      <div className="space-y-3">
        {error && (
          <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300">{error}</div>
        )}

        {result ? (
          <div className="space-y-3 text-sm">
            <div className="grid grid-cols-4 gap-2">
              <div className="rounded-lg border border-emerald-300 bg-emerald-50 p-3 text-center dark:border-emerald-800/60 dark:bg-emerald-950/30">
                <div className="text-2xl font-semibold text-emerald-700 dark:text-emerald-300">{result.criadas}</div>
                <div className="text-[10px] uppercase text-emerald-700/80 dark:text-emerald-300/80">Reparações</div>
              </div>
              <div className="rounded-lg border border-brand-300 bg-brand-50 p-3 text-center dark:border-brand-800/60 dark:bg-brand-950/30">
                <div className="text-2xl font-semibold text-brand-700 dark:text-brand-300">{result.clientesCriados}</div>
                <div className="text-[10px] uppercase text-brand-700/80 dark:text-brand-300/80">Clientes novos</div>
              </div>
              <div className="rounded-lg border border-zinc-300 bg-zinc-50 p-3 text-center dark:border-zinc-700 dark:bg-zinc-900">
                <div className="text-2xl font-semibold text-zinc-600 dark:text-zinc-300">{result.clientesReutilizados}</div>
                <div className="text-[10px] uppercase text-zinc-500">Já existentes</div>
              </div>
              <div className="rounded-lg border border-rose-300 bg-rose-50 p-3 text-center dark:border-rose-800/60 dark:bg-rose-950/30">
                <div className="text-2xl font-semibold text-rose-700 dark:text-rose-300">{result.comErro}</div>
                <div className="text-[10px] uppercase text-rose-700/80 dark:text-rose-300/80">Com erro</div>
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
              <p className="font-medium text-zinc-700 dark:text-zinc-300">Colunas reconhecidas (header):</p>
              <ul className="mt-1 space-y-0.5">
                <li>· <strong>equipamento</strong>, <strong>avaria</strong> (obrigatórios)</li>
                <li>· cliente / clientenome, telefone, nif, email — pelo menos 1 para identificar o cliente</li>
                <li>· imei/serial, diagnostico, notas (opcionais)</li>
                <li>· orcamento, preco/precofinal (€ — aceita "150" ou "150,00")</li>
                <li>· estado ("Recebido", "Diagnóstico", "Aguarda peça", "Em reparação", "Pronto", "Entregue"...)</li>
                <li>· recebidoem/data (yyyy-MM-dd ou dd/MM/yyyy)</li>
                <li>· pago (Sim/Não/Parcial)</li>
              </ul>
              <p className="mt-1">Se o cliente já existir (NIF ou telefone), reaproveita. Senão cria automaticamente.</p>
            </div>

            <div
              onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
              onDragLeave={() => setDragging(false)}
              onDrop={(e) => { e.preventDefault(); setDragging(false); const f = e.dataTransfer.files[0]; if (f) handleFile(f); }}
              className={`rounded-xl border-2 border-dashed p-6 text-center text-sm transition ${
                dragging
                  ? 'border-brand-500 bg-brand-50 dark:border-brand-400 dark:bg-brand-950/30'
                  : 'border-zinc-300 bg-white dark:border-zinc-700 dark:bg-zinc-950'
              }`}
            >
              {fileName ? (
                <>
                  <div className="font-medium">📄 {fileName}</div>
                  <button type="button" onClick={() => { setCsv(''); setFileName(null); }} className="mt-1 text-xs text-zinc-500 underline">Escolher outro</button>
                </>
              ) : (
                <>
                  <div className="text-zinc-500">Arrasta o ficheiro CSV para aqui</div>
                  <div className="mt-1 text-xs text-zinc-400">ou</div>
                  <label className="mt-2 inline-block cursor-pointer rounded-md bg-brand-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-700">
                    Selecionar ficheiro
                    <input type="file" accept=".csv,text/csv,text/plain" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f); }} />
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
