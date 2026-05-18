import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { BriefcaseBusiness, Plus, Search } from 'lucide-react';
import { isAxiosError } from 'axios';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { clientesApi } from '../../lib/clientes/api';
import { displayPhone } from '../../lib/phone/formatter';
import { trabalhosApi } from '../../lib/trabalhos/api';
import {
  CATEGORIA_LABEL,
  JOB_CATEGORY,
  TRABALHO_STATUS_COLOR,
  TRABALHO_STATUS_LABEL,
  type JobCategory,
  type Trabalho,
  type TrabalhoStatus,
} from '../../lib/trabalhos/types';
import { formatCents, formatDateOnly, parseEuros } from '../../lib/money';

const TABS: Array<{ value: TrabalhoStatus | null; label: string }> = [
  { value: null, label: 'Todos' },
  { value: 0, label: 'Orçamentos' },
  { value: 1, label: 'Aceites' },
  { value: 2, label: 'Em execução' },
  { value: 3, label: 'Concluídos' },
];

export default function Trabalhos() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [status, setStatus] = useState<TrabalhoStatus | null>(null);
  const [categoria, setCategoria] = useState<JobCategory | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<Trabalho | null>(null);

  const remove = useMutation({
    mutationFn: (t: Trabalho) => trabalhosApi.remove(t.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['trabalhos'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setConfirmDelete(null);
    },
  });

  const list = useQuery({
    queryKey: ['trabalhos', status, categoria, search, page],
    queryFn: () => trabalhosApi.list({ q: search, status, categoria, page, pageSize: 20 }),
    placeholderData: keepPreviousData,
  });
  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / 20));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Trabalhos"
        description="Servicos, websites, software e outros trabalhos fora da reparacao de bancada."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'trabalho' : 'trabalhos'}</span>}
        actions={<Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>Novo</Button>}
      />

      <div className="-mx-4 overflow-x-auto px-4 pb-1">
        <div className="flex gap-2">
          {TABS.map((t) => (
            <button
              key={t.label}
              type="button"
              onClick={() => { setStatus(t.value); setPage(1); }}
              className={`whitespace-nowrap rounded-full px-3 py-1.5 text-xs font-medium transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${
                t.value === status
                  ? 'bg-brand-600 text-white'
                  : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300'
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      <div className="flex gap-2">
        <select
          value={categoria ?? ''}
          onChange={(e) => { setCategoria(e.target.value === '' ? null : (Number(e.target.value) as JobCategory)); setPage(1); }}
          className="rounded-lg border border-zinc-300 bg-white px-2 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
        >
          <option value="">Todas categorias</option>
          {Object.entries(JOB_CATEGORY).map(([_, v]) => (
            <option key={v} value={v}>{CATEGORIA_LABEL[v]}</option>
          ))}
        </select>
        <div className="relative min-w-0 flex-1">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input
            type="search"
            placeholder="Pesquisar titulo, cliente..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </div>
      </div>

      <ul className="space-y-2">
        {list.isLoading && Array.from({ length: 4 }).map((_, index) => <SkeletonCard key={index} />)}
        {items.map((t) => (
          <Card
            key={t.id}
            t={t}
            onClick={() => navigate(`/trabalhos/${t.id}`)}
            onDelete={() => setConfirmDelete(t)}
          />
        ))}
        {items.length === 0 && !list.isLoading && (
          <li>
            <EmptyState
              icon={search ? Search : BriefcaseBusiness}
              title={search ? 'Nenhum trabalho encontrado' : 'Ainda nao ha trabalhos'}
              description={search ? 'Ajusta a pesquisa ou muda os filtros para encontrares o trabalho.' : 'Cria trabalhos para websites, software, assistencias e servicos que nao sao reparacoes.'}
              action={!search ? <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>Criar trabalho</Button> : undefined}
            />
          </li>
        )}
      </ul>

      {lastPage > 1 && (
        <div className="flex items-center justify-between text-xs text-zinc-500">
          <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="rounded-md px-2 py-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">← Anterior</button>
          <span>{page} / {lastPage}</span>
          <button disabled={page >= lastPage} onClick={() => setPage(p => p + 1)} className="rounded-md px-2 py-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">Seguinte →</button>
        </div>
      )}

      <CreateTrabalhoModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={() => {
          qc.invalidateQueries({ queryKey: ['trabalhos'] });
          setCreateOpen(false);
        }}
      />

      <Modal
        open={!!confirmDelete}
        title="Apagar trabalho"
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
        {confirmDelete && <p className="text-sm">Apagar <strong>#{confirmDelete.numero} {confirmDelete.titulo}</strong>?</p>}
      </Modal>
    </div>
  );
}

function Card({ t, onClick, onDelete }: { t: Trabalho; onClick: () => void; onDelete: () => void }) {
  return (
    <li className="relative rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      <button type="button" onClick={onClick} className="flex w-full flex-col gap-1 p-4 pr-12 text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs font-mono text-zinc-500">#{t.numero} · {CATEGORIA_LABEL[t.categoria]}</span>
          <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${TRABALHO_STATUS_COLOR[t.status]}`}>
            {TRABALHO_STATUS_LABEL[t.status]}
          </span>
        </div>
        <div className="font-medium">{t.titulo}</div>
        {t.cliente && (
          <div className="text-xs text-zinc-500">
            {t.cliente.nome}{t.cliente.telefone && ` · ${displayPhone(t.cliente.telefone)}`}
          </div>
        )}
        <div className="mt-1 flex items-center justify-between text-xs">
          <span className="text-zinc-500">{formatDateOnly(t.dataConclusao ?? t.dataInicio ?? t.createdAt)}</span>
          <span className="font-medium">{formatCents(t.precoFinalCents ?? t.orcamentoCents)}</span>
        </div>
      </button>
      <button
        type="button"
        onClick={(e) => { e.stopPropagation(); onDelete(); }}
        className="absolute right-2 top-2 rounded-md p-1.5 text-zinc-400 transition hover:bg-red-50 hover:text-red-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-red-950/40"
        aria-label="Apagar trabalho"
        title="Apagar"
      >
        ✕
      </button>
    </li>
  );
}

function CreateTrabalhoModal({ open, onClose, onCreated }: { open: boolean; onClose: () => void; onCreated: () => void }) {
  const [clienteSearch, setClienteSearch] = useState('');
  const [clienteId, setClienteId] = useState<string | null>(null);
  const [titulo, setTitulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [categoria, setCategoria] = useState<JobCategory>(JOB_CATEGORY.Outro);
  const [orcamento, setOrcamento] = useState('');
  const [error, setError] = useState<string | null>(null);

  const clientes = useQuery({
    queryKey: ['clientes-lookup', clienteSearch],
    queryFn: () => clientesApi.list(clienteSearch, 1, 10),
    enabled: open,
    placeholderData: keepPreviousData,
  });

  const create = useMutation({
    mutationFn: () => trabalhosApi.create({
      clienteId,
      titulo: titulo.trim(),
      descricao: descricao.trim() || null,
      categoria,
      orcamentoCents: parseEuros(orcamento),
      notas: null,
    }),
    onSuccess: () => { reset(); onCreated(); },
    onError: (err) => {
      if (isAxiosError(err)) {
        const data = err.response?.data as { detail?: string } | undefined;
        setError(data?.detail ?? 'Erro ao criar.');
      }
    },
  });

  function reset() {
    setClienteSearch(''); setClienteId(null); setTitulo(''); setDescricao('');
    setCategoria(JOB_CATEGORY.Outro); setOrcamento(''); setError(null);
  }

  return (
    <Modal open={open} title="Novo trabalho" onClose={() => { reset(); onClose(); }}
      footer={<>
        <button type="button" onClick={() => { reset(); onClose(); }} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button type="button" disabled={!titulo || !clienteId || create.isPending}
          onClick={() => create.mutate()}
          className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          title={!clienteId ? 'Selecciona um cliente primeiro' : undefined}>
          {create.isPending ? 'A criar…' : 'Criar'}
        </button>
      </>}
    >
      <div className="space-y-3">
        {error && <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950/40 dark:text-red-300">{error}</div>}

        <Field label="Título *">
          <input value={titulo} onChange={e => setTitulo(e.target.value)} className={inputCls} autoFocus />
        </Field>
        <Field label="Categoria">
          <select value={categoria} onChange={e => setCategoria(Number(e.target.value) as JobCategory)} className={inputCls}>
            {Object.entries(JOB_CATEGORY).map(([_, v]) => (
              <option key={v} value={v}>{CATEGORIA_LABEL[v]}</option>
            ))}
          </select>
        </Field>
        <Field label="Cliente *">
          {clienteId ? (
            <div className="flex items-center justify-between rounded-lg border border-zinc-300 bg-zinc-50 px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950">
              {clientes.data?.items.find(c => c.id === clienteId)?.nome ?? 'Selecionado'}
              <button type="button" onClick={() => setClienteId(null)} className="text-xs text-zinc-500">trocar</button>
            </div>
          ) : (
            <>
              <input placeholder="Pesquisar…" value={clienteSearch} onChange={e => setClienteSearch(e.target.value)} className={inputCls} />
              {clientes.data && clientes.data.items.length > 0 && (
                <ul className="mt-1 max-h-32 overflow-y-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
                  {clientes.data.items.map(c => (
                    <li key={c.id}>
                      <button type="button" onClick={() => setClienteId(c.id)} className="block w-full px-3 py-1.5 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
                        {c.nome} <span className="text-xs text-zinc-500">· {displayPhone(c.telefone)}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </Field>
        <Field label="Descrição">
          <textarea rows={3} value={descricao} onChange={e => setDescricao(e.target.value)} className={inputCls + ' resize-none'} />
        </Field>
        <Field label="Preço estimado (€)">
          <input inputMode="decimal" value={orcamento} onChange={e => setOrcamento(e.target.value)} placeholder="0,00" className={inputCls} />
        </Field>
      </div>
    </Modal>
  );
}

const inputCls = 'w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">{label}</label>
      {children}
    </div>
  );
}
