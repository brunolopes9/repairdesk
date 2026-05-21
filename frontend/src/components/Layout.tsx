import { useEffect, useState, type ComponentType } from 'react';
import { useQuery } from '@tanstack/react-query';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  Wrench,
  Briefcase,
  Receipt,
  ShoppingCart,
  PackageSearch,
  Tags,
  FileText,
  ClipboardList,
  Settings,
  Webhook,
  Workflow,
  Building2,
  Smartphone,
  Inbox,
  LogOut,
  Pin,
  PinOff,
  Sun,
  Moon,
  Monitor,
  Search,
} from 'lucide-react';
import { useAuth } from '../lib/auth/AuthContext';
import HealthIndicator from './HealthIndicator';
import { tenantSettingsApi } from '../lib/tenantSettings/api';
import { applyTheme, getStoredTheme, setStoredTheme, watchSystemTheme, type Theme } from '../lib/theme';

type IconCmp = ComponentType<{ className?: string; size?: number; strokeWidth?: number }>;

const nav: Array<{ to: string; label: string; icon: IconCmp; adminOnly?: boolean }> = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/clientes', label: 'Clientes', icon: Users },
  { to: '/reparacoes', label: 'Reparações', icon: Wrench },
  { to: '/trabalhos', label: 'Trabalhos', icon: Briefcase },
  { to: '/despesas', label: 'Despesas', icon: Receipt },
  // Sprint 148: inbox de faturas de fornecedor importadas via n8n IMAP.
  { to: '/importacoes', label: 'Importações', icon: Inbox, adminOnly: true },
  { to: '/vendas', label: 'Vendas', icon: ShoppingCart },
  { to: '/stock', label: 'Stock', icon: PackageSearch },
  { to: '/produtos', label: 'Produtos', icon: Smartphone, adminOnly: true },
  { to: '/precos', label: 'Preços', icon: Tags },
  { to: '/relatorios/iva', label: 'Relatorios', icon: FileText },
  { to: '/auditoria', label: 'Auditoria', icon: ClipboardList, adminOnly: true },
  { to: '/definicoes/webhooks', label: 'Webhooks', icon: Webhook, adminOnly: true },
  { to: '/definicoes/fornecedores', label: 'Fornecedores', icon: Building2, adminOnly: true },
  { to: '/definicoes/automacoes', label: 'Automações', icon: Workflow, adminOnly: true },
  { to: '/definicoes', label: 'Definições', icon: Settings },
];

const SIDEBAR_PIN_KEY = 'rd.sidebar.pinned';

export default function Layout() {
  const { user, logout, hasRole } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [hovered, setHovered] = useState(false);
  const [pinned, setPinned] = useState<boolean>(() => {
    try { return localStorage.getItem(SIDEBAR_PIN_KEY) === '1'; } catch { return false; }
  });
  const [theme, setTheme] = useState<Theme>(() => getStoredTheme());

  useEffect(() => {
    try { localStorage.setItem(SIDEBAR_PIN_KEY, pinned ? '1' : '0'); } catch { /* ignore */ }
  }, [pinned]);

  useEffect(() => {
    setStoredTheme(theme);
    applyTheme(theme);
    if (theme === 'system') return watchSystemTheme(() => applyTheme('system'));
  }, [theme]);

  function cycleTheme() {
    setTheme((t) => (t === 'light' ? 'dark' : t === 'dark' ? 'system' : 'light'));
  }
  const ThemeIcon = theme === 'light' ? Sun : theme === 'dark' ? Moon : Monitor;
  const themeLabel = theme === 'light' ? 'Claro' : theme === 'dark' ? 'Escuro' : 'Sistema';

  const expanded = hovered || pinned;

  const onboarding = useQuery({
    queryKey: ['onboarding-status'],
    queryFn: () => tenantSettingsApi.onboardingStatus(),
    staleTime: 30_000,
    enabled: Boolean(user) && hasRole('Admin'),
  });

  useEffect(() => {
    if (!user || !hasRole('Admin') || !onboarding.data || onboarding.data.onboardingCompletado) return;
    if (location.pathname === '/bemvindo') return;

    const key = `rd.onboarding.redirected.${user.tenantId}`;
    try {
      if (sessionStorage.getItem(key) === '1') return;
      sessionStorage.setItem(key, '1');
    } catch {
      /* sessionStorage may be unavailable; redirect anyway */
    }

    navigate('/bemvindo', { replace: true });
  }, [hasRole, location.pathname, navigate, onboarding.data, user]);

  async function handleLogout() {
    await logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
      <header className="sticky top-0 z-20 border-b border-zinc-200 bg-white/80 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/80">
        <div className="mx-auto flex h-14 max-w-5xl items-center justify-between px-4 sm:pl-20">
          <div className="flex items-center gap-2 font-semibold">
            <span className="text-brand-500">●</span> RepairDesk
          </div>
          <div className="flex items-center gap-2 text-xs text-zinc-500">
            {user && <span className="hidden sm:inline">{user.displayName}</span>}
            <HealthIndicator />
            <button
              type="button"
              onClick={() => {
                // Dispara o mesmo atalho que abre o CommandPalette
                window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }));
              }}
              title="Procurar / acções (Ctrl+K)"
              aria-label="Procurar"
              className="hidden min-h-10 items-center gap-1.5 rounded-md border border-zinc-200 px-3 py-2 text-zinc-500 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-800 dark:text-zinc-400 dark:hover:bg-zinc-800 sm:flex"
            >
              <Search size={12} strokeWidth={2} />
              <span>Procurar</span>
              <kbd className="ml-1 rounded border border-zinc-200 bg-white px-1 text-[10px] text-zinc-400 dark:border-zinc-700 dark:bg-zinc-900">Ctrl K</kbd>
            </button>
            <button
              type="button"
              onClick={cycleTheme}
              title={`Tema: ${themeLabel} — clica para alternar`}
              className="grid h-10 w-10 place-items-center rounded-md border border-zinc-200 text-zinc-600 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              <ThemeIcon size={14} strokeWidth={2} />
            </button>
            <button
              type="button"
              onClick={handleLogout}
              className="min-h-10 rounded-md border border-zinc-200 px-3 py-2 text-xs text-zinc-600 transition hover:bg-zinc-100 dark:border-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Sair
            </button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 pb-24 pt-6 sm:pl-20">
        <Outlet />
        <div className="mt-12 border-t border-zinc-200 pt-4 text-center text-[11px] text-zinc-400 dark:border-zinc-800">
          <div className="flex flex-wrap items-center justify-center gap-x-3 gap-y-1">
            <span>© {new Date().getFullYear()} LopesTech</span>
            <span aria-hidden>·</span>
            <NavLink to="/privacidade" className="hover:text-zinc-600 dark:hover:text-zinc-300">Privacidade</NavLink>
            <span aria-hidden>·</span>
            <NavLink to="/termos" className="hover:text-zinc-600 dark:hover:text-zinc-300">Termos</NavLink>
            <span aria-hidden>·</span>
            <NavLink to="/cookies" className="hover:text-zinc-600 dark:hover:text-zinc-300">Cookies</NavLink>
          </div>
        </div>
      </main>

      {/* Bottom nav (mobile) */}
      <nav
        className="fixed bottom-0 left-0 right-0 z-10 border-t border-zinc-200 bg-white/95 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/95 sm:hidden"
        aria-label="Bottom navigation"
      >
        <ul className="mx-auto flex max-w-5xl">
          {nav.filter((item) => !item.adminOnly || hasRole('Admin')).map((item) => (
            <li key={item.to} className="flex-1">
              <NavLink
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex flex-col items-center gap-0.5 py-2 text-[11px] ${
                    isActive ? 'text-brand-600' : 'text-zinc-500'
                  }`
                }
              >
                <item.icon size={18} strokeWidth={1.75} aria-hidden />
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      {/* Sidebar (desktop) — comprimida por defeito (w-14), expande no hover (w-56) */}
      <aside
        onMouseEnter={() => setHovered(true)}
        onMouseLeave={() => setHovered(false)}
        className={`fixed left-0 top-0 z-30 hidden h-screen border-r border-zinc-200 bg-white/95 backdrop-blur transition-[width] duration-200 ease-out dark:border-zinc-800 dark:bg-zinc-950/95 sm:flex sm:flex-col ${
          expanded ? 'w-56 shadow-xl shadow-black/5' : 'w-14'
        }`}
      >
        {/* Logo + pin */}
        <div className="flex h-14 items-center gap-2 border-b border-zinc-200 px-3 dark:border-zinc-800">
          <span className="grid h-8 w-8 flex-none place-items-center rounded-lg bg-brand-50 text-brand-600 dark:bg-zinc-800">●</span>
          <span
            className={`flex-1 truncate text-sm font-semibold transition-opacity ${
              expanded ? 'opacity-100' : 'pointer-events-none opacity-0'
            }`}
          >
            RepairDesk
          </span>
          {expanded && (
            <button
              type="button"
              onClick={() => setPinned((p) => !p)}
              className="rounded-md p-1 text-zinc-400 transition hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800"
              aria-label={pinned ? 'Desafixar menu' : 'Fixar menu aberto'}
              title={pinned ? 'Desafixar' : 'Fixar aberto'}
            >
              {pinned ? <Pin size={14} strokeWidth={2} /> : <PinOff size={14} strokeWidth={2} />}
            </button>
          )}
        </div>

        {/* Nav items */}
        <ul className="flex-1 space-y-1 p-2">
          {nav.filter((item) => !item.adminOnly || hasRole('Admin')).map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `group flex h-10 items-center gap-3 rounded-lg px-3 text-sm transition ${
                    isActive
                      ? 'bg-brand-50 text-brand-700 dark:bg-zinc-800 dark:text-brand-400'
                      : 'text-zinc-700 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'
                  }`
                }
                title={item.label}
              >
                <item.icon size={20} strokeWidth={1.75} aria-hidden />
                <span
                  className={`flex-1 truncate transition-opacity duration-150 ${
                    expanded ? 'opacity-100' : 'pointer-events-none opacity-0'
                  }`}
                >
                  {item.label}
                </span>
              </NavLink>
            </li>
          ))}
        </ul>

        {/* Footer: user + sair (visível só quando expandido) */}
        {user && (
          <div
            className={`border-t border-zinc-200 p-2 transition-opacity dark:border-zinc-800 ${
              expanded ? 'opacity-100' : 'pointer-events-none opacity-0'
            }`}
          >
            <div className="px-3 py-1.5 text-xs text-zinc-500 truncate">{user.displayName}</div>
            <button
              type="button"
              onClick={handleLogout}
              className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-zinc-700 transition hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              <LogOut size={16} strokeWidth={1.75} aria-hidden />
              <span>Sair</span>
            </button>
          </div>
        )}
      </aside>
    </div>
  );
}
