import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Save } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import { parseDecimal } from '../../lib/compras/format';
import { toast } from '../../lib/toast';

/**
 * Sprint 570: preço de venda por categoria (ex.: Películas a 15 €). Os artigos da categoria sem preço
 * próprio vendem a este preço; mudar aqui muda-os todos. Vazio = sem preço (fica o calculado do lote).
 */
export default function CategoriasModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const lista = useQuery({ queryKey: ['artigos', 'categorias'], queryFn: artigosApi.categorias, enabled: open });
  const [precos, setPrecos] = useState<Record<string, string>>({});
  const [nova, setNova] = useState({ nome: '', preco: '' });

  useEffect(() => {
    if (!open || !lista.data) return;
    setPrecos(Object.fromEntries(lista.data.map((c) => [c.nome, c.precoVendaCents != null ? (c.precoVendaCents / 100).toFixed(2).replace('.', ',') : ''])));
  }, [open, lista.data]);

  const guardar = useMutation({
    mutationFn: async ({ nome, preco }: { nome: string; preco: string }) => {
      const v = preco.trim() ? parseDecimal(preco) : null;
      if (v != null && v < 0) throw new Error('Preço inválido.');
      return artigosApi.guardarCategoria(nome, v == null ? null : Math.round(v * 100));
    },
    onSuccess: (c) => {
      toast.success(`Categoria ${c.nome} guardada`, c.precoVendaCents != null ? `${c.artigos} artigo(s) sem preço próprio passam a ${(c.precoVendaCents / 100).toFixed(2)} €` : 'Sem preço por defeito');
      qc.invalidateQueries({ queryKey: ['artigos'] });
      qc.invalidateQueries({ queryKey: ['compras', 'inventario'] });
      setNova({ nome: '', preco: '' });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar a categoria.'),
  });

  return (
    <Modal open={open} onClose={onClose} title="Categorias e preços">
      <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
        Preço de venda (com IVA) para todos os artigos da categoria que não tenham preço próprio. Um artigo com preço próprio mantém o seu.
      </p>
      <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
        {(lista.data ?? []).length === 0 && <li className="px-3 py-2 text-zinc-500">Ainda não há categorias.</li>}
        {lista.data?.map((c) => (
          <li key={c.nome} className="flex items-center gap-2 px-3 py-2">
            <span className="flex-1">{c.nome} <span className="text-xs text-zinc-500">· {c.artigos} artigo(s)</span></span>
            <input value={precos[c.nome] ?? ''} onChange={(e) => setPrecos((p) => ({ ...p, [c.nome]: e.target.value }))}
              className={`${inputCls} w-28`} inputMode="decimal" placeholder="sem preço" aria-label={`Preço de ${c.nome}`} />
            <Button type="button" variant="secondary" leftIcon={<Save size={14} />} loading={guardar.isPending && guardar.variables?.nome === c.nome}
              onClick={() => guardar.mutate({ nome: c.nome, preco: precos[c.nome] ?? '' })}>Guardar</Button>
          </li>
        ))}
      </ul>
      <div className="mt-4 grid grid-cols-[1fr_7rem_auto] items-end gap-2">
        <label><span className={labelCls}>Nova categoria</span>
          <input value={nova.nome} onChange={(e) => setNova({ ...nova, nome: e.target.value })} className={inputCls} placeholder="Películas" /></label>
        <label><span className={labelCls}>Preço (€)</span>
          <input value={nova.preco} onChange={(e) => setNova({ ...nova, preco: e.target.value })} className={inputCls} inputMode="decimal" placeholder="15,00" /></label>
        <Button type="button" leftIcon={<Plus size={14} />} disabled={!nova.nome.trim()} loading={guardar.isPending && guardar.variables?.nome === nova.nome}
          onClick={() => guardar.mutate({ nome: nova.nome.trim(), preco: nova.preco })}>Criar</Button>
      </div>
    </Modal>
  );
}
