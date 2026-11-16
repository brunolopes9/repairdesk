import { ArrowLeftRight, Banknote, CreditCard, Landmark, MoreHorizontal, Smartphone, type LucideIcon } from 'lucide-react';
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL, type PaymentMethod } from '../lib/vendas/types';

/**
 * Escolha do método de pagamento em botões (um toque, sem lista pendente). Cada método tem a sua cor e ícone
 * para se reconhecer de relance ao balcão. Usado na venda, no balcão e nos recibos do Moloni.
 */
const ESTILO: Record<PaymentMethod, { icon: LucideIcon; ativo: string; icone: string }> = {
  [PAYMENT_METHOD.Dinheiro]: { icon: Banknote, ativo: 'border-emerald-500 bg-emerald-50 text-emerald-800 ring-emerald-500 dark:bg-emerald-950/40 dark:text-emerald-200', icone: 'text-emerald-600' },
  [PAYMENT_METHOD.MBWay]: { icon: Smartphone, ativo: 'border-rose-500 bg-rose-50 text-rose-800 ring-rose-500 dark:bg-rose-950/40 dark:text-rose-200', icone: 'text-rose-600' },
  [PAYMENT_METHOD.Multibanco]: { icon: Landmark, ativo: 'border-sky-500 bg-sky-50 text-sky-800 ring-sky-500 dark:bg-sky-950/40 dark:text-sky-200', icone: 'text-sky-600' },
  [PAYMENT_METHOD.Cartao]: { icon: CreditCard, ativo: 'border-indigo-500 bg-indigo-50 text-indigo-800 ring-indigo-500 dark:bg-indigo-950/40 dark:text-indigo-200', icone: 'text-indigo-600' },
  [PAYMENT_METHOD.TransferenciaBancaria]: { icon: ArrowLeftRight, ativo: 'border-amber-500 bg-amber-50 text-amber-800 ring-amber-500 dark:bg-amber-950/40 dark:text-amber-200', icone: 'text-amber-600' },
  [PAYMENT_METHOD.Outro]: { icon: MoreHorizontal, ativo: 'border-zinc-500 bg-zinc-100 text-zinc-800 ring-zinc-500 dark:bg-zinc-800 dark:text-zinc-100', icone: 'text-zinc-500' },
};

/** Ordem: os mais usados primeiro. */
export const PAGAMENTOS_ORDEM: PaymentMethod[] = [
  PAYMENT_METHOD.MBWay, PAYMENT_METHOD.Dinheiro, PAYMENT_METHOD.Multibanco,
  PAYMENT_METHOD.Cartao, PAYMENT_METHOD.TransferenciaBancaria, PAYMENT_METHOD.Outro,
];

export function PagamentoPicker({ value, onChange, metodos = PAGAMENTOS_ORDEM, disabled, compacto }: {
  value: PaymentMethod;
  onChange: (m: PaymentMethod) => void;
  metodos?: PaymentMethod[];
  disabled?: boolean;
  /** Botões mais baixos (dentro de modais e cartões estreitos). */
  compacto?: boolean;
}) {
  return (
    <div role="radiogroup" aria-label="Método de pagamento" className="flex flex-wrap gap-2">
      {metodos.map((m) => {
        const e = ESTILO[m];
        const Icon = e.icon;
        const ativo = value === m;
        return (
          <button
            key={m}
            type="button"
            role="radio"
            aria-checked={ativo}
            disabled={disabled}
            onClick={() => onChange(m)}
            className={`inline-flex items-center gap-1.5 rounded-xl border-2 font-medium transition disabled:opacity-50 ${compacto ? 'min-h-9 px-2.5 text-xs' : 'min-h-11 px-3.5 text-sm'} ${ativo
              ? `${e.ativo} ring-1`
              : 'border-zinc-200 bg-white text-zinc-700 hover:border-zinc-300 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200 dark:hover:bg-zinc-800'}`}
          >
            <Icon size={compacto ? 15 : 17} className={ativo ? '' : e.icone} />
            {PAYMENT_METHOD_LABEL[m]}
          </button>
        );
      })}
    </div>
  );
}
