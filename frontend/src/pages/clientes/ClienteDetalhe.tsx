import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Mail, MessageCircle, Pencil, Phone, ShieldAlert } from 'lucide-react';
import { displayPhone } from '../../lib/phone/formatter';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui/Button';
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
import { toast } from '../../lib/toast';
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
  const [hardDeleteOpen, setHardDeleteOpen] = useState(false);
  const [hardDeleteConfirm, setHardDeleteConfirm] = useState('');
  const [hardDeleteMotivo, setHardDeleteMotivo] = useState('');

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

  const exportRgpd = useMutation({
    mutationFn: () => clientesApi.exportRgpd(id!),
    onSuccess: (blob) => {
      const nome = cliente.data?.nome ?? 'cliente';
      downloadBlob(blob, `repairdesk-${safeFileName(nome)}-rgpd.json`);
      toast.success('Exportação RGPD gerada', 'O JSON inclui cliente, reparações, trabalhos, fotos e auditoria.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível exportar os dados do cliente.'),
  });

  const hardDelete = useMutation({
    mutationFn: () => clientesApi.hardDelete(id!, hardDeleteConfirm, hardDeleteMotivo.trim() || null),
    onSuccess: (res) => {
      toast.success(
        'Cliente apagado definitivamente',
        `${res.reparacoes} reparação(ões), ${res.trabalhos} trabalho(s), ${res.despesas} despesa(s) e ${res.fotos} foto(s) removidos.`,
      );
      qc.invalidateQueries({ queryKey: ['clientes'] });
      qc.invalidateQueries({ queryKey: ['audit'] });
      navigate('/clientes');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível apagar definitivamente o cliente.'),
  });

  if (cliente.isLoading) return <div className="text-sm text-zinc-500">A carregar…</div>;
  if (cliente.isError || !cliente.data) return <div className="text-sm text-red-600">Cliente não encontrado.</div>;

  const c = cliente.data;
  const reps = reparacoes.data?.items ?? [];
  const trabs = trabalhos.data?.items ?? [];
  const hardDeleteExpected = `APAGAR ${c.nome}`;
  const canHardDelete = hardDeleteConfirm === hardDeleteExpected;

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
            className="inline-flex items-center gap-1 rounded-md border border-zinc-200 px-2 py-1 text-xs text-zinc-700 hover:bg-zinc-100 dark:border-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-800"
          >
            <Pencil size={12} strokeWidth={2} /> Editar
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
            <div className="flex items-center gap-1.5"><Phone size={13} strokeWidth={2} className="text-zinc-400" /> {displayPhone(c.telefone)}</div>
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

      {/* RGPD */}
      <section className="rounded-xl border border-amber-200 bg-amber-50/60 p-4 dark:border-amber-900/60 dark:bg-amber-950/20">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-950 dark:text-amber-100">
              <ShieldAlert size={16} strokeWidth={2} /> RGPD e portabilidade
            </h2>
            <p className="mt-1 text-xs text-amber-800 dark:text-amber-200/80">
              Exportação Art. 20.º e eliminação definitiva Art. 17.º. A eliminação é irreversível.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="secondary"
              size="sm"
              leftIcon={<Download size={14} strokeWidth={2} />}
              loading={exportRgpd.isPending}
              onClick={() => exportRgpd.mutate()}
            >
              Exportar dados
            </Button>
            <Button
              type="button"
              variant="danger"
              size="sm"
              leftIcon={<ShieldAlert size={14} strokeWidth={2} />}
              onClick={() => setHardDeleteOpen(true)}
            >
              Apagar definitivamente (RGPD)
            </Button>
          </div>
        </div>
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

      <Modal
        open={hardDeleteOpen}
        title="Apagar definitivamente (RGPD)"
        onClose={() => {
          if (hardDelete.isPending) return;
          setHardDeleteOpen(false);
          setHardDeleteConfirm('');
          setHardDeleteMotivo('');
        }}
        footer={<>
          <button
            type="button"
            disabled={hardDelete.isPending}
            onClick={() => {
              setHardDeleteOpen(false);
              setHardDeleteConfirm('');
              setHardDeleteMotivo('');
            }}
            className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 disabled:opacity-60 dark:text-zinc-300"
          >
            Cancelar
          </button>
          <button
            type="button"
            disabled={!canHardDelete || hardDelete.isPending}
            onClick={() => hardDelete.mutate()}
            className="rounded-md bg-red-700 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {hardDelete.isPending ? 'A apagar definitivamente...' : 'Apagar definitivamente'}
          </button>
        </>}
      >
        <div className="space-y-3 text-sm">
          <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-red-800 dark:border-red-900/70 dark:bg-red-950/30 dark:text-red-200">
            Isto remove fisicamente <strong>{c.nome}</strong>, reparações, trabalhos, despesas, fotos e histórico relacionado.
            Não é soft-delete e não há recuperação pela aplicação.
          </div>
          <label className="block">
            <span className="text-xs font-medium text-zinc-600 dark:text-zinc-300">
              Escreve exactamente <span className="font-mono">{hardDeleteExpected}</span>
            </span>
            <input
              value={hardDeleteConfirm}
              onChange={(e) => setHardDeleteConfirm(e.target.value)}
              className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-red-500 focus:ring-2 focus:ring-red-200 dark:border-zinc-700 dark:bg-zinc-950"
              autoComplete="off"
            />
          </label>
          <label className="block">
            <span className="text-xs font-medium text-zinc-600 dark:text-zinc-300">Motivo interno (opcional)</span>
            <textarea
              value={hardDeleteMotivo}
              onChange={(e) => setHardDeleteMotivo(e.target.value)}
              rows={3}
              className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
              placeholder="Ex: pedido formal do cliente em 18/05/2026"
            />
          </label>
        </div>
      </Modal>
    </div>
  );
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

function safeFileName(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-zA-Z0-9_-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .toLowerCase() || 'cliente';
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
