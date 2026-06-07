import type { ReactNode } from 'react';

type DetailWorkspaceProps = {
  children: ReactNode;
  rail?: ReactNode;
  className?: string;
};

export function DetailWorkspace({ children, rail, className = '' }: DetailWorkspaceProps) {
  return (
    <div className={`grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px] ${className}`}>
      <div className="min-w-0 space-y-4">{children}</div>
      {rail ? (
        <aside className="min-w-0 xl:sticky xl:top-20 xl:self-start">
          {rail}
        </aside>
      ) : null}
    </div>
  );
}

export function InspectorRail({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900 ${className}`}>
      {children}
    </div>
  );
}

type ViewTab = {
  key: string;
  label: string;
  meta?: ReactNode;
};

export function ViewTabs({
  tabs,
  value,
  onChange,
  className = '',
}: {
  tabs: ViewTab[];
  value: string;
  onChange: (value: string) => void;
  className?: string;
}) {
  return (
    <div className={`overflow-x-auto rounded-lg border border-zinc-200 bg-zinc-50 p-1 dark:border-zinc-800 dark:bg-zinc-950 ${className}`}>
      <div className="flex min-w-max gap-1">
        {tabs.map((tab) => {
          const active = tab.key === value;
          return (
            <button
              key={tab.key}
              type="button"
              onClick={() => onChange(tab.key)}
              className={`inline-flex min-h-9 items-center gap-2 rounded-md px-3 text-sm font-medium transition ${
                active
                  ? 'bg-white text-zinc-950 shadow-sm ring-1 ring-zinc-200 dark:bg-zinc-900 dark:text-zinc-50 dark:ring-zinc-800'
                  : 'text-zinc-600 hover:bg-white/70 hover:text-zinc-950 dark:text-zinc-400 dark:hover:bg-zinc-900/70 dark:hover:text-zinc-100'
              }`}
            >
              <span>{tab.label}</span>
              {tab.meta ? <span className="text-xs text-zinc-400">{tab.meta}</span> : null}
            </button>
          );
        })}
      </div>
    </div>
  );
}
