import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PackagePlus, Search } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import { formatEur } from '../../lib/compras/format';
import { correspondeTermos } from '../../lib/pesquisa';
import { toast } from '../../lib/toast';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Depois de criar: ids dos artigos novos (para escolher logo para etiquetas). */
  onCriados: (ids: string[]) => void;
}

/**
 * Sprint 569: criar os artigos da loja de uma vez a partir do stock. O Mender junta os lotes com a mesma
 * descrição (ex.: duas compras da mesma película) — cada grupo vira um artigo já com stock e preço calculado.
 */
export default function ArtigosDoStockModal({ open, onClose, onCriados }: Props) {
  const qc = useQueryClient();
  const sugestoes = useQuery({ queryKey: ['artigos', 'sugestoes'], queryFn: artigosApi.sugestoes, enabled: open });
  const [escolhidos, setEscolhidos] = useState<Record<number, string>>({}); // índice → nome (editável)
  const [q, setQ] = useState('');
  const [categoria, setCategoria] = useState('');

  useEffect(() => {
    if (!open) return;
    setEscolhidos({});
    setQ('');
    setCategoria('');
  }, [open]);

  const lista = useMemo(() => (sugestoes.data ?? []).map((s, i) => ({ ...s, i }))
    .filter((s) => !q.trim() || correspondeTermos(s.nome, q.trim().toLowerCase())), [sugestoes.data, q]);

  const criar = useMutation({
    mutationFn: () => artigosApi.criarEmLote(
      Object.entries(escolhidos).map(([i, nome]) => ({ nome: nome.trim(), loteIds: sugestoes.data![Number(i)].loteIds })),
      categoria.trim() || null),
    onSuccess: (criados) => {
      toast.success(`${criados.length} artigo(s) criados`, 'Já com stock e código de barras — escolhe-os para imprimir as etiquetas.');
      qc.invalidateQueries({ queryKey: ['artigos'] });
      qc.invalidateQueries({ queryKey: ['compras', 'inventario'] });
      onCriados(criados.map((a) => a.id));
      onClose();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível criar os artigos.'),
  });

  const n = Object.keys(escolhidos).length;
  const todosVisiveis = lista.length > 0 && lista.every((s) => escolhidos[s.i] !== undefined);
  return (
    <Modal open={open} onClose={onClose} wide title="Criar artigos a partir do stock"
      footer={<Button type="button" leftIcon={<PackagePlus size={15} />} loading={criar.isPending}
        disabled={n === 0 || Object.values(escolhidos).some((nome) => !nome.trim())} onClick={() => criar.mutate()}>
        Criar {n} artigo(s)
      </Button>}>
      <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
        Lotes em stock ainda sem artigo, juntos pela descrição. Podes mudar o nome (é o que aparece na etiqueta). O preço fica o calculado
        (custo + lucro + IVA) — muda-o depois no artigo se quiseres um preço fixo.
      </p>
      <div className="mb-3 grid gap-3 sm:grid-cols-2">
        <label className="relative block">
          <span className={labelCls}>Filtrar</span>
          <Search size={15} className="pointer-events-none absolute bottom-3.5 left-3 text-zinc-400" />
          <input value={q} onChange={(e) => setQ(e.target.value)} className={`${inputCls} pl-9`} placeholder="película, bateria…" />
        </label>
        <label className="block">
          <span className={labelCls}>Categoria para os escolhidos (opcional)</span>
          <input value={categoria} onChange={(e) => setCategoria(e.target.value)} className={inputCls} placeholder="Películas" />
        </label>
      </div>
      {sugestoes.isLoading ? <p className="text-sm text-zinc-500">A juntar o stock…</p>
        : lista.length === 0 ? <p className="text-sm text-zinc-500">Não há lotes em stock sem artigo{q ? ' com esse texto' : ''}.</p> : (
          <div className="max-h-[50vh] overflow-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-zinc-50 text-left text-xs uppercase text-zinc-500 dark:bg-zinc-900">
                <tr>
                  <th className="w-10 px-3 py-2">
                    <input type="checkbox" checked={todosVisiveis} aria-label="Escolher todos os visíveis"
                      onChange={(e) => setEscolhidos((s) => {
                        const n2 = { ...s };
                        for (const x of lista) { if (e.target.checked) n2[x.i] = n2[x.i] ?? x.nome; else delete n2[x.i]; }
                        return n2;
                      })} />
                  </th>
                  <th className="px-3 py-2">Nome do artigo</th>
                  <th className="px-3 py-2 text-right">Stock</th>
                  <th className="px-3 py-2 text-right">Preço</th>
                  <th className="px-3 py-2">Lotes</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {lista.map((s) => {
                  const marcado = escolhidos[s.i] !== undefined;
                  return (
                    <tr key={s.i}>
                      <td className="px-3 py-2">
                        <input type="checkbox" checked={marcado} aria-label={`Criar ${s.nome}`}
                          onChange={(e) => setEscolhidos((x) => { const n2 = { ...x }; if (e.target.checked) n2[s.i] = s.nome; else delete n2[s.i]; return n2; })} />
                      </td>
                      <td className="px-3 py-2">
                        {marcado
                          ? <input value={escolhidos[s.i]} onChange={(e) => setEscolhidos((x) => ({ ...x, [s.i]: e.target.value }))} className={`${inputCls} min-h-9 py-1`} />
                          : s.nome}
                      </td>
                      <td className="px-3 py-2 text-right tabular-nums">{s.stock}</td>
                      <td className="px-3 py-2 text-right tabular-nums">{s.precoCalculadoCents != null ? formatEur(s.precoCalculadoCents / 100) : '—'}</td>
                      <td className="px-3 py-2 text-xs text-zinc-500">{s.lotes.map((l) => `#${l}`).join(', ')}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
    </Modal>
  );
}
