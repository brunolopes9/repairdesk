import { useMemo, useState, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Euro, PackagePlus, Plus, Printer, ScanBarcode, Search, Tag } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SectionCard, SkeletonRow, SortableTh } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { Artigo } from '../../lib/artigos/types';
import { formatEur } from '../../lib/compras/format';
import { toast } from '../../lib/toast';
import { ordenarLista, useOrdenacao } from '../../lib/useOrdenacao';
import ArtigoModal from './ArtigoModal';
import ArtigosDoStockModal from './ArtigosDoStockModal';
import CategoriasModal from './CategoriasModal';

type Campo = 'sku' | 'nome' | 'stock' | 'preco' | 'local';

/** Tamanho do rolo da última impressão (Marklife P50 por defeito) — conveniência local, não crítico. */
function tamanhoGuardado(): { l: number; a: number } {
  try {
    const v = JSON.parse(localStorage.getItem('mender.etiquetas.tamanho') ?? 'null');
    if (v && typeof v.l === 'number' && typeof v.a === 'number') return v;
  } catch { /* sem storage */ }
  return { l: 50, a: 30 };
}

/**
 * Sprint 566: artigos da loja — SKU e código de barras próprios, stock = soma dos lotes, preço de venda.
 * A caixa de pesquisa aceita a pistola: lê o código e Enter abre o artigo.
 */
export default function Artigos() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [semStock, setSemStock] = useState(false);
  const [editar, setEditar] = useState<string | null | undefined>(undefined); // undefined = fechado, null = novo
  const [escolhidos, setEscolhidos] = useState<Record<string, number>>({});
  const [etiquetasAbertas, setEtiquetasAbertas] = useState(false);
  const [tamanho, setTamanho] = useState(tamanhoGuardado);
  const [comPreco, setComPreco] = useState(true);
  const [aImprimir, setAImprimir] = useState(false);
  const [doStock, setDoStock] = useState(false);
  const [categorias, setCategorias] = useState(false);

  const list = useQuery({ queryKey: ['artigos', 'lista', q], queryFn: () => artigosApi.list(q), placeholderData: keepPreviousData });
  const { ordem, alternar } = useOrdenacao<Campo>('nome', false);
  const artigos = useMemo(() => ordenarLista((list.data ?? []).filter((a) => !semStock || a.stock === 0), ordem, {
    sku: (a) => a.numero,
    nome: (a) => a.nome,
    stock: (a) => a.stock,
    preco: (a) => a.precoEfetivoCents,
    local: (a) => a.localizacao,
  }), [list.data, ordem, semStock]);

  async function onEnter(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key !== 'Enter' || !q.trim()) return;
    const a = await artigosApi.porCodigo(q);
    if (a) { setEditar(a.id); setQ(''); }
  }

  function alternarEscolha(a: Artigo) {
    setEscolhidos((s) => {
      const n = { ...s };
      if (n[a.id]) delete n[a.id]; else n[a.id] = Math.max(1, a.stock);
      return n;
    });
  }

  async function imprimir() {
    setAImprimir(true);
    try {
      try { localStorage.setItem('mender.etiquetas.tamanho', JSON.stringify(tamanho)); } catch { /* ignore */ }
      await artigosApi.abrirEtiquetas({
        itens: Object.entries(escolhidos).map(([artigoId, copias]) => ({ artigoId, copias })),
        larguraMm: tamanho.l,
        alturaMm: tamanho.a,
        mostrarPreco: comPreco,
      });
      setEtiquetasAbertas(false);
    } catch (err) {
      toast.fromError(err, 'Não foi possível gerar as etiquetas.');
    } finally {
      setAImprimir(false);
    }
  }

  const nEscolhidos = Object.keys(escolhidos).length;
  const totalEtiquetas = Object.values(escolhidos).reduce((s, n) => s + n, 0);
  const porId = new Map((list.data ?? []).map((a) => [a.id, a]));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Artigos"
        description="O que tens à venda na loja: código de barras para etiquetar, stock (soma das compras) e preço de venda."
        actions={
          <>
            <Button variant="secondary" leftIcon={<ScanBarcode size={15} />} onClick={() => navigate('/balcao')}>Balcão</Button>
            <Button variant="secondary" leftIcon={<Printer size={15} />} disabled={nEscolhidos === 0} onClick={() => setEtiquetasAbertas(true)}>
              Etiquetas{nEscolhidos ? ` (${totalEtiquetas})` : ''}
            </Button>
            <Button variant="secondary" leftIcon={<Euro size={15} />} onClick={() => setCategorias(true)}>Categorias e preços</Button>
            <Button variant="secondary" leftIcon={<PackagePlus size={15} />} onClick={() => setDoStock(true)}>Criar a partir do stock</Button>
            <Button leftIcon={<Plus size={15} />} onClick={() => setEditar(null)}>Novo artigo</Button>
          </>
        }
      />

      <SectionCard
        bodyClassName="p-0"
        title={
          <div className="flex flex-1 flex-wrap items-center gap-2">
            <label className="relative min-w-48 flex-1">
              <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
              <input value={q} onChange={(e) => setQ(e.target.value)} onKeyDown={onEnter} className={`${inputCls} pl-9`}
                placeholder="Procurar ou passar a pistola: película a34, LT-00012…" aria-label="Procurar artigos" />
            </label>
            <label className="flex items-center gap-2 text-sm text-zinc-600 dark:text-zinc-300">
              <input type="checkbox" checked={semStock} onChange={(e) => setSemStock(e.target.checked)} /> Só sem stock (para encomendar)
            </label>
          </div>
        }
      >
        {list.isLoading ? (
          <table className="w-full"><tbody>{[0, 1, 2].map((i) => <SkeletonRow key={i} columns={6} />)}</tbody></table>
        ) : artigos.length === 0 ? (
          <EmptyState icon={Tag} title={q ? 'Nenhum artigo com esse texto' : 'Ainda não há artigos'}
            description="Cria um artigo (ex.: Película Samsung A34) e liga-lhe os lotes que compraste — o stock passa a ser a soma deles." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-zinc-50 text-left text-xs uppercase text-zinc-500 dark:bg-zinc-900">
                <tr>
                  <th className="w-10 px-3 py-2" aria-label="Escolher para etiquetas" />
                  <SortableTh label="SKU" campo="sku" ordem={ordem} onOrdenar={alternar} className="px-3 py-2" />
                  <SortableTh label="Artigo" campo="nome" ordem={ordem} onOrdenar={alternar} className="px-3 py-2" />
                  <SortableTh label="Stock" campo="stock" ordem={ordem} onOrdenar={alternar} align="right" className="px-3 py-2" />
                  <SortableTh label="Preço" campo="preco" ordem={ordem} onOrdenar={alternar} align="right" className="px-3 py-2" />
                  <SortableTh label="Local" campo="local" ordem={ordem} onOrdenar={alternar} className="px-3 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {artigos.map((a) => (
                  <tr key={a.id} className={`cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-800/40 ${a.ativo ? '' : 'opacity-50'}`} onClick={() => setEditar(a.id)}>
                    <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                      <input type="checkbox" checked={!!escolhidos[a.id]} onChange={() => alternarEscolha(a)} aria-label={`Etiquetas de ${a.nome}`} />
                    </td>
                    <td className="px-3 py-2 font-mono text-xs">{a.sku}</td>
                    <td className="px-3 py-2">
                      <div className="font-medium">{a.nome}</div>
                      <div className="text-xs text-zinc-500">{[a.categoria, a.codigoBarras].filter(Boolean).join(' · ')}</div>
                    </td>
                    <td className="px-3 py-2 text-right">
                      {a.stock > 0
                        ? <span className="tabular-nums font-semibold">{a.stock}</span>
                        : <span className="rounded-full bg-red-50 px-2 py-0.5 text-xs font-medium text-red-700 dark:bg-red-950/40 dark:text-red-300">Sem stock</span>}
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums">
                      {a.precoEfetivoCents != null ? formatEur(a.precoEfetivoCents / 100) : '—'}
                      {a.origemPreco === 'categoria' && <div className="text-[11px] text-brand-600">da categoria</div>}
                      {a.origemPreco === 'calculado' && a.precoEfetivoCents != null && <div className="text-[11px] text-zinc-500">calculado</div>}
                    </td>
                    <td className="px-3 py-2 text-zinc-600 dark:text-zinc-300">{a.localizacao ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SectionCard>

      <CategoriasModal open={categorias} onClose={() => setCategorias(false)} />

      <ArtigosDoStockModal open={doStock} onClose={() => setDoStock(false)}
        onCriados={(ids) => setEscolhidos(Object.fromEntries(ids.map((id) => [id, 1])))} />

      <ArtigoModal open={editar !== undefined} artigoId={editar ?? null} onClose={() => setEditar(undefined)} />

      <Modal
        open={etiquetasAbertas}
        onClose={() => setEtiquetasAbertas(false)}
        title={`Imprimir ${totalEtiquetas} etiqueta(s)`}
        footer={<Button type="button" leftIcon={<Printer size={15} />} loading={aImprimir} disabled={totalEtiquetas === 0} onClick={imprimir}>Gerar PDF</Button>}
      >
        <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
          Uma etiqueta por página, do tamanho do rolo. Na Marklife P50 abre o PDF na app (ou partilha-o para o telemóvel) e imprime.
        </p>
        <div className="grid grid-cols-2 gap-3">
          <label><span className={labelCls}>Largura (mm)</span>
            <input type="number" min={20} max={120} value={tamanho.l} onChange={(e) => setTamanho((t) => ({ ...t, l: Number(e.target.value) }))} className={inputCls} /></label>
          <label><span className={labelCls}>Altura (mm)</span>
            <input type="number" min={10} max={120} value={tamanho.a} onChange={(e) => setTamanho((t) => ({ ...t, a: Number(e.target.value) }))} className={inputCls} /></label>
        </div>
        <label className="mt-3 flex items-center gap-2 text-sm"><input type="checkbox" checked={comPreco} onChange={(e) => setComPreco(e.target.checked)} /> Mostrar o preço na etiqueta</label>
        <ul className="mt-3 divide-y divide-zinc-100 rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
          {Object.entries(escolhidos).map(([id, n]) => (
            <li key={id} className="flex items-center gap-2 px-3 py-2">
              <span className="flex-1 truncate">{porId.get(id)?.nome ?? id}</span>
              <input type="number" min={0} max={200} value={n} className={`${inputCls} w-20`} aria-label="Cópias"
                onChange={(e) => setEscolhidos((s) => ({ ...s, [id]: Math.max(0, Number(e.target.value)) }))} />
            </li>
          ))}
        </ul>
      </Modal>
    </div>
  );
}
