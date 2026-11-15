import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import Modal from '../../components/Modal';
import { AutosaveStatus } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { comprasApi } from '../../lib/compras/api';
import { formatEur, parseDecimal } from '../../lib/compras/format';
import type { InventarioLinha } from '../../lib/compras/types';
import { formatDateOnly } from '../../lib/money';
import { useAutosave } from '../../lib/useAutosave';

const IVA_VENDA = 0.23;
const r2 = (n: number) => Math.round(n * 100) / 100;
const txt = (n: number) => r2(n).toFixed(2).replace('.', ',');

/** Custo e IVA de uma unidade do lote — espelha IvaEngine.Unidade (o servidor recalcula ao gravar). */
function custos(l: InventarioLinha) {
  const semIva = l.precoUnitarioPago / (1 + l.taxaIvaCompra);
  const ivaCompra = l.precoUnitarioPago - semIva;
  // UE autoliquidada: paga-se sem IVA, mas o custo com IVA (×1,23) é o que conta para decidir o preço.
  const comIva = l.taxaIvaCompra > 0 ? l.precoUnitarioPago : l.precoUnitarioPago * (1 + IVA_VENDA);
  return { semIva, ivaCompra, comIva };
}

interface Props {
  lote: InventarioLinha | null;
  onClose: () => void;
  onAbrirArtigo: (artigoId: string) => void;
}

/**
 * Sprint 671: a ficha de um lote do Stock — custo com e sem IVA, preço ao cliente e lucro, ligados um ao outro.
 * Escreve o preço final (ou o lucro) e grava sozinho; com artigo ligado, o artigo (loja e balcão) passa ao mesmo preço.
 */
export default function LoteModal({ lote, onClose, onAbrirArtigo }: Props) {
  const qc = useQueryClient();
  const [preco, setPreco] = useState('');
  const [lucro, setLucro] = useState('');
  const [noArtigo, setNoArtigo] = useState(true);

  const c = lote ? custos(lote) : null;
  const lucroNum = parseDecimal(lucro);
  const valido = lucroNum != null && lucroNum >= 0;
  const valor = useMemo(() => ({ lucroUnitario: lucroNum == null ? 0 : Math.round(lucroNum * 10000) / 10000, atualizarArtigo: !!lote?.artigoId && noArtigo }), [lucroNum, noArtigo, lote?.artigoId]);

  const auto = useAutosave(valor, async (v) => {
    if (!lote) return;
    const novo = await comprasApi.precoLote(lote.documentoId, lote.linhaId, v);
    qc.setQueryData<InventarioLinha[]>(['compras', 'inventario'], (old) => old?.map((x) => (x.linhaId === novo.linhaId ? novo : x)));
    if (v.atualizarArtigo) void qc.invalidateQueries({ queryKey: ['artigos'] });
    void qc.invalidateQueries({ queryKey: ['compras'], refetchType: 'none' });
  }, { ativo: !!lote && valido });

  // Ao abrir um lote: preenche e marca como «já gravado».
  useEffect(() => {
    if (!lote) return;
    const cc = custos(lote);
    setLucro(txt(lote.lucroUnitario));
    setPreco(txt((cc.semIva + lote.lucroUnitario) * (1 + IVA_VENDA)));
    setNoArtigo(true);
    auto.reiniciar({ lucroUnitario: Math.round(r2(lote.lucroUnitario) * 10000) / 10000, atualizarArtigo: !!lote.artigoId });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lote?.linhaId]);

  if (!lote || !c) return null;

  const mudarPreco = (v: string) => {
    setPreco(v);
    const p = parseDecimal(v);
    if (p != null) setLucro(txt(p / (1 + IVA_VENDA) - c.semIva));
  };
  const mudarLucro = (v: string) => {
    setLucro(v);
    const n = parseDecimal(v);
    if (n != null) setPreco(txt((c.semIva + n) * (1 + IVA_VENDA)));
  };

  const precoNum = parseDecimal(preco) ?? 0;
  const vendaSemIva = precoNum / (1 + IVA_VENDA);
  const ivaVenda = precoNum - vendaSemIva;
  const ivaEstado = ivaVenda - c.ivaCompra;
  const fechar = () => { void auto.gravarJa(); onClose(); };
  const precoArtigoDiferente = lote.artigoId && lote.precoVendaArtigo != null && Math.abs(lote.precoVendaArtigo - precoNum) >= 0.005;

  return (
    <Modal open title={`Lote ${lote.numero}`} onClose={fechar}
      footer={<div className="flex w-full items-center justify-between gap-3"><AutosaveStatus estado={auto.estado} erro={auto.erro} onTentar={auto.tentarDeNovo} /><button type="button" onClick={fechar} className="rounded-lg px-3 py-1.5 text-sm font-medium text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800">Fechar</button></div>}>
      <div className="space-y-5">
        <div>
          <div className="font-semibold leading-snug">{lote.descricao}</div>
          <div className="mt-1 text-xs text-zinc-500">
            <Link to={`/compras/${lote.documentoId}`} className="hover:underline">{lote.fornecedor} · {lote.referencia}</Link> · {formatDateOnly(lote.data)} · <b className="text-zinc-700 dark:text-zinc-300">{lote.quantidadeEmStock} em stock</b>
            {lote.localizacao && <> · {lote.localizacao}</>}
          </div>
          {lote.artigoId && (
            <button type="button" onClick={() => onAbrirArtigo(lote.artigoId!)} className="mt-1 block text-left text-xs font-medium text-brand-600 hover:underline">
              Artigo {lote.artigoSku}{lote.artigoNome ? ` · ${lote.artigoNome}` : ''}
            </button>
          )}
        </div>

        <div className="rounded-xl bg-zinc-50 px-4 py-3 dark:bg-zinc-800/60">
          <div className="text-xs text-zinc-500">Custo por unidade</div>
          <div className="text-lg font-semibold tabular-nums">{formatEur(c.comIva)} <span className="text-sm font-normal text-zinc-500">c/ IVA</span></div>
          <div className="text-xs text-zinc-500 tabular-nums">{formatEur(c.semIva)} s/ IVA{lote.autoliquidacaoUe && ' · fornecedor UE (IVA autoliquidado)'}</div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <label className="block">
            <span className={labelCls}>Preço ao cliente (c/ IVA)</span>
            <div className="relative">
              <input value={preco} onChange={(e) => mudarPreco(e.target.value)} inputMode="decimal" autoFocus onFocus={(e) => e.target.select()}
                className={`${inputCls} pr-8 text-base font-semibold tabular-nums`} aria-label="Preço ao cliente com IVA" />
              <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-zinc-400">€</span>
            </div>
          </label>
          <label className="block">
            <span className={labelCls}>Lucro por unidade (s/ IVA)</span>
            <div className="relative">
              <input value={lucro} onChange={(e) => mudarLucro(e.target.value)} inputMode="decimal" onFocus={(e) => e.target.select()}
                className={`${inputCls} pr-8 tabular-nums`} aria-label="Lucro por unidade sem IVA" />
              <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-sm text-zinc-400">€</span>
            </div>
          </label>
        </div>
        {!valido && <p className="-mt-3 text-xs text-red-600">{lucroNum == null ? 'Escreve um valor.' : `Abaixo do custo — o preço mínimo é ${formatEur(c.semIva * (1 + IVA_VENDA))}.`}</p>}

        <dl className="grid grid-cols-2 gap-x-4 gap-y-1.5 text-sm">
          <dt className="text-zinc-500">Venda s/ IVA</dt><dd className="text-right tabular-nums">{formatEur(vendaSemIva)}</dd>
          <dt className="text-zinc-500">IVA da venda (23%)</dt><dd className="text-right tabular-nums">{formatEur(ivaVenda)}</dd>
          {c.ivaCompra > 0.004 && <><dt className="text-zinc-500">IVA já pago na compra</dt><dd className="text-right tabular-nums">−{formatEur(c.ivaCompra)}</dd></>}
          <dt className="text-zinc-500">IVA a entregar ao Estado</dt><dd className="text-right tabular-nums">{formatEur(ivaEstado)}</dd>
          <dt className="font-medium">Lucro que fica (por unidade)</dt><dd className={`text-right font-semibold tabular-nums ${valido ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-600'}`}>{formatEur(lucroNum)}</dd>
          {lote.quantidadeEmStock > 1 && <><dt className="text-zinc-500">Lucro do lote ({lote.quantidadeEmStock} un.)</dt><dd className="text-right tabular-nums">{formatEur((lucroNum ?? 0) * lote.quantidadeEmStock)}</dd></>}
        </dl>

        {lote.artigoId ? (
          <label className="flex items-start gap-2 rounded-lg border border-zinc-200 px-3 py-2.5 text-sm dark:border-zinc-700">
            <input type="checkbox" checked={noArtigo} onChange={(e) => setNoArtigo(e.target.checked)} className="mt-0.5" />
            <span>
              Usar este preço também no artigo {lote.artigoSku} <span className="text-zinc-500">(balcão e loja online)</span>
              {precoArtigoDiferente && <span className="block text-xs text-amber-700 dark:text-amber-400">Agora o artigo vende a {formatEur(lote.precoVendaArtigo)}.</span>}
            </span>
          </label>
        ) : (
          <p className="text-xs text-zinc-500">Peça de reparação sem artigo — este é o preço que a venda propõe quando escolhes o lote.</p>
        )}
      </div>
    </Modal>
  );
}
