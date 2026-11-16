import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardList, PackageCheck, Plus, Trash2, Truck, Undo2 } from 'lucide-react';
import { Button, SectionCard } from '../../components/ui';
import { inputCls } from '../../components/ui/formClasses';
import { api } from '../../lib/api';
import { fornecedoresApi } from '../../lib/fornecedores/api';
import { toast } from '../../lib/toast';

/** Sprint 687: lista de compras da oficina (o que é preciso encomendar, fora das reparações). */
interface Item {
  id: string;
  descricao: string;
  quantidade: number;
  fornecedor: string | null;
  nota: string | null;
  estado: 0 | 1 | 2;
  criadoEm: string;
  encomendadoEm: string | null;
  recebidoEm: string | null;
}

const listaApi = {
  listar: () => api.get<Item[]>('/lista-encomendas').then((r) => r.data),
  criar: (b: { descricao: string; quantidade: number; fornecedor: string | null }) => api.post<Item>('/lista-encomendas', b).then((r) => r.data),
  estado: (id: string, estado: number) => api.put<Item>(`/lista-encomendas/${id}/estado`, { estado }).then((r) => r.data),
  apagar: (id: string) => api.delete(`/lista-encomendas/${id}`),
};

export default function ListaEncomendas() {
  const qc = useQueryClient();
  const lista = useQuery({ queryKey: ['lista-encomendas'], queryFn: listaApi.listar });
  const fornecedores = useQuery({ queryKey: ['fornecedores', false], queryFn: () => fornecedoresApi.list(false), staleTime: 300_000 });
  const [desc, setDesc] = useState('');
  const [qtd, setQtd] = useState('1');
  const [forn, setForn] = useState('');
  const recarregar = () => qc.invalidateQueries({ queryKey: ['lista-encomendas'] });

  const criar = useMutation({
    mutationFn: () => listaApi.criar({ descricao: desc.trim(), quantidade: Math.max(1, Number(qtd) || 1), fornecedor: forn.trim() || null }),
    onSuccess: () => { setDesc(''); setQtd('1'); recarregar(); },
    onError: (e) => toast.fromError(e),
  });
  const estado = useMutation({
    mutationFn: (p: { id: string; estado: number }) => listaApi.estado(p.id, p.estado),
    onSuccess: recarregar,
    onError: (e) => toast.fromError(e),
  });
  const apagar = useMutation({ mutationFn: (id: string) => listaApi.apagar(id), onSuccess: recarregar, onError: (e) => toast.fromError(e) });

  function submeter(e: FormEvent) {
    e.preventDefault();
    if (desc.trim()) criar.mutate();
  }

  const itens = lista.data ?? [];
  const grupos: { titulo: string; est: 0 | 1 | 2; cor: string }[] = [
    { titulo: 'Por encomendar', est: 0, cor: 'text-amber-700 dark:text-amber-300' },
    { titulo: 'Encomendado — a caminho', est: 1, cor: 'text-sky-700 dark:text-sky-300' },
    { titulo: 'Recebido (últimos 7 dias)', est: 2, cor: 'text-emerald-700 dark:text-emerald-300' },
  ];

  return (
    <SectionCard title={<span className="flex items-center gap-2"><ClipboardList size={16} /> Lista de compras</span>}
      action={<span className="text-sm text-zinc-500">{itens.filter((i) => i.estado === 0).length} por encomendar</span>}>
      <form onSubmit={submeter} className="flex flex-wrap items-end gap-2">
        <input value={desc} onChange={(e) => setDesc(e.target.value)} placeholder="O que precisas? Ex.: película Redmi 15C" aria-label="Artigo"
          className={`${inputCls} min-w-[16rem] flex-1`} maxLength={200} />
        <input value={qtd} onChange={(e) => setQtd(e.target.value.replace(/\D/g, ''))} inputMode="numeric" aria-label="Quantidade" className={`${inputCls} w-20`} />
        <input value={forn} onChange={(e) => setForn(e.target.value)} placeholder="Fornecedor (opcional)" aria-label="Fornecedor" className={`${inputCls} w-44`} maxLength={120} list="lista-encomendas-fornecedores" />
        <datalist id="lista-encomendas-fornecedores">
          {(fornecedores.data ?? []).map((f) => <option key={f.id} value={f.name} />)}
        </datalist>
        <Button type="submit" leftIcon={<Plus size={15} />} loading={criar.isPending} disabled={!desc.trim()}>Juntar</Button>
      </form>
      <p className="mt-1 text-xs text-zinc-500">Também podes dizer ao assistente do Telegram: «junta à lista: película Redmi 15C, 5».</p>

      <div className="mt-4 space-y-4">
        {grupos.map((g) => {
          const gi = itens.filter((i) => i.estado === g.est);
          if (gi.length === 0) return null;
          return (
            <div key={g.est}>
              <h3 className={`mb-1 text-xs font-bold uppercase tracking-wide ${g.cor}`}>{g.titulo} · {gi.length}</h3>
              <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
                {gi.map((i) => (
                  <li key={i.id} className="flex flex-wrap items-center gap-2 px-3 py-2 text-sm">
                    <span className={`flex-1 ${i.estado === 2 ? 'text-zinc-400 line-through' : ''}`}>
                      <strong className="tabular-nums">{i.quantidade} ×</strong> {i.descricao}
                      {i.fornecedor && <span className="ml-2 rounded-full bg-zinc-100 px-2 py-0.5 text-xs text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">{i.fornecedor}</span>}
                    </span>
                    {i.estado === 0 && (
                      <Button size="sm" variant="secondary" leftIcon={<Truck size={14} />} onClick={() => estado.mutate({ id: i.id, estado: 1 })}>Encomendei</Button>
                    )}
                    {i.estado === 1 && (
                      <Button size="sm" variant="success" leftIcon={<PackageCheck size={14} />} onClick={() => estado.mutate({ id: i.id, estado: 2 })}>Chegou</Button>
                    )}
                    {i.estado > 0 && (
                      <button type="button" className="text-xs text-zinc-500 underline" onClick={() => estado.mutate({ id: i.id, estado: i.estado - 1 })}
                        title="Voltar ao passo anterior"><Undo2 size={13} className="inline" /> desfazer</button>
                    )}
                    <button type="button" aria-label="Apagar" className="text-zinc-400 hover:text-rose-600" onClick={() => apagar.mutate(i.id)}>
                      <Trash2 size={15} />
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
        {!lista.isLoading && itens.length === 0 && <p className="text-sm text-zinc-500">A lista está vazia.</p>}
      </div>
    </SectionCard>
  );
}
