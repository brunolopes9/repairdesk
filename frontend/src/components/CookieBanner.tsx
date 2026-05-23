import { useEffect, useState } from 'react';
import { Cookie, X } from 'lucide-react';
import { Link } from 'react-router-dom';

const STORAGE_KEY = 'rd.cookie.ack';

/**
 * Banner discreto que reconhece uso de cookies essenciais.
 * NÃO bloqueia navegação — o Mender usa apenas cookies essenciais (sessão, JWT, preferências).
 * Quando aparecerem analytics/marketing, este componente terá de ser substituído por
 * banner com consentimento granular real.
 */
export default function CookieBanner() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    try {
      const acked = localStorage.getItem(STORAGE_KEY);
      if (!acked) {
        // pequena delay para não chocar com o login
        const t = setTimeout(() => setVisible(true), 800);
        return () => clearTimeout(t);
      }
    } catch {
      /* localStorage indisponível — não mostrar */
    }
  }, []);

  function dismiss() {
    try { localStorage.setItem(STORAGE_KEY, new Date().toISOString()); } catch { /* ignore */ }
    setVisible(false);
  }

  if (!visible) return null;

  return (
    <div
      role="region"
      aria-label="Aviso de cookies"
      className="fixed inset-x-3 bottom-3 z-40 mx-auto max-w-2xl rounded-2xl border border-zinc-200 bg-white/95 p-4 shadow-lg backdrop-blur dark:border-zinc-800 dark:bg-zinc-900/95"
    >
      <div className="flex items-start gap-3">
        <span className="grid h-9 w-9 flex-none place-items-center rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300">
          <Cookie size={16} strokeWidth={2} />
        </span>
        <div className="flex-1 text-sm">
          <p className="font-medium text-zinc-900 dark:text-zinc-100">Cookies essenciais</p>
          <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
            Usamos apenas cookies essenciais para o site funcionar e manter a tua sessão segura.
            Não usamos cookies de analítica ou marketing. Mais detalhes na{' '}
            <Link to="/cookies" className="underline hover:text-zinc-900 dark:hover:text-zinc-100">Política de Cookies</Link>.
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={dismiss}
              className="rounded-lg bg-zinc-900 px-3 py-1.5 text-xs font-medium text-white transition hover:bg-zinc-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-200"
            >
              Percebi
            </button>
            <Link
              to="/cookies"
              className="rounded-lg px-3 py-1.5 text-xs text-zinc-600 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Saber mais
            </Link>
          </div>
        </div>
        <button
          type="button"
          onClick={dismiss}
          aria-label="Fechar"
          className="rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-zinc-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-300"
        >
          <X size={14} strokeWidth={2} />
        </button>
      </div>
    </div>
  );
}
