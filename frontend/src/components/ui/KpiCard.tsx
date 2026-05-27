import type { LucideIcon } from 'lucide-react';
import { Card } from './Card';

export type KpiTone = 'brand' | 'emerald' | 'amber' | 'red' | 'zinc';

const TONE: Record<KpiTone, string> = {
  brand: 'bg-brand-50 text-brand-600 dark:bg-brand-900/30 dark:text-brand-300',
  emerald: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-900/30 dark:text-emerald-300',
  amber: 'bg-amber-50 text-amber-600 dark:bg-amber-900/30 dark:text-amber-300',
  red: 'bg-red-50 text-red-600 dark:bg-red-900/30 dark:text-red-300',
  zinc: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300',
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
    <Card className="p-4">
      <div className="flex items-start justify-between gap-2">
        <span className={`grid h-9 w-9 flex-none place-items-center rounded-lg ${TONE[tone]}`}>
          <Icon size={18} strokeWidth={2} />
        </span>
      </div>
      <p className="mt-3 text-xs font-medium text-zinc-500">{label}</p>
      <div className="mt-0.5 flex items-baseline gap-1.5">
        <span className="text-2xl font-semibold tabular-nums tracking-tight">{value}</span>
        {sub && <span className="text-xs text-zinc-400">{sub}</span>}
      </div>
      {delta && (
        <p className={`mt-1 text-[11px] font-medium ${deltaPositive === undefined ? 'text-zinc-400' : deltaPositive ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}`}>
          {delta}
        </p>
      )}
    </Card>
  );
}
