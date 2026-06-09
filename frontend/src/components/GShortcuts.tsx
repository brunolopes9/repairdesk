import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';

/**
 * Atalhos g-prefix estilo Vim / Linear / GitHub.
 * Carregas `g` e depois uma letra dentro de 1.5s.
 *
 *  g d → Dashboard
 *  g c → Clientes
 *  g r → Reparações
 *  g t → Trabalhos
 *  g s → Stock
 *  g p → Preços
 *  g e → Despesas
 *  g a → Auditoria
 *  g i → Definições (Init/Setup)
 *
 * Não dispara dentro de inputs/textareas. Esc cancela a sequência.
 */
type ShortcutTarget = {
  to: string;
  label: string;
};

const NAV_KEYS: Record<string, ShortcutTarget> = {
  d: { to: '/', label: 'Dashboard' },
  c: { to: '/clientes', label: 'Clientes' },
  r: { to: '/reparacoes', label: 'Reparações' },
  t: { to: '/trabalhos', label: 'Trabalhos' },
  b: { to: '/balcao', label: 'Balcão' },
  o: { to: '/compras-operacao', label: 'Operação' },
  e: { to: '/despesas', label: 'Despesas' },
  l: { to: '/catalogo', label: 'Catálogo' },
  s: { to: '/stock', label: 'Stock' },
  u: { to: '/produtos', label: 'Produtos' },
  p: { to: '/precos', label: 'Preços' },
  a: { to: '/auditoria', label: 'Auditoria' },
  i: { to: '/definicoes', label: 'Definições' },
};

export default function GShortcuts() {
  const navigate = useNavigate();
  const [waitingForSecondKey, setWaitingForSecondKey] = useState(false);

  useEffect(() => {
    let timeoutId: number | null = null;

    function isTyping(target: EventTarget | null): boolean {
      const el = target as HTMLElement | null;
      if (!el) return false;
      const tag = el.tagName;
      return (
        tag === 'INPUT' ||
        tag === 'TEXTAREA' ||
        tag === 'SELECT' ||
        el.isContentEditable === true
      );
    }

    function onKeyDown(e: KeyboardEvent) {
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      if (isTyping(e.target)) return;

      if (waitingForSecondKey) {
        // Segunda tecla pressionada
        if (e.key === 'Escape') {
          setWaitingForSecondKey(false);
          if (timeoutId) window.clearTimeout(timeoutId);
          return;
        }

        const target = NAV_KEYS[e.key.toLowerCase()];
        if (target) {
          e.preventDefault();
          navigate(target.to);
        }
        setWaitingForSecondKey(false);
        if (timeoutId) {
          window.clearTimeout(timeoutId);
          timeoutId = null;
        }
        return;
      }

      // Aguardar primeira tecla `g`
      if (e.key === 'g') {
        e.preventDefault();
        setWaitingForSecondKey(true);
        // Cancela sequência se nada premido em 1.5s
        timeoutId = window.setTimeout(() => {
          setWaitingForSecondKey(false);
          timeoutId = null;
        }, 1500);
      }
    }

    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      if (timeoutId) window.clearTimeout(timeoutId);
    };
  }, [navigate, waitingForSecondKey]);

  // Indicador visual quando a aguardar segunda tecla
  if (!waitingForSecondKey) return null;
  return (
    <div className="pointer-events-none fixed bottom-4 right-4 z-40 w-[min(23rem,calc(100vw-2rem))] rounded-xl border border-zinc-800 bg-zinc-950 p-3 text-xs text-white shadow-2xl dark:border-zinc-200 dark:bg-zinc-50 dark:text-zinc-900">
      <div className="mb-2 flex items-center justify-between gap-3">
        <span className="font-semibold">Atalho de navegação</span>
        <span className="text-zinc-400 dark:text-zinc-500">Esc cancela</span>
      </div>
      <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-3">
        {Object.entries(NAV_KEYS).map(([key, target]) => (
          <span key={key} className="flex min-w-0 items-center gap-2 rounded-lg bg-white/5 px-2 py-1.5 dark:bg-zinc-900/5">
            <kbd className="rounded border border-white/15 px-1.5 py-0.5 font-mono text-[11px] dark:border-zinc-900/15">
              g {key}
            </kbd>
            <span className="truncate text-zinc-300 dark:text-zinc-700">{target.label}</span>
          </span>
        ))}
      </div>
    </div>
  );
}
