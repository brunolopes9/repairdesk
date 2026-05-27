import type { ReactNode } from 'react';

/**
 * Sprint 372: superfície base do sistema de design. Card branco (zinc-900 dark) com borda
 * subtil + raio xl + sombra leve, sobre a superfície cinza da página. Base de TODAS as páginas.
 */
export function Card({ className = '', children }: { className?: string; children: ReactNode }) {
  return (
    <div className={`rounded-xl border border-zinc-200/80 bg-white shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900 ${className}`}>
      {children}
    </div>
  );
}

/**
 * Card com cabeçalho opcional (título + ação à direita) e corpo com padding. Para os blocos
 * do dashboard e listas (Fila operacional, Ritmo 7 dias, etc.).
 */
export function SectionCard({
  title,
  action,
  bodyClassName = 'p-4',
  className = '',
  children,
}: {
  title?: ReactNode;
  action?: ReactNode;
  bodyClassName?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <Card className={className}>
      {(title || action) && (
        <div className="flex items-center justify-between gap-3 border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
          {typeof title === 'string'
            ? <h2 className="text-sm font-semibold tracking-tight">{title}</h2>
            : title}
          {action}
        </div>
      )}
      <div className={bodyClassName}>{children}</div>
    </Card>
  );
}
