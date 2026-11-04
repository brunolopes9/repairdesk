import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';

/**
 * Botão "Voltar" para topo de páginas de detalhe (Reparação, Cliente, Trabalho…).
 * Por defeito navega para `to`. Se não for dado, faz history back (navigate(-1)).
 * Preferir `to` para destino previsível (ex.: /reparacoes/:id → /reparacoes).
 * Sprint 592: se se chegou aqui de dentro do Mender, volta atrás no histórico — à mesma página da lista, com os
 * mesmos filtros e no mesmo sítio do scroll. `to` fica para quando se abre o link direto (sem histórico).
 */
export function BackButton({ to, label = 'Voltar', className = '' }: { to?: string; label?: string; className?: string }) {
  const navigate = useNavigate();
  const base = 'inline-flex items-center gap-1.5 rounded-md px-2 py-1.5 text-sm text-zinc-600 transition hover:bg-zinc-100 hover:text-zinc-900 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:text-zinc-300 dark:hover:bg-zinc-800 dark:hover:text-zinc-100';
  const cls = `${base} ${className}`.trim();

  const temHistorico = ((window.history.state as { idx?: number } | null)?.idx ?? 0) > 0;
  if (to) {
    return (
      <Link to={to} className={cls} aria-label={label}
        onClick={(e) => { if (temHistorico && !e.metaKey && !e.ctrlKey && !e.shiftKey) { e.preventDefault(); navigate(-1); } }}>
        <ArrowLeft size={15} /> {label}
      </Link>
    );
  }
  return (
    <button type="button" onClick={() => navigate(-1)} className={cls} aria-label={label}>
      <ArrowLeft size={15} /> {label}
    </button>
  );
}
