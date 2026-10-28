import { useMemo, useState, type KeyboardEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Camera, Euro, PackagePlus, Plus, Printer, ScanBarcode, Search, Tag } from 'lucide-react';
import LeitorCamera from '../../components/LeitorCamera';
import { Button, EmptyState, PageHeader, SectionCard, SkeletonRow, SortableTh, ViewTabs } from '../../components/ui';
import { useAuth } from '../../lib/auth/AuthContext';
import { inputCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { Artigo } from '../../lib/artigos/types';
import { formatEur } from '../../lib/compras/format';
import { toast } from '../../lib/toast';
import { ordenarLista, useOrdenacao } from '../../lib/useOrdenacao';
import ArtigoModal from './ArtigoModal';
import ArtigosDoStockModal from './ArtigosDoStockModal';
import CatalogoMoloni from './CatalogoMoloni';
import CategoriasModal from './CategoriasModal';
import EtiquetasModal from './EtiquetasModal';

type Campo = 'sku' | 'nome' | 'stock' | 'preco' | 'local';

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
  const [doStock, setDoStock] = useState(false);
  const [categorias, setCategorias] = useState(false);
  const [vista, setVista] = useState<'loja' | 'moloni'>('loja');
  const [camara, setCamara] = useState(false);
  const isAdmin = useAuth().hasRole('Admin');

  const list = useQuery({ queryKey: ['artigos', 'lista', q], queryFn: () => artigosApi.list(q), placeholderData: keepPreviousData });
  // Sprint 581: o que entrou em stock e ainda não tem artigo nem é peça de reparação — nada fica escondido.
  const porLigar = useQuery({ queryKey: ['artigos', 'sugestoes'], queryFn: artigosApi.sugestoes, staleTime: 60_000 });
  const nPorLigar = porLigar.data?.length ?? 0;
  const unPorLigar = porLigar.data?.reduce((s, x) => s + x.stock, 0) ?? 0;
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
    await abrirPorCodigo(q);
  }

  /** Código lido (pistola ou câmara): abre o artigo; se não for de nenhum, fica como pesquisa. */
  async function abrirPorCodigo(c: string) {
    const a = await artigosApi.porCodigo(c);
    if (a) { setEditar(a.id); setQ(''); } else setQ(c);
  }

  function alternarEscolha(a: Artigo) {
    setEscolhidos((s) => {
      const n = { ...s };
      if (n[a.id]) delete n[a.id]; else n[a.id] = Math.max(1, a.stock);
      return n;
    });
  }

  /** Etiquetas de um artigo (ícone da linha ou botão no artigo): uma por unidade em stock. */
  function etiquetasDe(a: Artigo) {
    setEscolhidos({ [a.id]: Math.max(1, a.stock) });
    setEtiquetasAbertas(true);
  }

  /** Sem nada marcado, "Etiquetas" propõe todos os visíveis com stock (uma etiqueta por unidade). */
  function abrirEtiquetas() {
    if (Object.keys(escolhidos).length === 0) {
      const comStock = artigos.filter((a) => a.ativo && a.stock > 0);
      if (comStock.length === 0) { toast.error('Nada para etiquetar', 'Não há artigos com stock nesta lista.'); return; }
      setEscolhidos(Object.fromEntries(comStock.map((a) => [a.id, a.stock])));
    }
    setEtiquetasAbertas(true);
  }

  const todosMarcados = artigos.length > 0 && artigos.every((a) => !!escolhidos[a.id]);
  const nEscolhidos = Object.keys(escolhidos).length;
  const totalEtiquetas = Object.values(escolhidos).reduce((s, n) => s + n, 0);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Artigos"
        description="O que tens à venda na loja: código de barras para etiquetar, stock (soma das compras) e preço de venda."
        actions={
          <>
            <span className="hidden sm:contents"><Button variant="secondary" leftIcon={<ScanBarcode size={15} />} onClick={() => navigate('/balcao')}>Balcão</Button></span>
            <Button variant="secondary" leftIcon={<Printer size={15} />} onClick={abrirEtiquetas}
              title="Etiquetas com código de barras dos artigos marcados (sem nenhum marcado: todos os que têm stock)">
              Etiquetas{nEscolhidos ? ` (${totalEtiquetas})` : ''}
            </Button>
            <Button variant="secondary" leftIcon={<Euro size={15} />} onClick={() => setCategorias(true)}>
              <span className="sm:hidden">Preços</span><span className="hidden sm:inline">Categorias e preços</span>
            </Button>
            <Button variant="secondary" leftIcon={<PackagePlus size={15} />} onClick={() => setDoStock(true)}>
              <span className="sm:hidden">Por ligar{nPorLigar ? ` (${nPorLigar})` : ''}</span><span className="hidden sm:inline">Stock por ligar{nPorLigar ? ` (${nPorLigar})` : ''}</span>
            </Button>
            <Button leftIcon={<Plus size={15} />} onClick={() => setEditar(null)}>Novo artigo</Button>
          </>
        }
      />

      <ViewTabs value={vista} onChange={(v) => setVista(v as 'loja' | 'moloni')}
        tabs={[{ key: 'loja', label: 'Artigos da loja' }, { key: 'moloni', label: 'Artigos e serviços no Moloni' }]} />

      {vista === 'moloni' ? <CatalogoMoloni isAdmin={isAdmin} /> : (<>
      {nPorLigar > 0 && (
        <button type="button" onClick={() => setDoStock(true)}
          className="flex w-full items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-left text-sm text-amber-900 hover:bg-amber-100 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          <PackagePlus size={16} className="mt-0.5 shrink-0" />
          <span>
            <strong>{nPorLigar} produto(s) em stock sem artigo ({unPorLigar} un.)</strong> — liga-os a um artigo (ex.: «Película vidro»),
            cria um artigo novo, ou marca-os como peça de reparação (ecrãs, baterias…) se não se vendem ao balcão.
          </span>
        </button>
      )}
      <p className="text-sm text-zinc-500">
        <Printer size={14} className="mr-1 inline" />
        Etiquetas com código de barras: marca os artigos na caixa à esquerda e carrega em <strong>Etiquetas</strong>, ou usa a impressora no fim de cada linha.
      </p>
      <SectionCard
        bodyClassName="p-0"
        title={
          <div className="flex flex-1 flex-wrap items-center gap-2">
            <div className="flex min-w-48 flex-1 gap-2">
              <label className="relative flex-1">
                <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
                <input value={q} onChange={(e) => setQ(e.target.value)} onKeyDown={onEnter} className={`${inputCls} pl-9`} enterKeyHint="search"
                  placeholder="Procurar ou passar a pistola: película a34, LT-00012…" aria-label="Procurar artigos" />
              </label>
              <button type="button" onClick={() => setCamara(true)} aria-label="Procurar com a câmara" title="Ler o código de barras com a câmara"
                className="grid h-11 w-11 flex-none place-items-center rounded-lg border border-zinc-300 text-brand-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-brand-300 dark:hover:bg-zinc-800">
                <Camera size={18} />
              </button>
            </div>
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
          <>
          {/* Sprint 572: no telemóvel, cartões (marcar para etiquetas, abrir, imprimir). */}
          <ul className="divide-y divide-zinc-100 sm:hidden dark:divide-zinc-800">
            {artigos.map((a) => (
              <li key={a.id} className={`flex items-center gap-3 px-3 py-3 ${a.ativo ? '' : 'opacity-50'}`}>
                <input type="checkbox" className="h-5 w-5 flex-none" checked={!!escolhidos[a.id]} onChange={() => alternarEscolha(a)} aria-label={`Etiquetas de ${a.nome}`} />
                <button type="button" onClick={() => setEditar(a.id)} className="min-w-0 flex-1 text-left">
                  <span className="block truncate font-medium">{a.nome}</span>
                  <span className="block truncate text-xs text-zinc-500">{[a.sku, a.categoria, a.localizacao].filter(Boolean).join(' · ')}</span>
                  <span className="mt-1 flex items-center gap-2 text-xs">
                    {a.stock > 0
                      ? <span className="font-semibold tabular-nums text-emerald-700 dark:text-emerald-400">{a.stock} em stock</span>
                      : <span className="rounded-full bg-red-50 px-2 py-0.5 font-medium text-red-700 dark:bg-red-950/40 dark:text-red-300">Sem stock</span>}
                    <span className="ml-auto text-sm font-semibold tabular-nums">{a.precoEfetivoCents != null ? formatEur(a.precoEfetivoCents / 100) : '—'}</span>
                  </span>
                </button>
                <button type="button" aria-label={`Imprimir etiquetas de ${a.nome}`} onClick={() => etiquetasDe(a)}
                  className="grid h-11 w-11 flex-none place-items-center rounded-lg text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800">
                  <Printer size={18} />
                </button>
              </li>
            ))}
          </ul>
          <div className="hidden overflow-x-auto sm:block">
            <table className="w-full text-sm">
              <thead className="bg-zinc-50 text-left text-xs uppercase text-zinc-500 dark:bg-zinc-900">
                <tr>
                  <th className="w-10 px-3 py-2">
                    <input type="checkbox" checked={todosMarcados} aria-label="Marcar todos para etiquetas"
                      onChange={(e) => setEscolhidos(e.target.checked ? Object.fromEntries(artigos.map((a) => [a.id, Math.max(1, a.stock)])) : {})} />
                  </th>
                  <SortableTh label="SKU" campo="sku" ordem={ordem} onOrdenar={alternar} className="px-3 py-2" />
                  <SortableTh label="Artigo" campo="nome" ordem={ordem} onOrdenar={alternar} className="px-3 py-2" />
                  <SortableTh label="Stock" campo="stock" ordem={ordem} onOrdenar={alternar} align="right" className="px-3 py-2" />
                  <SortableTh label="Preço" campo="preco" ordem={ordem} onOrdenar={alternar} align="right" className="px-3 py-2" />
                  <SortableTh label="Local" campo="local" ordem={ordem} onOrdenar={alternar} className="px-3 py-2" />
                  <th className="w-10 px-3 py-2" aria-label="Etiqueta" />
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
                    <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                      <button type="button" title={`Imprimir etiquetas de ${a.nome}`} aria-label={`Imprimir etiquetas de ${a.nome}`}
                        className="rounded p-1 text-zinc-500 hover:bg-zinc-100 hover:text-brand-700 dark:hover:bg-zinc-800" onClick={() => etiquetasDe(a)}>
                        <Printer size={15} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          </>
        )}
      </SectionCard>
      </>)}

      <LeitorCamera open={camara} onClose={() => setCamara(false)} onLido={(c) => { void abrirPorCodigo(c); }} titulo="Procurar artigo pelo código" />

      <CategoriasModal open={categorias} onClose={() => setCategorias(false)} />

      <ArtigosDoStockModal open={doStock} onClose={() => setDoStock(false)}
        onCriados={(ids) => setEscolhidos(Object.fromEntries(ids.map((id) => [id, 1])))} />

      <ArtigoModal open={editar !== undefined} artigoId={editar ?? null} onClose={() => setEditar(undefined)}
        onEtiquetas={(a) => { setEditar(undefined); etiquetasDe(a); }} />

      <EtiquetasModal open={etiquetasAbertas} onClose={() => setEtiquetasAbertas(false)} escolhidos={escolhidos} onChange={setEscolhidos}
        nomes={new Map((list.data ?? []).map((a) => [a.id, a.nome]))} />
    </div>
  );
}
