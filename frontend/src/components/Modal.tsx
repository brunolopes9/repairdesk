import { useEffect, type ReactNode } from 'react';

interface Props {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
}

export default function Modal({ open, title, onClose, children, footer }: Props) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', onKey);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = '';
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 p-0 sm:items-center sm:p-4">
      <div
        className="flex max-h-[90vh] w-full max-w-lg flex-col rounded-t-2xl border border-zinc-200 bg-white shadow-xl dark:border-zinc-800 dark:bg-zinc-900 sm:rounded-2xl"
        role="dialog"
        aria-modal="true"
      >
        <header className="flex items-center justify-between border-b border-zinc-200 px-5 py-3 dark:border-zinc-800">
          <h2 className="text-base font-semibold">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            className="grid h-10 w-10 place-items-center rounded-md text-zinc-500 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800"
            aria-label="Fechar"
          >
            ×
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">{children}</div>
        {footer && (
          <footer className="flex flex-col-reverse gap-2 border-t border-zinc-200 px-5 py-3 dark:border-zinc-800 [&>button]:min-h-11 [&>button]:w-full sm:flex-row sm:justify-end sm:[&>button]:w-auto">
            {footer}
          </footer>
        )}
      </div>
    </div>
  );
}
