import { useMemo, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banknote, CreditCard, FileDown, Landmark, Lock, Minus, Plus, ReceiptText, WalletCards } from 'lucide-react';
import Modal from '../../components/Modal';
import { useConfirm } from '../../components/ConfirmDialog';
import { Button, EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { useDisclosure } from '../../hooks/useDisclosure';
import { useAuth } from '../../lib/auth/AuthContext';
import {
  CASH_MOVEMENT_TYPE,
  DAILY_CLOSING_STATUS,
  PAYMENT_METHOD,
  cashApi,
  type CashMovementType,
  type DailyClosingDto,
  type PaymentMethod,
} from '../../lib/cash/api';
import { formatDateShort, formatDateTime } from '../../lib/dates';
import { downloadFile } from '../../lib/downloadPdf';
import { formatCents, parseEuros } from '../../lib/money';
import { toast } from '../../lib/toast';

type MovementPreset = {
  type: CashMovementType;
  title: string;
  description: string;
  paymentMethod: PaymentMethod;
};

const locationId: string | null = null;
const todayQueryKey = ['cash', 'today', locationId] as const;

const movementPresets: MovementPreset[] = [
  { type: CASH_MOVEMENT_TYPE.Sangria, title: 'Sangria', description: 'Retirar dinheiro da caixa para banco/cofre.', paymentMethod: PAYMENT_METHOD.Dinheiro },
  { type: CASH_MOVEMENT_TYPE.Reforco, title: 'Reforço', description: 'Adicionar dinheiro vindo do banco/cofre.', paymentMethod: PAYMENT_METHOD.Dinheiro },
  { type: CASH_MOVEMENT_TYPE.DespesaCaixa, title: 'Despesa caixa', description: 'Despesa paga em dinheiro da caixa.', paymentMethod: PAYMENT_METHOD.Dinheiro },
];

const movementLabels: Record<CashMovementType, string> = {
  0: 'Pagamento',
  1: 'Reforço',
  2: 'Sangria',
  3: 'Despesa caixa',
  4: 'Troco',
  5: 'Ajuste manual',
};

const paymentLabels: Record<PaymentMethod, string> = {
  0: 'Dinheiro',
  1: 'Multibanco',
  2: 'MBWay',
  3: 'Transferência',
  4: 'Cartão',
  99: 'Outro',
};

export default function Cash({ embedded = false }: { embedded?: boolean } = {}) {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const { hasRole } = useAuth();
  const isAdmin = hasRole('Admin');
  const movementModal = useDisclosure();
  const closeModal = useDisclosure();
  const [opening, setOpening] = useState('');
  const [openingNotas, setOpeningNotas] = useState('');
  const [selectedPreset, setSelectedPreset] = useState<MovementPreset>(movementPresets[0]);
  const [movementAmount, setMovementAmount] = useState('');
  const [movementDescricao, setMovementDescricao] = useState('');
  const [actualClosing, setActualClosing] = useState('');
  const [closingNotas, setClosingNotas] = useState('');

  const today = useQuery({
    queryKey: todayQueryKey,
    queryFn: () => cashApi.today(locationId),
    staleTime: 15_000,
  });

  const closing = today.data ?? null;
  const isOpen = closing?.status === DAILY_CLOSING_STATUS.Open || closing?.status === DAILY_CLOSING_STATUS.Reopened;
  const isClosed = closing?.status === DAILY_CLOSING_STATUS.Closed;

  const sortedMovements = useMemo(
    () => [...(closing?.movimentos ?? [])].sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime()),
    [closing?.movimentos],
  );

  function invalidateToday() {
    qc.invalidateQueries({ queryKey: todayQueryKey });
  }

  const openDay = useMutation({
    mutationFn: () => {
      const openingCents = parseEuros(opening);
      if (openingCents == null) throw new Error('Indica um valor de abertura válido.');
      return cashApi.open({ openingCents, locationId, notas: openingNotas.trim() || null });
    },
    onSuccess: () => {
      toast.success('Caixa aberta');
      setOpening('');
      setOpeningNotas('');
      invalidateToday();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível abrir a caixa.'),
  });

  const recordMovement = useMutation({
    mutationFn: () => {
      const amountCents = parseEuros(movementAmount);
      const descricao = movementDescricao.trim();
      if (amountCents == null) throw new Error('Indica um valor válido.');
      if (!descricao) throw new Error('A descrição é obrigatória.');
      return cashApi.recordMovement({
        type: selectedPreset.type,
        paymentMethod: selectedPreset.paymentMethod,
        amountCents,
        descricao,
        locationId,
      });
    },
    onSuccess: () => {
      toast.success(`${selectedPreset.title} registada`);
      setMovementAmount('');
      setMovementDescricao('');
      movementModal.onClose();
      invalidateToday();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível registar o movimento.'),
  });

  const closeDay = useMutation({
    mutationFn: async () => {
      if (!closing) throw new Error('Não existe caixa aberta.');
      const actualClosingCents = parseEuros(actualClosing);
      if (actualClosingCents == null) throw new Error('Indica um valor de fecho válido.');
      const ok = await confirm({
        title: 'Fechar caixa?',
        description: `O fecho fica registado com ${formatCents(actualClosingCents)} em dinheiro real.`,
        confirmLabel: 'Fechar caixa',
      });
      if (!ok) throw new Error('Fecho cancelado.');
      return cashApi.close(closing.id, { actualClosingCents, notas: closingNotas.trim() || null });
    },
    onSuccess: () => {
      toast.success('Caixa fechada');
      setActualClosing('');
      setClosingNotas('');
      closeModal.onClose();
      invalidateToday();
    },
    onError: (err) => {
      if (err instanceof Error && err.message === 'Fecho cancelado.') return;
      toast.fromError(err, 'Não foi possível fechar a caixa.');
    },
  });

  function openMovementModal(preset: MovementPreset) {
    setSelectedPreset(preset);
    setMovementAmount('');
    setMovementDescricao('');
    movementModal.onOpen();
  }

  return (
    <div className="space-y-4">
      {!embedded && (
        <PageHeader
          title={`Caixa hoje · ${formatDateShort(new Date())}`}
          description="Abertura, movimentos e fecho diário do dinheiro de balcão."
          meta={isClosed ? <StatusPill tone="closed">Fechada</StatusPill> : isOpen ? <StatusPill tone="open">Aberta</StatusPill> : undefined}
        />
      )}

      {today.isLoading && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <SkeletonCard />
          <SkeletonCard />
        </div>
      )}

      {!today.isLoading && !closing && (
        <OpenCashCard
          opening={opening}
          openingNotas={openingNotas}
          loading={openDay.isPending}
          onOpeningChange={setOpening}
          onNotasChange={setOpeningNotas}
          onSubmit={(e) => {
            e.preventDefault();
            openDay.mutate();
          }}
        />
      )}

      {closing && (
        <>
          <KpiGrid closing={closing} />

          {isOpen && (
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-4">
              {movementPresets.map((preset) => (
                <Button
                  key={preset.type}
                  type="button"
                  variant="secondary"
                  className="min-h-12 justify-start"
                  leftIcon={preset.type === CASH_MOVEMENT_TYPE.Reforco ? <Plus size={17} /> : <Minus size={17} />}
                  onClick={() => openMovementModal(preset)}
                >
                  + {preset.title}
                </Button>
              ))}
              {isAdmin ? (
                <Button type="button" variant="danger" className="min-h-12 justify-start" leftIcon={<Lock size={17} />} onClick={closeModal.onOpen}>
                  Fechar caixa
                </Button>
              ) : (
                <div className="flex min-h-12 items-center rounded-lg border border-dashed border-zinc-300 px-3 text-sm text-zinc-500 dark:border-zinc-700">
                  Fecho apenas Admin
                </div>
              )}
            </div>
          )}

          {isClosed && <ClosedSummary closing={closing} />}

          <section className="rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex items-center justify-between border-b border-zinc-200 px-4 py-3 dark:border-zinc-800">
              <h2 className="text-sm font-semibold">Movimentos do dia</h2>
              <span className="text-xs text-zinc-500">{sortedMovements.length}</span>
            </div>
            {sortedMovements.length > 0 ? (
              <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {sortedMovements.map((m) => (
                  <li key={m.id} className="flex items-start justify-between gap-3 px-4 py-3">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-medium">{m.descricao}</span>
                        <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                          {movementLabels[m.type]}
                        </span>
                      </div>
                      <div className="mt-1 text-xs text-zinc-500">
                        {paymentLabels[m.paymentMethod]} · {formatDateTime(m.occurredAt)}
                      </div>
                    </div>
                    <div className={`shrink-0 font-mono text-sm font-semibold tabular-nums ${movementSign(m.type) < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-emerald-700 dark:text-emerald-400'}`}>
                      {movementSign(m.type) < 0 ? '-' : '+'}{formatCents(m.amountCents)}
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="p-4">
                <EmptyState icon={ReceiptText} title="Sem movimentos" description="Quando houver vendas, reforços ou sangrias, aparecem aqui." />
              </div>
            )}
          </section>
        </>
      )}

      <MovementModal
        open={movementModal.open}
        preset={selectedPreset}
        amount={movementAmount}
        descricao={movementDescricao}
        loading={recordMovement.isPending}
        onClose={movementModal.onClose}
        onAmountChange={setMovementAmount}
        onDescricaoChange={setMovementDescricao}
        onSubmit={(e) => {
          e.preventDefault();
          recordMovement.mutate();
        }}
      />

      <CloseCashModal
        open={closeModal.open}
        expected={closing?.expectedClosingCents ?? 0}
        actual={actualClosing}
        notas={closingNotas}
        loading={closeDay.isPending}
        onClose={closeModal.onClose}
        onActualChange={setActualClosing}
        onNotasChange={setClosingNotas}
        onSubmit={(e) => {
          e.preventDefault();
          closeDay.mutate();
        }}
      />
    </div>
  );
}

function OpenCashCard({
  opening,
  openingNotas,
  loading,
  onOpeningChange,
  onNotasChange,
  onSubmit,
}: {
  opening: string;
  openingNotas: string;
  loading: boolean;
  onOpeningChange: (value: string) => void;
  onNotasChange: (value: string) => void;
  onSubmit: (e: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <form onSubmit={onSubmit} className="rounded-xl border border-zinc-200 bg-white p-4 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-start gap-3">
        <div className="grid h-11 w-11 shrink-0 place-items-center rounded-lg bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
          <Banknote size={21} />
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="font-semibold">Abrir caixa</h2>
          <p className="mt-1 text-sm text-zinc-500">Conta o fundo inicial antes da primeira venda do dia.</p>
        </div>
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-[1fr_1.4fr_auto] sm:items-end">
        <label className="grid gap-1 text-sm">
          <span className="font-medium">Abertura (€)</span>
          <input
            value={opening}
            onChange={(e) => onOpeningChange(e.target.value)}
            inputMode="decimal"
            placeholder="0,00"
            className="min-h-12 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-base outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
        <label className="grid gap-1 text-sm">
          <span className="font-medium">Notas</span>
          <input
            value={openingNotas}
            onChange={(e) => onNotasChange(e.target.value)}
            placeholder="Opcional"
            className="min-h-12 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
        <Button type="submit" loading={loading} className="min-h-12">Abrir</Button>
      </div>
    </form>
  );
}

function KpiGrid({ closing }: { closing: DailyClosingDto }) {
  return (
    <section className="grid grid-cols-2 gap-3 lg:grid-cols-5">
      <Kpi title="Saldo dinheiro previsto" value={closing.expectedClosingCents} icon={Banknote} tone="emerald" />
      <Kpi title="MBWay" value={closing.mbwayCents} icon={WalletCards} />
      <Kpi title="Multibanco" value={closing.multibancoCents} icon={Landmark} />
      <Kpi title="Cartão" value={closing.cardCents} icon={CreditCard} />
      <Kpi title="Outros" value={closing.otherCents} icon={ReceiptText} />
    </section>
  );
}

function Kpi({ title, value, icon: Icon, tone = 'zinc' }: { title: string; value: number; icon: typeof Banknote; tone?: 'emerald' | 'zinc' }) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-medium text-zinc-500">{title}</span>
        <Icon size={17} className={tone === 'emerald' ? 'text-emerald-600' : 'text-zinc-400'} />
      </div>
      <div className="mt-2 font-mono text-lg font-semibold tabular-nums">{formatCents(value)}</div>
    </div>
  );
}

function ClosedSummary({ closing }: { closing: DailyClosingDto }) {
  const diff = closing.diffCents ?? 0;
  const tone = diff >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400';

  async function handleDownloadZReport() {
    try {
      await downloadFile(cashApi.zReportPdfPath(closing.id), `ZReport_${closing.date}.pdf`);
    } catch (err) {
      toast.fromError(err, 'Não foi possível descarregar o Z-Report.');
    }
  }

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="grid gap-3 sm:grid-cols-3">
        <SummaryLine label="Esperado" value={closing.expectedClosingCents} />
        <SummaryLine label="Real" value={closing.actualClosingCents ?? 0} />
        <div>
          <div className="text-xs text-zinc-500">Diferença</div>
          <div className={`mt-1 font-mono text-xl font-semibold tabular-nums ${tone}`}>
            {diff > 0 ? '+' : diff < 0 ? '-' : ''}{formatCents(Math.abs(diff))}
          </div>
        </div>
      </div>
      {closing.closedAt && <p className="mt-3 text-xs text-zinc-500">Fechada em {formatDateTime(closing.closedAt)}</p>}
      {closing.notas && <p className="mt-2 text-sm text-zinc-600 dark:text-zinc-300">{closing.notas}</p>}
      <div className="mt-3 flex">
        <Button type="button" variant="secondary" leftIcon={<FileDown size={17} />} onClick={handleDownloadZReport}>
          Descarregar Z-Report
        </Button>
      </div>
    </section>
  );
}

function SummaryLine({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <div className="text-xs text-zinc-500">{label}</div>
      <div className="mt-1 font-mono text-xl font-semibold tabular-nums">{formatCents(value)}</div>
    </div>
  );
}

function MovementModal({
  open,
  preset,
  amount,
  descricao,
  loading,
  onClose,
  onAmountChange,
  onDescricaoChange,
  onSubmit,
}: {
  open: boolean;
  preset: MovementPreset;
  amount: string;
  descricao: string;
  loading: boolean;
  onClose: () => void;
  onAmountChange: (value: string) => void;
  onDescricaoChange: (value: string) => void;
  onSubmit: (e: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <Modal
      open={open}
      title={preset.title}
      onClose={onClose}
      footer={
        <>
          <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="cash-movement-form" loading={loading}>Registar</Button>
        </>
      }
    >
      <form id="cash-movement-form" onSubmit={onSubmit} className="space-y-3">
        <p className="text-sm text-zinc-500">{preset.description}</p>
        <label className="grid gap-1 text-sm">
          <span className="font-medium">Valor (€)</span>
          <input
            value={amount}
            onChange={(e) => onAmountChange(e.target.value)}
            inputMode="decimal"
            autoFocus
            placeholder="0,00"
            className="min-h-12 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-base outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
        <label className="grid gap-1 text-sm">
          <span className="font-medium">Descrição</span>
          <textarea
            value={descricao}
            onChange={(e) => onDescricaoChange(e.target.value)}
            rows={3}
            placeholder="Ex.: depósito banco, compra de papel..."
            className="min-h-24 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
      </form>
    </Modal>
  );
}

function CloseCashModal({
  open,
  expected,
  actual,
  notas,
  loading,
  onClose,
  onActualChange,
  onNotasChange,
  onSubmit,
}: {
  open: boolean;
  expected: number;
  actual: string;
  notas: string;
  loading: boolean;
  onClose: () => void;
  onActualChange: (value: string) => void;
  onNotasChange: (value: string) => void;
  onSubmit: (e: FormEvent<HTMLFormElement>) => void;
}) {
  const actualCents = parseEuros(actual);
  const diff = actualCents == null ? null : actualCents - expected;
  return (
    <Modal
      open={open}
      title="Fechar caixa"
      onClose={onClose}
      footer={
        <>
          <Button type="button" variant="secondary" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="cash-close-form" variant="danger" loading={loading}>Fechar caixa</Button>
        </>
      }
    >
      <form id="cash-close-form" onSubmit={onSubmit} className="space-y-3">
        <div className="rounded-lg bg-zinc-50 p-3 text-sm dark:bg-zinc-950">
          <span className="text-zinc-500">Dinheiro previsto</span>
          <div className="mt-1 font-mono text-lg font-semibold">{formatCents(expected)}</div>
        </div>
        <label className="grid gap-1 text-sm">
          <span className="font-medium">Dinheiro real (€)</span>
          <input
            value={actual}
            onChange={(e) => onActualChange(e.target.value)}
            inputMode="decimal"
            autoFocus
            placeholder="0,00"
            className="min-h-12 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-base outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
        {diff != null && (
          <div className={`text-sm font-medium ${diff >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'}`}>
            Diferença: {diff > 0 ? '+' : diff < 0 ? '-' : ''}{formatCents(Math.abs(diff))}
          </div>
        )}
        <label className="grid gap-1 text-sm">
          <span className="font-medium">Notas</span>
          <textarea
            value={notas}
            onChange={(e) => onNotasChange(e.target.value)}
            rows={3}
            placeholder="Opcional"
            className="min-h-24 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
      </form>
    </Modal>
  );
}

function StatusPill({ tone, children }: { tone: 'open' | 'closed'; children: string }) {
  return (
    <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${tone === 'open' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300' : 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300'}`}>
      {children}
    </span>
  );
}

function movementSign(type: CashMovementType) {
  return type === CASH_MOVEMENT_TYPE.Sangria || type === CASH_MOVEMENT_TYPE.DespesaCaixa || type === CASH_MOVEMENT_TYPE.Troco ? -1 : 1;
}
