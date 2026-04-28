import { NavLink, Outlet } from 'react-router-dom';

const nav = [
  { to: '/', label: 'Dashboard', icon: '📊' },
  { to: '/clientes', label: 'Clientes', icon: '👥' },
  { to: '/reparacoes', label: 'Reparações', icon: '🔧' },
  { to: '/pecas', label: 'Peças', icon: '📦' },
  { to: '/faturacao', label: 'Faturação', icon: '🧾' },
];

export default function Layout() {
  return (
    <div className="min-h-screen bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
      <header className="sticky top-0 z-10 border-b border-zinc-200 bg-white/80 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/80">
        <div className="mx-auto flex h-14 max-w-5xl items-center justify-between px-4">
          <div className="flex items-center gap-2 font-semibold">
            <span className="text-brand-500">●</span> RepairDesk
          </div>
          <span className="text-xs text-zinc-500">LopesTech</span>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 pb-24 pt-6">
        <Outlet />
      </main>

      <nav
        className="fixed bottom-0 left-0 right-0 z-10 border-t border-zinc-200 bg-white/95 backdrop-blur dark:border-zinc-800 dark:bg-zinc-950/95 sm:hidden"
        aria-label="Bottom navigation"
      >
        <ul className="mx-auto flex max-w-5xl">
          {nav.map((item) => (
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
                <span aria-hidden className="text-base">
                  {item.icon}
                </span>
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>

      <aside className="fixed left-0 top-14 hidden h-[calc(100vh-3.5rem)] w-56 border-r border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-950 sm:block">
        <ul className="space-y-1">
          {nav.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex items-center gap-2 rounded-lg px-3 py-2 text-sm ${
                    isActive
                      ? 'bg-brand-50 text-brand-700 dark:bg-zinc-800 dark:text-brand-500'
                      : 'text-zinc-700 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'
                  }`
                }
              >
                <span aria-hidden>{item.icon}</span>
                {item.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </aside>
    </div>
  );
}
