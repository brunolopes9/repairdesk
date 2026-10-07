import { useQuery } from '@tanstack/react-query';
import { AlertTriangle } from 'lucide-react';
import { SectionCard, SkeletonCard } from '../../components/ui';
import { comprasApi } from '../../lib/compras/api';
import type { ResumoColuna } from '../../lib/compras/types';
import { formatEur, formatPct } from '../../lib/compras/format';
import { apiErrorMessage } from '../../lib/errors';
import { REGIME_IVA } from '../../lib/fornecedores/api';

/**
 * Resumo das compras (SPEC compras §4): o que já foi vendido vs o que está em stock, e o IVA
 * que cada parte implica. O IVA trimestral completo (vendas + despesas) vive em IVA & Resultados.
 */
export default function ResumoTab() {
  const resumo = useQuery({ queryKey: ['compras', 'resumo'], queryFn: comprasApi.resumo });

  if (resumo.isLoading) return <SkeletonCard />;
  if (resumo.isError || !resumo.data) return <p className="text-sm text-rose-600">{apiErrorMessage(resumo.error)}</p>;
  const r = resumo.data;

  return (
    <div className="space-y-4">
      {(r.documentosComFaturaEmFalta > 0 || r.documentosComDiferenca > 0) && (
        <div className="flex items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          <AlertTriangle size={16} className="mt-0.5 shrink-0" />
          <span>
            {r.documentosComFaturaEmFalta > 0 && <>{r.documentosComFaturaEmFalta} compra(s) sem fatura — sem fatura o IVA não é dedutível. </>}
            {r.documentosComDiferenca > 0 && <>{r.documentosComDiferenca} documento(s) não batem com o total impresso.</>}
          </span>
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <Coluna titulo="Em stock (se vender tudo ao preço sugerido)" c={r.emStock} />
        <Coluna titulo="Já vendido" c={r.jaVendido} />
      </div>

      <SectionCard title="IVA das compras">
        <dl className="grid gap-x-8 gap-y-2 text-sm tabular-nums sm:grid-cols-2">
          <Row label="IVA pago a fornecedores nacionais (dedutível)" value={r.ivaComprasNacionais} />
          <Row label="IVA dos portes nacionais (dedutível)" value={r.ivaPortesNacionais} />
          <Row label="Autoliquidação UE declarada (liquida e deduz — efeito 0 €)" value={r.autoliquidacaoUeDeclarada} />
          <Row label="Portes pagos (total)" value={r.portesPagos} />
        </dl>
        <p className="mt-3 text-xs text-zinc-500">Taxa de IVA nas vendas: {formatPct(r.taxaIvaVenda)}. Valores a confirmar com o contabilista antes de submeter a declaração.</p>
      </SectionCard>

      <SectionCard title="Por fornecedor" bodyClassName="p-0">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[40rem] text-sm">
            <thead className="border-b border-zinc-100 text-left text-xs text-zinc-500 dark:border-zinc-800">
              <tr>
                <th className="px-4 py-2 font-medium">Fornecedor</th>
                <th className="px-4 py-2 text-right font-medium">Docs</th>
                <th className="px-4 py-2 text-right font-medium">Peças</th>
                <th className="px-4 py-2 text-right font-medium">IVA peças</th>
                <th className="px-4 py-2 text-right font-medium">Portes</th>
                <th className="px-4 py-2 text-right font-medium">Total gasto</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {r.porFornecedor.map((f) => (
                <tr key={f.fornecedorId}>
                  <td className="px-4 py-2.5">
                    {f.nome}
                    {f.regimeIva === REGIME_IVA.UeAutoliquidacao && <span className="ml-1.5 text-[10px] font-semibold text-amber-700 dark:text-amber-400">UE</span>}
                    {f.faturasEmFalta > 0 && <span className="ml-2 text-xs text-amber-700 dark:text-amber-400">{f.faturasEmFalta} sem fatura</span>}
                  </td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{f.documentos}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{formatEur(f.totalPecas)}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{formatEur(f.ivaPecas)}</td>
                  <td className="px-4 py-2.5 text-right tabular-nums">{formatEur(f.portes)}</td>
                  <td className="px-4 py-2.5 text-right font-medium tabular-nums">{formatEur(f.totalGasto)}</td>
                </tr>
              ))}
              {r.porFornecedor.length === 0 && (
                <tr><td colSpan={6} className="px-4 py-6 text-center text-zinc-500">Sem compras registadas.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </SectionCard>
    </div>
  );
}

function Coluna({ titulo, c }: { titulo: string; c: ResumoColuna }) {
  return (
    <SectionCard title={titulo}>
      <dl className="space-y-2 text-sm tabular-nums">
        <Row label="Unidades" value={c.unidades} raw />
        <Row label="Valor cobrado c/ IVA" value={c.valorCobradoComIva} />
        <Row label="Custo das peças (pago)" value={c.valorPecasPago} />
        <Row label="IVA da venda" value={c.ivaDaVenda} />
        <Row label="IVA já pago a fornecedores" value={c.ivaJaPagoFornecedores} />
        <Row label="IVA a entregar ao Estado" value={c.ivaAPagarEstado} strong />
        <Row label="  · de peças nacionais" value={c.ivaAPagarNacionais} muted />
        <Row label="  · de peças UE" value={c.ivaAPagarUe} muted />
        <Row label="Lucro que sobra" value={c.lucro} strong />
      </dl>
    </SectionCard>
  );
}

function Row({ label, value, raw, strong, muted }: { label: string; value: number; raw?: boolean; strong?: boolean; muted?: boolean }) {
  return (
    <div className={`flex justify-between gap-4 ${strong ? 'font-semibold' : ''} ${muted ? 'pl-3 text-xs text-zinc-500' : ''}`}>
      <dt className={muted ? '' : 'text-zinc-600 dark:text-zinc-300'}>{label.trim()}</dt>
      <dd>{raw ? value : formatEur(value)}</dd>
    </div>
  );
}
