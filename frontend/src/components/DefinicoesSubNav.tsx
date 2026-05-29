import { NavLink } from 'react-router-dom';
import {
  Building2, SlidersHorizontal, Webhook, Truck, Boxes, Workflow, Sparkles, Users, UserCircle, Wrench,
} from 'lucide-react';

/**
 * Sprint 416 (Doc 90 Tier 1 #1): segunda sidebar para /definicoes/*, ao estilo RoApp.
 * Aparece à esquerda do conteúdo, à direita da sidebar principal. Mantém o utilizador
 * orientado sem ter de voltar a `/definicoes` para mudar de sub-secção.
 */
const ITEMS = [
  { to: '/definicoes', label: 'Geral', icon: Building2, end: true },
  { to: '/definicoes/perfil', label: 'O meu perfil', icon: UserCircle },
  { to: '/definicoes/preferencias', label: 'Preferências', icon: SlidersHorizontal },
  { to: '/definicoes/webhooks', label: 'Webhooks', icon: Webhook },
  { to: '/definicoes/fornecedores', label: 'Fornecedores', icon: Truck },
  { to: '/definicoes/kits', label: 'Kits de peças', icon: Boxes },
  { to: '/definicoes/servicos', label: 'Catálogo de serviços', icon: Wrench },
  { to: '/definicoes/automacoes', label: 'Automações', icon: Workflow },
  { to: '/definicoes/llm-usage', label: 'Uso de IA', icon: Sparkles },
  { to: '/definicoes/utilizadores', label: 'Utilizadores', icon: Users },
];

export default function DefinicoesSubNav() {
  return (
    <aside className="w-56 flex-none">
      <nav className="sticky top-4 space-y-0.5 rounded-xl border border-zinc-200 bg-white p-1.5 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="px-2.5 pb-1.5 pt-1 text-[10px] font-semibold uppercase tracking-wider text-zinc-400">Definições</div>
        {ITEMS.map(({ to, label, icon: Icon, end }) => (
          <NavLink
            key={to}
            to={to}
            end={end}
            className={({ isActive }) =>
              `flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm font-medium transition ${
                isActive
                  ? 'bg-brand-50 text-brand-700 dark:bg-brand-950/40 dark:text-brand-300'
                  : 'text-zinc-600 hover:bg-zinc-50 hover:text-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800 dark:hover:text-zinc-100'
              }`
            }
          >
            <Icon size={15} className="flex-none" />
            <span className="truncate">{label}</span>
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
