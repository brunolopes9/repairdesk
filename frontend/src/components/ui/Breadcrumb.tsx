import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';

export type BreadcrumbItem = {
  label: string;
  to?: string;
};

export function Breadcrumb({ items }: { items: BreadcrumbItem[] }) {
  if (items.length === 0) return null;

  return (
    <nav aria-label="Breadcrumb" className="flex items-center gap-1 text-xs text-zinc-500">
      <ol className="flex items-center gap-1 overflow-hidden">
        {items.map((item, i) => {
          const isLast = i === items.length - 1;
          return (
            <li key={i} className="flex items-center gap-1 min-w-0">
              {item.to && !isLast ? (
                <Link
                  to={item.to}
                  className="truncate hover:text-zinc-900 hover:underline dark:hover:text-zinc-100"
                >
                  {item.label}
                </Link>
              ) : (
                <span
                  className={
                    isLast
                      ? 'truncate font-medium text-zinc-900 dark:text-zinc-100'
                      : 'truncate'
                  }
                  aria-current={isLast ? 'page' : undefined}
                >
                  {item.label}
                </span>
              )}
              {!isLast && (
                <ChevronRight size={12} strokeWidth={2} className="shrink-0 text-zinc-300 dark:text-zinc-600" aria-hidden />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
