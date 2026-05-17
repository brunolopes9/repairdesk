import type { ComponentType, ReactNode } from 'react';

type IconCmp = ComponentType<{ className?: string; size?: number; strokeWidth?: number }>;

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  compact,
}: {
  icon?: IconCmp;
  title: string;
  description?: string;
  action?: ReactNode;
  compact?: boolean;
}) {
  return (
    <div className={`rounded-xl border border-dashed border-zinc-300 bg-white text-center dark:border-zinc-700 dark:bg-zinc-900 ${compact ? 'p-4' : 'p-6'}`}>
      {Icon && (
        <div className="mx-auto grid h-10 w-10 place-items-center rounded-full bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-300">
          <Icon size={18} strokeWidth={1.8} />
        </div>
      )}
      <h2 className="mt-3 text-sm font-semibold">{title}</h2>
      {description && <p className="mx-auto mt-1 max-w-md text-sm text-zinc-500">{description}</p>}
      {action && <div className="mt-4 flex justify-center">{action}</div>}
    </div>
  );
}
