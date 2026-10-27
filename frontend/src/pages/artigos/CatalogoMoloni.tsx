import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus, Search, Trash2 } from 'lucide-react';
import Modal from '../../components/Modal';
import { useConfirm } from '../../components/ConfirmDialog';
import { Button, EmptyState, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { formatEur, parseDecimal } from '../../lib/compras/format';
import { moloniApi, MOTIVOS_ISENCAO, type MoloniProduto } from '../../lib/moloni/api';
import { toast } from '../../lib/toast';

/**
 * Sprint 571 (antes página Faturação, Sprint 568): artigos e serviços do Moloni ON. Os artigos da loja
 * entram sozinhos (referência = SKU) ao serem vendidos; aqui geres o resto (ex.: serviços com preço fixo).
 */
export default function CatalogoMoloni({ isAdmin }: { isAdmin: boolean }) {
  const estado = useQuery({ queryKey: ['moloni', 'estado'], queryFn: moloniApi.estado, staleTime: 5 * 60_000 });
  if (estado.data && !estado.data.configurado) {
    return <EmptyState icon={Search} title="Moloni ON por ligar" description="Liga-o em Definições → Moloni ON para gerires aqui os artigos e serviços do Moloni." />;
  }
  return <ArtigosMoloni isAdmin={isAdmin} />;
}

const VAZIO = { nome: '', referencia: '', tipo: 2, preco: '', taxa: 23, motivo: '', categoriaId: '' as number | '', unidadeId: '' as number | '' };

function ArtigosMoloni({ isAdmin }: { isAdmin: boolean }) {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [editar, setEditar] = useState<MoloniProduto | null | undefined>(undefined);
  const [f, setF] = useState(VAZIO);
  const lista = useQuery({ queryKey: ['moloni', 'artigos', q, page], queryFn: () => moloniApi.artigos(q, page), placeholderData: keepPreviousData });
  const refs = useQuery({ queryKey: ['moloni', 'referencias'], queryFn: moloniApi.referencias, enabled: editar !== undefined, staleTime: 10 * 60_000 });

  function abrir(p: MoloniProduto | null) {
    setEditar(p);
    setF(p ? {
      nome: p.nome, referencia: p.referencia, tipo: p.tipo, preco: p.precoComIva.toFixed(2).replace('.', ','), taxa: p.taxa,
      motivo: p.motivoIsencao ?? '', categoriaId: p.categoriaId ?? '', unidadeId: p.unidadeId ?? '',
    } : VAZIO);
  }

  const guardar = useMutation({
    mutationFn: () => {
      const preco = parseDecimal(f.preco);
      if (preco == null || preco < 0) throw new Error('Preço inválido.');
      const req = {
        nome: f.nome.trim(), referencia: f.referencia.trim(), tipo: f.tipo, precoComIva: preco, taxa: f.taxa,
        motivoIsencao: f.taxa === 0 ? f.motivo || null : null,
        categoriaId: f.categoriaId === '' ? null : f.categoriaId, unidadeId: f.unidadeId === '' ? null : f.unidadeId,
      };
      return editar ? moloniApi.atualizarArtigo(editar.productId, req) : moloniApi.criarArtigo(req);
    },
    onSuccess: (p) => { toast.success(editar ? 'Artigo atualizado no Moloni ON' : 'Artigo criado no Moloni ON', `${p.referencia} · ${formatEur(p.precoComIva)}`); setEditar(undefined); qc.invalidateQueries({ queryKey: ['moloni', 'artigos'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar o artigo.'),
  });
  const apagar = useMutation({
    mutationFn: (id: number) => moloniApi.apagarArtigo(id),
    onSuccess: () => { toast.success('Artigo apagado do Moloni ON'); qc.invalidateQueries({ queryKey: ['moloni', 'artigos'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível apagar.'),
  });

  const items = lista.data?.items ?? [];
  const paginas = Math.max(1, Math.ceil((lista.data?.total ?? 0) / 25));
  return (
    <SectionCard bodyClassName="p-0" title={
      <div className="flex flex-1 items-center gap-2">
        <label className="relative block flex-1">
          <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} className={`${inputCls} pl-9`} placeholder="Nome ou referência…" aria-label="Procurar artigos" />
        </label>
        {isAdmin && <Button type="button" leftIcon={<Plus size={15} />} onClick={() => abrir(null)}>Novo</Button>}
      </div>
    }>
      {lista.isLoading ? <p className="p-4 text-sm text-zinc-500">A ler do Moloni ON…</p> : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[40rem] text-sm">
            <thead className="bg-zinc-50 text-left text-xs uppercase text-zinc-500 dark:bg-zinc-900">
              <tr><th className="px-3 py-2">Referência</th><th className="px-3 py-2">Nome</th><th className="px-3 py-2 text-right">Preço c/ IVA</th><th className="px-3 py-2">IVA</th><th className="px-3 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {items.map((p) => (
                <tr key={p.productId}>
                  <td className="px-3 py-2 font-mono text-xs">{p.referencia}</td>
                  <td className="px-3 py-2">{p.nome}<span className="block text-xs text-zinc-500">{p.tipo === 2 ? 'Serviço' : p.tipo === 1 ? 'Produto' : 'Outro'}{p.categoria ? ` · ${p.categoria}` : ''}</span></td>
                  <td className="px-3 py-2 text-right tabular-nums">{formatEur(p.precoComIva)}</td>
                  <td className="px-3 py-2 text-xs">{p.taxa > 0 ? `${p.taxa}%` : p.motivoIsencao ?? '0%'}</td>
                  <td className="whitespace-nowrap px-3 py-2 text-right">
                    {isAdmin && <button type="button" title="Editar" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => abrir(p)}><Pencil size={15} /></button>}
                    {isAdmin && p.podeApagar && (
                      <button type="button" title="Apagar" className="rounded p-1 text-rose-600 hover:bg-zinc-100 dark:hover:bg-zinc-800"
                        onClick={async () => { if (await confirm({ title: `Apagar ${p.referencia}?`, description: 'Apaga o artigo do Moloni ON.', confirmLabel: 'Apagar', destructive: true })) apagar.mutate(p.productId); }}>
                        <Trash2 size={15} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {paginas > 1 && (
        <div className="flex items-center justify-end gap-2 border-t border-zinc-100 p-3 text-sm dark:border-zinc-800">
          <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
          <span className="tabular-nums text-zinc-500">{page} / {paginas}</span>
          <Button type="button" variant="secondary" disabled={page >= paginas} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
        </div>
      )}

      <Modal open={editar !== undefined} onClose={() => setEditar(undefined)} title={editar ? `Editar ${editar.referencia}` : 'Novo artigo no Moloni ON'}
        footer={<Button type="button" loading={guardar.isPending} disabled={f.nome.trim().length < 2 || !f.referencia.trim() || (f.taxa === 0 && !f.motivo)} onClick={() => guardar.mutate()}>Guardar no Moloni</Button>}>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="sm:col-span-2"><span className={labelCls}>Nome</span><input value={f.nome} onChange={(e) => setF({ ...f, nome: e.target.value })} className={inputCls} /></label>
          <label><span className={labelCls}>Referência (única)</span><input value={f.referencia} onChange={(e) => setF({ ...f, referencia: e.target.value })} className={inputCls} /></label>
          <label><span className={labelCls}>Tipo</span>
            <select value={f.tipo} onChange={(e) => setF({ ...f, tipo: Number(e.target.value) })} className={inputCls}>
              <option value={2}>Serviço</option><option value={1}>Produto</option><option value={3}>Outro</option>
            </select>
          </label>
          <label><span className={labelCls}>Preço com IVA (€)</span><input value={f.preco} onChange={(e) => setF({ ...f, preco: e.target.value })} className={inputCls} inputMode="decimal" /></label>
          <label><span className={labelCls}>IVA</span>
            <select value={f.taxa} onChange={(e) => setF({ ...f, taxa: Number(e.target.value) })} className={inputCls}>
              {(refs.data?.taxas ?? [23, 13, 6, 0]).map((t) => <option key={t} value={t}>{t === 0 ? 'Isento (0%)' : `${t}%`}</option>)}
            </select>
          </label>
          {f.taxa === 0 && (
            <label className="sm:col-span-2"><span className={labelCls}>Motivo de isenção</span>
              <select value={f.motivo} onChange={(e) => setF({ ...f, motivo: e.target.value })} className={inputCls}>
                <option value="">Escolher…</option>
                {MOTIVOS_ISENCAO.map((m) => <option key={m.codigo} value={m.codigo}>{m.label}</option>)}
              </select>
            </label>
          )}
          <label><span className={labelCls}>Categoria</span>
            <select value={f.categoriaId} onChange={(e) => setF({ ...f, categoriaId: e.target.value ? Number(e.target.value) : '' })} className={inputCls}>
              <option value="">{editar ? 'Manter' : 'Predefinida'}</option>
              {refs.data?.categorias.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </label>
          <label><span className={labelCls}>Unidade</span>
            <select value={f.unidadeId} onChange={(e) => setF({ ...f, unidadeId: e.target.value ? Number(e.target.value) : '' })} className={inputCls}>
              <option value="">{editar ? 'Manter' : 'Unidade (un)'}</option>
              {refs.data?.unidades.map((u) => <option key={u.id} value={u.id}>{u.nome}</option>)}
            </select>
          </label>
        </div>
      </Modal>
    </SectionCard>
  );
}
