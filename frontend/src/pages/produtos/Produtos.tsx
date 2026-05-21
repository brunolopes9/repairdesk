import { useEffect, useMemo, useRef, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, EyeOff, Plus, Search, Smartphone, Trash2, Upload, XCircle } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonRow, StatusBadge } from '../../components/ui';
import { toast } from '../../lib/toast';
import {
  PRODUCT_CATEGORY,
  PRODUCT_CATEGORY_LABEL,
  PRODUCT_GRADING,
  PRODUCT_GRADING_LABEL,
  PRODUCT_SUPPLY_TYPE,
  PRODUCT_SUPPLY_TYPE_LABEL,
  productsApi,
  type ImportProductsResponse,
  type ProductCategory,
  type ProductGrading,
  type ProductImageWriteRequest,
  type ProductSupplyType,
  type ProductWriteRequest,
} from '../../lib/products/api';
import { fornecedoresApi } from '../../lib/fornecedores/api';
import { formatCents, parseEuros } from '../../lib/money';

const emptyForm = (): ProductWriteRequest => ({
  sku: null,
  slug: null,
  brand: '',
  model: '',
  storage: null,
  color: null,
  grading: PRODUCT_GRADING.Novo,
  supplyType: PRODUCT_SUPPLY_TYPE.Stock,
  // Sprint 151
  category: PRODUCT_CATEGORY.Phone,
  dropshipSupplierSku: null,
  priceCents: 0,
  compareAtPriceCents: null,
  stockQuantity: 0,
  stockMinima: 0,
  custoUnitarioCents: 0,
  descriptionMarkdown: null,
  attributesJson: null,
  seoTitle: null,
  seoDescription: null,
  openBoxReason: null,
  active: true,
  mostrarLojaOnline: true,
  fornecedorId: null,
  images: [],
});

export default function Produtos() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [includeInactive, setIncludeInactive] = useState(false);
  const list = useQuery({
    queryKey: ['products', search, includeInactive],
    queryFn: () => productsApi.list({ search: search.trim() || undefined, includeInactive, pageSize: 100 }),
    placeholderData: keepPreviousData,
  });
  const fornecedores = useQuery({
    queryKey: ['fornecedores'],
    queryFn: () => fornecedoresApi.list(false),
    staleTime: 5 * 60_000,
  });

  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<ProductWriteRequest>(emptyForm);
  const [priceStr, setPriceStr] = useState('0,00');
  const [custoStr, setCustoStr] = useState('0,00');
  const [newImageUrl, setNewImageUrl] = useState('');
  // Sprint 153b: importer CSV Molano UI.
  const [importOpen, setImportOpen] = useState(false);
  const [importFornecedorId, setImportFornecedorId] = useState('');
  const [importResult, setImportResult] = useState<ImportProductsResponse | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const editingQuery = useQuery({
    queryKey: ['product', editingId],
    queryFn: () => productsApi.get(editingId!),
    enabled: !!editingId,
  });

  useEffect(() => {
    if (editingQuery.data) {
      const p = editingQuery.data;
      setForm({
        sku: p.sku,
        slug: p.slug,
        brand: p.brand,
        model: p.model,
        storage: p.storage,
        color: p.color,
        grading: p.grading,
        supplyType: p.supplyType,
        category: p.category,
        dropshipSupplierSku: p.dropshipSupplierSku,
        priceCents: p.priceCents,
        compareAtPriceCents: p.compareAtPriceCents,
        stockQuantity: p.stockQuantity,
        stockMinima: p.stockMinima,
        custoUnitarioCents: p.custoUnitarioCents,
        descriptionMarkdown: p.descriptionMarkdown,
        attributesJson: p.attributesJson,
        seoTitle: p.seoTitle,
        seoDescription: p.seoDescription,
        openBoxReason: p.openBoxReason,
        active: p.active,
        mostrarLojaOnline: p.mostrarLojaOnline,
        fornecedorId: p.fornecedorId,
        images: p.images.map((i) => ({ url: i.url, alt: i.alt, ordem: i.ordem, isCurated: i.isCurated })),
      });
      setPriceStr((p.priceCents / 100).toFixed(2).replace('.', ','));
      setCustoStr((p.custoUnitarioCents / 100).toFixed(2).replace('.', ','));
    }
  }, [editingQuery.data]);

  function openCreate() {
    setEditingId(null);
    setForm(emptyForm());
    setPriceStr('0,00');
    setCustoStr('0,00');
    setOpen(true);
  }
  function openEdit(id: string) {
    setEditingId(id);
    setOpen(true);
  }

  const save = useMutation({
    mutationFn: () => {
      const payload: ProductWriteRequest = {
        ...form,
        priceCents: parseEuros(priceStr) ?? 0,
        custoUnitarioCents: parseEuros(custoStr) ?? 0,
        brand: form.brand.trim(),
        model: form.model.trim(),
      };
      return editingId ? productsApi.update(editingId, payload) : productsApi.create(payload);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['products'] });
      setOpen(false);
      toast.success(editingId ? 'Produto atualizado.' : 'Produto criado.');
    },
    onError: (e) => toast.fromError(e, 'Erro ao guardar.'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => productsApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['products'] });
      toast.success('Produto removido.');
    },
    onError: (e) => toast.fromError(e, 'Erro ao remover.'),
  });

  // Sprint 153b: import CSV Molano. Lê o ficheiro como texto + envia ao backend.
  const importMolano = useMutation({
    mutationFn: async (vars: { fornecedorId: string; file: File }) => {
      const csv = await vars.file.text();
      return productsApi.importMolano(vars.fornecedorId, csv);
    },
    onSuccess: (result) => {
      setImportResult(result);
      qc.invalidateQueries({ queryKey: ['products'] });
      toast.success(
        `Importação concluída`,
        `${result.created} criados, ${result.updated} actualizados, ${result.skipped} ignorados, ${result.errors.length} erro(s).`,
      );
    },
    onError: (e) => toast.fromError(e, 'Erro ao importar CSV.'),
  });

  const items = list.data?.items ?? [];
  const canSubmit = useMemo(() => form.brand.trim().length > 0 && form.model.trim().length > 0, [form]);

  return (
    <div className="space-y-5">
      <PageHeader
        title="Produtos"
        description="Telemóveis revendidos (Molano, Tudo4Mobile, etc) para a loja online. Distintos de Peças (peças técnicas em Stock)."
        meta={<span className="text-sm text-zinc-500">{list.data?.total ?? 0} produto(s)</span>}
        actions={<>
          <Button leftIcon={<Upload size={15} />} variant="secondary" onClick={() => { setImportResult(null); setImportFornecedorId(''); setImportOpen(true); }}>
            Importar CSV Molano
          </Button>
          <Button leftIcon={<Plus size={15} />} onClick={openCreate}>Novo produto</Button>
        </>}
      />

      <section className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex flex-wrap items-center gap-3 border-b border-zinc-100 px-4 py-2 text-xs dark:border-zinc-800">
          <div className="relative flex-1">
            <Search size={13} className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-zinc-400" />
            <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="iPhone 12, Samsung A15…" className={`${inputCls} pl-8`} />
          </div>
          <label className="inline-flex cursor-pointer items-center gap-1.5 text-zinc-600 dark:text-zinc-300">
            <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} />
            Mostrar inactivos
          </label>
        </div>
        <table className="w-full text-sm">
          <thead className="bg-zinc-50 text-left text-xs uppercase tracking-wider text-zinc-500 dark:bg-zinc-800/60">
            <tr>
              <th className="px-4 py-2.5">Produto</th>
              <th className="px-4 py-2.5">Condição</th>
              <th className="px-4 py-2.5">Stock</th>
              <th className="px-4 py-2.5">Preço</th>
              <th className="px-4 py-2.5">Estado</th>
              <th className="px-4 py-2.5" />
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {list.isLoading && Array.from({ length: 3 }).map((_, i) => <tr key={i}><td colSpan={6}><SkeletonRow columns={6} /></td></tr>)}
            {!list.isLoading && items.map((p) => (
              <tr key={p.id} onClick={() => openEdit(p.id)} className="cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                <td className="px-4 py-3">
                  <div className="font-medium">{p.brand} {p.model}</div>
                  <div className="text-[11px] text-zinc-500">
                    {[p.storage, p.color].filter(Boolean).join(' · ')}
                    {p.fornecedorNome && <> · <span className="text-zinc-600">{p.fornecedorNome}</span></>}
                  </div>
                </td>
                <td className="px-4 py-3"><StatusBadge tone="zinc">{PRODUCT_GRADING_LABEL[p.grading]}</StatusBadge></td>
                <td className="px-4 py-3 text-xs">
                  {p.supplyType === PRODUCT_SUPPLY_TYPE.Stock ? `${p.stockQuantity} un.` : 'Dropship'}
                </td>
                <td className="px-4 py-3 font-medium">{formatCents(p.priceCents)}</td>
                <td className="px-4 py-3">
                  <div className="flex flex-col gap-0.5 text-[11px]">
                    {p.active
                      ? <span className="inline-flex items-center gap-1 text-emerald-700 dark:text-emerald-400"><CheckCircle2 size={11} /> Activo</span>
                      : <span className="inline-flex items-center gap-1 text-zinc-500"><XCircle size={11} /> Inactivo</span>}
                    {!p.mostrarLojaOnline && (
                      <span className="inline-flex items-center gap-1 text-amber-700 dark:text-amber-400"><EyeOff size={11} /> Oculto da loja</span>
                    )}
                  </div>
                </td>
                <td className="px-4 py-3 text-right">
                  <button
                    type="button"
                    onClick={(e) => { e.stopPropagation(); if (confirm(`Remover ${p.brand} ${p.model}?`)) remove.mutate(p.id); }}
                    className="rounded-md p-1 text-zinc-500 hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-950/40"
                    aria-label="Remover"
                  >
                    <Trash2 size={15} />
                  </button>
                </td>
              </tr>
            ))}
            {!list.isLoading && items.length === 0 && (
              <tr><td colSpan={6} className="p-6">
                <EmptyState
                  icon={Smartphone}
                  title="Sem produtos"
                  description="Cria iPhone 12, Samsung A15 ou outros telemóveis para revender. Aparecem no catálogo da loja online se mostrarLojaOnline=true."
                />
              </td></tr>
            )}
          </tbody>
        </table>
      </section>

      <Modal open={open} title={editingId ? 'Editar produto' : 'Novo produto'} onClose={() => setOpen(false)}>
        {editingId && editingQuery.isLoading ? (
          <div className="py-6 text-center text-sm text-zinc-500">A carregar…</div>
        ) : (
          <form onSubmit={(e) => { e.preventDefault(); if (canSubmit) save.mutate(); }} className="space-y-4">

            {/* Básico */}
            <details open className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
              <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-zinc-600">Básico</summary>
              <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
                <Field label="Marca *">
                  <input value={form.brand} onChange={(e) => setForm({ ...form, brand: e.target.value })} className={inputCls} placeholder="Apple" />
                </Field>
                <Field label="Modelo *">
                  <input value={form.model} onChange={(e) => setForm({ ...form, model: e.target.value })} className={inputCls} placeholder="iPhone 12" />
                </Field>
                <Field label="Armazenamento">
                  <input value={form.storage ?? ''} onChange={(e) => setForm({ ...form, storage: e.target.value || null })} className={inputCls} placeholder="128GB" />
                </Field>
                <Field label="Cor">
                  <input value={form.color ?? ''} onChange={(e) => setForm({ ...form, color: e.target.value || null })} className={inputCls} placeholder="Black" />
                </Field>
                <Field label="SKU" hint="Deixa vazio para gerar automaticamente.">
                  <input value={form.sku ?? ''} onChange={(e) => setForm({ ...form, sku: e.target.value || null })} className={inputCls} />
                </Field>
                <Field label="Slug" hint="URL-friendly. Auto-gerado de Brand+Model+Storage+Cor+Grading.">
                  <input value={form.slug ?? ''} onChange={(e) => setForm({ ...form, slug: e.target.value || null })} className={inputCls} placeholder="iphone-12-128gb-black-grade-a" />
                </Field>
              </div>
            </details>

            {/* Condição & fornecedor */}
            <details open className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
              <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-zinc-600">
                Condição e fornecedor
                {/* Sprint 152: badge "Stock virtual" quando Dropship — distingue claramente do stock físico. */}
                {form.supplyType === PRODUCT_SUPPLY_TYPE.Dropship && (
                  <span className="ml-2 rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-medium text-amber-800 dark:bg-amber-900/40 dark:text-amber-200">
                    Stock virtual
                  </span>
                )}
              </summary>
              <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
                <Field label="Categoria" hint="Sprint 151: separar telemóveis de acessórios para filtros loja.">
                  <select value={form.category} onChange={(e) => setForm({ ...form, category: Number(e.target.value) as ProductCategory })} className={inputCls}>
                    {Object.values(PRODUCT_CATEGORY).map((v) => (
                      <option key={v} value={v}>{PRODUCT_CATEGORY_LABEL[v]}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Grading">
                  <select value={form.grading} onChange={(e) => setForm({ ...form, grading: Number(e.target.value) as ProductGrading })} className={inputCls}>
                    {Object.values(PRODUCT_GRADING).map((v) => (
                      <option key={v} value={v}>{PRODUCT_GRADING_LABEL[v]}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Tipo de fornecimento">
                  <select value={form.supplyType} onChange={(e) => setForm({ ...form, supplyType: Number(e.target.value) as ProductSupplyType })} className={inputCls}>
                    {Object.values(PRODUCT_SUPPLY_TYPE).map((v) => (
                      <option key={v} value={v}>{PRODUCT_SUPPLY_TYPE_LABEL[v]}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Fornecedor">
                  <select value={form.fornecedorId ?? ''} onChange={(e) => setForm({ ...form, fornecedorId: e.target.value || null })} className={inputCls}>
                    <option value="">— sem fornecedor —</option>
                    {(fornecedores.data ?? []).map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
                  </select>
                </Field>
                {form.supplyType === PRODUCT_SUPPLY_TYPE.Dropship && (
                  <Field label="SKU do fornecedor" hint="Para reconciliação com CSV importer (Molano etc).">
                    <input value={form.dropshipSupplierSku ?? ''} onChange={(e) => setForm({ ...form, dropshipSupplierSku: e.target.value || null })} className={inputCls} placeholder="MOL-IP12-128-BLK" />
                  </Field>
                )}
              </div>
            </details>

            {/* Sprint 152: Loja online — todos os campos shop-specific juntos. */}
            <details className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
              <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-zinc-600">
                Loja online
                {form.mostrarLojaOnline ? (
                  <span className="ml-2 rounded bg-emerald-100 px-1.5 py-0.5 text-[10px] font-medium text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200">
                    Visível
                  </span>
                ) : (
                  <span className="ml-2 rounded bg-zinc-200 px-1.5 py-0.5 text-[10px] font-medium text-zinc-700 dark:bg-zinc-700 dark:text-zinc-300">
                    Oculto
                  </span>
                )}
              </summary>
              <div className="mt-3 space-y-3">
                <label className="inline-flex cursor-pointer items-center gap-2 text-sm">
                  <input type="checkbox" checked={form.mostrarLojaOnline} onChange={(e) => setForm({ ...form, mostrarLojaOnline: e.target.checked })} />
                  <strong>Publicar na loja online</strong>
                </label>
                <p className="-mt-2 text-xs text-zinc-500">Quando ON, este produto aparece em lopestech.pt. Webhook é disparado automaticamente ao mudar.</p>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <Field label="Preço comparativo (€)" hint="Sprint 151: preço antes promoção (strike-through). Deixa vazio se não há promoção.">
                    <input
                      type="number"
                      step="0.01"
                      value={form.compareAtPriceCents != null ? (form.compareAtPriceCents / 100).toFixed(2) : ''}
                      onChange={(e) => setForm({ ...form, compareAtPriceCents: e.target.value ? Math.round(Number(e.target.value) * 100) : null })}
                      className={inputCls}
                      placeholder="ex: 349,90"
                    />
                  </Field>
                  {form.grading === PRODUCT_GRADING.OpenBox && (
                    <Field label="Razão Open Box" hint="Texto curto que aparece na PDP loja.">
                      <input
                        value={form.openBoxReason ?? ''}
                        onChange={(e) => setForm({ ...form, openBoxReason: e.target.value || null })}
                        className={inputCls}
                        placeholder="Devolução cliente, embalagem aberta"
                        maxLength={500}
                      />
                    </Field>
                  )}
                </div>
              </div>
            </details>

            {/* Preço & stock */}
            <details open className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
              <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-zinc-600">Preço e stock</summary>
              <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-3">
                <Field label="Preço de venda (€)">
                  <input value={priceStr} onChange={(e) => setPriceStr(e.target.value)} className={inputCls} placeholder="299,99" />
                </Field>
                <Field label="Custo unitário (€)" hint="Interno — não exposto na loja.">
                  <input value={custoStr} onChange={(e) => setCustoStr(e.target.value)} className={inputCls} placeholder="180,00" />
                </Field>
                <Field label="Stock (un.)" hint="Para Dropship usa valor alto.">
                  <input type="number" value={form.stockQuantity} onChange={(e) => setForm({ ...form, stockQuantity: Number(e.target.value) || 0 })} className={inputCls} />
                </Field>
                <Field label="Stock mínimo (alerta)">
                  <input type="number" value={form.stockMinima} onChange={(e) => setForm({ ...form, stockMinima: Number(e.target.value) || 0 })} className={inputCls} />
                </Field>
              </div>
            </details>

            {/* Descrição & SEO */}
            <details className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
              <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-zinc-600">Descrição e SEO</summary>
              <div className="mt-3 space-y-3">
                <Field label="Descrição (Markdown)">
                  <textarea rows={4} value={form.descriptionMarkdown ?? ''} onChange={(e) => setForm({ ...form, descriptionMarkdown: e.target.value || null })} className={`${inputCls} resize-none`} placeholder="Estado impecável, 100% bateria, garantia 18 meses..." />
                </Field>
                <Field label="Atributos (JSON)" hint='Schema livre. Ex: {"ram":"4GB","processador":"A15 Bionic"}'>
                  <textarea rows={2} value={form.attributesJson ?? ''} onChange={(e) => setForm({ ...form, attributesJson: e.target.value || null })} className={`${inputCls} resize-none font-mono text-xs`} />
                </Field>
                <Field label="SEO Title">
                  <input value={form.seoTitle ?? ''} onChange={(e) => setForm({ ...form, seoTitle: e.target.value || null })} className={inputCls} maxLength={200} />
                </Field>
                <Field label="SEO Description">
                  <textarea rows={2} value={form.seoDescription ?? ''} onChange={(e) => setForm({ ...form, seoDescription: e.target.value || null })} className={`${inputCls} resize-none`} maxLength={500} />
                </Field>
              </div>
            </details>

            {/* Imagens */}
            <details className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
              <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-zinc-600">
                Imagens ({form.images.length})
              </summary>
              <div className="mt-3 space-y-2">
                <div className="flex gap-2">
                  <input
                    value={newImageUrl}
                    onChange={(e) => setNewImageUrl(e.target.value)}
                    placeholder="https://cdn.lopestech.pt/..."
                    className={`${inputCls} flex-1`}
                  />
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={!newImageUrl.trim()}
                    onClick={() => {
                      const newImage: ProductImageWriteRequest = {
                        url: newImageUrl.trim(),
                        alt: null,
                        ordem: form.images.length,
                        // Sprint 151: upload manual = curada (Bruno verificou).
                        isCurated: true,
                      };
                      setForm({ ...form, images: [...form.images, newImage] });
                      setNewImageUrl('');
                    }}
                  >
                    Adicionar
                  </Button>
                </div>
                {form.images.length === 0 && (
                  <p className="text-[11px] text-zinc-500">URLs externas (R2/S3/Imgur). Upload nativo fica para sprint futuro.</p>
                )}
                <ul className="space-y-1">
                  {form.images.map((img, idx) => (
                    <li key={idx} className="flex items-center gap-2 rounded-md border border-zinc-200 bg-zinc-50 px-2 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-900">
                      <span className="text-zinc-400">{idx + 1}.</span>
                      <span className="flex-1 truncate font-mono">{img.url}</span>
                      <button
                        type="button"
                        onClick={() => setForm({ ...form, images: form.images.filter((_, i) => i !== idx) })}
                        className="text-rose-600 hover:text-rose-700"
                      >
                        <Trash2 size={12} />
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            </details>

            {/* Toggle Activo — Sprint 152: "Mostrar na loja online" mudou para tab Loja online. */}
            <div className="flex flex-wrap gap-3 rounded-lg border border-zinc-200 bg-zinc-50/50 p-3 text-xs dark:border-zinc-700 dark:bg-zinc-900/50">
              <label className="inline-flex cursor-pointer items-center gap-2">
                <input type="checkbox" checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />
                Activo
              </label>
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="ghost" onClick={() => setOpen(false)}>Cancelar</Button>
              <Button type="submit" disabled={!canSubmit || save.isPending}>{editingId ? 'Guardar' : 'Criar'}</Button>
            </div>
          </form>
        )}
      </Modal>

      {/* Sprint 153b: modal upload CSV Molano. Idempotente — re-importar mesmo CSV não duplica. */}
      <Modal open={importOpen} title="Importar CSV de fornecedor (Molano, dropship)" onClose={() => setImportOpen(false)}>
        <div className="space-y-4">
          <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3 text-xs text-zinc-600 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-300">
            <p><strong>Como funciona:</strong></p>
            <ul className="mt-1 list-inside list-disc space-y-0.5">
              <li>Upsert idempotente por <code>(Fornecedor, SKU)</code>. Re-importar não duplica.</li>
              <li>Default: <code>SupplyType=Dropship</code>, <code>MostrarLojaOnline=false</code>. Tu decides depois quais publicar na loja.</li>
              <li>Imagens vindas do CSV ficam <strong>raw</strong> — substitui por curadas na ficha do produto.</li>
            </ul>
            <p className="mt-2"><strong>Header CSV aceite:</strong> <code>sku, brand, model, price</code> (obrigatórios) + <code>storage, color, grading, stock, images, cost</code> (opcionais).</p>
          </div>

          <Field label="Fornecedor *">
            <select
              value={importFornecedorId}
              onChange={(e) => setImportFornecedorId(e.target.value)}
              className={inputCls}
            >
              <option value="">— escolhe um fornecedor —</option>
              {(fornecedores.data ?? []).map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
          </Field>

          <input
            ref={fileInputRef}
            type="file"
            accept=".csv,text/csv"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (!file || !importFornecedorId) return;
              importMolano.mutate({ fornecedorId: importFornecedorId, file });
              if (fileInputRef.current) fileInputRef.current.value = '';
            }}
          />
          <Button
            type="button"
            leftIcon={<Upload size={15} />}
            disabled={!importFornecedorId || importMolano.isPending}
            onClick={() => fileInputRef.current?.click()}
          >
            {importMolano.isPending ? 'A importar…' : 'Escolher CSV…'}
          </Button>

          {importResult && (
            <div className="space-y-2 rounded-lg border border-zinc-200 p-3 dark:border-zinc-700">
              <div className="grid grid-cols-4 gap-2 text-center text-xs">
                <div className="rounded bg-emerald-50 p-2 dark:bg-emerald-950/40">
                  <div className="text-lg font-bold text-emerald-700 dark:text-emerald-300">{importResult.created}</div>
                  <div className="text-emerald-600 dark:text-emerald-400">Criados</div>
                </div>
                <div className="rounded bg-blue-50 p-2 dark:bg-blue-950/40">
                  <div className="text-lg font-bold text-blue-700 dark:text-blue-300">{importResult.updated}</div>
                  <div className="text-blue-600 dark:text-blue-400">Actualizados</div>
                </div>
                <div className="rounded bg-zinc-100 p-2 dark:bg-zinc-800">
                  <div className="text-lg font-bold text-zinc-700 dark:text-zinc-300">{importResult.skipped}</div>
                  <div className="text-zinc-500">Ignorados</div>
                </div>
                <div className="rounded bg-rose-50 p-2 dark:bg-rose-950/40">
                  <div className="text-lg font-bold text-rose-700 dark:text-rose-300">{importResult.errors.length}</div>
                  <div className="text-rose-600 dark:text-rose-400">Erros</div>
                </div>
              </div>
              {importResult.errors.length > 0 && (
                <details className="text-xs">
                  <summary className="cursor-pointer font-medium text-rose-700 dark:text-rose-400">Ver erros</summary>
                  <ul className="mt-2 space-y-1 font-mono">
                    {importResult.errors.slice(0, 20).map((err, i) => (
                      <li key={i} className="rounded bg-rose-50 px-2 py-1 dark:bg-rose-950/40">
                        Linha {err.line} · <strong>{err.field}</strong> · {err.message}
                        {err.sku && <span className="text-zinc-500"> · SKU {err.sku}</span>}
                      </li>
                    ))}
                    {importResult.errors.length > 20 && (
                      <li className="text-zinc-500">… e mais {importResult.errors.length - 20} erros.</li>
                    )}
                  </ul>
                </details>
              )}
            </div>
          )}

          <div className="flex justify-end pt-2">
            <Button type="button" variant="ghost" onClick={() => setImportOpen(false)}>Fechar</Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

const inputCls =
  'w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 dark:border-zinc-700 dark:bg-zinc-900';

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <label className="text-[11px] font-medium uppercase tracking-wide text-zinc-500">{label}</label>
      {children}
      {hint && <p className="text-[10px] text-zinc-500">{hint}</p>}
    </div>
  );
}
