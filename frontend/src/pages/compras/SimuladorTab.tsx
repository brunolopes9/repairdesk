import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { SectionCard } from '../../components/ui';
import { comprasApi } from '../../lib/compras/api';
import { formatEur, parseDecimal } from '../../lib/compras/format';
import { REGIME_IVA, REGIME_IVA_LABEL, type RegimeIva } from '../../lib/fornecedores/api';
import { inputCls, labelCls } from '../../components/ui/formClasses';

/** Calculadora de uma peça (SPEC compras §4.6): quanto cobrar, quanto IVA entregar, quanto sobra. Não grava nada. */
export default function SimuladorTab() {
  const [preco, setPreco] = useState('');
  const [regime, setRegime] = useState<RegimeIva>(REGIME_IVA.Nacional);
  const [lucro, setLucro] = useState('5');
  const [venda, setVenda] = useState('');

  const simular = useMutation({ mutationFn: comprasApi.simular });
  const { mutate } = simular;

  const precoN = parseDecimal(preco);
  const lucroN = parseDecimal(lucro) ?? 0;
  const vendaN = parseDecimal(venda);

  useEffect(() => {
    if (precoN == null || precoN < 0) return;
    const t = setTimeout(() => mutate({ precoPago: precoN, regime, lucro: lucroN, precoVendaComIva: vendaN }), 250);
    return () => clearTimeout(t);
  }, [precoN, regime, lucroN, vendaN, mutate]);

  const r = precoN == null ? null : simular.data;

  return (
    <div className="grid gap-4 lg:grid-cols-[22rem_1fr]">
      <SectionCard title="Peça">
        <div className="space-y-3">
          <label className="block">
            <span className={labelCls}>Preço pago ao fornecedor (por unidade)</span>
            <input inputMode="decimal" value={preco} onChange={(e) => setPreco(e.target.value)} className={inputCls} placeholder="ex.: 91,95" autoFocus />
          </label>
          <label className="block">
            <span className={labelCls}>Fornecedor</span>
            <select value={regime} onChange={(e) => setRegime(Number(e.target.value) as RegimeIva)} className={inputCls}>
              {Object.entries(REGIME_IVA_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
            </select>
          </label>
          <label className="block">
            <span className={labelCls}>Lucro que quero (sem IVA)</span>
            <input inputMode="decimal" value={lucro} onChange={(e) => setLucro(e.target.value)} className={inputCls} />
          </label>
          <label className="block">
            <span className={labelCls}>E se eu cobrar… (c/ IVA, opcional)</span>
            <input inputMode="decimal" value={venda} onChange={(e) => setVenda(e.target.value)} className={inputCls} placeholder="ex.: 100" />
          </label>
        </div>
      </SectionCard>

      <SectionCard title="Resultado">
        {!r ? (
          <p className="text-sm text-zinc-500">Escreve o preço pago para ver as contas.</p>
        ) : (
          <div className="space-y-4 text-sm tabular-nums">
            <div className="rounded-xl bg-brand-50 p-4 dark:bg-brand-950/30">
              <div className="text-xs text-zinc-500">Preço a cobrar ao cliente (c/ IVA)</div>
              <div className="text-3xl font-semibold">{formatEur(r.precoFinalComIva)}</div>
            </div>
            <dl className="grid gap-x-8 gap-y-2 sm:grid-cols-2">
              <Row label="Custo sem IVA" value={r.custoSemIva} />
              <Row label="IVA pago na compra" value={r.ivaPagoNaCompra} />
              {r.autoliquidacaoUe > 0 && <Row label="Autoliquidação UE (efeito 0 €)" value={r.autoliquidacaoUe} />}
              <Row label="Preço de venda sem IVA" value={r.precoVendaSemIva} />
              <Row label="IVA da venda" value={r.ivaDaVenda} />
              <Row label="IVA a entregar ao Estado" value={r.ivaAPagarEstado} strong />
              <Row label="Lucro que sobra" value={r.lucroQueSobra} strong />
            </dl>
            {r.vendaLucro != null && r.vendaIvaAPagar != null && (
              <div className="rounded-xl border border-zinc-200 p-4 dark:border-zinc-800">
                <div className="mb-2 font-medium">A cobrar {formatEur(vendaN)}</div>
                <dl className="grid gap-x-8 gap-y-2 sm:grid-cols-2">
                  <Row label="IVA a entregar ao Estado" value={r.vendaIvaAPagar} />
                  <Row label="Lucro que sobra" value={r.vendaLucro} strong />
                </dl>
                {r.vendaLucro < 0 && <p className="mt-2 text-xs font-medium text-rose-600">A este preço perdes dinheiro.</p>}
              </div>
            )}
          </div>
        )}
      </SectionCard>
    </div>
  );
}

function Row({ label, value, strong }: { label: string; value: number; strong?: boolean }) {
  return (
    <div className={`flex justify-between gap-4 ${strong ? 'font-semibold' : ''}`}>
      <dt className="text-zinc-600 dark:text-zinc-300">{label}</dt>
      <dd>{formatEur(value)}</dd>
    </div>
  );
}
