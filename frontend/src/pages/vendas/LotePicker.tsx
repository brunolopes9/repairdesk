import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import Modal from '../../components/Modal';
import { inputCls } from '../../components/ui/formClasses';
import { comprasApi } from '../../lib/compras/api';
import type { InventarioLinha } from '../../lib/compras/types';
import { formatEur } from '../../lib/compras/format';

/** Escolher um lote de stock (linha de compra com stock > 0) para uma linha de venda. */
export default function LotePicker({
  open,
  onClose,
  onPick,
  reservado,
}: {
  open: boolean;
  onClose: () => void;
  onPick: (lote: InventarioLinha) => void;
  /** Unidades já postas nesta venda por lote (ainda não gravadas) — para mostrar o disponível real. */
  reservado: Record<string, number>;
}) {
  const [q, setQ] = useState('');
  const inv = useQuery({ queryKey: ['compras', 'inventario'], queryFn: comprasApi.inventario, enabled: open });

  const linhas = useMemo(() => {
    const t = q.trim().toLowerCase();
    const all = inv.data ?? [];
    return t ? all.filter((l) => `${l.descricao} ${l.fornecedor} ${l.localizacao ?? ''}`.toLowerCase().includes(t)) : all;
  }, [inv.data, q]);

  return (
    <Modal open={open} title="Escolher do stock" onClose={onClose}>
      <div className="space-y-3">
        <label className="relative block">
          <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input value={q} onChange={(e) => setQ(e.target.value)} className={`${inputCls} pl-9`} placeholder="Procurar peça ou artigo…" autoFocus aria-label="Procurar no stock" />
        </label>
        {inv.isLoading && <p className="text-sm text-zinc-500">A carregar o stock…</p>}
        {!inv.isLoading && linhas.length === 0 && <p className="text-sm text-zinc-500">Nada em stock com esse nome.</p>}
        <ul className="max-h-[50vh] divide-y divide-zinc-100 overflow-y-auto dark:divide-zinc-800">
          {linhas.map((l) => {
            const disponivel = l.quantidadeEmStock - (reservado[l.linhaId] ?? 0);
            return (
              <li key={l.linhaId}>
                <button
                  type="button"
                  disabled={disponivel <= 0}
                  onClick={() => onPick(l)}
                  className="flex w-full items-start justify-between gap-3 px-1 py-2.5 text-left text-sm hover:bg-zinc-50 disabled:opacity-40 dark:hover:bg-zinc-800"
                >
                  <span>
                    <span className="block font-medium">{l.descricao}</span>
                    <span className="block text-xs text-zinc-500">{l.fornecedor} · {disponivel} disponível{l.localizacao ? ` · ${l.localizacao}` : ''}</span>
                  </span>
                  <span className="text-right tabular-nums">
                    <span className="block font-medium">{formatEur(l.precoFinalComIva)}</span>
                    <span className="block text-xs text-zinc-500">pago {formatEur(l.precoUnitarioPago)}</span>
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      </div>
    </Modal>
  );
}
