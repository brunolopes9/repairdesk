import type { ReactNode } from 'react';

type BadgeTone = 'amber' | 'violet' | 'sky' | 'blue' | 'emerald' | 'rose' | 'zinc' | 'yellow';

const tones: Record<BadgeTone, string> = {
  amber: 'bg-amber-100 text-amber-900 ring-1 ring-amber-200 dark:bg-amber-900/40 dark:text-amber-100 dark:ring-amber-700/60',
  violet: 'bg-violet-100 text-violet-900 ring-1 ring-violet-200 dark:bg-violet-900/40 dark:text-violet-100 dark:ring-violet-700/60',
  sky: 'bg-sky-100 text-sky-900 ring-1 ring-sky-200 dark:bg-sky-900/40 dark:text-sky-100 dark:ring-sky-700/60',
  blue: 'bg-blue-100 text-blue-900 ring-1 ring-blue-200 dark:bg-blue-900/40 dark:text-blue-100 dark:ring-blue-700/60',
  emerald: 'bg-emerald-100 text-emerald-900 ring-1 ring-emerald-200 dark:bg-emerald-900/40 dark:text-emerald-100 dark:ring-emerald-700/60',
  rose: 'bg-rose-700 text-white ring-1 ring-rose-800 dark:bg-rose-800 dark:text-rose-100 dark:ring-rose-700',
  zinc: 'bg-zinc-200 text-zinc-800 ring-1 ring-zinc-300 dark:bg-zinc-700 dark:text-zinc-200 dark:ring-zinc-600',
  yellow: 'bg-yellow-100 text-yellow-900 ring-1 ring-yellow-200 dark:bg-yellow-900/40 dark:text-yellow-100 dark:ring-yellow-700/60',
};

interface Props {
  tone?: BadgeTone;
  icon?: ReactNode;
  children: ReactNode;
  size?: 'sm' | 'md';
}

export function StatusBadge({ tone = 'zinc', icon, children, size = 'sm' }: Props) {
  const sz = size === 'sm' ? 'px-2 py-0.5 text-[10px]' : 'px-2.5 py-1 text-xs';
  return (
    <span className={`inline-flex items-center gap-1 rounded-full font-medium ${sz} ${tones[tone]}`}>
      {icon && <span className="inline-flex">{icon}</span>}
      {children}
    </span>
  );
}
