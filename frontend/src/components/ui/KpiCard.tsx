import type { LucideIcon } from 'lucide-react';

export type KpiTone = 'brand' | 'emerald' | 'amber' | 'red' | 'zinc';

const TONE: Record<KpiTone, string> = {
  brand: 'bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300',
  emerald: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  amber: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  red: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
  zinc: 'bg-zinc-200 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
};

// Sprint 376: fundo tonal subtil do card (o "mais cores" pedido pelo Bruno) + borda da cor.
const CARD_TONE: Record<KpiTone, string> = {
  brand: 'border-brand-200/70 bg-gradient-to-br from-brand-50 to-white dark:border-brand-900/40 dark:from-brand-900/15 dark:to-zinc-900',
  emerald: 'border-emerald-200/70 bg-gradient-to-br from-emerald-50 to-white dark:border-emerald-900/40 dark:from-emerald-900/15 dark:to-zinc-900',
  amber: 'border-amber-200/70 bg-gradient-to-br from-amber-50 to-white dark:border-amber-900/40 dark:from-amber-900/15 dark:to-zinc-900',
  red: 'border-red-200/70 bg-gradient-to-br from-red-50 to-white dark:border-red-900/40 dark:from-red-900/15 dark:to-zinc-900',
  zinc: 'border-zinc-200/80 bg-white dark:border-zinc-800 dark:bg-zinc-900',
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
