import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, CheckCircle2, Mail, Phone, Plus, Trash2, XCircle } from 'lucide-react';
import Modal from '../../components/Modal';
import { BackButton, Button, DetailWorkspace, EmptyState, InspectorRail, PageHeader, SkeletonRow } from '../../components/ui';
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
  intraUe: false,
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

  function openCreate() {
    setEditing(null);
    setForm(emptyForm);
    setOpen(true);
  }

  function openEdit(f: Fornecedor) {
    setEditing(f);
    setForm({
      name: f.name,
      email: f.email,
      rmaEmail: f.rmaEmail,
      phone: f.phone,
      website: f.website,
      garantiaB2BDiasDefault: f.garantiaB2BDiasDefault,
      notas: f.notas,
      active: f.active,
      intraUe: f.intraUe,
    });
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
  const activeCount = items.filter((f) => f.active).length;
  const inactiveCount = items.filter((f) => !f.active).length;
  const intraUeCount = items.filter((f) => f.intraUe).length;
  const rmaCount = items.filter((f) => f.rmaEmail).length;
  const warrantyCount = items.filter((f) => f.garantiaB2BDiasDefault).length;

  const rail = (
    <InspectorRail>
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-brand-600 dark:text-brand-300">Compras B2B</p>
        <h2 className="mt-1 text-base font-semibold text-zinc-950 dark:text-zinc-50">Rede de fornecedores</h2>
        <p className="mt-1 text-sm text-zinc-500">
          Contactos, RMA e regras fiscais que alimentam compras, garantias e inventário.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <SupplierStat label="Activos" value={activeCount} />
        <SupplierStat label="RMA" value={rmaCount} />
        <SupplierStat label="Intra-UE" value={intraUeCount} tone={intraUeCount > 0 ? 'warning' : 'default'} />
        <SupplierStat label="Garantia" value={warrantyCount} />
      </div>

      <label className="flex cursor-pointer items-start gap-2 rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-sm dark:border-zinc-800 dark:bg-zinc-950">
        <input
          type="checkbox"
          className="mt-0.5"
          checked={includeInactive}
          onChange={(e) => setIncludeInactive(e.target.checked)}
        />
        <span>
          <span className="block font-medium text-zinc-900 dark:text-zinc-100">Mostrar inactivos</span>
          <span className="mt-0.5 block text-xs text-zinc-500">
            {inactiveCount} fornecedores fora da operação corrente nesta vista.
          </span>
        </span>
      </label>

      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs leading-5 text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/25 dark:text-amber-300">
        <strong>Intra-UE:</strong> marca fornecedores europeus para evitar inflar IVA dedutível nas compras.
      </div>
    </InspectorRail>
  );

  return (
    <div className="space-y-5">
      <BackButton to="/definicoes" label="Voltar a Definições" />
      <PageHeader
        title="Fornecedores"
        description="Fornecedores B2B com contactos RMA, garantia padrão e contexto fiscal para compras de peças."
        meta={<span className="text-sm font-normal text-zinc-500">{items.length} {items.length === 1 ? 'fornecedor' : 'fornecedores'}</span>}
        actions={<Button leftIcon={<Plus size={15} />} onClick={openCreate}>Novo fornecedor</Button>}
      />

      <DetailWorkspace rail={rail}>
        <section className="overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
            <div>
              <h2 className="text-sm font-semibold text-zinc-950 dark:text-zinc-50">Lista de fornecedores</h2>
              <p className="text-xs text-zinc-500">Clica numa linha para editar contactos, garantia ou estado.</p>
            </div>
            <span className="rounded-full bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
              {activeCount} activos
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] text-sm">
              <thead className="bg-zinc-50 text-left text-xs uppercase tracking-wider text-zinc-500 dark:bg-zinc-800/60">
                <tr>
                  <th className="px-4 py-2.5">Fornecedor</th>
                  <th className="px-4 py-2.5">Contactos</th>
                  <th className="px-4 py-2.5">Garantia B2B</th>
                  <th className="px-4 py-2.5">Estado</th>
                  <th className="px-4 py-2.5" />
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {list.isLoading && Array.from({ length: 3 }).map((_, i) => <tr key={i}><td colSpan={5}><SkeletonRow columns={5} /></td></tr>)}
                {!list.isLoading && items.map((f) => (
                  <tr key={f.id} onClick={() => openEdit(f)} className="cursor-pointer transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap items-center gap-1.5 font-medium text-zinc-950 dark:text-zinc-50">
                        {f.name}
                        {f.intraUe && (
                          <span
                            className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-950/40 dark:text-amber-400"
                            title="Fornecedor intra-UE com autoliquidação"
                          >
                            UE
                          </span>
                        )}
                      </div>
                      {f.website && (
                        <a
                          href={f.website}
                          target="_blank"
                          rel="noreferrer"
                          onClick={(e) => e.stopPropagation()}
                          className="mt-0.5 block truncate text-[11px] text-brand-600 hover:underline dark:text-brand-300"
                        >
                          {f.website}
                        </a>
                      )}
                    </td>
                    <td className="px-4 py-3 text-xs text-zinc-600 dark:text-zinc-300">
                      {f.email && <div className="flex items-center gap-1"><Mail size={11} /> {f.email}</div>}
                      {f.phone && <div className="flex items-center gap-1"><Phone size={11} /> {f.phone}</div>}
                      {f.rmaEmail && <div className="text-[11px] text-amber-700 dark:text-amber-400">RMA: {f.rmaEmail}</div>}
                      {!f.email && !f.phone && !f.rmaEmail && <span className="text-zinc-400">Sem contactos</span>}
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
                        className="rounded-md p-1 text-zinc-500 transition hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-950/40"
                        aria-label="Remover"
                      >
                        <Trash2 size={15} />
                      </button>
                    </td>
                  </tr>
                ))}
                {!list.isLoading && items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="p-6">
                      <EmptyState
                        icon={Building2}
                        title="Sem fornecedores"
                        description="Adiciona Molano, Tudo4Mobile e outros fornecedores. A garantia B2B padrão é usada como sugestão ao registar compras."
                      />
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </DetailWorkspace>

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
          <label className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-2.5 text-xs dark:border-amber-900/40 dark:bg-amber-950/20">
            <input
              type="checkbox"
              className="mt-0.5"
              checked={form.intraUe ?? false}
              onChange={(e) => setForm({ ...form, intraUe: e.target.checked })}
            />
            <span>
              <span className="font-medium text-amber-800 dark:text-amber-300">Fornecedor intra-UE (autoliquidação)</span>
              <span className="mt-0.5 block text-amber-700/80 dark:text-amber-400/70">
                Compras a fornecedores de outro país da UE. O IVA é autoliquidado e <strong>não conta como IVA dedutível</strong> no Relatório IVA.
              </span>
            </span>
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

function SupplierStat({ label, value, tone = 'default' }: { label: string; value: number; tone?: 'default' | 'warning' }) {
  return (
    <div className={`rounded-lg border p-3 ${tone === 'warning' ? 'border-amber-200 bg-amber-50 dark:border-amber-900/60 dark:bg-amber-950/25' : 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900'}`}>
      <div className={`text-lg font-semibold ${tone === 'warning' ? 'text-amber-700 dark:text-amber-300' : 'text-zinc-950 dark:text-zinc-50'}`}>{value}</div>
      <div className="text-[11px] text-zinc-500">{label}</div>
    </div>
  );
}

const inputCls =
  'w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-zinc-700 dark:bg-zinc-900';
