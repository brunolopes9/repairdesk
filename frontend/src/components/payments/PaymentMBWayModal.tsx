import { useEffect, useState } from 'react';
import { paymentsApi } from '../../lib/payments/api';
import { PAYMENT_PROVIDER, PAYMENT_STATUS, type PaymentDto } from '../../lib/payments/types';
import { PAYMENT_METHOD } from '../../lib/vendas/types';
import { Button } from '../ui';

interface Props {
  vendaId: string;
  amountCents: number;
  defaultPhone?: string;
  /** Chamado quando o pagamento confirma (Status = Pago). Caller marca venda como paga. */
  onConfirmed: (payment: PaymentDto) => void;
  onCancel: () => void;
}

/**
 * Sprint 303 Fase C: modal MBWay para POS. Inicia pagamento via IFTHENPAY,
 * mostra countdown + estado, e dispara onConfirmed quando webhook actualiza
 * o Payment para Pago. Polling a cada 2s ao Payment via /api/payments/{id}.
 */
export function PaymentMBWayModal({ vendaId, amountCents, defaultPhone, onConfirmed, onCancel }: Props) {
  const [phone, setPhone] = useState(defaultPhone ?? '');
  const [payment, setPayment] = useState<PaymentDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [remainingSeconds, setRemainingSeconds] = useState<number | null>(null);

  // Countdown
  useEffect(() => {
    if (!payment?.expiresAt) return;
    const expiresAt = new Date(payment.expiresAt).getTime();
    const tick = () => {
      const remaining = Math.max(0, Math.floor((expiresAt - Date.now()) / 1000));
      setRemainingSeconds(remaining);
    };
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, [payment?.expiresAt]);

  // Poll status até Pago / expirado
  useEffect(() => {
    if (!payment || payment.status === PAYMENT_STATUS.Pago) return;
    if (remainingSeconds !== null && remainingSeconds <= 0) return;
    const id = setInterval(async () => {
      try {
        const fresh = await paymentsApi.get(payment.id);
        setPayment(fresh);
        if (fresh.status === PAYMENT_STATUS.Pago) {
          clearInterval(id);
          onConfirmed(fresh);
        }
      } catch {
        // network blip — próximo tick re-tenta
      }
    }, 2000);
    return () => clearInterval(id);
  }, [payment, remainingSeconds, onConfirmed]);

  async function handleSubmit() {
    setError(null);
    if (!phone.match(/^\+?\d[\d\s]{8,}$/)) {
      setError('Telemóvel inválido (mínimo 9 dígitos).');
      return;
    }
    setSubmitting(true);
    try {
      const res = await paymentsApi.initiate({
        vendaId,
        method: PAYMENT_METHOD.MBWay,
        provider: PAYMENT_PROVIDER.Ifthenpay,
        amountCents,
        customerPhone: phone,
      });
      setPayment(res);
    } catch (err) {
      setError((err as Error).message ?? 'Erro ao iniciar pagamento.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-2xl dark:bg-zinc-900">
        <h2 className="text-lg font-semibold">Pagar com MBWay</h2>
        <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
          Total: <strong>{(amountCents / 100).toFixed(2)}€</strong>
        </p>

        {!payment && (
          <div className="mt-4 space-y-3">
            <label className="block">
              <span className="text-sm font-medium">Telemóvel cliente</span>
              <input
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="9XXXXXXXX"
                className="mt-1 w-full rounded-lg border border-zinc-300 px-3 py-2 dark:border-zinc-700 dark:bg-zinc-800"
              />
            </label>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <div className="flex gap-2">
              <Button onClick={handleSubmit} disabled={submitting || !phone}>
                {submitting ? 'A enviar…' : 'Enviar pedido MBWay'}
              </Button>
              <Button variant="ghost" onClick={onCancel}>Cancelar</Button>
            </div>
          </div>
        )}

        {payment && payment.status !== PAYMENT_STATUS.Pago && (
          <div className="mt-4 space-y-3">
            <div className="rounded-lg bg-blue-50 p-4 dark:bg-blue-950">
              <p className="text-sm text-blue-900 dark:text-blue-200">
                {payment.failureReason
                  ? `Erro: ${payment.failureReason}`
                  : 'A aguardar confirmação na app MBWay do cliente…'}
              </p>
              {remainingSeconds !== null && remainingSeconds > 0 && (
                <p className="mt-2 text-xs text-blue-700 dark:text-blue-300">
                  Expira em {Math.floor(remainingSeconds / 60)}:{String(remainingSeconds % 60).padStart(2, '0')}
                </p>
              )}
              {remainingSeconds === 0 && (
                <p className="mt-2 text-xs text-red-700 dark:text-red-300">Pedido expirou.</p>
              )}
            </div>
            <Button variant="ghost" onClick={onCancel}>Fechar</Button>
          </div>
        )}

        {payment?.status === PAYMENT_STATUS.Pago && (
          <div className="mt-4 rounded-lg bg-green-50 p-4 dark:bg-green-950">
            <p className="text-sm font-semibold text-green-900 dark:text-green-200">
              ✓ Pagamento confirmado
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
