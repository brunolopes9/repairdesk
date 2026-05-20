import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, CheckCircle2, Mail, Phone, Plus, Trash2, XCircle } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonRow } from '../../components/ui';
import { toast } from '../../lib/toast';
import { fornecedoresApi, type Fornecedor, type FornecedorWriteRequest } from '../../lib/fornecedores/api';

const emptyForm: FornecedorWriteRequest = {
  name: '',
  email: null,
  rmaEmail: null,
  phone: null,
  website: null,
  garantiaB2BDiasDefault: null,
  notas: null,
  active: true,
};

export default function Fornecedores() {
  const qc = useQueryClient();
  const [includeInactive, setIncludeInactive] = useState(false);
  const list = useQuery({
    queryKey: ['fornecedores', includeInactive],
    queryFn: () => fornecedoresApi.list(includeInactive),
  });

  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<Fornecedor | null>(null);
  const [form, setForm] = useState<FornecedorWriteRequest>(emptyForm);

  function openCreate() { setEditing(null); setForm(emptyForm); setOpen(true); }
  function openEdit(f: Fornecedor) {
    setEditing(f);
    setForm({ name: f.name, email: f.email, rmaEmail: f.rmaEmail, phone: f.phone,
      website: f.website, garantiaB2BDiasDefault: f.garantiaB2BDiasDefault, notas: f.notas, active: f.active });
    setOpen(true);
  }

  const save = useMutation({
    mutationFn: () => editing ? fornecedoresApi.update(editing.id, form) : fornecedoresApi.create(form),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['fornecedores'] });
      setOpen(false);
      toast.success(editing ? 'Fornecedor atualizado.' : 'Fornecedor criado.');
    },
    onError: (e) => toast.fromError(e, 'Erro ao guardar.'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => fornecedoresApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['fornecedores'] });
      toast.success('Fornecedor removido.');
    },
    onError: (e) => toast.fromError(e, 'Erro ao remover.'),
  });

  const items = list.data ?? [];

  return (
    <div className="space-y-5">
      <PageHeader
        title="Fornecedores"
        description="Fornecedores B2B (Molano, Tudo4Mobile, etc) com contactos RMA e garantia padrão. Usados em encomendas e compras de peças."
        actions={<Button leftIcon={<Plus size={15} />} onClick={openCreate}>Novo fornecedor</Button>}
      />

      <section className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-center gap-2 border-b border-zinc-100 px-4 py-2 text-xs dark:border-zinc-800">
          <label className="inline-flex cursor-pointer items-center gap-1.5 text-zinc-600 dark:text-zinc-300">
            <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} />
            Mostrar inactivos
          </label>
        </div>
        <table className="w-full text-sm">
          <thead className="bg-zinc-50 text-left text-xs uppercase tracking-wider text-zinc-500 dark:bg-zinc-800/60">
            <tr>
              <th className="px-4 py-2.5">Nome</th>
              <th className="px-4 py-2.5">Contactos</th>
              <th className="px-4 py-2.5">Garantia B2B</th>
              <th className="px-4 py-2.5">Estado</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {list.isLoading && Array.from({ length: 3 }).map((_, i) => <tr key={i}><td colSpan={5}><SkeletonRow columns={5} /></td></tr>)}
            {!list.isLoading && items.map((f) => (
              <tr key={f.id} onClick={() => openEdit(f)} className="cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                <td className="px-4 py-3">
                  <div className="font-medium">{f.name}</div>
                  {f.website && <div className="text-[11px] text-zinc-500">{f.website}</div>}
                </td>
                <td className="px-4 py-3 text-xs text-zinc-600 dark:text-zinc-300">
                  {f.email && <div className="flex items-center gap-1"><Mail size={11} /> {f.email}</div>}
                  {f.phone && <div className="flex items-center gap-1"><Phone size={11} /> {f.phone}</div>}
                  {f.rmaEmail && <div className="text-[11px] text-amber-700 dark:text-amber-400">RMA: {f.rmaEmail}</div>}
                </td>
                <td className="px-4 py-3 text-xs text-zinc-600 dark:text-zinc-300">
                  {f.garantiaB2BDiasDefault ? `${f.garantiaB2BDiasDefault} dias` : '—'}
                </td>
                <td className="px-4 py-3">
                  {f.active
                    ? <span className="inline-flex items-center gap-1 text-xs text-emerald-700 dark:text-emerald-400"><CheckCircle2 size={12} /> Activo</span>
                    : <span className="inline-flex items-center gap-1 text-xs text-zinc-500"><XCircle size={12} /> Inactivo</span>}
                </td>
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    onClick={(e) => { e.stopPropagation(); if (confirm(`Remover ${f.name}?`)) remove.mutate(f.id); }}
                    className="rounded-md p-1 text-zinc-500 hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-950/40"
                    aria-label="Remover"
                  >
                    <Trash2 size={15} />
                  </button>
                </td>
              </tr>
            ))}
            {!list.isLoading && items.length === 0 && (
              <tr><td colSpan={5} className="p-6">
                <EmptyState
                  icon={Building2}
                  title="Sem fornecedores"
                  description="Adiciona Molano, Tudo4Mobile e outros fornecedores. A garantia B2B padrão é usada como sugestão ao registar compras."
                />
              </td></tr>
            )}
          </tbody>
        </table>
      </section>

      <Modal open={open} title={editing ? 'Editar fornecedor' : 'Novo fornecedor'} onClose={() => setOpen(false)}>
        <form onSubmit={(e) => { e.preventDefault(); save.mutate(); }} className="space-y-3">
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-500">Nome *</span>
            <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className={inputCls} placeholder="Molano" required />
          </label>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-zinc-500">Email</span>
              <input type="email" value={form.email ?? ''} onChange={(e) => setForm({ ...form, email: e.target.value || null })} className={inputCls} placeholder="info@..." />
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-zinc-500">Email RMA</span>
              <input type="email" value={form.rmaEmail ?? ''} onChange={(e) => setForm({ ...form, rmaEmail: e.target.value || null })} className={inputCls} placeholder="rma@..." />
            </label>
          </div>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-zinc-500">Telefone</span>
              <input value={form.phone ?? ''} onChange={(e) => setForm({ ...form, phone: e.target.value || null })} className={inputCls} />
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-zinc-500">Website</span>
              <input value={form.website ?? ''} onChange={(e) => setForm({ ...form, website: e.target.value || null })} className={inputCls} placeholder="https://..." />
            </label>
          </div>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-500">
              Garantia B2B padrão (dias)
              <span className="ml-1 text-[10px] text-zinc-400">— ex: Molano open-box 60 dias</span>
            </span>
            <input
              type="number"
              min={0}
              max={1825}
              value={form.garantiaB2BDiasDefault ?? ''}
              onChange={(e) => setForm({ ...form, garantiaB2BDiasDefault: e.target.value ? Number(e.target.value) : null })}
              className={inputCls}
              placeholder="60"
            />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-500">Notas</span>
            <textarea rows={3} value={form.notas ?? ''} onChange={(e) => setForm({ ...form, notas: e.target.value || null })} className={`${inputCls} resize-none`} placeholder="Pagamento por Multibanco, devoluções até 14d..." />
          </label>
          <label className="flex items-center gap-2 text-xs">
            <input type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />
            Activo (aparece em sugestões/autocomplete)
          </label>
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancelar</Button>
            <Button type="submit" disabled={!form.name.trim() || save.isPending}>{editing ? 'Guardar' : 'Criar'}</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

const inputCls =
  'w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-zinc-700 dark:bg-zinc-900';
