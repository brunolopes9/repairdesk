import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink, ImageOff, PackagePlus, Plus, Search, ShoppingBag } from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonTable } from '../../components/ui';
import { apiErrorMessage } from '../../lib/errors';
import { ImportarProdutos } from './ImportarProdutos';
import { DoStockModal } from './DoStockModal';
import { CATEGORIA_LABEL, DISPONIBILIDADE_LABEL, ESTADO_LABEL, euro, lojaApi } from '../../lib/loja/api';

/**
 * Sprint 614 (Doc 97): produtos da loja do lopestech.pt. O site lê daqui — preço, fotos e estado
 * mudam aqui e o site atualiza sozinho. O custo e o lucro só aparecem aqui.
 */
export default function Loja() {
  const navigate = useNavigate();
  const [importar, setImportar] = useState(false);
  const [doStock, setDoStock] = useState(false);
  const lista = useQuery({ queryKey: ['loja', 'produtos'], queryFn: lojaApi.list });
  const todos = useMemo(() => lista.data ?? [], [lista.data]);
  const publicados = todos.filter((p) => p.publicado).length;
  // Sprint 663: com dezenas de produtos, encontrar depressa — tipo (como no site), estado e pesquisa.
  const [q, setQ] = useState('');
  const [tipo, setTipo] = useState('');
  const [estado, setEstado] = useState<'' | 'site' | 'rascunho' | 'esgotado'>('');
  const tipos = useMemo(() => {
    const m = new Map<string, number>();
    for (const p of todos) m.set(tipoDe(p.titulo, CATEGORIA_LABEL[p.categoria]), (m.get(tipoDe(p.titulo, CATEGORIA_LABEL[p.categoria])) ?? 0) + 1);
    return [...m.entries()].sort((a, b) => b[1] - a[1]);
  }, [todos]);
  const itens = todos.filter((p) =>
    (!tipo || tipoDe(p.titulo, CATEGORIA_LABEL[p.categoria]) === tipo)
    && (!estado || (estado === 'site' ? p.publicado : estado === 'rascunho' ? !p.publicado : p.disponibilidade === 2))
    && (!q.trim() || p.titulo.toLowerCase().includes(q.trim().toLowerCase())));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Loja online"
        description="Produtos à venda em lopestech.pt/loja. Publicas aqui e o site atualiza sozinho. Preço e stock vêm do artigo ligado."
        actions={(
          <div className="flex gap-2">
            <Button variant="secondary" onClick={() => navigate('/loja/encomendas')}>Encomendas</Button>
            <Button variant="secondary" onClick={() => navigate('/loja/newsletter')}>Newsletter</Button>
            <Button variant="ghost" onClick={() => setImportar(true)}>Importar</Button>
            <Button variant="success" leftIcon={<PackagePlus size={16} />} onClick={() => setDoStock(true)}>Do stock</Button>
            <Button leftIcon={<Plus size={16} />} onClick={() => navigate('/loja/novo')}>Novo produto</Button>
          </div>
        )}
      />

      {lista.isLoading ? (
        <SkeletonTable columns={4} rows={4} />
      ) : lista.isError ? (
        <p className="text-sm text-rose-600">{apiErrorMessage(lista.error)}</p>
      ) : todos.length === 0 ? (
        <EmptyState
          icon={ShoppingBag}
          title="Ainda não há produtos"
          description="Cria o primeiro: título, estado, garantia, preço e fotos. Só aparece no site quando o publicares."
          action={<Button leftIcon={<Plus size={16} />} onClick={() => navigate('/loja/novo')}>Novo produto</Button>}
        />
      ) : (
        <>
          <div className="space-y-3 rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex flex-wrap items-center gap-2">
              <div className="relative min-w-56 flex-1">
                <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
                <input type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Procurar produto…"
                  className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950" />
              </div>
              {([['', 'Todos'], ['site', 'No site'], ['rascunho', 'Rascunhos'], ['esgotado', 'Sem stock']] as const).map(([v, l]) => (
                <button key={v} type="button" onClick={() => setEstado(v)}
                  className={`min-h-11 rounded-full px-4 text-sm font-semibold transition ${estado === v ? 'bg-brand-600 text-white' : 'bg-zinc-100 text-zinc-700 hover:bg-brand-50 dark:bg-zinc-800 dark:text-zinc-200'}`}>{l}</button>
              ))}
            </div>
            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={() => setTipo('')}
                className={`min-h-10 rounded-full border px-3.5 text-sm font-medium ${tipo === '' ? 'border-brand-500 bg-brand-50 text-brand-800 dark:bg-brand-950/40 dark:text-brand-200' : 'border-zinc-200 text-zinc-700 hover:border-brand-300 dark:border-zinc-700 dark:text-zinc-200'}`}>
                Tudo <span className="text-zinc-400">{todos.length}</span>
              </button>
              {tipos.map(([t, n]) => (
                <button key={t} type="button" onClick={() => setTipo(t)}
                  className={`min-h-10 rounded-full border px-3.5 text-sm font-medium ${tipo === t ? 'border-brand-500 bg-brand-50 text-brand-800 dark:bg-brand-950/40 dark:text-brand-200' : 'border-zinc-200 text-zinc-700 hover:border-brand-300 dark:border-zinc-700 dark:text-zinc-200'}`}>
                  {t} <span className="text-zinc-400">{n}</span>
                </button>
              ))}
            </div>
          </div>
          <p className="text-sm text-zinc-500">{itens.length} de {todos.length} produto(s) · {publicados} no site</p>
          <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {itens.map((p) => {
              const capa = p.fotos.find((f) => f.enviada && f.url);
              return (
                <li key={p.id}>
                  <Link
                    to={`/loja/${p.id}`}
                    className="flex gap-3 rounded-xl border border-zinc-200 bg-white p-3 transition hover:border-brand-300 hover:shadow-sm dark:border-zinc-800 dark:bg-zinc-900"
                  >
                    <span className="grid h-20 w-20 shrink-0 place-items-center overflow-hidden rounded-lg bg-zinc-50 dark:bg-zinc-950">
                      {capa?.url ? <img src={capa.url} alt="" className="h-full w-full object-contain" /> : <ImageOff size={22} className="text-zinc-300" />}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-2 text-xs text-zinc-500">
                        #{p.numero} · {CATEGORIA_LABEL[p.categoria]}
                        <span className={`rounded-full px-2 py-0.5 font-medium ${p.publicado ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300' : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>
                          {p.publicado ? 'No site' : 'Rascunho'}
                        </span>
                      </span>
                      <span className="mt-0.5 block truncate font-semibold text-zinc-950 dark:text-zinc-50">{p.titulo}</span>
                      <span className="block truncate text-xs text-zinc-500">
                        {ESTADO_LABEL[p.estado]} · {p.garantiaMeses} meses · {DISPONIBILIDADE_LABEL[p.disponibilidade]}
                      </span>
                      <span className="mt-1 flex items-baseline gap-3 text-sm">
                        <strong className="tabular-nums">{euro(p.preco)}</strong>
                        {p.lucroEstimado != null && (
                          <span className={`text-xs tabular-nums ${p.lucroEstimado >= 0 ? 'text-emerald-700 dark:text-emerald-300' : 'text-rose-600'}`}>
                            lucro {euro(p.lucroEstimado)}
                          </span>
                        )}
                      </span>
                    </span>
                  </Link>
                </li>
              );
            })}
          </ul>
          <a href="https://lopestech.pt/loja" target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-sm text-brand-700 hover:underline dark:text-brand-300">
            Ver a loja no site <ExternalLink size={14} />
          </a>
        </>
      )}
      {importar && <ImportarProdutos onClose={() => setImportar(false)} />}
      {doStock && <DoStockModal jaNaLoja={new Set(todos.map((p) => p.artigoId).filter((x): x is string => !!x))} onClose={() => setDoStock(false)} />}
    </div>
  );
}

/** Sprint 663: o mesmo agrupamento do site (lopestech.pt/loja) — pelo título; o resto fica na categoria. */
const TIPOS: [string, RegExp][] = [
  ['Powerbanks', /powerbank/i], ['Protetores de câmara', /protetor de c[âa]mara|pel[ií]cula de c[âa]mara/i], ['Películas', /pel[ií]cula|vidro temperado/i],
  ['Capas', /capa/i], ['Carregadores', /carregador/i], ['Cabos', /cabo/i], ['Suportes de carro', /suporte/i], ['Auriculares', /auricular|buds|earbuds/i],
];
function tipoDe(titulo: string, categoria: string): string {
  return TIPOS.find(([, re]) => re.test(titulo))?.[0] ?? categoria;
}
