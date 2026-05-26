import { useEffect, useState } from 'react';
import { paymentsApi } from '../../lib/payments/api';
import { PAYMENT_PROVIDER, PAYMENT_STATUS, type PaymentDto } from '../../lib/payments/types';
import { PAYMENT_METHOD } from '../../lib/vendas/types';
import { Button } from '../ui';

interface Props {
  vendaId: string;
  amountCents: number;
  onConfirmed: (payment: PaymentDto) => void;
  onCancel: () => void;
}

/**
 * Sprint 303 Fase C: modal Multibanco. Gera referência IFTHENPAY, mostra
 * Entidade/Referência/Montante para o cliente pagar em ATM/homebanking.
 * Polling lento (10s) — refs MB demoram horas/dias a confirmar.
 */
export function PaymentMultibancoModal({ vendaId, amountCents, onConfirmed, onCancel }: Props) {
  const [payment, setPayment] = useState<PaymentDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function generate() {
    setError(null);
    setSubmitting(true);
    try {
      const res = await paymentsApi.initiate({
        vendaId,
        method: PAYMENT_METHOD.Multibanco,
        provider: PAYMENT_PROVIDER.Ifthenpay,
        amountCents,
      });
      setPayment(res);
    } catch (err) {
      setError((err as Error).message ?? 'Erro ao gerar referência.');
    } finally {
      setSubmitting(false);
    }
  }

  // Poll status até Pago — 10s (cliente paga horas depois normalmente)
  useEffect(() => {
    if (!payment || payment.status === PAYMENT_STATUS.Pago) return;
    const id = setInterval(async () => {
      try {
        const fresh = await paymentsApi.get(payment.id);
        setPayment(fresh);
        if (fresh.status === PAYMENT_STATUS.Pago) {
          clearInterval(id);
          onConfirmed(fresh);
        }
      } catch { /* network blip */ }
    }, 10_000);
    return () => clearInterval(id);
  }, [payment, onConfirmed]);

  const meta = payment?.metadata as { entidade?: string; referencia?: string } | undefined;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-2xl dark:bg-zinc-900">
        <h2 className="text-lg font-semibold">Pagar com Multibanco</h2>
        <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
          Total: <strong>{(amountCents / 100).toFixed(2)}€</strong>
        </p>

        {!payment && (
          <div className="mt-4 space-y-3">
            {error && <p className="text-sm text-red-600">{error}</p>}
            <div className="flex gap-2">
              <Button onClick={generate} disabled={submitting}>
                {submitting ? 'A gerar…' : 'Gerar referência MB'}
              </Button>
              <Button variant="ghost" onClick={onCancel}>Cancelar</Button>
            </div>
          </div>
        )}

        {payment && payment.status !== PAYMENT_STATUS.Pago && meta && (
          <div className="mt-4 space-y-3">
            <div className="rounded-lg border border-zinc-300 p-4 font-mono text-base dark:border-zinc-700">
              <div className="flex justify-between"><span>Entidade</span><strong>{meta.entidade}</strong></div>
              <div className="flex justify-between"><span>Referência</span><strong>{meta.referencia}</strong></div>
              <div className="flex justify-between"><span>Montante</span><strong>{(amountCents / 100).toFixed(2)}€</strong></div>
            </div>
            <p className="text-xs text-zinc-500">
              Cliente paga em ATM, homebanking ou app. Referência válida 72h. A janela actualiza
              automaticamente quando o pagamento for confirmado.
            </p>
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
