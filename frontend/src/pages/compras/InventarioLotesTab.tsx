import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Boxes, Search } from 'lucide-react';
import { DateRangeFilter, EmptyState, SEM_DATAS, SectionCard, SkeletonRow, SortableTh, type IntervaloDatas } from '../../components/ui';
import { ordenarLista, useOrdenacao } from '../../lib/useOrdenacao';
import { comprasApi } from '../../lib/compras/api';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import { inputCls } from '../../components/ui/formClasses';

/** Lotes com stock > 0 (SPEC compras §4.5), com preço de venda sugerido e IVA a entregar ao Estado. */
export default function InventarioLotesTab() {
  const [q, setQ] = useState('');
  const [datas, setDatas] = useState<IntervaloDatas>(SEM_DATAS);
  const { ordem, alternar } = useOrdenacao<'numero' | 'artigo' | 'data' | 'stock' | 'pago' | 'venda' | 'lucro' | 'iva'>('numero', false);
  const inv = useQuery({ queryKey: ['compras', 'inventario'], queryFn: comprasApi.inventario });

  const linhas = useMemo(() => {
    const t = q.trim().toLowerCase();
    const all = (inv.data ?? []).filter((l) => {
      const d = l.data.slice(0, 10);
      return (!datas.de || d >= datas.de) && (!datas.ate || d <= datas.ate);
    });
    const filtradas = t ? all.filter((l) => `${l.numero} ${l.descricao} ${l.fornecedor} ${l.referencia} ${l.localizacao ?? ''}`.toLowerCase().includes(t)) : all;
    return ordenarLista(filtradas, ordem, {
      numero: (l) => l.numero,
      artigo: (l) => l.descricao,
      data: (l) => l.data,
      stock: (l) => l.quantidadeEmStock,
      pago: (l) => l.precoUnitarioPago,
      venda: (l) => l.precoFinalComIva,
      lucro: (l) => l.lucro,
      iva: (l) => l.ivaAPagarEstado,
    });
  }, [inv.data, q, datas, ordem]);

  const totais = useMemo(() => linhas.reduce(
    (s, l) => ({ un: s.un + l.quantidadeEmStock, pago: s.pago + l.totalPago, lucro: s.lucro + l.lucro }),
    { un: 0, pago: 0, lucro: 0 },
  ), [linhas]);

  return (
    <SectionCard
      bodyClassName="p-0"
      title={
        <div className="flex flex-1 flex-wrap items-center gap-2">
          <label className="relative min-w-48 flex-1">
            <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
            <input value={q} onChange={(e) => setQ(e.target.value)} className={`${inputCls} pl-9`} placeholder="Procurar nº do lote, artigo, fornecedor, localização…" aria-label="Procurar" />
          </label>
          <DateRangeFilter value={datas} onChange={setDatas} />
        </div>
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
                <SortableTh className="px-4 py-2" label="Lote" campo="numero" ordem={ordem} onOrdenar={alternar} />
                <SortableTh className="px-4 py-2" label="Artigo" campo="artigo" ordem={ordem} onOrdenar={alternar} />
                <SortableTh className="px-4 py-2" label="Compra" campo="data" ordem={ordem} onOrdenar={alternar} />
                <SortableTh className="px-4 py-2" label="Stock" campo="stock" ordem={ordem} onOrdenar={alternar} align="right" />
                <SortableTh className="px-4 py-2" label="Pago un." campo="pago" ordem={ordem} onOrdenar={alternar} align="right" />
                <SortableTh className="px-4 py-2" label="Venda c/ IVA" campo="venda" ordem={ordem} onOrdenar={alternar} align="right" />
                <SortableTh className="px-4 py-2" label="Lucro (lote)" campo="lucro" ordem={ordem} onOrdenar={alternar} align="right" />
                <SortableTh className="px-4 py-2" label="IVA ao Estado (lote)" campo="iva" ordem={ordem} onOrdenar={alternar} align="right" />
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
