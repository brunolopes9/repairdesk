import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { AlertTriangle, Lock, Phone, Snowflake } from 'lucide-react';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import DespesasImputadas from '../../components/DespesasImputadas';
import Modal from '../../components/Modal';
import WhatsAppMenu from '../../components/WhatsAppMenu';
import { Breadcrumb } from '../../components/ui/Breadcrumb';
import { tenantSettingsApi } from '../../lib/tenantSettings/api';
import { displayPhone } from '../../lib/phone/formatter';
import { templatesForTrabalhoStatus } from '../../lib/whatsapp/templates';
import { trabalhosApi } from '../../lib/trabalhos/api';
import { toast } from '../../lib/toast';
import {
  CATEGORIA_LABEL,
  JOB_CATEGORY,
  PAYMENT_STATUS,
  TRABALHO_PRIMARY_STATUSES,
  TRABALHO_STATUS,
  TRABALHO_STATUS_COLOR,
  TRABALHO_STATUS_LABEL,
  TRABALHO_VALID_TRANSITIONS,
  type JobCategory,
  type TrabalhoStatus,
} from '../../lib/trabalhos/types';
import { formatCents, formatDate, parseEuros } from '../../lib/money';

export default function TrabalhoDetalhe() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const detail = useQuery({
    queryKey: ['trabalho', id],
    queryFn: () => trabalhosApi.get(id!),
    enabled: !!id,
  });

  const tenant = useQuery({
    queryKey: ['tenant-settings'],
    queryFn: () => tenantSettingsApi.getMine(),
    staleTime: 5 * 60_000,
  });

  const billing = useQuery({
    queryKey: ['tenant-billing-settings'],
    queryFn: () => tenantSettingsApi.getBilling(),
    staleTime: 5 * 60_000,
  });

  const [titulo, setTitulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [categoria, setCategoria] = useState<JobCategory>(JOB_CATEGORY.Outro);
  const [precoFinal, setPrecoFinal] = useState('');
  const [horas, setHoras] = useState('0');
  const [pago, setPago] = useState(false);
  const [notas, setNotas] = useState('');
  const [estadoNotas, setEstadoNotas] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [pagamentoPrompt, setPagamentoPrompt] = useState<TrabalhoStatus | null>(null);
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const hydratedRef = useRef(false);

  useEffect(() => {
    if (!detail.data) return;
    const t = detail.data;
    setTitulo(t.titulo);
    setDescricao(t.descricao ?? '');
    setCategoria(t.categoria);
    setPrecoFinal(t.precoFinalCents != null ? (t.precoFinalCents / 100).toFixed(2) : '');
    setHoras(String(t.horasGastas));
    setPago(t.estadoPagamento === PAYMENT_STATUS.Pago);
    setNotas(t.notas ?? '');
    hydratedRef.current = true;
  }, [detail.data]);

  const update = useMutation({
    mutationFn: (overrides?: Partial<{ status: TrabalhoStatus; pagoOverride: boolean }>) => {
      const t = detail.data!;
      const cents = parseEuros(precoFinal);
      const status = overrides?.status ?? t.status;
      const wantsPago = overrides?.pagoOverride ?? pago;
      const dataConclusao =
        status === TRABALHO_STATUS.Concluido && t.status !== TRABALHO_STATUS.Concluido
          ? new Date().toISOString()
          : t.dataConclusao;
      const dataInicio =
        status === TRABALHO_STATUS.EmExecucao && !t.dataInicio
          ? new Date().toISOString()
          : t.dataInicio;
      return trabalhosApi.update(t.id, {
        clienteId: t.cliente?.id ?? null,
        titulo: titulo.trim(),
        descricao: descricao.trim() || null,
        categoria,
        status,
        dataInicio,
        dataConclusao,
        orcamentoCents: cents,
        precoFinalCents: cents,
        horasGastas: Number(horas.replace(',', '.')) || 0,
        notas: notas.trim() || null,
        estadoPagamento: wantsPago ? PAYMENT_STATUS.Pago : PAYMENT_STATUS.NaoPago,
      });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['trabalho', id] });
      qc.invalidateQueries({ queryKey: ['trabalhos'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['dashboard-alertas'] });
      qc.invalidateQueries({ queryKey: ['dashboard-financeiro'] });
      setSavedAt(new Date());
      setError(null);
    },
    onError: (err) => {
      if (isAxiosError(err)) {
        setError((err.response?.data as { detail?: string } | undefined)?.detail ?? 'Erro ao guardar');
      }
    },
  });

  // Auto-save com debounce 1.2s
  useEffect(() => {
    if (!hydratedRef.current || !detail.data) return;
    const t = detail.data;
    // Frozen (Concluído): só auto-save de pagamento
    if (t.status === TRABALHO_STATUS.Concluido) {
      const wantsPago = pago ? PAYMENT_STATUS.Pago : PAYMENT_STATUS.NaoPago;
      if (wantsPago !== t.estadoPagamento && t.estadoPagamento !== PAYMENT_STATUS.Pago) {
        const timer = setTimeout(() => update.mutate(undefined), 300);
        return () => clearTimeout(timer);
      }
      return;
    }

    const cents = parseEuros(precoFinal);
    const wantsPago = pago ? PAYMENT_STATUS.Pago : PAYMENT_STATUS.NaoPago;
    const same =
      titulo.trim() === t.titulo &&
      (descricao.trim() || null) === (t.descricao ?? null) &&
      categoria === t.categoria &&
      cents === t.precoFinalCents &&
      (Number(horas.replace(',', '.')) || 0) === t.horasGastas &&
      (notas.trim() || null) === (t.notas ?? null) &&
      wantsPago === t.estadoPagamento;
    if (same) return;

    const timer = setTimeout(() => update.mutate(undefined), 1200);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [titulo, descricao, categoria, precoFinal, horas, notas, pago, detail.data]);

  const remove = useMutation({
    mutationFn: () => trabalhosApi.remove(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['trabalhos'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      navigate('/trabalhos');
    },
  });

  const emitirFatura = useMutation({
    mutationFn: () => trabalhosApi.emitirFatura(id!),
    onSuccess: (invoice) => {
      qc.invalidateQueries({ queryKey: ['trabalho', id] });
      toast.success(`Fatura ${invoice.number} emitida`, invoice.pdfUrl ? 'PDF disponível na ficha.' : undefined);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível emitir a fatura.'),
  });

  const reabrir = useMutation({
    mutationFn: () => trabalhosApi.reabrir(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['trabalho', id] });
      qc.invalidateQueries({ queryKey: ['trabalhos'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setPago(false);
    },
    onError: (err) => {
      if (isAxiosError(err)) {
        setError((err.response?.data as { detail?: string } | undefined)?.detail ?? 'Não foi possível reabrir.');
      }
    },
  });

  if (detail.isLoading) return <div className="text-sm text-zinc-500">A carregar…</div>;
  if (detail.isError || !detail.data) return <div className="text-sm text-red-600">Não encontrado.</div>;

  const t = detail.data;
  const receitaCents = parseEuros(precoFinal) ?? 0;
  const lucroLive = receitaCents - t.custoDespesasCents;
  const cleanPhone = t.cliente?.telefone?.replace(/\s/g, '') ?? '';
  // 3 tiers: aberto / frozen (Concluído sem pagamento) / locked (Concluído + Pago)
  const isFrozen = t.status === TRABALHO_STATUS.Concluido;
  const isLocked = isFrozen && t.estadoPagamento === PAYMENT_STATUS.Pago;
  const canEmitMoloniInvoice = t.estadoPagamento === PAYMENT_STATUS.Pago
    && billing.data?.provider === 1
    && !t.invoiceExternalId;
  const possibleNext = TRABALHO_VALID_TRANSITIONS[t.status] ?? [];

  const valorParaCobrar = t.precoFinalCents ?? t.orcamentoCents ?? null;
  const waVars = {
    cliente_nome: t.cliente?.nome?.split(' ')[0] ?? 'olá',
    equipamento: t.titulo,
    loja_nome: tenant.data?.name ?? undefined,
    numero_reparacao: t.numero,
    valor: valorParaCobrar != null ? formatCents(valorParaCobrar) : undefined,
    link_review_google: tenant.data?.googleReviewUrl ?? undefined,
  };
  const waTemplates = templatesForTrabalhoStatus(t.status);

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between text-sm">
        <Breadcrumb
          items={[
            { label: 'Trabalhos', to: '/trabalhos' },
            { label: `#${t.numero} · ${t.titulo}` },
          ]}
        />
        <div className="flex items-center gap-2">
          {isFrozen && (
            <button
              type="button"
              disabled={reabrir.isPending}
              onClick={() => reabrir.mutate()}
              className="rounded-md bg-amber-100 px-2 py-1 text-xs font-medium text-amber-900 hover:bg-amber-200 disabled:opacity-60 dark:bg-amber-950/40 dark:text-amber-300"
            >
              🔓 Reabrir
            </button>
          )}
          <button
            type="button"
            onClick={() => setConfirmDelete(true)}
            className="rounded-md px-2 py-1 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40"
          >
            Apagar
          </button>
        </div>
      </div>

      {isLocked && (
        <div className="flex items-start gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300">
          <Lock size={15} strokeWidth={2} className="mt-0.5 flex-none" />
          <span>Encerrado — concluído e pago. Campos bloqueados. Clica "Reabrir" se precisares de corrigir.</span>
        </div>
      )}
      {isFrozen && !isLocked && (
        <div className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300">
          <Snowflake size={15} strokeWidth={2} className="mt-0.5 flex-none" />
          <span>Concluído mas ainda não pago. Para evitar erros, só podes marcar como Pago ou Reabrir. Para outras alterações, clica "Reabrir".</span>
        </div>
      )}

      <header className="space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs font-mono text-zinc-500">#{t.numero} · {CATEGORIA_LABEL[t.categoria]}</span>
          <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${TRABALHO_STATUS_COLOR[t.status]}`}>
            {TRABALHO_STATUS_LABEL[t.status]}
          </span>
        </div>
        <h1 className="text-2xl font-semibold tracking-tight">{t.titulo}</h1>
        {!t.cliente && (
          <div className="flex items-start gap-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-800/60 dark:bg-amber-950/30 dark:text-amber-200">
            <AlertTriangle size={13} strokeWidth={2} className="mt-0.5 flex-none" />
            <span>Este trabalho não tem cliente associado. O orçamento PDF vai aparecer com "cliente a definir". Edita e selecciona um cliente para corrigir.</span>
          </div>
        )}
        {t.cliente && (
          <>
            <Link to={`/clientes/${t.cliente.id}`} className="text-sm text-zinc-700 hover:underline dark:text-zinc-300">
              {t.cliente.nome}
            </Link>
            {cleanPhone && (
              <div className="flex flex-wrap gap-2">
                <a href={`tel:${cleanPhone}`} className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-700"><Phone size={12} strokeWidth={2} /> Ligar</a>
                <WhatsAppMenu phone={cleanPhone} vars={waVars} customList={waTemplates} />
                <span className="self-center text-xs text-zinc-500">{displayPhone(t.cliente.telefone)}</span>
              </div>
            )}
          </>
        )}
        <p className="text-xs text-zinc-500">criado {formatDate(t.createdAt)}</p>
        <div className="flex flex-wrap gap-2 pt-1">
          <button
            type="button"
            onClick={() => openPdfInNewTab(`/trabalhos/${t.id}/orcamento.pdf`)}
            className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800"
            title="Abrir PDF do orçamento (não é factura)"
          >
            📄 PDF Orçamento
          </button>
          {t.invoiceExternalId ? (
            <a
              href={t.invoicePdfUrl ?? '#'}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 rounded-lg border border-emerald-300 bg-emerald-50 px-3 py-1.5 text-xs font-medium text-emerald-800 hover:bg-emerald-100 dark:border-emerald-800/60 dark:bg-emerald-950/30 dark:text-emerald-200"
            >
              Fatura {t.invoiceNumber ?? t.invoiceExternalId}
            </a>
          ) : canEmitMoloniInvoice && (
            <button
              type="button"
              disabled={emitirFatura.isPending}
              onClick={() => emitirFatura.mutate()}
              className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-60"
            >
              {emitirFatura.isPending ? 'A emitir…' : 'Emitir fatura via Moloni'}
            </button>
          )}
          {(t.status === TRABALHO_STATUS.EmExecucao || t.status === TRABALHO_STATUS.Concluido) && (
            <a
              href="https://irs.portaldasfinancas.gov.pt/recibos/portal/emitir"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-xs font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              🧾 Emitir factura no Portal AT
            </a>
          )}
        </div>
      </header>

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">{error}</div>}

      <WorkflowStepper
        status={t.status}
        possibleNext={isFrozen ? [] : possibleNext}
        notas={estadoNotas}
        onNotasChange={setEstadoNotas}
        onChange={(st) => {
          if (st === TRABALHO_STATUS.Concluido && t.estadoPagamento === PAYMENT_STATUS.NaoPago) {
            setPagamentoPrompt(st);
          } else {
            update.mutate({ status: st });
          }
        }}
        pending={update.isPending}
      />

      <Modal
        open={pagamentoPrompt !== null}
        title="Foi pago?"
        onClose={() => setPagamentoPrompt(null)}
      >
        <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-400">
          Antes de marcar como <strong>Concluído</strong>, regista o estado do pagamento.
          Esta resposta entra no Lucro Realizado do dashboard.
        </p>
        <div className="grid grid-cols-3 gap-2">
          <button
            type="button"
            onClick={() => { setPago(true); update.mutate({ status: TRABALHO_STATUS.Concluido, pagoOverride: true }); setPagamentoPrompt(null); }}
            className="rounded-lg border border-emerald-300 bg-emerald-50 px-3 py-3 text-sm font-medium text-emerald-800 transition hover:bg-emerald-100 dark:border-emerald-800/60 dark:bg-emerald-950/30 dark:text-emerald-200 dark:hover:bg-emerald-950/50"
          >
            ✓ Pago
          </button>
          <button
            type="button"
            onClick={() => { setPago(false); update.mutate({ status: TRABALHO_STATUS.Concluido, pagoOverride: false }); setPagamentoPrompt(null); }}
            className="rounded-lg border border-zinc-300 bg-white px-3 py-3 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300 dark:hover:bg-zinc-900"
          >
            ✕ Ainda não
          </button>
          <button
            type="button"
            onClick={() => setPagamentoPrompt(null)}
            className="rounded-lg border border-zinc-300 bg-white px-3 py-3 text-sm font-medium text-zinc-500 transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-400 dark:hover:bg-zinc-900"
          >
            Cancelar
          </button>
        </div>
        <p className="mt-3 text-xs text-zinc-500">
          Podes sempre actualizar depois marcando "Pago" no detalhe do trabalho.
        </p>
      </Modal>

      <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="text-sm font-semibold">Detalhes</h2>
        <Field label="Título">
          <input disabled={isFrozen} value={titulo} onChange={e => setTitulo(e.target.value)} className={inputCls} />
        </Field>
        <Field label="Categoria">
          <select disabled={isFrozen} value={categoria} onChange={e => setCategoria(Number(e.target.value) as JobCategory)} className={inputCls}>
            {Object.entries(JOB_CATEGORY).map(([_, v]) => <option key={v} value={v}>{CATEGORIA_LABEL[v]}</option>)}
          </select>
        </Field>
        <Field label="Descrição">
          <textarea disabled={isFrozen} rows={3} value={descricao} onChange={e => setDescricao(e.target.value)} className={inputCls + ' resize-none'} placeholder="O que vai ser feito…" />
        </Field>
        <Field label="Notas internas">
          <textarea disabled={isFrozen} rows={2} value={notas} onChange={e => setNotas(e.target.value)} className={inputCls + ' resize-none'} placeholder="Contactos, links, lembretes…" />
        </Field>
      </section>

      <DespesasImputadas trabalhoId={t.id} invalidateKeys={[['trabalho', id]]} readOnly={isFrozen} />

      <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="text-sm font-semibold">Preço & lucro</h2>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Preço final ao cliente (€)">
            <input disabled={isFrozen} inputMode="decimal" value={precoFinal} onChange={e => setPrecoFinal(e.target.value)} className={inputCls} placeholder="0,00" />
          </Field>
          <Field label="Horas gastas">
            <input disabled={isFrozen} inputMode="decimal" value={horas} onChange={e => setHoras(e.target.value)} className={inputCls} />
          </Field>
        </div>
        <div className="rounded-lg bg-zinc-50 p-3 text-sm dark:bg-zinc-950 space-y-1">
          <div className="flex justify-between"><span className="text-zinc-500">Receita:</span><span>{formatCents(receitaCents)}</span></div>
          <div className="flex justify-between"><span className="text-zinc-500">Despesas:</span><span>−{formatCents(t.custoDespesasCents)}</span></div>
          <div className="flex justify-between border-t border-zinc-200 pt-1 font-semibold dark:border-zinc-800">
            <span>Lucro:</span>
            <span className={lucroLive >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'}>{formatCents(lucroLive)}</span>
          </div>
        </div>
        {t.status === TRABALHO_STATUS.Concluido && (
          <label className={`flex items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-sm dark:border-zinc-800 dark:bg-zinc-950 ${isLocked ? '' : 'cursor-pointer'}`}>
            <input type="checkbox" disabled={isLocked} checked={pago} onChange={e => setPago(e.target.checked)} />
            <span>Pago pelo cliente</span>
          </label>
        )}
      </section>

      {!isLocked && (
        <div className="flex items-center justify-end gap-2 text-xs text-zinc-500">
          {update.isPending ? (
            <span>A guardar…</span>
          ) : savedAt ? (
            <span className="text-emerald-600 dark:text-emerald-400">
              ✓ Guardado às {savedAt.toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit' })}
            </span>
          ) : (
            <span>Alterações guardam automaticamente.</span>
          )}
        </div>
      )}

      <Modal
        open={confirmDelete}
        title="Apagar trabalho"
        onClose={() => setConfirmDelete(false)}
        footer={<>
          <button type="button" onClick={() => setConfirmDelete(false)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={remove.isPending} onClick={() => remove.mutate()}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {remove.isPending ? 'A apagar…' : 'Apagar'}
          </button>
        </>}
      >
        <p className="text-sm">Apagar <strong>#{t.numero} {t.titulo}</strong>?</p>
      </Modal>
    </div>
  );
}

function WorkflowStepper({
  status,
  possibleNext,
  notas,
  onNotasChange,
  onChange,
  pending,
}: {
  status: TrabalhoStatus;
  possibleNext: TrabalhoStatus[];
  notas: string;
  onNotasChange: (v: string) => void;
  onChange: (s: TrabalhoStatus) => void;
  pending: boolean;
}) {
  const currentIdx = TRABALHO_PRIMARY_STATUSES.indexOf(status);
  const isCancelado = status === TRABALHO_STATUS.Cancelado;
  const nextPrimary = possibleNext.find((s) => TRABALHO_PRIMARY_STATUSES.includes(s));
  const canCancel = possibleNext.includes(TRABALHO_STATUS.Cancelado);
  const isConcluir = nextPrimary === TRABALHO_STATUS.Concluido;

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <ol className="-mx-1 flex items-center overflow-x-auto pb-1">
        {TRABALHO_PRIMARY_STATUSES.map((s, i) => {
          const isCurrent = s === status;
          const isDone = currentIdx > -1 && i < currentIdx;
          const dotColor = isCancelado
            ? 'bg-zinc-300 dark:bg-zinc-700'
            : isCurrent
              ? 'bg-brand-600 ring-4 ring-brand-200 dark:ring-brand-900'
              : isDone
                ? 'bg-emerald-500'
                : 'bg-zinc-300 dark:bg-zinc-700';
          const labelColor = isCurrent
            ? 'font-semibold text-zinc-900 dark:text-zinc-100'
            : isDone
              ? 'text-emerald-700 dark:text-emerald-400'
              : 'text-zinc-400 dark:text-zinc-600';
          return (
            <li key={s} className="flex flex-1 min-w-[80px] items-center">
              <div className="flex flex-1 flex-col items-center gap-1 px-1">
                <div className={`h-3 w-3 rounded-full transition ${dotColor}`} />
                <span className={`text-center text-[10px] sm:text-xs ${labelColor}`}>
                  {TRABALHO_STATUS_LABEL[s]}
                </span>
              </div>
              {i < TRABALHO_PRIMARY_STATUSES.length - 1 && (
                <div className={`mb-4 h-0.5 flex-1 ${
                  isCancelado || i >= currentIdx ? 'bg-zinc-200 dark:bg-zinc-800' : 'bg-emerald-500'
                }`} />
              )}
            </li>
          );
        })}
      </ol>

      {isCancelado && (
        <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-xs text-red-700 dark:bg-red-950/40 dark:text-red-300">
          Trabalho cancelado.
        </p>
      )}

      {possibleNext.length > 0 && (
        <div className="mt-4 space-y-2">
          <input
            type="text"
            placeholder="Notas (opcional)"
            value={notas}
            onChange={(e) => onNotasChange(e.target.value)}
            className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-1.5 text-sm dark:border-zinc-700 dark:bg-zinc-950"
          />
          <div className="flex flex-col gap-2 sm:flex-row">
            {nextPrimary != null && (
              <button
                type="button"
                disabled={pending}
                onClick={() => onChange(nextPrimary)}
                className="flex-1 rounded-lg bg-brand-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:opacity-60"
              >
                {pending ? 'A guardar…' : isConcluir ? '✓ Marcar Concluído (Pago)' : `→ ${TRABALHO_STATUS_LABEL[nextPrimary]}`}
              </button>
            )}
            {canCancel && (
              <button
                type="button"
                disabled={pending}
                onClick={() => onChange(TRABALHO_STATUS.Cancelado)}
                className="rounded-lg border border-red-200 bg-white px-3 py-2 text-xs font-medium text-red-700 hover:bg-red-50 disabled:opacity-50 dark:border-red-900 dark:bg-zinc-900 dark:text-red-400"
              >
                Cancelar
              </button>
            )}
          </div>
          {isConcluir && (
            <p className="text-[11px] text-zinc-500">
              Ao Concluir, fica automaticamente <strong>Pago</strong>. Desmarca em "Preço & lucro" se for caso de não pagamento.
            </p>
          )}
        </div>
      )}
    </section>
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
