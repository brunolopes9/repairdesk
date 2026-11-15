import { useState, type ReactNode } from 'react';
import { ChevronRight } from 'lucide-react';

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
  collapsible = false,
  defaultOpen = true,
}: {
  title?: ReactNode;
  action?: ReactNode;
  bodyClassName?: string;
  className?: string;
  children: ReactNode;
  /** Sprint 662: o título abre/fecha o bloco (para secções que raramente se usam e alongam a página). */
  collapsible?: boolean;
  defaultOpen?: boolean;
}) {
  const [aberto, setAberto] = useState(defaultOpen);
  if (collapsible) {
    return (
      <Card className={className}>
        <div className={`flex flex-wrap items-center justify-between gap-x-3 gap-y-2 px-4 py-3 ${aberto ? 'border-b border-zinc-100 dark:border-zinc-800' : ''}`}>
          <button type="button" onClick={() => setAberto((a) => !a)} aria-expanded={aberto}
            className="flex min-h-9 flex-1 items-center gap-2 text-left text-sm font-semibold tracking-tight">
            <ChevronRight size={16} className={`shrink-0 text-zinc-400 transition-transform ${aberto ? 'rotate-90' : ''}`} aria-hidden />
            {title}
          </button>
          {aberto && action}
        </div>
        {aberto && <div className={bodyClassName}>{children}</div>}
      </Card>
    );
  }
  return (
    <Card className={className}>
      {(title || action) && (
        // flex-wrap: no telemóvel as ações descem para a linha seguinte em vez de saírem do ecrã.
        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
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
