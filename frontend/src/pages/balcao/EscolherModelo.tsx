import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import Modal from '../../components/Modal';
import { inputCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { Artigo, ArtigoLote } from '../../lib/artigos/types';
import { correspondeTermos } from '../../lib/pesquisa';

interface Props {
  /** Artigo agrupado à espera de modelo (null = fechado). */
  artigo: Artigo | null;
  /** O que estava escrito na pesquisa (ex.: "iphone 11") — filtra logo os modelos. */
  filtroInicial?: string;
  onEscolher: (lote: ArtigoLote) => void;
  onClose: () => void;
}

/**
 * Sprint 581: artigo agrupado (ex.: "Película vidro") — um código para vários modelos. Ao vender, escolhe-se o
 * modelo (o lote, com a descrição tal como veio na fatura) para o stock de cada modelo ficar certo.
 */
export default function EscolherModelo({ artigo, filtroInicial, onEscolher, onClose }: Props) {
  const detalhe = useQuery({ queryKey: ['artigos', artigo?.id], queryFn: () => artigosApi.get(artigo!.id), enabled: !!artigo });
  const [q, setQ] = useState('');
  useEffect(() => {
    // Só aproveita a pesquisa se não for o próprio código de barras.
    if (artigo) setQ(filtroInicial && !/^\d{8,}$/.test(filtroInicial) ? filtroInicial : '');
  }, [artigo, filtroInicial]);

  const lotes = useMemo(() => {
    const t = q.trim().toLowerCase();
    const comStock = (detalhe.data?.lotes ?? []).filter((l) => l.quantidadeEmStock > 0);
    const filtrados = comStock.filter((l) => !t || correspondeTermos(l.descricao, t));
    // Se o filtro não apanha nada, mostra todos (melhor do que uma lista vazia).
    return (filtrados.length ? filtrados : comStock).sort((a, b) => a.descricao.localeCompare(b.descricao));
  }, [detalhe.data, q]);

  return (
    <Modal open={!!artigo} onClose={onClose} title={artigo ? `${artigo.nome} — qual modelo?` : ''}>
      <label className="relative mb-3 block">
        <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input value={q} onChange={(e) => setQ(e.target.value)} className={`${inputCls} pl-9`} placeholder="iphone 11, a16…" autoFocus aria-label="Filtrar modelos" />
      </label>
      {detalhe.isLoading ? <p className="text-sm text-zinc-500">A carregar os modelos…</p>
        : lotes.length === 0 ? <p className="text-sm text-zinc-500">Sem stock de nenhum modelo.</p> : (
          <ul className="max-h-[55vh] divide-y divide-zinc-100 overflow-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {lotes.map((l) => (
              <li key={l.linhaId}>
                <button type="button" onClick={() => onEscolher(l)}
                  className="flex w-full items-center gap-3 px-3 py-3 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                  <span className="min-w-0 flex-1">
                    <span className="block">{l.descricao}</span>
                    <span className="block text-xs text-zinc-500">{l.fornecedor} · lote #{l.numero}</span>
                  </span>
                  <span className="flex-none tabular-nums text-emerald-700 dark:text-emerald-400">{l.quantidadeEmStock} un</span>
                </button>
              </li>
            ))}
          </ul>
        )}
    </Modal>
  );
}
