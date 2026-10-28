import { useEffect, useState, type ComponentType } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { LogOut, Menu, X } from 'lucide-react';

type IconCmp = ComponentType<{ size?: number; strokeWidth?: number; className?: string; 'aria-hidden'?: boolean }>;
export interface MobileNavItem { to: string; label: string; icon: IconCmp; grupo?: string }

/** Os separadores fixos em baixo — o que se usa ao balcão com o telemóvel na mão. */
const PRINCIPAIS = ['/vendas', '/balcao', '/artigos', '/clientes'];

interface Props {
  items: MobileNavItem[];
  badge: (to: string) => number;
  tema: { Icon: IconCmp; label: string; alternar: () => void };
  onSair: () => void;
}

/**
 * Sprint 572: navegação no telemóvel. Antes eram 17 botões espremidos numa barra (incluindo as
 * Definições); agora 4 separadores fixos + "Mais", uma folha com o resto agrupado.
 */
export default function MobileNav({ items, badge, tema, onSair }: Props) {
  const [aberto, setAberto] = useState(false);
  const location = useLocation();
  useEffect(() => { setAberto(false); }, [location.pathname]);

  const principais = PRINCIPAIS.map((to) => items.find((i) => i.to === to)).filter((i): i is MobileNavItem => !!i);
  const resto = items.filter((i) => !PRINCIPAIS.includes(i.to));
  const grupos = resto.reduce<Record<string, MobileNavItem[]>>((g, i) => {
    (g[i.grupo ?? 'Geral'] ??= []).push(i);
    return g;
  }, {});
  const maisAtivo = resto.some((i) => location.pathname === i.to || (i.to !== '/' && location.pathname.startsWith(i.to + '/')));
  const badgeMais = resto.reduce((s, i) => s + badge(i.to), 0);

  const separador = 'flex min-h-14 flex-1 flex-col items-center justify-center gap-0.5 text-[11px] font-medium';
  return (
    <>
      <nav aria-label="Navegação principal"
        className="fixed inset-x-0 bottom-0 z-30 border-t border-zinc-200 bg-white/95 pb-[env(safe-area-inset-bottom)] backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/95 sm:hidden">
        <ul className="flex">
          {principais.map((item) => (
            <li key={item.to} className="flex flex-1">
              <NavLink to={item.to} end={item.to === '/clientes'}
                className={({ isActive }) => `${separador} ${isActive && !aberto ? 'text-brand-600' : 'text-zinc-500 dark:text-zinc-400'}`}>
                <item.icon size={21} strokeWidth={1.75} aria-hidden />
                {item.label === 'Base de clientes' ? 'Clientes' : item.label}
              </NavLink>
            </li>
          ))}
          <li className="flex flex-1">
            <button type="button" onClick={() => setAberto((a) => !a)} aria-expanded={aberto}
              className={`${separador} relative ${aberto || maisAtivo ? 'text-brand-600' : 'text-zinc-500 dark:text-zinc-400'}`}>
              {aberto ? <X size={21} strokeWidth={1.75} aria-hidden /> : <Menu size={21} strokeWidth={1.75} aria-hidden />}
              Mais
              {badgeMais > 0 && <span className="absolute right-[calc(50%-18px)] top-2 h-2 w-2 rounded-full bg-red-500" />}
            </button>
          </li>
        </ul>
      </nav>

      {aberto && (
        <div className="fixed inset-0 z-20 sm:hidden" role="dialog" aria-modal="true" aria-label="Mais">
          <button type="button" aria-label="Fechar" className="absolute inset-0 bg-black/40" onClick={() => setAberto(false)} />
          <div className="absolute inset-x-0 bottom-0 max-h-[80vh] overflow-y-auto rounded-t-2xl bg-white px-4 pb-[calc(4.5rem+env(safe-area-inset-bottom))] pt-4 shadow-2xl dark:bg-zinc-900">
            {Object.entries(grupos).map(([grupo, lista]) => (
              <section key={grupo} className="mb-4">
                <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500">{grupo}</h2>
                <ul className="grid grid-cols-3 gap-2">
                  {lista.map((item) => (
                    <li key={item.to}>
                      <NavLink to={item.to} end
                        className={({ isActive }) => `relative flex min-h-20 flex-col items-center justify-center gap-1 rounded-xl border px-1 text-center text-xs font-medium ${isActive
                          ? 'border-brand-300 bg-brand-50 text-brand-700 dark:border-brand-800 dark:bg-brand-950/40 dark:text-brand-300'
                          : 'border-zinc-200 text-zinc-700 dark:border-zinc-800 dark:text-zinc-200'}`}>
                        <item.icon size={20} strokeWidth={1.75} aria-hidden />
                        {item.label}
                        {badge(item.to) > 0 && (
                          <span className="absolute right-1.5 top-1.5 grid h-4 min-w-4 place-items-center rounded-full bg-red-500 px-1 text-[10px] font-semibold text-white">{badge(item.to)}</span>
                        )}
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </section>
            ))}
            <div className="flex gap-2 border-t border-zinc-200 pt-3 dark:border-zinc-800">
              <button type="button" onClick={tema.alternar} className="flex min-h-11 flex-1 items-center justify-center gap-2 rounded-lg border border-zinc-200 text-sm dark:border-zinc-700">
                <tema.Icon size={16} /> Tema: {tema.label}
              </button>
              <button type="button" onClick={onSair} className="flex min-h-11 flex-1 items-center justify-center gap-2 rounded-lg border border-zinc-200 text-sm text-rose-600 dark:border-zinc-700">
                <LogOut size={16} /> Sair
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
