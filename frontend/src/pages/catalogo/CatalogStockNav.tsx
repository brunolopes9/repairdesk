import { NavLink } from 'react-router-dom';
import { BarChart3, Boxes, ClipboardCheck, PackageSearch, type LucideIcon } from 'lucide-react';

type ModuleLink = {
  to: string;
  label: string;
  eyebrow: string;
  description: string;
  icon: LucideIcon;
  end?: boolean;
};

const LINKS: ModuleLink[] = [
  {
    to: '/catalogo',
    label: 'Visao geral',
    eyebrow: 'Read model',
    description: 'Tudo o que existe: pecas, variantes, stock fisico, dropship e publicacao.',
    icon: BarChart3,
    end: true,
  },
  {
    to: '/stock',
    label: 'Stock pecas',
    eyebrow: 'Loja fisica',
    description: 'Pecas tecnicas, acessorios e consumiveis que estao na oficina.',
    icon: Boxes,
    end: true,
  },
  {
    to: '/produtos',
    label: 'Produtos retail',
    eyebrow: 'Loja online',
    description: 'Telemoveis e variantes vendidas online, stock proprio ou dropshipping.',
    icon: PackageSearch,
    end: true,
  },
  {
    to: '/inventario',
    label: 'Contagens fisicas',
    eyebrow: 'Reconcilia',
    description: 'Conta a prateleira e gera ajustes reais de stock quando fechares.',
    icon: ClipboardCheck,
    end: true,
  },
];

export function CatalogStockNav({ showGuide = false }: { showGuide?: boolean }) {
  return (
    <section className="space-y-3">
      <nav
        aria-label="Catalogo e stock"
        className="grid gap-2 rounded-xl border border-zinc-200 bg-white p-2 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900 sm:grid-cols-2 xl:grid-cols-4"
      >
        {LINKS.map((item) => {
          const Icon = item.icon;
          return (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => (
                `group flex min-h-[92px] gap-3 rounded-lg border p-3 transition ${
                  isActive
                    ? 'border-brand-200 bg-brand-50 text-brand-950 shadow-sm dark:border-brand-900/60 dark:bg-brand-950/35 dark:text-brand-50'
                    : 'border-transparent text-zinc-700 hover:border-zinc-200 hover:bg-zinc-50 dark:text-zinc-300 dark:hover:border-zinc-800 dark:hover:bg-zinc-950/60'
                }`
              )}
            >
              {({ isActive }) => (
                <>
                  <span
                    className={`mt-0.5 grid h-9 w-9 flex-none place-items-center rounded-lg ${
                      isActive
                        ? 'bg-brand-600 text-white'
                        : 'bg-zinc-100 text-zinc-500 group-hover:bg-white dark:bg-zinc-800 dark:text-zinc-300 dark:group-hover:bg-zinc-900'
                    }`}
                  >
                    <Icon size={18} />
                  </span>
                  <span className="min-w-0">
                    <span className="block text-[10px] font-semibold uppercase tracking-[0.16em] text-zinc-400">
                      {item.eyebrow}
                    </span>
                    <span className="mt-0.5 block text-sm font-semibold">{item.label}</span>
                    <span className="mt-1 block text-xs leading-5 text-zinc-500 dark:text-zinc-400">
                      {item.description}
                    </span>
                  </span>
                </>
              )}
            </NavLink>
          );
        })}
      </nav>

      {showGuide ? (
        <div className="grid gap-3 lg:grid-cols-3">
          <GuideCard
            title="Loja fisica"
            text="Stock pecas representa o que podes tocar: pecas tecnicas, capas, peliculas, ecras e consumiveis."
          />
          <GuideCard
            title="Loja online"
            text="Produtos retail gere conteudo, SEO, preco e visibilidade. Cada variante pode ser stock proprio ou dropship."
          />
          <GuideCard
            title="Contagem"
            text="Contagens fisicas reconciliam apenas stock real. Dropship e stock virtual nao entram na prateleira."
          />
        </div>
      ) : null}
    </section>
  );
}

export default CatalogStockNav;

function GuideCard({ title, text }: { title: string; text: string }) {
  return (
    <div className="rounded-lg border border-zinc-200 bg-zinc-50/70 p-3 dark:border-zinc-800 dark:bg-zinc-950/60">
      <p className="text-sm font-semibold text-zinc-950 dark:text-zinc-50">{title}</p>
      <p className="mt-1 text-xs leading-5 text-zinc-500 dark:text-zinc-400">{text}</p>
    </div>
  );
}
