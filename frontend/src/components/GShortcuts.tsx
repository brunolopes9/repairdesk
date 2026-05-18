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
const NAV_KEYS: Record<string, string> = {
  d: '/',
  c: '/clientes',
  r: '/reparacoes',
  t: '/trabalhos',
  s: '/stock',
  p: '/precos',
  e: '/despesas',
  a: '/auditoria',
  i: '/definicoes',
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
          navigate(target);
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
    <div className="pointer-events-none fixed bottom-4 right-4 z-40 rounded-full bg-zinc-900 px-3 py-1.5 text-xs text-white shadow-lg dark:bg-zinc-100 dark:text-zinc-900">
      <kbd className="font-mono">g</kbd> + ... · Esc para cancelar
    </div>
  );
}
