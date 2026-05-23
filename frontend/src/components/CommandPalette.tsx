import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowRight,
  Briefcase,
  ChevronsRight,
  LayoutDashboard,
  PackageSearch,
  Plus,
  Receipt,
  ScrollText,
  Search,
  Settings,
  ShieldCheck,
  SlidersHorizontal,
  Tags,
  Users,
  User,
  Wrench,
} from 'lucide-react';
import { clientesApi } from '../lib/clientes/api';
import { reparacoesApi } from '../lib/reparacoes/api';

interface Command {
  id: string;
  label: string;
  hint?: string;
  icon: typeof LayoutDashboard;
  keywords: string[];
  /** Returns string to navigate, or function to call. */
  action: () => void;
}

/**
 * Command palette estilo Linear / Notion / Stripe.
 * Atalho global: Cmd+K (Mac) ou Ctrl+K (Win/Linux).
 * Esc fecha. ↑↓ navega. Enter executa.
 *
 * Lista de comandos é estática — navegação + acções rápidas.
 * Não inclui search server-side de clientes/reparações para evitar
 * round-trip + manter UX rápida. Pode ser adicionado depois.
 */
export default function CommandPalette() {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [selected, setSelected] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  // Debounce 200ms para evitar request por cada tecla.
  useEffect(() => {
    const handler = setTimeout(() => setDebouncedQuery(query.trim()), 200);
    return () => clearTimeout(handler);
  }, [query]);

  const shouldSearch = open && debouncedQuery.length >= 2;

  // Search clientes server-side. Top 5 matches.
  const clientesSearch = useQuery({
    queryKey: ['palette-clientes', debouncedQuery],
    queryFn: () => clientesApi.list(debouncedQuery, 1, 5),
    enabled: shouldSearch,
    staleTime: 60_000,
  });

  // Search reparações server-side. Top 5 matches.
  const reparacoesSearch = useQuery({
    queryKey: ['palette-reparacoes', debouncedQuery],
    queryFn: () => reparacoesApi.list({ q: debouncedQuery, pageSize: 5 }),
    enabled: shouldSearch,
    staleTime: 60_000,
  });

  const commands: Command[] = useMemo(
    () => [
      // Navegação
      { id: 'nav-dash', label: 'Dashboard', icon: LayoutDashboard, keywords: ['dashboard', 'home', 'inicio'], action: () => navigate('/') },
      { id: 'nav-clientes', label: 'Clientes', icon: Users, keywords: ['clientes', 'contactos'], action: () => navigate('/clientes') },
      { id: 'nav-reparacoes', label: 'Reparações', icon: Wrench, keywords: ['reparacoes', 'reparações', 'tickets'], action: () => navigate('/reparacoes') },
      { id: 'nav-trabalhos', label: 'Trabalhos', icon: Briefcase, keywords: ['trabalhos', 'jobs', 'projectos'], action: () => navigate('/trabalhos') },
      { id: 'nav-despesas', label: 'Compras & Despesas', icon: Receipt, keywords: ['despesas', 'custos', 'compras', 'fornecedor', 'material'], action: () => navigate('/despesas') },
      { id: 'nav-stock', label: 'Stock', icon: PackageSearch, keywords: ['stock', 'inventario', 'peças'], action: () => navigate('/stock') },
      { id: 'nav-precos', label: 'Tabela de preços', icon: Tags, keywords: ['precos', 'tabela', 'pricing'], action: () => navigate('/precos') },
      { id: 'nav-auditoria', label: 'Auditoria', icon: ScrollText, keywords: ['auditoria', 'audit', 'log', 'rgpd'], action: () => navigate('/auditoria') },
      { id: 'nav-definicoes', label: 'Definições', icon: Settings, keywords: ['definicoes', 'settings', 'config'], action: () => navigate('/definicoes') },
      { id: 'nav-preferencias', label: 'Preferências da loja', icon: SlidersHorizontal, keywords: ['preferencias', 'customizacao', 'tenant'], action: () => navigate('/definicoes/preferencias') },

      // Acções rápidas
      { id: 'new-reparacao', label: 'Nova reparação', hint: 'Criar nova ficha de reparação', icon: Plus, keywords: ['nova', 'criar', 'reparacao', 'add'], action: () => navigate('/reparacoes?new=1') },
      { id: 'new-cliente', label: 'Novo cliente', hint: 'Adicionar cliente', icon: Plus, keywords: ['novo', 'criar', 'cliente', 'add'], action: () => navigate('/clientes?new=1') },
      { id: 'new-trabalho', label: 'Novo trabalho', hint: 'Criar trabalho não-reparação', icon: Plus, keywords: ['novo', 'criar', 'trabalho', 'job'], action: () => navigate('/trabalhos?new=1') },

      // Atalhos específicos
      { id: 'backup-now', label: 'Correr backup agora', hint: 'Manual SQL backup', icon: ShieldCheck, keywords: ['backup', 'guardar', 'safety'], action: () => navigate('/definicoes?tab=backups') },
    ],
    [navigate],
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const matchedCommands = !q
      ? commands
      : commands.filter((c) =>
          c.label.toLowerCase().includes(q)
          || c.keywords.some((k) => k.toLowerCase().includes(q))
          || (c.hint && c.hint.toLowerCase().includes(q)),
        );

    // Quando há query >=2 chars, adiciona resultados server-side
    if (!shouldSearch) return matchedCommands;

    const clienteCmds: Command[] = (clientesSearch.data?.items ?? []).slice(0, 5).map((c) => ({
      id: `cliente-${c.id}`,
      label: c.nome,
      hint: c.telefone ?? c.email ?? (c.nif ? `NIF ${c.nif}` : 'Cliente'),
      icon: User,
      keywords: [c.nome, c.telefone ?? '', c.email ?? '', c.nif ?? ''],
      action: () => navigate(`/clientes/${c.id}`),
    }));

    const reparacaoCmds: Command[] = (reparacoesSearch.data?.items ?? []).slice(0, 5).map((r) => ({
      id: `reparacao-${r.id}`,
      label: `#${r.numero} · ${r.equipamento}`,
      hint: r.cliente?.nome ?? 'sem cliente',
      icon: Wrench,
      keywords: [String(r.numero), r.equipamento, r.cliente?.nome ?? '', r.imei ?? ''],
      action: () => navigate(`/reparacoes/${r.id}`),
    }));

    return [...matchedCommands, ...clienteCmds, ...reparacaoCmds];
  }, [commands, query, shouldSearch, clientesSearch.data, reparacoesSearch.data, navigate]);

  // Reset selection when filter changes
  useEffect(() => {
    setSelected(0);
  }, [query]);

  // Global keyboard listener — Cmd+K / Ctrl+K
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        setOpen((v) => !v);
      } else if (e.key === 'Escape' && open) {
        setOpen(false);
      }
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [open]);

  // Focus input when opens
  useEffect(() => {
    if (open) {
      setQuery('');
      setSelected(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [open]);

  const execute = useCallback(
    (cmd: Command) => {
      cmd.action();
      setOpen(false);
    },
    [],
  );

  function onArrowKey(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setSelected((s) => Math.min(filtered.length - 1, s + 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setSelected((s) => Math.max(0, s - 1));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const cmd = filtered[selected];
      if (cmd) execute(cmd);
    }
  }

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center bg-black/40 p-4 pt-[15vh] backdrop-blur-sm"
      onClick={() => setOpen(false)}
      role="dialog"
      aria-modal="true"
      aria-label="Command palette"
    >
      <div
        className="w-full max-w-xl overflow-hidden rounded-2xl border border-zinc-200 bg-white shadow-2xl dark:border-zinc-700 dark:bg-zinc-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-2 border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
          <Search size={16} strokeWidth={2} className="flex-none text-zinc-400" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={onArrowKey}
            placeholder="Procura ou escolhe uma acção…"
            className="flex-1 bg-transparent text-sm outline-none placeholder:text-zinc-400"
          />
          <kbd className="rounded border border-zinc-200 px-1.5 py-0.5 text-[10px] text-zinc-500 dark:border-zinc-700">esc</kbd>
        </div>
        <ul className="max-h-[50vh] overflow-y-auto py-2">
          {shouldSearch && (clientesSearch.isFetching || reparacoesSearch.isFetching) && (
            <li className="px-4 py-1 text-[11px] italic text-zinc-400">A procurar clientes e reparações…</li>
          )}
          {filtered.length === 0 && !clientesSearch.isFetching && !reparacoesSearch.isFetching && (
            <li className="px-4 py-6 text-center text-sm text-zinc-500">Sem resultados para "{query}"</li>
          )}
          {filtered.map((cmd, idx) => (
            <li key={cmd.id}>
              <button
                type="button"
                onMouseEnter={() => setSelected(idx)}
                onClick={() => execute(cmd)}
                className={`flex w-full items-center gap-3 px-4 py-2 text-left transition ${
                  idx === selected
                    ? 'bg-brand-50 text-brand-700 dark:bg-zinc-800 dark:text-brand-300'
                    : 'text-zinc-700 dark:text-zinc-300'
                }`}
              >
                <cmd.icon size={15} strokeWidth={1.75} className="flex-none" />
                <span className="flex-1 text-sm">{cmd.label}</span>
                {cmd.hint && <span className="text-[11px] text-zinc-500">{cmd.hint}</span>}
                {idx === selected && <ArrowRight size={13} strokeWidth={2} className="flex-none text-zinc-400" />}
              </button>
            </li>
          ))}
        </ul>
        <div className="flex items-center justify-between border-t border-zinc-100 bg-zinc-50 px-4 py-2 text-[11px] text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950">
          <div className="flex items-center gap-3">
            <span className="inline-flex items-center gap-1">
              <kbd className="rounded border border-zinc-200 px-1 dark:border-zinc-700">↑↓</kbd> navegar
            </span>
            <span className="inline-flex items-center gap-1">
              <kbd className="rounded border border-zinc-200 px-1 dark:border-zinc-700">enter</kbd> executar
            </span>
          </div>
          <span className="inline-flex items-center gap-1">
            <ChevronsRight size={11} strokeWidth={2} /> {filtered.length} {filtered.length === 1 ? 'acção' : 'acções'}
          </span>
        </div>
      </div>
    </div>
  );
}
