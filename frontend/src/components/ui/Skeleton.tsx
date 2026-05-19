export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded bg-zinc-200 dark:bg-zinc-800 ${className}`} />;
}

export function SkeletonCard() {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <Skeleton className="h-3 w-24" />
      <Skeleton className="mt-3 h-5 w-2/3" />
      <Skeleton className="mt-2 h-3 w-1/2" />
    </div>
  );
}

export function SkeletonRow({ columns = 4, widths }: { columns?: number; widths?: string[] }) {
  const cells = widths ?? Array.from({ length: columns }, () => undefined);

  return (
    <div className="grid gap-3 p-3" style={{ gridTemplateColumns: `repeat(${cells.length}, minmax(0, 1fr))` }}>
      {cells.map((width, column) => (
        <Skeleton key={column} className={`h-3 ${width ?? ''}`} />
      ))}
    </div>
  );
}

export function SkeletonTable({ columns, rows, minWidth }: { columns: number; rows: number; minWidth?: string }) {
  const width = minWidth ?? (columns >= 7 ? 'min-w-[760px]' : columns >= 5 ? 'min-w-[640px]' : 'min-w-[480px]');

  return (
    <div className="overflow-x-auto rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      <div className={width}>
        <div className="grid gap-px bg-zinc-100 p-3 dark:bg-zinc-800" style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}>
          {Array.from({ length: columns }).map((_, index) => (
            <Skeleton key={`h-${index}`} className="h-3 bg-zinc-300 dark:bg-zinc-700" />
          ))}
        </div>
        <div className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {Array.from({ length: rows }).map((_, row) => (
            <SkeletonRow key={row} columns={columns} />
          ))}
        </div>
      </div>
    </div>
  );
}
