import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import { AlertTriangle, Trash2 } from 'lucide-react';
import Modal from './Modal';

/**
 * Sprint 254 (Doc 79): substitui `window.confirm()` nativo (8 ocorrências).
 *
 * Vantagens vs confirm():
 * - Theme dark/light, mobile-friendly
 * - Botão destructive vermelho com ícone — sinal visual mais claro
 * - Permite título + descrição multi-linha
 * - Não bloqueia event loop
 * - Customizável (texto botões, ícone)
 *
 * @example
 *   const confirm = useConfirm();
 *   const ok = await confirm({ title: 'Apagar cliente?', description: '...', destructive: true });
 *   if (ok) remove.mutate(id);
 */

interface ConfirmOptions {
  title: string;
  description?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}

interface ConfirmContextValue {
  confirm: (options: ConfirmOptions) => Promise<boolean>;
}

const ConfirmContext = createContext<ConfirmContextValue | null>(null);

interface PendingConfirm extends ConfirmOptions {
  resolve: (value: boolean) => void;
}

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [pending, setPending] = useState<PendingConfirm | null>(null);

  const confirm = useCallback((options: ConfirmOptions) => {
    return new Promise<boolean>((resolve) => {
      setPending({ ...options, resolve });
    });
  }, []);

  const handleClose = (result: boolean) => {
    if (!pending) return;
    pending.resolve(result);
    setPending(null);
  };

  return (
    <ConfirmContext.Provider value={{ confirm }}>
      {children}
      <Modal
        open={!!pending}
        onClose={() => handleClose(false)}
        title={pending?.title ?? ''}
      >
        {pending && (
          <div>
            <div className="flex items-start gap-3">
              <div className={`grid h-10 w-10 shrink-0 place-items-center rounded-full ${
                pending.destructive
                  ? 'bg-rose-100 text-rose-600 dark:bg-rose-900/40 dark:text-rose-400'
                  : 'bg-amber-100 text-amber-600 dark:bg-amber-900/40 dark:text-amber-400'
              }`}>
                {pending.destructive
                  ? <Trash2 size={18} strokeWidth={2} aria-hidden />
                  : <AlertTriangle size={18} strokeWidth={2} aria-hidden />}
              </div>
              {pending.description && (
                <div className="text-sm text-zinc-700 dark:text-zinc-300">{pending.description}</div>
              )}
            </div>
            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => handleClose(false)}
                className="min-h-10 rounded-xl border border-zinc-300 bg-white px-4 text-sm font-medium text-zinc-700 transition hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300"
              >
                {pending.cancelLabel ?? 'Cancelar'}
              </button>
              <button
                type="button"
                onClick={() => handleClose(true)}
                autoFocus
                className={`min-h-10 rounded-xl px-4 text-sm font-medium text-white shadow-sm transition ${
                  pending.destructive
                    ? 'bg-rose-600 hover:bg-rose-700 focus-visible:ring-2 focus-visible:ring-rose-400'
                    : 'bg-brand-600 hover:bg-brand-700 focus-visible:ring-2 focus-visible:ring-brand-400'
                }`}
              >
                {pending.confirmLabel ?? (pending.destructive ? 'Apagar' : 'Confirmar')}
              </button>
            </div>
          </div>
        )}
      </Modal>
    </ConfirmContext.Provider>
  );
}

/**
 * Devolve uma função `confirm(opts) => Promise<boolean>`. Tem que estar dentro
 * de `<ConfirmProvider>` (registado em App.tsx).
 */
export function useConfirm() {
  const ctx = useContext(ConfirmContext);
  if (!ctx) throw new Error('useConfirm precisa de <ConfirmProvider>');
  return ctx.confirm;
}
