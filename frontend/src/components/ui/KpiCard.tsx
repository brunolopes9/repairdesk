import type { LucideIcon } from 'lucide-react';

export type KpiTone = 'brand' | 'emerald' | 'amber' | 'red' | 'zinc';

const TONE: Record<KpiTone, string> = {
  brand: 'bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300',
  emerald: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  amber: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  red: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
  zinc: 'bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
};

// Sprint 376/378: fundo tonal SÓLIDO do card (o "mais cores" pedido pelo Bruno) + borda da cor.
// Tom -100 (light) bem visível, não gradiente esbatido.
const CARD_TONE: Record<KpiTone, string> = {
  brand: 'border-brand-200 bg-brand-50 dark:border-brand-900/50 dark:bg-brand-900/20',
  emerald: 'border-emerald-200 bg-emerald-50 dark:border-emerald-900/50 dark:bg-emerald-900/20',
  amber: 'border-amber-200 bg-amber-50 dark:border-amber-900/50 dark:bg-amber-900/20',
  red: 'border-red-200 bg-red-50 dark:border-red-900/50 dark:bg-red-900/20',
  zinc: 'border-zinc-200 bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900',
};

/**
 * Sprint 372: cartão KPI do novo dashboard — ícone tonal + label + valor grande (tabular) +
 * delta opcional ("+3 vs ontem") com cor por direção. Reutilizável em todo o lado.
 */
export function KpiCard({
  icon: Icon,
  label,
  value,
  sub,
  delta,
  deltaPositive,
  tone = 'brand',
}: {
  icon: LucideIcon;
  label: string;
  value: string;
  sub?: string;
  delta?: string;
  deltaPositive?: boolean;
  tone?: KpiTone;
}) {
  return (
    <div className={`rounded-xl border p-4 shadow-sm shadow-black/[0.02] ${CARD_TONE[tone]}`}>
      <div className="flex items-start justify-between gap-2">
        <span className={`grid h-9 w-9 flex-none place-items-center rounded-lg ${TONE[tone]}`}>
          <Icon size={18} strokeWidth={2} />
        </span>
      </div>
      <p className="mt-3 text-xs font-medium text-zinc-500 dark:text-zinc-400">{label}</p>
      <div className="mt-0.5 flex items-baseline gap-1.5">
        <span className="text-2xl font-semibold tabular-nums tracking-tight">{value}</span>
        {sub && <span className="text-xs text-zinc-400">{sub}</span>}
      </div>
      {delta && (
        <p className={`mt-1 text-[11px] font-medium ${deltaPositive === undefined ? 'text-zinc-400' : deltaPositive ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}`}>
          {delta}
        </p>
      )}
    </div>
  );
}
