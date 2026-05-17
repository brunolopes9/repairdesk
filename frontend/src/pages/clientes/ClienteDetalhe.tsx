import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Mail, MessageCircle, Phone } from 'lucide-react';
import Modal from '../../components/Modal';
import { clientesApi } from '../../lib/clientes/api';
import { reparacoesApi } from '../../lib/reparacoes/api';
import { STATUS_COLOR, STATUS_LABEL, type Reparacao } from '../../lib/reparacoes/types';
import { trabalhosApi } from '../../lib/trabalhos/api';
import {
  CATEGORIA_LABEL,
  TRABALHO_STATUS_COLOR,
  TRABALHO_STATUS_LABEL,
  type Trabalho,
} from '../../lib/trabalhos/types';
import { formatCents, formatDateOnly } from '../../lib/money';
import ClienteFormView from './ClienteForm';

export default function ClienteDetalhe() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const cliente = useQuery({
    queryKey: ['cliente', id],
    queryFn: () => clientesApi.get(id!),
    enabled: !!id,
  });

  const reparacoes = useQuery({
    queryKey: ['cliente-reparacoes', id],
    queryFn: () => reparacoesApi.list({ clienteId: id, pageSize: 100 }),
    enabled: !!id,
  });

  const trabalhos = useQuery({
    queryKey: ['cliente-trabalhos', id],
    queryFn: () => trabalhosApi.list({ clienteId: id, pageSize: 100 }),
    enabled: !!id,
  });

  const [editOpen, setEditOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  const update = useMutation({
    mutationFn: (form: Parameters<typeof clientesApi.update>[1]) => clientesApi.update(id!, form),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cliente', id] });
      qc.invalidateQueries({ queryKey: ['clientes'] });
      setEditOpen(false);
    },
  });

  const remove = useMutation({
    mutationFn: () => clientesApi.remove(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clientes'] });
      navigate('/clientes');
    },
  });

  if (cliente.isLoading) return <div className="text-sm text-zinc-500">A carregar…</div>;
  if (cliente.isError || !cliente.data) return <div className="text-sm text-red-600">Cliente não encontrado.</div>;

  const c = cliente.data;
  const reps = reparacoes.data?.items ?? [];
  const trabs = trabalhos.data?.items ?? [];

  // KPIs
  const repsPagas = reps.filter((r) => r.estado === 5);
  const trabsPagos = trabs.filter((t) => t.status === 3); // TRABALHO_STATUS.Concluido
  const totalGasto =
    repsPagas.reduce((s, r) => s + (r.precoFinalCents ?? r.orcamentoCents ?? 0), 0) +
    trabsPagos.reduce((s, t) => s + (t.precoFinalCents ?? t.orcamentoCents ?? 0), 0);
  const lucroTotal =
    repsPagas.reduce((s, r) => s + r.lucroCents, 0) +
    trabsPagos.reduce((s, t) => s + t.lucroCents, 0);
  const ultimaVisita = [...reps, ...trabs]
    .map((x) => ('recebidoEm' in x ? x.recebidoEm : x.createdAt))
    .sort()
    .at(-1);
  const abertosCount =
    reps.filter((r) => r.estado !== 5 && r.estado !== 6).length +
    trabs.filter((t) => t.status !== 3 && t.status !== 4).length;

  const cleanPhone = c.telefone?.replace(/\s/g, '') ?? '';

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between gap-2 text-sm">
        <button onClick={() => navigate(-1)} className="text-zinc-500 hover:underline">← voltar</button>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setEditOpen(true)}
            className="rounded-md border border-zinc-200 px-2 py-1 text-xs text-zinc-700 hover:bg-zinc-100 dark:border-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-800"
          >
            ✎ Editar
          </button>
          <button
            type="button"
            onClick={() => setConfirmDelete(true)}
            className="rounded-md px-2 py-1 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40"
          >
            Apagar
          </button>
        </div>
      </div>

      <header className="space-y-2">
        <h1 className="text-2xl font-semibold tracking-tight">{c.nome}</h1>
        <div className="space-y-1 text-sm text-zinc-600 dark:text-zinc-300">
          {c.telefone ? (
            <div className="flex items-center gap-1.5"><Phone size={13} strokeWidth={2} className="text-zinc-400" /> {c.telefone}</div>
          ) : (
            <div className="italic text-zinc-400">sem telefone (Messenger)</div>
          )}
          {c.email && <div className="flex items-center gap-1.5"><Mail size={13} strokeWidth={2} className="text-zinc-400" /> {c.email}</div>}
          {c.nif && <div>NIF {c.nif}</div>}
        </div>
        {cleanPhone && (
          <div className="flex gap-2">
            <a
              href={`tel:${cleanPhone}`}
              className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-700"
            ><Phone size={12} strokeWidth={2} /> Ligar</a>
            <a
              href={`https://wa.me/${cleanPhone.replace('+', '')}?text=${encodeURIComponent(`Olá ${c.nome}, é da LopesTech.`)}`}
              target="_blank" rel="noopener noreferrer"
              className="inline-flex items-center gap-1 rounded-lg bg-green-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-green-600"
            ><MessageCircle size={12} strokeWidth={2} /> WhatsApp</a>
          </div>
        )}
        {c.notas && (
          <p className="rounded-lg bg-zinc-50 px-3 py-2 text-sm text-zinc-700 dark:bg-zinc-950 dark:text-zinc-300">
            {c.notas}
          </p>
        )}
      </header>

      {/* KPIs */}
      <section className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Kpi label="Total gasto" value={formatCents(totalGasto)} tone="emerald" />
        <Kpi label="Lucro gerado" value={formatCents(lucroTotal)} tone={lucroTotal >= 0 ? 'emerald' : 'red'} />
        <Kpi label="Em curso" value={String(abertosCount)} tone={abertosCount > 0 ? 'amber' : undefined} />
        <Kpi label="Última visita" value={ultimaVisita ? formatDateOnly(ultimaVisita) : '—'} />
      </section>

      {/* Reparações */}
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-semibold">Reparações <span className="text-zinc-500">· {reps.length}</span></h2>
        </div>
        {reps.length === 0 ? (
          <p className="mt-2 text-xs text-zinc-500">Sem reparações registadas.</p>
        ) : (
          <ul className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
            {reps.map((r) => <RepRow key={r.id} r={r} />)}
          </ul>
        )}
      </section>

      {/* Trabalhos */}
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-semibold">Trabalhos <span className="text-zinc-500">· {trabs.length}</span></h2>
        </div>
        {trabs.length === 0 ? (
          <p className="mt-2 text-xs text-zinc-500">Sem trabalhos registados.</p>
        ) : (
          <ul className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
            {trabs.map((t) => <TrabRow key={t.id} t={t} />)}
          </ul>
        )}
      </section>

      <Modal
        open={editOpen}
        title="Editar cliente"
        onClose={() => setEditOpen(false)}
        footer={<>
          <button type="button" onClick={() => setEditOpen(false)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="submit" form="cliente-form" disabled={update.isPending}
            className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {update.isPending ? 'A guardar…' : 'Guardar'}
          </button>
        </>}
      >
        <ClienteFormView
          initial={c}
          submitting={update.isPending}
          onCancel={() => setEditOpen(false)}
          onSubmit={async (form) => { await update.mutateAsync(form); }}
        />
      </Modal>

      <Modal
        open={confirmDelete}
        title="Apagar cliente"
        onClose={() => setConfirmDelete(false)}
        footer={<>
          <button type="button" onClick={() => setConfirmDelete(false)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={remove.isPending} onClick={() => remove.mutate()}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {remove.isPending ? 'A apagar…' : 'Apagar'}
          </button>
        </>}
      >
        <p className="text-sm">
          Apagar <strong>{c.nome}</strong>? O histórico de reparações/trabalhos fica preservado mas o cliente deixa de aparecer nas listas.
        </p>
      </Modal>
    </div>
  );
}

function Kpi({ label, value, tone }: { label: string; value: string; tone?: 'emerald' | 'red' | 'amber' }) {
  const toneCls =
    tone === 'emerald' ? 'text-emerald-700 dark:text-emerald-400'
      : tone === 'red' ? 'text-red-700 dark:text-red-400'
      : tone === 'amber' ? 'text-amber-700 dark:text-amber-400'
      : '';
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="text-[10px] uppercase tracking-wide text-zinc-500">{label}</div>
      <div className={`mt-1 text-lg font-semibold ${toneCls}`}>{value}</div>
    </div>
  );
}

function RepRow({ r }: { r: Reparacao }) {
  return (
    <li>
      <Link to={`/reparacoes/${r.id}`} className="flex items-center justify-between gap-3 px-2 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-xs font-mono text-zinc-500">#{r.numero}</span>
            <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${STATUS_COLOR[r.estado]}`}>
              {STATUS_LABEL[r.estado]}
            </span>
            <span className="text-[11px] text-zinc-500">{formatDateOnly(r.recebidoEm)}</span>
          </div>
          <div className="mt-0.5 truncate font-medium">{r.equipamento}</div>
          <div className="text-[11px] text-zinc-500 line-clamp-1">{r.avaria}</div>
        </div>
        <div className="text-right">
          <div className="font-medium">{formatCents(r.precoFinalCents ?? r.orcamentoCents)}</div>
          {r.estado === 5 && <div className="text-[11px] text-emerald-600 dark:text-emerald-400">Lucro: {formatCents(r.lucroCents)}</div>}
        </div>
      </Link>
    </li>
  );
}

function TrabRow({ t }: { t: Trabalho }) {
  return (
    <li>
      <Link to={`/trabalhos/${t.id}`} className="flex items-center justify-between gap-3 px-2 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-xs font-mono text-zinc-500">#{t.numero}</span>
            <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${TRABALHO_STATUS_COLOR[t.status]}`}>
              {TRABALHO_STATUS_LABEL[t.status]}
            </span>
            <span className="text-[11px] text-zinc-500">{CATEGORIA_LABEL[t.categoria]}</span>
          </div>
          <div className="mt-0.5 truncate font-medium">{t.titulo}</div>
        </div>
        <div className="text-right">
          <div className="font-medium">{formatCents(t.precoFinalCents ?? t.orcamentoCents)}</div>
          {t.status === 3 && <div className="text-[11px] text-emerald-600 dark:text-emerald-400">Lucro: {formatCents(t.lucroCents)}</div>}
        </div>
      </Link>
    </li>
  );
}
