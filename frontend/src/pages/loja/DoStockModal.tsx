import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { PackagePlus, Search } from 'lucide-react';
import Modal from '../../components/Modal';
import { EmptyState } from '../../components/ui';
import { artigosApi } from '../../lib/artigos/api';
import { formatCents } from '../../lib/money';

/**
 * Sprint 667: pôr na loja um artigo que já está no stock (ex.: o ecrã A52 que sobrou de uma reparação).
 * Mostra os artigos com stock que ainda não estão na loja; ao escolher abre o produto já ligado ao artigo
 * (preço, custo e unidades vêm do artigo) — só falta título, fotos e publicar.
 */
export function DoStockModal({ jaNaLoja, onClose }: { jaNaLoja: Set<string>; onClose: () => void }) {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const artigos = useQuery({ queryKey: ['artigos', 'para-loja'], queryFn: () => artigosApi.list() });
  const lista = useMemo(() => (artigos.data ?? [])
    .filter((a) => a.stock > 0 && !jaNaLoja.has(a.id))
    .filter((a) => !q.trim() || a.nome.toLowerCase().includes(q.trim().toLowerCase()))
    .sort((a, b) => (a.categoria ?? '').localeCompare(b.categoria ?? '') || a.nome.localeCompare(b.nome)), [artigos.data, jaNaLoja, q]);

  return (
    <Modal open onClose={onClose} wide title="Pôr na loja um artigo do stock">
      <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
        Escolhe o artigo. Abre o produto da loja já ligado a ele — o preço e as unidades vêm do artigo e, quando o vendes ao balcão, o site fica sem stock sozinho.
      </p>
      <div className="relative mb-3">
        <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input type="search" autoFocus value={q} onChange={(e) => setQ(e.target.value)} placeholder="Procurar no stock (ex.: ecrã A52)…"
          className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950" />
      </div>
      {artigos.isLoading ? <p className="text-sm text-zinc-500">A carregar…</p> : lista.length === 0 ? (
        <EmptyState icon={PackagePlus} title="Nada para pôr na loja" description="Todos os artigos com stock já estão na loja (ou não há stock)." compact />
      ) : (
        <ul className="max-h-[60vh] divide-y divide-zinc-100 overflow-y-auto rounded-xl border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
          {lista.map((a) => (
            <li key={a.id}>
              <button type="button" onClick={() => navigate(`/loja/novo?artigo=${a.id}`)}
                className="flex min-h-14 w-full items-center gap-3 px-4 py-2 text-left hover:bg-brand-50 dark:hover:bg-brand-950/30">
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-semibold">{a.nome}</span>
                  <span className="block text-xs text-zinc-500">#{a.numero} · {a.categoria ?? 'Sem categoria'} · {a.stock} em stock</span>
                </span>
                <span className="text-sm font-semibold tabular-nums">{a.precoEfetivoCents != null ? formatCents(a.precoEfetivoCents) : 'sem preço'}</span>
                <PackagePlus size={18} className="text-brand-600" />
              </button>
            </li>
          ))}
        </ul>
      )}
    </Modal>
  );
}
