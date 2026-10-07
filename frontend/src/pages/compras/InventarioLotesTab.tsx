import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Boxes, Search } from 'lucide-react';
import { EmptyState, SectionCard, SkeletonRow } from '../../components/ui';
import { comprasApi } from '../../lib/compras/api';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import { inputCls } from '../../components/ui/formClasses';

/** Lotes com stock > 0 (SPEC compras §4.5), com preço de venda sugerido e IVA a entregar ao Estado. */
export default function InventarioLotesTab() {
  const [q, setQ] = useState('');
  const inv = useQuery({ queryKey: ['compras', 'inventario'], queryFn: comprasApi.inventario });

  const linhas = useMemo(() => {
    const t = q.trim().toLowerCase();
    const all = inv.data ?? [];
    return t ? all.filter((l) => `${l.numero} ${l.descricao} ${l.fornecedor} ${l.referencia} ${l.localizacao ?? ''}`.toLowerCase().includes(t)) : all;
  }, [inv.data, q]);

  const totais = useMemo(() => linhas.reduce(
    (s, l) => ({ un: s.un + l.quantidadeEmStock, pago: s.pago + l.totalPago, lucro: s.lucro + l.lucro }),
    { un: 0, pago: 0, lucro: 0 },
  ), [linhas]);

  return (
    <SectionCard
      bodyClassName="p-0"
      title={
        <label className="relative min-w-48 flex-1">
          <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input value={q} onChange={(e) => setQ(e.target.value)} className={`${inputCls} pl-9`} placeholder="Procurar nº do lote, artigo, fornecedor, localização…" aria-label="Procurar" />
        </label>
      }
      action={<span className="whitespace-nowrap text-sm text-zinc-500 tabular-nums">{totais.un} un. · {formatEur(totais.pago)} pago</span>}
    >
      {!inv.isLoading && linhas.length === 0 ? (
        <div className="p-4">
          <EmptyState icon={Boxes} title={q ? 'Nada encontrado' : 'Sem stock'} description={q ? undefined : 'O stock nasce das linhas das compras.'} />
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[52rem] text-sm">
            <thead className="border-b border-zinc-100 text-left text-xs text-zinc-500 dark:border-zinc-800">
              <tr>
                <th className="px-4 py-2 font-medium">Lote</th>
                <th className="px-4 py-2 font-medium">Artigo</th>
                <th className="px-4 py-2 font-medium">Compra</th>
                <th className="px-4 py-2 text-right font-medium">Stock</th>
                <th className="px-4 py-2 text-right font-medium">Pago un.</th>
                <th className="px-4 py-2 text-right font-medium">Venda c/ IVA</th>
                <th className="px-4 py-2 text-right font-medium">Lucro (lote)</th>
                <th className="px-4 py-2 text-right font-medium">IVA ao Estado (lote)</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {inv.isLoading && Array.from({ length: 5 }).map((_, i) => <tr key={i}><td colSpan={8}><SkeletonRow columns={8} /></td></tr>)}
              {linhas.map((l) => (
                <tr key={l.linhaId}>
                  <td className="px-4 py-3 tabular-nums text-zinc-500">{l.numero}</td>
                  <td className="px-4 py-3">
                    <div className="font-medium">{l.descricao}</div>
                    {l.localizacao && <div className="text-xs text-zinc-500">{l.localizacao}</div>}
                  </td>
                  <td className="px-4 py-3 text-xs text-zinc-500">
                    <Link to={`/compras/${l.documentoId}`} className="hover:underline">{l.fornecedor} · {l.referencia}</Link>
                    <div>{formatDateOnly(l.data)}{l.faturaEmFalta && <span className="ml-1 text-amber-700 dark:text-amber-400">· fatura em falta</span>}</div>
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums">{l.quantidadeEmStock}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{formatEur(l.precoUnitarioPago)}</td>
                  <td className="px-4 py-3 text-right font-medium tabular-nums">{formatEur(l.precoFinalComIva)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{formatEur(l.lucro)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{formatEur(l.ivaAPagarEstado)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </SectionCard>
  );
}
