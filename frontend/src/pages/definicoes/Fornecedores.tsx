import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, CheckCircle2, History, Mail, Phone, Plus, Trash2, XCircle } from 'lucide-react';
import Modal from '../../components/Modal';
import { BackButton, Button, DetailWorkspace, EmptyState, InspectorRail, PageHeader, SkeletonRow, SortableTh } from '../../components/ui';
import { ordenarLista, useOrdenacao } from '../../lib/useOrdenacao';
import { toast } from '../../lib/toast';
import { formatCents, formatDateOnly } from '../../lib/money';
import { fornecedoresApi, REGIME_IVA, REGIME_IVA_LABEL, type Fornecedor, type FornecedorWriteRequest, type RegimeIva } from '../../lib/fornecedores/api';

const emptyForm: FornecedorWriteRequest = {
  name: '',
  email: null,
  rmaEmail: null,
  phone: null,
  website: null,
  garantiaB2BDiasDefault: null,
  notas: null,
  active: true,
  regimeIva: REGIME_IVA.Nacional,
  pais: 'PT',
  nif: null,
};

/** Sprint 579: "Hetzner" e "Hetzner Online GmbH" — mesma 1.ª palavra, provável repetido a juntar. */
const primeiraPalavra = (nome: string) => nome.toLowerCase().replace(/[^a-z0-9 ]/g, '').trim().split(/\s+/)[0] ?? '';

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
  // Sprint 548 (Doc 93 #3): histórico consolidado por fornecedor.
  const [historicoDe, setHistoricoDe] = useState<Fornecedor | null>(null);

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
      regimeIva: f.regimeIva,
      pais: f.pais,
      nif: f.nif,
    });
    setJuntar('');
    setOpen(true);
  }

  const save = useMutation({
    // Na edição o NIF vai sempre ("" apaga); null no backend significaria "não mexer".
    mutationFn: () => editing ? fornecedoresApi.update(editing.id, { ...form, nif: form.nif ?? '' }) : fornecedoresApi.create(form),
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

  // Sprint 579: juntar um fornecedor repetido no que está a ser editado.
  const [juntar, setJuntar] = useState('');
  const fundir = useMutation({
    mutationFn: () => fornecedoresApi.fundir(editing!.id, juntar),
    onSuccess: (f) => {
      qc.invalidateQueries({ queryKey: ['fornecedores'] });
      setOpen(false);
      toast.success(`Fornecedores juntos em "${f.name}".`);
    },
    onError: (e) => toast.fromError(e, 'Não foi possível juntar.'),
  });

  const { ordem, alternar } = useOrdenacao<'nome' | 'garantia' | 'estado'>('nome', false);
  const items = ordenarLista(list.data ?? [], ordem, {
    nome: (f) => f.name,
    garantia: (f) => f.garantiaB2BDiasDefault ?? null,
    estado: (f) => (f.active ? 0 : 1),
  });
  const activeCount = items.filter((f) => f.active).length;
  const inactiveCount = items.filter((f) => !f.active).length;
  const intraUeCount = items.filter((f) => f.intraUe).length;
  const rmaCount = items.filter((f) => f.rmaEmail).length;
  const warrantyCount = items.filter((f) => f.garantiaB2BDiasDefault).length;
  const porPalavra = new Map<string, number>();
  for (const f of items) porPalavra.set(primeiraPalavra(f.name), (porPalavra.get(primeiraPalavra(f.name)) ?? 0) + 1);
  const repetido = (f: Fornecedor) => (porPalavra.get(primeiraPalavra(f.name)) ?? 0) > 1;

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
                  <SortableTh className="px-4 py-2.5" label="Fornecedor" campo="nome" ordem={ordem} onOrdenar={alternar} />
                  <th className="px-4 py-2.5">Contactos</th>
                  <SortableTh className="px-4 py-2.5" label="Garantia B2B" campo="garantia" ordem={ordem} onOrdenar={alternar} />
                  <SortableTh className="px-4 py-2.5" label="Estado" campo="estado" ordem={ordem} onOrdenar={alternar} />
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
                        {repetido(f) && (
                          <span
                            className="rounded bg-rose-100 px-1.5 py-0.5 text-[10px] font-semibold text-rose-700 dark:bg-rose-950/40 dark:text-rose-300"
                            title="Há outro fornecedor com nome parecido — se é o mesmo, abre-o e usa «Juntar»"
                          >
                            repetido?
                          </span>
                        )}
                      </div>
                      {f.nif && <div className="mt-0.5 text-[11px] tabular-nums text-zinc-500">NIF {f.nif}</div>}
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
                        onClick={(e) => { e.stopPropagation(); setHistoricoDe(f); }}
                        className="rounded-md p-1 text-zinc-500 transition hover:bg-zinc-100 hover:text-brand-600 dark:hover:bg-zinc-800"
                        aria-label="Histórico"
                        title="Histórico consolidado: compras, faturas, taxa de defeito"
                      >
                        <History size={15} />
                      </button>
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
                        description="Adiciona Tudo4Mobile, Utopya e outros fornecedores. A garantia B2B padrão é usada como sugestão ao registar compras."
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
            <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} className={inputCls} placeholder="Tudo4Mobile" required />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-zinc-500">
              NIF / nº de IVA
              <span className="ml-1 text-[10px] text-zinc-400">— as faturas deste fornecedor são reconhecidas por ele</span>
            </span>
            <input value={form.nif ?? ''} onChange={(e) => setForm({ ...form, nif: e.target.value || null })} className={inputCls} placeholder="PT500000000 · DE811234567" />
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
              <span className="ml-1 text-[10px] text-zinc-400">— ex: 60 dias</span>
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
          <div className="grid grid-cols-[1fr_6rem] gap-2">
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-zinc-500">Regime de IVA nas compras</span>
              <select
                value={form.regimeIva ?? REGIME_IVA.Nacional}
                onChange={(e) => setForm({ ...form, regimeIva: Number(e.target.value) as RegimeIva })}
                className={inputCls}
              >
                {Object.entries(REGIME_IVA_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
              </select>
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-zinc-500">País</span>
              <input
                value={form.pais ?? ''}
                maxLength={2}
                onChange={(e) => setForm({ ...form, pais: e.target.value.toUpperCase() || null })}
                className={inputCls}
                placeholder="PT"
              />
            </label>
          </div>
          {form.regimeIva === REGIME_IVA.UeAutoliquidacao && (
            <p className="rounded-lg border border-amber-200 bg-amber-50 p-2.5 text-xs text-amber-800 dark:border-amber-900/40 dark:bg-amber-950/20 dark:text-amber-300">
              Faturas sem IVA: o IVA é autoliquidado na declaração (liquidado e deduzido ao mesmo tempo) — <strong>não reduz</strong> o IVA a pagar das vendas.
            </p>
          )}
          <label className="flex items-center gap-2 text-xs">
            <input type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />
            Activo (aparece em sugestões/autocomplete)
          </label>
          {editing && (
            <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
              <p className="text-xs font-medium text-zinc-700 dark:text-zinc-200">Juntar um fornecedor repetido neste</p>
              <p className="mt-0.5 text-[11px] text-zinc-500">
                As compras, faturas e despesas do escolhido passam para «{editing.name}» e o repetido desaparece.
              </p>
              <div className="mt-2 flex flex-col gap-2 sm:flex-row">
                <select value={juntar} onChange={(e) => setJuntar(e.target.value)} className={inputCls} aria-label="Fornecedor repetido">
                  <option value="">Escolhe o repetido…</option>
                  {(list.data ?? [])
                    .filter((f) => f.id !== editing.id)
                    .sort((a, b) => Number(primeiraPalavra(b.name) === primeiraPalavra(editing.name)) - Number(primeiraPalavra(a.name) === primeiraPalavra(editing.name)) || a.name.localeCompare(b.name))
                    .map((f) => <option key={f.id} value={f.id}>{f.name}{f.nif ? ` · ${f.nif}` : ''}</option>)}
                </select>
                <Button
                  type="button"
                  variant="secondary"
                  disabled={!juntar || fundir.isPending}
                  loading={fundir.isPending}
                  onClick={() => {
                    const origem = list.data?.find((f) => f.id === juntar);
                    if (origem && confirm(`Juntar «${origem.name}» em «${editing.name}»? Não dá para desfazer.`)) fundir.mutate();
                  }}
                >
                  Juntar
                </Button>
              </div>
            </div>
          )}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancelar</Button>
            <Button type="submit" disabled={!form.name.trim() || save.isPending}>{editing ? 'Guardar' : 'Criar'}</Button>
          </div>
        </form>
      </Modal>

      <FornecedorHistoricoModal fornecedor={historicoDe} onClose={() => setHistoricoDe(null)} />
    </div>
  );
}

/** Sprint 548 (Doc 93 #3): histórico do fornecedor — tudo numa vista. */
function FornecedorHistoricoModal({ fornecedor, onClose }: { fornecedor: Fornecedor | null; onClose: () => void }) {
  const historico = useQuery({
    queryKey: ['fornecedor-historico', fornecedor?.id],
    queryFn: () => fornecedoresApi.historico(fornecedor!.id),
    enabled: !!fornecedor,
    staleTime: 60_000,
  });
  const h = historico.data;

  return (
    <Modal open={!!fornecedor} title={`Histórico — ${fornecedor?.name ?? ''}`} onClose={onClose}>
      {historico.isLoading || !h ? (
        <p className="text-sm text-zinc-500">A carregar…</p>
      ) : (
        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <HistoricoStat label="Compras stock" value={formatCents(h.comprasStockCents)} />
            <HistoricoStat label="Despesas" value={formatCents(h.despesasCents)} />
            <HistoricoStat label="Faturas" value={`${h.importsTotal}`} sub={h.importsPendentes > 0 ? `${h.importsPendentes} pendente${h.importsPendentes === 1 ? '' : 's'}` : undefined} />
            <HistoricoStat
              label="Defeito 12m"
              value={h.itensVendidos12m === 0 ? '—' : `${h.taxaDefeitoPct12m}%`}
              sub={h.itensVendidos12m > 0 ? `${h.itensComReparacao12m}/${h.itensVendidos12m} c/ IMEI` : 'sem vendas c/ IMEI'}
              tone={h.taxaDefeitoPct12m > 10 ? 'warning' : 'default'}
            />
          </div>

          <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-zinc-500">
            {h.ultimaCompraEm && <span>Última compra: <strong className="text-zinc-700 dark:text-zinc-300">{formatDateOnly(h.ultimaCompraEm)}</strong></span>}
            {h.intraUe && <span className="text-amber-700 dark:text-amber-400">Intra-UE (autoliquidação)</span>}
            {h.garantiaB2BDiasDefault != null && <span>Garantia B2B: {h.garantiaB2BDiasDefault} dias</span>}
            <span>Regra import: {h.defaultImportAction}</span>
          </div>

          <div>
            <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-zinc-500">Últimas faturas</p>
            {h.ultimasFaturas.length === 0 ? (
              <p className="rounded-md bg-zinc-50 px-3 py-3 text-center text-sm text-zinc-500 dark:bg-zinc-950/60">Sem faturas importadas deste fornecedor.</p>
            ) : (
              <ul className="space-y-1">
                {h.ultimasFaturas.map((f) => (
                  <li key={f.importId} className="flex items-center justify-between gap-3 rounded-lg border border-zinc-100 px-3 py-2 text-sm dark:border-zinc-800">
                    <div>
                      <span className="font-medium">{f.numero ?? 'Sem número'}</span>
                      <span className="ml-2 text-xs text-zinc-500">{f.data ? formatDateOnly(f.data) : '—'}</span>
                      {f.status === 'Pending' && <span className="ml-2 rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-950/40 dark:text-amber-300">pendente</span>}
                      {f.status === 'Rejected' && <span className="ml-2 rounded bg-red-100 px-1.5 py-0.5 text-[10px] font-semibold text-red-700 dark:bg-red-900/40 dark:text-red-300">rejeitada</span>}
                    </div>
                    <span className="font-semibold tabular-nums">{f.totalCents != null ? formatCents(f.totalCents) : '—'}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </Modal>
  );
}

function HistoricoStat({ label, value, sub, tone = 'default' }: { label: string; value: string; sub?: string; tone?: 'default' | 'warning' }) {
  return (
    <div className={`rounded-lg border p-3 ${tone === 'warning' ? 'border-amber-200 bg-amber-50 dark:border-amber-900/60 dark:bg-amber-950/25' : 'border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900'}`}>
      <div className={`text-base font-semibold tabular-nums ${tone === 'warning' ? 'text-amber-700 dark:text-amber-300' : 'text-zinc-950 dark:text-zinc-50'}`}>{value}</div>
      <div className="text-[11px] text-zinc-500">{label}</div>
      {sub && <div className="text-[10px] text-zinc-400">{sub}</div>}
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
