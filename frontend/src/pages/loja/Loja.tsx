import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink, ImageOff, Plus, ShoppingBag } from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonTable } from '../../components/ui';
import { apiErrorMessage } from '../../lib/errors';
import { CATEGORIA_LABEL, DISPONIBILIDADE_LABEL, ESTADO_LABEL, euro, lojaApi } from '../../lib/loja/api';

/**
 * Sprint 614 (Doc 97): produtos da loja do lopestech.pt. O site lê daqui — preço, fotos e estado
 * mudam aqui e o site atualiza sozinho. O custo e o lucro só aparecem aqui.
 */
export default function Loja() {
  const navigate = useNavigate();
  const lista = useQuery({ queryKey: ['loja', 'produtos'], queryFn: lojaApi.list });
  const itens = lista.data ?? [];
  const publicados = itens.filter((p) => p.publicado).length;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Loja online"
        description="Produtos à venda em lopestech.pt/loja. Publicas aqui e o site atualiza sozinho todas as manhãs (07:30)."
        actions={<Button leftIcon={<Plus size={16} />} onClick={() => navigate('/loja/novo')}>Novo produto</Button>}
      />

      {lista.isLoading ? (
        <SkeletonTable columns={4} rows={4} />
      ) : lista.isError ? (
        <p className="text-sm text-rose-600">{apiErrorMessage(lista.error)}</p>
      ) : itens.length === 0 ? (
        <EmptyState
          icon={ShoppingBag}
          title="Ainda não há produtos"
          description="Cria o primeiro: título, estado, garantia, preço e fotos. Só aparece no site quando o publicares."
          action={<Button leftIcon={<Plus size={16} />} onClick={() => navigate('/loja/novo')}>Novo produto</Button>}
        />
      ) : (
        <>
          <p className="text-sm text-zinc-500">{itens.length} produto(s) · {publicados} no site</p>
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
    </div>
  );
}
