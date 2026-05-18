import { useEffect, useState } from 'react';
import { Keyboard, X } from 'lucide-react';

/**
 * Modal de atalhos de teclado.
 * Activado por '?' (sem modifier, fora de inputs/textareas).
 * Esc fecha.
 */
const SHORTCUTS = [
  { keys: ['Ctrl', 'K'], label: 'Procurar / acções rápidas (command palette)', macKeys: ['⌘', 'K'] },
  { keys: ['?'], label: 'Mostrar este painel' },
  { keys: ['Esc'], label: 'Fechar modal / cancelar' },
];

const G_SHORTCUTS = [
  { keys: ['g', 'd'], label: 'Ir para Dashboard' },
  { keys: ['g', 'c'], label: 'Ir para Clientes' },
  { keys: ['g', 'r'], label: 'Ir para Reparações' },
  { keys: ['g', 't'], label: 'Ir para Trabalhos' },
  { keys: ['g', 'e'], label: 'Ir para Despesas' },
  { keys: ['g', 's'], label: 'Ir para Stock' },
  { keys: ['g', 'p'], label: 'Ir para Preços' },
  { keys: ['g', 'a'], label: 'Ir para Auditoria' },
  { keys: ['g', 'i'], label: 'Ir para Definições' },
];

export default function KeyboardHelp() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      // Ignora se estamos a digitar num input / textarea / contenteditable
      const target = e.target as HTMLElement | null;
      const isTyping =
        target &&
        (target.tagName === 'INPUT' ||
          target.tagName === 'TEXTAREA' ||
          target.tagName === 'SELECT' ||
          (target as HTMLElement).isContentEditable);

      if (e.key === '?' && !e.ctrlKey && !e.metaKey && !e.altKey && !isTyping) {
        e.preventDefault();
        setOpen((v) => !v);
      } else if (e.key === 'Escape' && open) {
        setOpen(false);
      }
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [open]);

  if (!open) return null;

  const isMac = typeof navigator !== 'undefined' && /Mac/i.test(navigator.platform);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-sm"
      onClick={() => setOpen(false)}
      role="dialog"
      aria-modal="true"
      aria-label="Atalhos de teclado"
    >
      <div
        className="w-full max-w-md overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-2xl dark:border-zinc-700 dark:bg-zinc-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
          <div className="flex items-center gap-2">
            <Keyboard size={16} strokeWidth={2} className="text-zinc-500" />
            <h2 className="text-sm font-semibold">Atalhos de teclado</h2>
          </div>
          <button
            type="button"
            onClick={() => setOpen(false)}
            aria-label="Fechar"
            className="rounded-md p-1 text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800"
          >
            <X size={14} strokeWidth={2} />
          </button>
        </div>
        <div className="max-h-[70vh] overflow-y-auto px-4 py-3">
          <ul className="space-y-1">
            {SHORTCUTS.map((s, i) => (
              <li key={i} className="flex items-center justify-between gap-3 py-1.5 text-sm">
                <span className="text-zinc-700 dark:text-zinc-300">{s.label}</span>
                <span className="flex items-center gap-1">
                  {(isMac && s.macKeys ? s.macKeys : s.keys).map((k, idx) => (
                    <kbd
                      key={idx}
                      className="rounded border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-[11px] font-mono text-zinc-700 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300"
                    >
                      {k}
                    </kbd>
                  ))}
                </span>
              </li>
            ))}
          </ul>
          <div className="mt-4 mb-2 text-[10px] uppercase tracking-wide text-zinc-500">Navegação rápida (g + tecla)</div>
          <ul className="space-y-1">
            {G_SHORTCUTS.map((s, i) => (
              <li key={i} className="flex items-center justify-between gap-3 py-1.5 text-sm">
                <span className="text-zinc-700 dark:text-zinc-300">{s.label}</span>
                <span className="flex items-center gap-1">
                  {s.keys.map((k, idx) => (
                    <kbd
                      key={idx}
                      className="rounded border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-[11px] font-mono text-zinc-700 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300"
                    >
                      {k}
                    </kbd>
                  ))}
                </span>
              </li>
            ))}
          </ul>
        </div>
        <div className="border-t border-zinc-100 bg-zinc-50 px-4 py-2 text-[11px] text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950">
          Carrega <kbd className="rounded border border-zinc-200 bg-white px-1 dark:border-zinc-700 dark:bg-zinc-900">g</kbd> seguido de outra tecla. Esc cancela.
        </div>
      </div>
    </div>
  );
}
