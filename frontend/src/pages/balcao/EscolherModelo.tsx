import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { Button } from '../../components/ui';
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
  /** modelo = escrito à mão quando o lote não diz para que telemóvel é. */
  onEscolher: (lote: ArtigoLote, modelo: string | null) => void;
  onClose: () => void;
}

/**
 * Sprint 581: artigo agrupado (ex.: "Película vidro") — um código para vários modelos. Ao vender, escolhe-se o
 * modelo (o lote, com a descrição tal como veio na fatura) para o stock de cada modelo ficar certo.
 */
export default function EscolherModelo({ artigo, filtroInicial, onEscolher, onClose }: Props) {
  const detalhe = useQuery({ queryKey: ['artigos', artigo?.id], queryFn: () => artigosApi.get(artigo!.id), enabled: !!artigo });
  const [q, setQ] = useState('');
  // Sprint 583: lote sem modelo na descrição → pede o modelo (a fatura e a garantia dizem sempre o telemóvel).
  const [semModelo, setSemModelo] = useState<ArtigoLote | null>(null);
  const [modelo, setModelo] = useState('');
  useEffect(() => {
    // Só aproveita a pesquisa se não for o próprio código de barras.
    if (artigo) setQ(filtroInicial && !/^\d{8,}$/.test(filtroInicial) ? filtroInicial : '');
    setSemModelo(null);
    setModelo('');
  }, [artigo, filtroInicial]);

  function escolher(l: ArtigoLote) {
    if (l.precisaModelo) { setSemModelo(l); setModelo(q.trim() && !/^\d{8,}$/.test(q.trim()) ? q.trim() : ''); return; }
    onEscolher(l, null);
  }

  const lotes = useMemo(() => {
    const t = q.trim().toLowerCase();
    const comStock = (detalhe.data?.lotes ?? []).filter((l) => l.quantidadeEmStock > 0);
    const filtrados = comStock.filter((l) => !t || correspondeTermos(`${l.textoFatura ?? ""} ${l.descricao}`, t));
    // Se o filtro não apanha nada, mostra todos (melhor do que uma lista vazia).
    return (filtrados.length ? filtrados : comStock).sort((a, b) => (a.textoFatura ?? a.descricao).localeCompare(b.textoFatura ?? b.descricao));
  }, [detalhe.data, q]);

  return (
    <Modal open={!!artigo} onClose={onClose} title={artigo ? `${artigo.nome} — qual modelo?` : ''}>
      <label className="relative mb-3 block">
        <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input value={q} onChange={(e) => setQ(e.target.value)} className={`${inputCls} pl-9`} placeholder="iphone 11, a16…" autoFocus aria-label="Filtrar modelos" />
      </label>
      {semModelo ? (
        <form onSubmit={(e) => { e.preventDefault(); onEscolher(semModelo, modelo.trim() || null); }} className="space-y-3">
          <p className="text-sm text-zinc-600 dark:text-zinc-300">
            «{semModelo.descricao}» não diz o modelo. Escreve para que telemóvel é — sai na fatura: <strong>{artigo?.nome} – {modelo.trim() || '…'}</strong>
          </p>
          <input value={modelo} onChange={(e) => setModelo(e.target.value)} className={inputCls} placeholder="iPhone 12, Samsung A16…" maxLength={60} autoFocus aria-label="Modelo do telemóvel" />
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={() => onEscolher(semModelo, null)}>Sem modelo</Button>
            <Button type="submit" disabled={!modelo.trim()}>Juntar à venda</Button>
          </div>
        </form>
      ) : detalhe.isLoading ? <p className="text-sm text-zinc-500">A carregar os modelos…</p>
        : lotes.length === 0 ? <p className="text-sm text-zinc-500">Sem stock de nenhum modelo.</p> : (
          <ul className="max-h-[55vh] divide-y divide-zinc-100 overflow-auto rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {lotes.map((l) => (
              <li key={l.linhaId}>
                <button type="button" onClick={() => escolher(l)}
                  className="flex w-full items-center gap-3 px-3 py-3 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium">{l.textoFatura ?? l.descricao}{l.precisaModelo && <span className="ml-1 text-xs font-normal text-amber-700 dark:text-amber-400">· escreves o modelo</span>}</span>
                    <span className="block text-xs text-zinc-500">{l.descricao} · {l.fornecedor} · lote #{l.numero}</span>
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
