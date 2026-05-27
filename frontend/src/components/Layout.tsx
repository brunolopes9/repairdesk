import { useEffect, useState, type ComponentType } from 'react';
import { useQuery } from '@tanstack/react-query';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  Wrench,
  Briefcase,
  Receipt,
  Banknote,
  ShoppingCart,
  PackageSearch,
  Tags,
  FileText,
  BarChart3,
  ChevronDown,
  ClipboardList,
  CalendarClock,
  Bell,
  Plus,
  Settings,
  Webhook,
  Workflow,
  Sparkles,
  Building2,
  Smartphone,
  LogOut,
  Pin,
  PinOff,
  Sun,
  Moon,
  Monitor,
  Search,
  SlidersHorizontal,
  UserCog,
  Lock,
} from 'lucide-react';
import { useAuth } from '../lib/auth/AuthContext';
import PwaStatus from './PwaStatus';
import HealthIndicator from './HealthIndicator';
import ActiveTimerBanner from './ActiveTimerBanner';
import { AssistantWidget } from './AssistantWidget';
import { tenantSettingsApi } from '../lib/tenantSettings/api';
import { repairRequestsApi } from '../lib/repairRequests/api';
import { applyTheme, getStoredTheme, setStoredTheme, watchSystemTheme, type Theme } from '../lib/theme';

type IconCmp = ComponentType<{ className?: string; size?: number; strokeWidth?: number }>;
type NavItem = {
  to?: string;
  label: string;
  icon: IconCmp;
  adminOnly?: boolean;
  /** Sprint 368: roles que veem este item (além de Admin, que vê tudo). Vazio = todos. */
  roles?: string[];
  /** Sprint 356: mostra bolha de contagem (ex: pedidos online pendentes). */
  badgeKey?: 'repair-requests';
  children?: Array<{ to: string; label: string; icon: IconCmp; adminOnly?: boolean; roles?: string[] }>;
};

const nav: NavItem[] = [
  // Modelo de 2 roles (decisão Bruno 2026-05-27): Admin (dono) vê tudo; Empregado (Tech) faz
  // todo o operacional. Numa loja de reparações ninguém é "só caixa" — não há split Cashier.
  // Operacional = sem tag (visível a Admin + Tech). Sensível = adminOnly (só Admin).
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/clientes', label: 'Clientes', icon: Users },
  { to: '/reparacoes', label: 'Reparações', icon: Wrench },
  { to: '/pedidos-online', label: 'Pedidos online', icon: Wrench, badgeKey: 'repair-requests' },
  { to: '/agendamentos', label: 'Agendamentos', icon: CalendarClock },
  { to: '/trabalhos', label: 'Trabalhos', icon: Briefcase },
  // Sprint 383 (Doc 86): "Balcão" unifica POS + Caixa numa página com tabs (/balcao). A regra
  // "não vendes com caixa fechada" vive na própria POS. Filhos = deep-link para os tabs.
  {
    label: 'Balcão',
    icon: ShoppingCart,
    children: [
      { to: '/balcao', label: 'Venda rápida', icon: ShoppingCart },
      { to: '/balcao?tab=caixa', label: 'Caixa de hoje', icon: Banknote },
      { to: '/balcao?tab=fecho', label: 'Fecho & Z-Reports', icon: Lock },
    ],
  },
  {
    label: 'Compras e Operação',
    icon: Receipt,
    children: [
      { to: '/compras-operacao', label: 'Visão geral', icon: LayoutDashboard },
      { to: '/compras', label: 'Inbox de faturas', icon: Receipt },
      { to: '/despesas', label: 'Despesas & custos', icon: Banknote },
    ],
  },
  { to: '/stock', label: 'Stock', icon: PackageSearch },
  { to: '/produtos', label: 'Produtos', icon: Smartphone, adminOnly: true },
  { to: '/precos', label: 'Preços', icon: Tags },
  {
    label: 'Relatorios',
    icon: FileText,
    children: [
      { to: '/relatorios/iva', label: 'IVA', icon: FileText },
      { to: '/relatorios/negocio', label: 'Negocio', icon: BarChart3 },
      { to: '/relatorios/produtividade', label: 'Produtividade', icon: BarChart3, adminOnly: true },
    ],
  },
  { to: '/auditoria', label: 'Auditoria', icon: ClipboardList, adminOnly: true },
  // Sprint 240: agrupar tudo de configuração num único dropdown "Definições" — antes eram
  // 6 items soltos na sidebar (Preferências, Webhooks, Fornecedores, Automações, Uso de IA,
  // Definições) que poluíam o menu principal.
  {
    label: 'Definições',
    icon: Settings,
    adminOnly: true,
    children: [
      { to: '/definicoes', label: 'Empresa & Faturação', icon: Settings },
      { to: '/definicoes/preferencias', label: 'Preferências', icon: SlidersHorizontal },
      { to: '/definicoes/fornecedores', label: 'Fornecedores', icon: Building2 },
      { to: '/definicoes/kits', label: 'Kits de peças', icon: PackageSearch, adminOnly: true },
      { to: '/definicoes/webhooks', label: 'Webhooks', icon: Webhook },
      { to: '/definicoes/automacoes', label: 'Automações', icon: Workflow },
      { to: '/definicoes/llm-usage', label: 'Uso de IA', icon: Sparkles },
      { to: '/definicoes/utilizadores', label: 'Utilizadores', icon: UserCog },
    ],
  },
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
  // Sprint 368: gating por role. Admin vê tudo; adminOnly só Admin; senão, se houver `roles`
  // tem de ter pelo menos uma; sem `roles` é visível a todos (ex: Dashboard, Clientes).
  const canSee = (item: { adminOnly?: boolean; roles?: string[] }) => {
    if (hasRole('Admin')) return true;
    if (item.adminOnly) return false;
    if (item.roles && item.roles.length > 0) return item.roles.some((r) => hasRole(r));
    return true;
  };
  const visibleNav = nav.filter(canSee);
  const mobileNav = visibleNav.flatMap((item) =>
    (item.children?.filter(canSee)) ?? (item.to ? [item as { to: string; label: string; icon: IconCmp }] : []));

  const onboarding = useQuery({
    queryKey: ['onboarding-status'],
    queryFn: () => tenantSettingsApi.onboardingStatus(),
    staleTime: 30_000,
    enabled: Boolean(user) && hasRole('Admin'),
  });

  // Sprint 356: contagem de pedidos online pendentes para badge na sidebar.
  const pedidosPendentes = useQuery({
    queryKey: ['repair-requests-count'],
    queryFn: () => repairRequestsApi.countPendentes(),
    enabled: Boolean(user),
    refetchInterval: 60_000,
  });
  const badgeCount = (key?: NavItem['badgeKey']) =>
    key === 'repair-requests' ? (pedidosPendentes.data ?? 0) : 0;

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
        <div className="flex h-14 w-full items-center justify-between gap-3 px-4 sm:pl-20 sm:pr-6">
          <div className="flex items-center gap-2 font-semibold">
            <span className="grid h-7 w-7 place-items-center rounded-lg bg-brand-600 text-[13px] font-bold text-white">M</span>
            <span className="hidden text-sm sm:inline">Mender</span>
          </div>
          <div className="flex items-center gap-2">
            {/* Sprint 379: ação primária no topo (como o mockup) */}
            <button
              type="button"
              onClick={() => navigate('/reparacoes?new=1')}
              className="flex h-9 items-center gap-1.5 rounded-lg bg-brand-600 px-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-700"
            >
              <Plus size={16} strokeWidth={2.5} />
              <span className="hidden sm:inline">Nova reparação</span>
            </button>
            {/* Pesquisa global — ocupa espaço como uma barra, estilo SaaS */}
            <button
              type="button"
              onClick={() => window.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', ctrlKey: true }))}
              title="Procurar / acções (Ctrl+K)"
              aria-label="Procurar"
              className="hidden h-9 w-56 items-center gap-2 rounded-lg border border-zinc-200 bg-zinc-50 px-3 text-sm text-zinc-400 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-800 dark:bg-zinc-900 dark:hover:bg-zinc-800 md:flex"
            >
              <Search size={15} strokeWidth={2} />
              <span className="flex-1 text-left">Procurar…</span>
              <kbd className="rounded border border-zinc-200 bg-white px-1 text-[10px] text-zinc-400 dark:border-zinc-700 dark:bg-zinc-950">Ctrl K</kbd>
            </button>

            <HealthIndicator />

            <button
              type="button"
              onClick={cycleTheme}
              title={`Tema: ${themeLabel} — clica para alternar`}
              className="grid h-9 w-9 place-items-center rounded-lg text-zinc-500 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:text-zinc-400 dark:hover:bg-zinc-800"
            >
              <ThemeIcon size={17} strokeWidth={2} />
            </button>

            <button
              type="button"
              onClick={() => navigate('/pedidos-online')}
              title="Pedidos online"
              aria-label="Notificações"
              className="relative grid h-9 w-9 place-items-center rounded-lg text-zinc-500 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:text-zinc-400 dark:hover:bg-zinc-800"
            >
              <Bell size={17} strokeWidth={2} />
              {(pedidosPendentes.data ?? 0) > 0 && (
                <span className="absolute -right-0.5 -top-0.5 grid h-4 min-w-4 place-items-center rounded-full bg-red-500 px-1 text-[10px] font-semibold text-white">
                  {pedidosPendentes.data}
                </span>
              )}
            </button>

            {/* Chip de perfil */}
            <div className="ml-1 flex items-center gap-2 rounded-lg border border-zinc-200 py-1 pl-1 pr-2 dark:border-zinc-800">
              <span className="grid h-7 w-7 place-items-center rounded-md bg-brand-100 text-xs font-semibold text-brand-700 dark:bg-brand-900/40 dark:text-brand-300">
                {(user?.displayName ?? '?').trim().charAt(0).toUpperCase()}
              </span>
              <span className="hidden text-xs font-medium text-zinc-700 dark:text-zinc-200 sm:inline">{user?.displayName}</span>
              <button
                type="button"
                onClick={handleLogout}
                title="Sair"
                aria-label="Sair"
                className="grid h-6 w-6 place-items-center rounded text-zinc-400 transition hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800 dark:hover:text-zinc-200"
              >
                <LogOut size={14} strokeWidth={2} />
              </button>
            </div>
          </div>
        </div>
        {/* Sprint 351: banner global quando há timer activo. */}
        <ActiveTimerBanner />
      </header>

      <main className="mx-auto max-w-[1600px] px-4 pb-24 pt-6 sm:pl-20 sm:pr-6">
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

      {/* Sprint 194: PWA Fase 1 — indicador offline + botão instalar. */}
      <PwaStatus />

      {/* Sprint 369: assistente interno read-only (flutuante). */}
      <AssistantWidget />

      {/* Bottom nav (mobile) */}
      <nav
        className="fixed bottom-0 left-0 right-0 z-10 border-t border-zinc-200 bg-white/95 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/95 sm:hidden"
        aria-label="Bottom navigation"
      >
        <ul className="mx-auto flex max-w-5xl">
          {mobileNav.map((item) => (
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
        className={`fixed left-0 top-0 z-30 hidden h-screen border-r border-slate-800 bg-slate-900 text-slate-300 transition-[width] duration-200 ease-out sm:flex sm:flex-col ${
          expanded ? 'w-56 shadow-xl shadow-black/20' : 'w-14'
        }`}
      >
        {/* Logo + pin */}
        <div className="flex h-14 items-center gap-2 border-b border-slate-800 px-3">
          <span className="grid h-8 w-8 flex-none place-items-center rounded-lg bg-brand-600 text-sm font-bold text-white">M</span>
          <span
            className={`flex-1 truncate text-sm font-semibold text-white transition-opacity ${
              expanded ? 'opacity-100' : 'pointer-events-none opacity-0'
            }`}
          >
            Mender
          </span>
          {expanded && (
            <button
              type="button"
              onClick={() => setPinned((p) => !p)}
              className="rounded-md p-1 text-slate-400 transition hover:bg-slate-800 hover:text-white"
              aria-label={pinned ? 'Desafixar menu' : 'Fixar menu aberto'}
              title={pinned ? 'Desafixar' : 'Fixar aberto'}
            >
              {pinned ? <Pin size={14} strokeWidth={2} /> : <PinOff size={14} strokeWidth={2} />}
            </button>
          )}
        </div>

        {/* Nav items */}
        <ul className="flex-1 space-y-1 p-2">
          {visibleNav.map((item) => (
            <li key={item.to ?? item.label}>
              {item.children ? (
                <div>
                  <div
                    className={`group flex h-10 items-center gap-3 rounded-lg px-3 text-sm transition ${
                      // Sprint 240: highlight quando QUALQUER child está activo (suporta dropdowns
                      // genéricos — antes era hardcoded só /relatorios).
                      item.children.some((c) => location.pathname === c.to || location.pathname.startsWith(c.to + '/'))
                        ? 'bg-brand-600 text-white shadow-sm'
                        : 'text-slate-300 hover:bg-slate-800 hover:text-white'
                    }`}
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
                    <ChevronDown
                      size={14}
                      strokeWidth={1.75}
                      className={`transition-opacity ${expanded ? 'opacity-70' : 'opacity-0'}`}
                      aria-hidden
                    />
                  </div>
                  {expanded && (
                    <ul className="mt-1 space-y-1 pl-8">
                      {item.children.filter(canSee).map((child) => (
                        <li key={child.to}>
                          <NavLink
                            to={child.to}
                            className={({ isActive }) =>
                              `flex h-9 items-center gap-2 rounded-lg px-3 text-sm transition ${
                                isActive
                                  ? 'bg-brand-600 text-white shadow-sm'
                                  : 'text-slate-400 hover:bg-slate-800 hover:text-white'
                              }`
                            }
                            title={child.label}
                          >
                            <child.icon size={16} strokeWidth={1.75} aria-hidden />
                            <span className="truncate">{child.label}</span>
                          </NavLink>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              ) : item.to ? (
                <NavLink
                  to={item.to}
                  end={item.to === '/'}
                  className={({ isActive }) =>
                    `group flex h-10 items-center gap-3 rounded-lg px-3 text-sm transition ${
                      isActive
                        ? 'bg-brand-600 text-white shadow-sm'
                        : 'text-slate-300 hover:bg-slate-800 hover:text-white'
                    }`
                  }
                  title={item.label}
                >
                  <span className="relative">
                    <item.icon size={20} strokeWidth={1.75} aria-hidden />
                    {badgeCount(item.badgeKey) > 0 && !expanded && (
                      <span className="absolute -right-1.5 -top-1.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-rose-600 px-1 text-[9px] font-semibold text-white">
                        {badgeCount(item.badgeKey)}
                      </span>
                    )}
                  </span>
                  <span
                    className={`flex-1 truncate transition-opacity duration-150 ${
                      expanded ? 'opacity-100' : 'pointer-events-none opacity-0'
                    }`}
                  >
                    {item.label}
                  </span>
                  {badgeCount(item.badgeKey) > 0 && expanded && (
                    <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-600 px-1.5 text-[10px] font-semibold text-white">
                      {badgeCount(item.badgeKey)}
                    </span>
                  )}
                </NavLink>
              ) : null}
            </li>
          ))}
        </ul>

        {/* Footer: user + sair (visível só quando expandido) */}
        {user && (
          <div
            className={`border-t border-slate-800 p-2 transition-opacity ${
              expanded ? 'opacity-100' : 'pointer-events-none opacity-0'
            }`}
          >
            <div className="truncate px-3 py-1.5 text-xs text-slate-400">{user.displayName}</div>
            <button
              type="button"
              onClick={handleLogout}
              className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-slate-300 transition hover:bg-slate-800 hover:text-white"
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
