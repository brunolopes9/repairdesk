import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { AlertTriangle, History, PackagePlus, PackageSearch, Pencil, Search, SlidersHorizontal, Trash2, Upload } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonCard, SkeletonTable, StatusBadge } from '../../components/ui';
import { formatCents, formatDate, parseEuros } from '../../lib/money';
import { toast } from '../../lib/toast';
import { stockApi } from '../../lib/stock/api';
import { fornecedoresApi } from '../../lib/fornecedores/api';
import {
  PART_CATEGORIA,
  PART_CATEGORIA_LABEL,
  PART_MOVIMENTO_LABEL,
  PART_MOVIMENTO_MOTIVO,
  type Part,
  type PartCategoria,
  type PartForm,
  type PartMovimento,
  type PartMovimentoMotivo,
} from '../../lib/stock/types';

const PAGE_SIZE = 50;

export default function Stock() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [categoria, setCategoria] = useState<PartCategoria | null>(null);
  const [marca, setMarca] = useState<string | null>(null);
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [createOpen, setCreateOpen] = useState(false);
  const [pdfTextSnippet, setPdfTextSnippet] = useState<string | null>(null);
  const [pdfSuggestions, setPdfSuggestions] = useState<import('../../lib/stock/api').PdfParseSuggestions | null>(null);
  const [editing, setEditing] = useState<Part | null>(null);
  const [adjusting, setAdjusting] = useState<Part | null>(null);
  const [historyPart, setHistoryPart] = useState<Part | null>(null);
  const [importOpen, setImportOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<Part | null>(null);

  const list = useQuery({
    queryKey: ['stock', search, categoria, marca, lowStockOnly, page],
    queryFn: () => stockApi.list({ q: search, categoria, marca, lowStockOnly, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });

  const marcas = useQuery({
    queryKey: ['stock-marcas'],
    queryFn: () => stockApi.marcas(),
    staleTime: 60_000,
  });

  const remove = useMutation({
    mutationFn: (part: Part) => stockApi.remove(part.id),
    onSuccess: () => {
      toast.success('Peça apagada');
      invalidate();
      setConfirmDelete(null);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível apagar a peça.'),
  });

  function invalidate() {
    qc.invalidateQueries({ queryKey: ['stock'] });
    qc.invalidateQueries({ queryKey: ['stock-marcas'] });
    qc.invalidateQueries({ queryKey: ['dashboard'] });
  }

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const totalStockCents = items.reduce((sum, p) => sum + p.valorTotalStockCents, 0);
  const lowCount = items.filter((p) => p.stockBaixo).length;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock de peças"
        description="Inventário, custos, fornecedores e alertas de stock baixo. Ao usar uma peça numa reparação, o stock decrementa automaticamente."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'peça' : 'peças'} · valor nesta página: {formatCents(totalStockCents)}</span>}
        actions={
          <>
            <Button type="button" variant="secondary" onClick={() => setImportOpen(true)} leftIcon={<Upload size={15} />}>
              Importar CSV
            </Button>
            <label className="inline-flex h-10 cursor-pointer items-center gap-1.5 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm font-medium text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200">
              <Upload size={15} /> Importar PDF
              <input
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                onChange={async (e) => {
                  const f = e.target.files?.[0];
                  if (!f) return;
                  try {
                    const result = await stockApi.extractPdf(f);
                    setPdfTextSnippet(result.text);
                    setPdfSuggestions(result.suggestions);
                    setCreateOpen(true);
                    const supplier = result.suggestions?.supplierName;
                    if (supplier && result.suggestions?.confidence === 2) {
                      toast.success(`PDF lido: ${supplier}`, `Order ${result.suggestions.orderId ?? '?'} · campos pré-preenchidos.`);
                    } else {
                      toast.success('PDF lido', `${result.pagesRead} página(s) extraídas.`);
                    }
                  } catch (err) {
                    toast.fromError(err, 'Não foi possível ler o PDF.');
                  } finally {
                    e.target.value = '';
                  }
                }}
              />
            </label>
            <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<PackagePlus size={15} />}>
              Nova peça
            </Button>
          </>
        }
      />
      <header className="space-y-3">

        <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
          <select
            value={categoria ?? ''}
            onChange={(e) => { setCategoria(e.target.value === '' ? null : (Number(e.target.value) as PartCategoria)); setPage(1); }}
            className={inputCls}
          >
            <option value="">Todas categorias</option>
            {Object.values(PART_CATEGORIA).map((value) => (
              <option key={value} value={value}>{PART_CATEGORIA_LABEL[value]}</option>
            ))}
          </select>
          <select
            value={marca ?? ''}
            onChange={(e) => { setMarca(e.target.value || null); setPage(1); }}
            className={inputCls}
          >
            <option value="">Todas marcas</option>
            {marcas.data?.map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
          <label className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950">
            <input
              type="checkbox"
              checked={lowStockOnly}
              onChange={(e) => { setLowStockOnly(e.target.checked); setPage(1); }}
              className="scale-125 sm:scale-100"
            />
            Só stock baixo
          </label>
          <input
            type="search"
            placeholder="Pesquisar SKU, nome, modelo, fornecedor..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="min-h-11 min-w-0 flex-1 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950"
          />
        </div>
      </header>

      {(lowCount > 0 || lowStockOnly) && (
        <div className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200">
          <AlertTriangle size={16} />
          {lowCount > 0 ? `${lowCount} peça(s) com stock baixo nesta página.` : 'Sem peças em stock baixo nos filtros actuais.'}
        </div>
      )}

      {list.isLoading ? (
        <SkeletonTable columns={9} rows={8} minWidth="min-w-[920px]" />
      ) : (
      <section className="overflow-x-auto rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <table className="min-w-[920px] text-sm">
          <thead className="bg-zinc-50 text-left text-xs text-zinc-500 dark:bg-zinc-950">
            <tr>
              <th className="px-3 py-2">SKU</th>
              <th className="px-3 py-2">Peça</th>
              <th className="px-3 py-2">Marca / Modelo</th>
              <th className="px-3 py-2 text-right">Stock</th>
              <th className="px-3 py-2 text-right">Mín.</th>
              <th className="px-3 py-2 text-right">Custo</th>
              <th className="px-3 py-2 text-right">Valor stock</th>
              <th className="px-3 py-2">Local</th>
              <th className="px-3 py-2"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {items.map((part) => (
              <tr key={part.id} className={`hover:bg-zinc-50 dark:hover:bg-zinc-800/50 ${!part.activo ? 'opacity-50' : ''}`}>
                <td className="px-3 py-2 font-mono text-xs text-zinc-500">{part.sku ?? '—'}</td>
                <td className="px-3 py-2">
                  <button type="button" onClick={() => setHistoryPart(part)} className="text-left font-medium hover:underline">
                    {part.nome}
                  </button>
                  <div className="mt-1 flex flex-wrap gap-1">
                    <StatusBadge tone="zinc">{PART_CATEGORIA_LABEL[part.categoria]}</StatusBadge>
                    {part.stockBaixo && <StatusBadge tone="amber" icon={<AlertTriangle size={11} />}>Stock baixo</StatusBadge>}
                    {!part.activo && <StatusBadge tone="rose">Inactiva</StatusBadge>}
                  </div>
                </td>
                <td className="px-3 py-2">
                  <div>{part.marca ?? '—'}</div>
                  <div className="text-xs text-zinc-500">{part.modelo ?? '—'}</div>
                </td>
                <td className={`px-3 py-2 text-right font-semibold tabular-nums ${part.stockBaixo ? 'text-amber-700 dark:text-amber-300' : ''}`}>{part.qtdStock}</td>
                <td className="px-3 py-2 text-right tabular-nums text-zinc-500">{part.qtdMinima}</td>
                <td className="px-3 py-2 text-right tabular-nums">{formatCents(part.custoUnitarioCents)}</td>
                <td className="px-3 py-2 text-right font-medium tabular-nums">{formatCents(part.valorTotalStockCents)}</td>
                <td className="px-3 py-2 text-xs text-zinc-500">{part.localArmazenamento ?? '—'}</td>
                <td className="px-3 py-2">
                  <div className="flex justify-end gap-1">
                    <Button type="button" variant="icon" title="Ajustar stock" onClick={() => setAdjusting(part)}>
                      <SlidersHorizontal size={15} />
                    </Button>
                    <Button type="button" variant="icon" title="Histórico" onClick={() => setHistoryPart(part)}>
                      <History size={15} />
                    </Button>
                    <Button type="button" variant="icon" title="Editar" onClick={() => setEditing(part)}>
                      <Pencil size={15} />
                    </Button>
                    <Button type="button" variant="icon" title="Apagar" onClick={() => setConfirmDelete(part)}>
                      <Trash2 size={15} />
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
            {items.length === 0 && !list.isLoading && (
              <tr>
                <td colSpan={9} className="px-3 py-2">
                  {search || categoria != null || marca || lowStockOnly ? (
                    <EmptyState
                      icon={Search}
                      title="Sem peças para estes filtros"
                      description="Ajusta os filtros — categoria, marca, search ou stock baixo — ou limpa-os para ver tudo."
                      compact
                    />
                  ) : (
                    <EmptyState
                      icon={PackageSearch}
                      title="Ainda não há peças em stock"
                      description="Adiciona a primeira peça manualmente, ou importa um CSV com SKU, nome, marca, modelo, quantidade e custo."
                      action={
                        <div className="flex flex-wrap justify-center gap-2">
                          <Button type="button" variant="secondary" leftIcon={<Upload size={15} />} onClick={() => setImportOpen(true)}>Importar CSV</Button>
                          <Button type="button" leftIcon={<PackagePlus size={15} />} onClick={() => setCreateOpen(true)}>Criar peça</Button>
                        </div>
                      }
                    />
                  )}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </section>
      )}

      {lastPage > 1 && (
        <div className="flex items-center justify-between text-xs text-zinc-500">
          <Button type="button" variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
          <span>{page} / {lastPage}</span>
          <Button type="button" variant="ghost" size="sm" disabled={page >= lastPage} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
        </div>
      )}

      <PartFormModal
        open={createOpen}
        onClose={() => { setCreateOpen(false); setPdfTextSnippet(null); setPdfSuggestions(null); }}
        onSaved={() => { invalidate(); setCreateOpen(false); setPdfTextSnippet(null); setPdfSuggestions(null); }}
        pdfReferenceText={pdfTextSnippet}
        pdfSuggestions={pdfSuggestions}
      />
      <PartFormModal open={!!editing} editing={editing} onClose={() => setEditing(null)} onSaved={() => { invalidate(); setEditing(null); }} />
      <AdjustStockModal part={adjusting} onClose={() => setAdjusting(null)} onSaved={() => { invalidate(); setAdjusting(null); }} />
      <HistoryModal part={historyPart} onClose={() => setHistoryPart(null)} />
      <ImportCsvModal open={importOpen} onClose={() => setImportOpen(false)} onDone={invalidate} />

      <Modal
        open={!!confirmDelete}
        title="Apagar peça"
        onClose={() => setConfirmDelete(null)}
        footer={<>
          <Button type="button" variant="ghost" onClick={() => setConfirmDelete(null)}>Cancelar</Button>
          <Button type="button" variant="danger" loading={remove.isPending} onClick={() => confirmDelete && remove.mutate(confirmDelete)}>Apagar</Button>
        </>}
      >
        <p className="text-sm">
          Apagar <strong>{confirmDelete?.nome}</strong>? A peça fica oculta por soft delete.
        </p>
      </Modal>
    </div>
  );
}

function PartFormModal({ open, editing, onClose, onSaved, pdfReferenceText, pdfSuggestions }: { open: boolean; editing?: Part | null; onClose: () => void; onSaved: () => void; pdfReferenceText?: string | null; pdfSuggestions?: import('../../lib/stock/api').PdfParseSuggestions | null }) {
  const qc = useQueryClient();
  // Sprint 124: lookup de fornecedores existentes para detectar se a sugestão precisa de ser criada.
  const fornecedoresQuery = useQuery({
    queryKey: ['fornecedores', false],
    queryFn: () => fornecedoresApi.list(false),
    enabled: !!pdfSuggestions?.supplierName,
    staleTime: 60_000,
  });
  const createFornecedor = useMutation({
    mutationFn: (name: string) => fornecedoresApi.create({
      name, email: null, rmaEmail: null, phone: null, website: null,
      garantiaB2BDiasDefault: null, notas: null, active: true,
    }),
    onSuccess: (f) => {
      qc.invalidateQueries({ queryKey: ['fornecedores'] });
      toast.success(`Fornecedor "${f.name}" criado.`);
    },
    onError: (e) => toast.fromError(e, 'Não foi possível criar fornecedor.'),
  });
  const suggestedSupplierExists = pdfSuggestions?.supplierName
    && (fornecedoresQuery.data ?? []).some((f) => f.name.toLowerCase() === pdfSuggestions.supplierName!.toLowerCase());
  const [form, setForm] = useState<PartForm>({
    sku: null,
    nome: '',
    categoria: PART_CATEGORIA.Outro,
    marca: null,
    modelo: null,
    priceTableEntryId: null,
    qtdStock: 0,
    qtdMinima: 0,
    custoUnitarioCents: 0,
    fornecedor: null,
    localArmazenamento: null,
    notas: null,
    mostrarLojaOnline: false,
  });
  const [activo, setActivo] = useState(true);
  const [stockStr, setStockStr] = useState('0');
  const [minStr, setMinStr] = useState('0');
  const [custoStr, setCustoStr] = useState('0,00');

  useEffect(() => {
    if (editing) {
      setForm({
        sku: editing.sku,
        nome: editing.nome,
        categoria: editing.categoria,
        marca: editing.marca,
        modelo: editing.modelo,
        priceTableEntryId: editing.priceTableEntryId,
        qtdStock: editing.qtdStock,
        qtdMinima: editing.qtdMinima,
        custoUnitarioCents: editing.custoUnitarioCents,
        fornecedor: editing.fornecedor,
        localArmazenamento: editing.localArmazenamento,
        notas: editing.notas,
        mostrarLojaOnline: editing.mostrarLojaOnline,
      });
      setActivo(editing.activo);
      setStockStr(String(editing.qtdStock));
      setMinStr(String(editing.qtdMinima));
      setCustoStr((editing.custoUnitarioCents / 100).toFixed(2).replace('.', ','));
    } else if (open) {
      // Sprint 124/134: pré-preencher com sugestões do PDF se disponíveis.
      // Sprint 134: brand/model agora vêm do parser; categoria adivinhada por keywords.
      const s = pdfSuggestions;
      const firstItem = s?.items?.[0];
      const desc = firstItem?.description ?? '';
      const lowerDesc = desc.toLowerCase();
      const categoria: PartCategoria =
        lowerDesc.includes('touch') || lowerDesc.includes('display') || lowerDesc.includes('ecrã') || lowerDesc.includes('ecra') || lowerDesc.includes('lcd') ? PART_CATEGORIA.Ecra
        : lowerDesc.includes('bater') || lowerDesc.includes('battery') ? PART_CATEGORIA.Bateria
        : lowerDesc.includes('cam') ? PART_CATEGORIA.Camara
        : lowerDesc.includes('conector') || lowerDesc.includes('charge') || lowerDesc.includes('carga') ? PART_CATEGORIA.Conector
        : lowerDesc.includes('flex') || lowerDesc.includes('cabo') ? PART_CATEGORIA.CaboFlex
        : lowerDesc.includes('vidro') ? PART_CATEGORIA.VidroTraseiro
        : lowerDesc.includes('capa') || lowerDesc.includes('case') ? PART_CATEGORIA.Acessorio
        : PART_CATEGORIA.Outro;
      setForm({
        sku: null,
        nome: desc,
        categoria,
        marca: firstItem?.brand ?? null,
        modelo: firstItem?.model ?? null,
        priceTableEntryId: null,
        qtdStock: firstItem?.quantity ?? 0,
        qtdMinima: 0,
        custoUnitarioCents: firstItem?.lineTotalCents ?? 0,
        fornecedor: s?.supplierName ?? null,
        localArmazenamento: null,
        notas: s?.orderId ? `Encomenda ${s.orderId}${s.dateAdded ? ` · ${new Date(s.dateAdded).toLocaleDateString('pt-PT')}` : ''}` : null,
        mostrarLojaOnline: false,
      });
      setActivo(true);
      setStockStr(String(firstItem?.quantity ?? 0));
      setMinStr('0');
      setCustoStr(firstItem ? (firstItem.lineTotalCents / 100).toFixed(2).replace('.', ',') : '0,00');
    }
  }, [editing, open, pdfSuggestions]);

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        ...form,
        sku: form.sku?.trim() || null,
        nome: form.nome.trim(),
        marca: form.marca?.trim() || null,
        modelo: form.modelo?.trim() || null,
        fornecedor: form.fornecedor?.trim() || null,
        localArmazenamento: form.localArmazenamento?.trim() || null,
        notas: form.notas?.trim() || null,
        qtdStock: Number(stockStr || 0),
        qtdMinima: Number(minStr || 0),
        custoUnitarioCents: parseEuros(custoStr) ?? 0,
      };
      if (editing) return stockApi.update(editing.id, { ...payload, activo });
      return stockApi.create(payload);
    },
    onSuccess: () => {
      toast.success(editing ? 'Peça guardada' : 'Peça criada');
      onSaved();
    },
    onError: (err) => toast.fromError(err, 'Erro ao guardar peça.'),
  });

  return (
    <Modal
      open={open}
      title={editing ? 'Editar peça' : 'Nova peça'}
      onClose={onClose}
      footer={<>
        <Button type="button" variant="ghost" onClick={onClose}>Cancelar</Button>
        <Button type="button" loading={save.isPending} disabled={!form.nome.trim()} onClick={() => save.mutate()}>
          {editing ? 'Guardar' : 'Criar'}
        </Button>
      </>}
    >
      <div className="space-y-3">
        {pdfSuggestions && pdfSuggestions.confidence > 0 && (
          <div className="rounded-lg border border-emerald-300 bg-emerald-50/50 p-3 dark:border-emerald-800/60 dark:bg-emerald-950/30">
            <div className="text-xs font-medium text-emerald-800 dark:text-emerald-300">
              ✓ Detectei {pdfSuggestions.supplierName ?? 'fornecedor desconhecido'}
              {pdfSuggestions.orderId && ` · Order ${pdfSuggestions.orderId}`}
              {pdfSuggestions.totalCents !== null && ` · ${formatCents(pdfSuggestions.totalCents)}`}
            </div>
            <p className="mt-1 text-[10px] text-emerald-700 dark:text-emerald-400">
              Campos pré-preenchidos automaticamente. Confirma antes de guardar.
            </p>
            {pdfSuggestions.supplierName && !suggestedSupplierExists && !fornecedoresQuery.isLoading && (
              <button
                type="button"
                onClick={() => createFornecedor.mutate(pdfSuggestions.supplierName!)}
                disabled={createFornecedor.isPending}
                className="mt-2 inline-flex items-center gap-1 rounded-md border border-emerald-400 bg-white px-2 py-1 text-[11px] font-medium text-emerald-700 hover:bg-emerald-50 dark:border-emerald-700 dark:bg-zinc-900 dark:text-emerald-400"
              >
                {createFornecedor.isPending ? 'A criar…' : `+ Criar fornecedor "${pdfSuggestions.supplierName}"`}
              </button>
            )}
            {suggestedSupplierExists && (
              <div className="mt-1 text-[10px] text-emerald-700 dark:text-emerald-400">
                ✓ Fornecedor "{pdfSuggestions.supplierName}" já existe.
              </div>
            )}
          </div>
        )}
        {pdfReferenceText && (
          <details className="rounded-lg border border-brand-200 bg-brand-50/50 p-3 dark:border-brand-800/60 dark:bg-brand-950/30" open>
            <summary className="cursor-pointer text-xs font-medium text-brand-700 dark:text-brand-300">
              Texto extraído do PDF (clica para copiar/colar nos campos abaixo)
            </summary>
            <pre className="mt-2 max-h-48 overflow-auto whitespace-pre-wrap rounded border border-zinc-200 bg-white p-2 font-mono text-[11px] leading-snug dark:border-zinc-700 dark:bg-zinc-950">
              {pdfReferenceText.slice(0, 4000)}
              {pdfReferenceText.length > 4000 && '…'}
            </pre>
          </details>
        )}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="SKU" hint="Deixa vazio para gerar automaticamente (ex: ECRA-0001).">
            <input value={form.sku ?? ''} onChange={(e) => setForm({ ...form, sku: e.target.value || null })} placeholder="Auto · ou define o teu (LCD-IP12-A)" className={inputCls} />
          </Field>
          <Field label="Categoria">
            <select value={form.categoria} onChange={(e) => setForm({ ...form, categoria: Number(e.target.value) as PartCategoria })} className={inputCls}>
              {Object.values(PART_CATEGORIA).map((value) => <option key={value} value={value}>{PART_CATEGORIA_LABEL[value]}</option>)}
            </select>
          </Field>
        </div>
        <Field label="Nome *"><input value={form.nome} onChange={(e) => setForm({ ...form, nome: e.target.value })} placeholder="Ecrã iPhone 12" className={inputCls} /></Field>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Marca"><input value={form.marca ?? ''} onChange={(e) => setForm({ ...form, marca: e.target.value || null })} placeholder="Apple" className={inputCls} /></Field>
          <Field label="Modelo"><input value={form.modelo ?? ''} onChange={(e) => setForm({ ...form, modelo: e.target.value || null })} placeholder="iPhone 12" className={inputCls} /></Field>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <Field label="Stock"><input inputMode="numeric" value={stockStr} onChange={(e) => setStockStr(e.target.value)} className={inputCls} /></Field>
          <Field label="Mínimo"><input inputMode="numeric" value={minStr} onChange={(e) => setMinStr(e.target.value)} className={inputCls} /></Field>
          <Field label="Custo (€)"><input inputMode="decimal" value={custoStr} onChange={(e) => setCustoStr(e.target.value)} className={inputCls} /></Field>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Fornecedor"><input value={form.fornecedor ?? ''} onChange={(e) => setForm({ ...form, fornecedor: e.target.value || null })} placeholder="Mobiltrust" className={inputCls} /></Field>
          <Field label="Local"><input value={form.localArmazenamento ?? ''} onChange={(e) => setForm({ ...form, localArmazenamento: e.target.value || null })} placeholder="Prateleira A3" className={inputCls} /></Field>
        </div>
        <Field label="Notas"><textarea rows={2} value={form.notas ?? ''} onChange={(e) => setForm({ ...form, notas: e.target.value || null })} className={inputCls + ' resize-none'} /></Field>
        <div className="space-y-2 rounded-md border border-zinc-200 bg-zinc-50/50 p-3 dark:border-zinc-700 dark:bg-zinc-900/50">
          <label className="flex cursor-pointer items-start gap-2 text-xs">
            <input
              type="checkbox"
              checked={form.mostrarLojaOnline}
              onChange={(e) => setForm({ ...form, mostrarLojaOnline: e.target.checked })}
              className="mt-0.5 scale-125 sm:scale-100"
            />
            <div>
              <div className="font-medium">Mostrar na loja online</div>
              <div className="text-[10px] text-zinc-500">
                Esta peça vai aparecer no catálogo público da loja em shop.lopestech.pt. Desliga para peças internas (charge boards, ferramentas, etc).
              </div>
            </div>
          </label>
        </div>
        {editing && (
          <label className="flex items-center gap-2 text-xs">
            <input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} className="scale-125 sm:scale-100" />
            Activa
          </label>
        )}
      </div>
    </Modal>
  );
}

function AdjustStockModal({ part, onClose, onSaved }: { part: Part | null; onClose: () => void; onSaved: () => void }) {
  const [motivo, setMotivo] = useState<PartMovimentoMotivo>(PART_MOVIMENTO_MOTIVO.Entrada);
  const [qty, setQty] = useState('1');
  const [notas, setNotas] = useState('');

  useEffect(() => {
    if (part) {
      setMotivo(PART_MOVIMENTO_MOTIVO.Entrada);
      setQty('1');
      setNotas('');
    }
  }, [part]);

  const save = useMutation({
    mutationFn: () => {
      if (!part) throw new Error('Sem peça seleccionada.');
      const raw = Number(qty);
      const signed = motivo === PART_MOVIMENTO_MOTIVO.Saida
        ? -Math.abs(raw)
        : motivo === PART_MOVIMENTO_MOTIVO.AjusteManual
          ? raw
          : Math.abs(raw);
      return stockApi.addMovimento(part.id, {
        quantidade: signed,
        motivo,
        reparacaoId: null,
        notas: notas.trim() || null,
      });
    },
    onSuccess: () => {
      toast.success('Stock actualizado');
      onSaved();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível ajustar stock.'),
  });

  return (
    <Modal
      open={!!part}
      title={`Ajustar stock${part ? ` · ${part.nome}` : ''}`}
      onClose={onClose}
      footer={<>
        <Button type="button" variant="ghost" onClick={onClose}>Cancelar</Button>
        <Button type="button" loading={save.isPending} disabled={!qty || Number(qty) === 0} onClick={() => save.mutate()}>Guardar movimento</Button>
      </>}
    >
      {part && (
        <div className="space-y-3">
          <div className="rounded-lg bg-zinc-50 p-3 text-sm dark:bg-zinc-950">
            Stock actual: <strong>{part.qtdStock}</strong> · mínimo: {part.qtdMinima}
          </div>
          <Field label="Motivo">
            <select value={motivo} onChange={(e) => setMotivo(Number(e.target.value) as PartMovimentoMotivo)} className={inputCls}>
              <option value={PART_MOVIMENTO_MOTIVO.Entrada}>Entrada</option>
              <option value={PART_MOVIMENTO_MOTIVO.Saida}>Saída</option>
              <option value={PART_MOVIMENTO_MOTIVO.AjusteManual}>Ajuste manual</option>
              <option value={PART_MOVIMENTO_MOTIVO.Devolucao}>Devolução</option>
            </select>
          </Field>
          <Field label={motivo === PART_MOVIMENTO_MOTIVO.AjusteManual ? 'Delta (+/-)' : 'Quantidade'}>
            <input inputMode="numeric" value={qty} onChange={(e) => setQty(e.target.value)} className={inputCls} />
          </Field>
          <Field label="Notas"><textarea rows={2} value={notas} onChange={(e) => setNotas(e.target.value)} className={inputCls + ' resize-none'} /></Field>
        </div>
      )}
    </Modal>
  );
}

function HistoryModal({ part, onClose }: { part: Part | null; onClose: () => void }) {
  const history = useQuery({
    queryKey: ['stock-movimentos', part?.id],
    queryFn: () => stockApi.movimentos({ partId: part!.id }),
    enabled: !!part,
  });
  return (
    <Modal open={!!part} title={`Histórico${part ? ` · ${part.nome}` : ''}`} onClose={onClose}>
      <MovimentosList movimentos={history.data ?? []} loading={history.isLoading} />
    </Modal>
  );
}

function MovimentosList({ movimentos, loading }: { movimentos: PartMovimento[]; loading: boolean }) {
  if (loading) {
    return (
      <div className="space-y-2">
        <SkeletonCard />
        <SkeletonCard />
      </div>
    );
  }
  if (movimentos.length === 0) return <p className="text-sm text-zinc-500">Ainda não há movimentos.</p>;
  return (
    <ul className="max-h-96 space-y-2 overflow-y-auto">
      {movimentos.map((m) => (
        <li key={m.id} className="rounded-lg border border-zinc-200 p-3 text-sm dark:border-zinc-800">
          <div className="flex items-center justify-between gap-2">
            <div>
              <span className="font-medium">{PART_MOVIMENTO_LABEL[m.motivo]}</span>
              <span className={`ml-2 font-mono ${m.quantidade < 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
                {m.quantidade > 0 ? '+' : ''}{m.quantidade}
              </span>
            </div>
            <span className="text-xs text-zinc-500">{formatDate(m.createdAt)}</span>
          </div>
          <div className="mt-1 text-xs text-zinc-500">
            Stock {m.stockAntes} → {m.stockDepois}
            {m.reparacaoId && <> · reparação ligada</>}
          </div>
          {m.notas && <div className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">{m.notas}</div>}
        </li>
      ))}
    </ul>
  );
}

function ImportCsvModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const [csv, setCsv] = useState('');
  const [result, setResult] = useState<Awaited<ReturnType<typeof stockApi.importCsv>> | null>(null);

  const imp = useMutation({
    mutationFn: () => stockApi.importCsv(csv),
    onSuccess: (r) => {
      setResult(r);
      toast.success(`${r.criadas} peça(s) importada(s)`);
      onDone();
    },
    onError: (err) => toast.fromError(err, 'Erro ao importar CSV.'),
  });

  function reset() {
    setCsv('');
    setResult(null);
  }

  return (
    <Modal
      open={open}
      title="Importar peças de CSV"
      onClose={() => { reset(); onClose(); }}
      footer={result ? (
        <Button type="button" onClick={() => { reset(); onClose(); }}>Fechar</Button>
      ) : (
        <>
          <Button type="button" variant="ghost" onClick={() => { reset(); onClose(); }}>Cancelar</Button>
          <Button type="button" loading={imp.isPending} disabled={!csv.trim()} onClick={() => imp.mutate()}>Importar</Button>
        </>
      )}
    >
      <div className="space-y-3">
        {result ? (
          <div className="space-y-3 text-sm">
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
              <Stat label="Criadas" value={result.criadas} tone="emerald" />
              <Stat label="Ignoradas" value={result.ignoradas} tone="zinc" />
              <Stat label="Com erro" value={result.comErro} tone="rose" />
            </div>
            {result.erros.length > 0 && (
              <ul className="max-h-48 space-y-1 overflow-y-auto rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
                {result.erros.map((e, i) => (
                  <li key={i} className="text-xs">
                    <span className="font-mono text-zinc-500">L{e.linha}</span> <strong>{e.campo}:</strong>{' '}
                    <span className="text-rose-700 dark:text-rose-300">{e.mensagem}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        ) : (
          <>
            <div className="rounded-lg bg-zinc-50 p-3 text-xs text-zinc-600 dark:bg-zinc-950 dark:text-zinc-400">
              Colunas: <strong>nome</strong> obrigatório. Opcionais: sku, categoria, marca, modelo, stock, minimo, custo, fornecedor, local, notas.
            </div>
            <textarea
              rows={9}
              value={csv}
              onChange={(e) => setCsv(e.target.value)}
              placeholder={'sku,nome,categoria,marca,modelo,stock,minimo,custo,fornecedor,local\nLCD-IP12,Ecrã iPhone 12,ecra,Apple,iPhone 12,3,1,42.00,Mobiltrust,A3'}
              className={inputCls + ' font-mono text-xs'}
            />
          </>
        )}
      </div>
    </Modal>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone: 'emerald' | 'zinc' | 'rose' }) {
  const cls = tone === 'emerald'
    ? 'border-emerald-300 bg-emerald-50 text-emerald-800 dark:border-emerald-800/60 dark:bg-emerald-950/30 dark:text-emerald-200'
    : tone === 'rose'
      ? 'border-rose-300 bg-rose-50 text-rose-800 dark:border-rose-800/60 dark:bg-rose-950/30 dark:text-rose-200'
      : 'border-zinc-300 bg-zinc-50 text-zinc-800 dark:border-zinc-700 dark:bg-zinc-900 dark:text-zinc-200';
  return (
    <div className={`rounded-lg border p-3 text-center ${cls}`}>
      <div className="text-2xl font-semibold">{value}</div>
      <div className="text-[10px] uppercase">{label}</div>
    </div>
  );
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">{label}</span>
      {children}
      {hint && <span className="mt-1 block text-[11px] text-zinc-500">{hint}</span>}
    </label>
  );
}

const inputCls = 'block min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';
