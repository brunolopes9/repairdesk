import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { FolderUp, Pencil, Plus, Search, Tags, X as XIcon } from 'lucide-react';
import { isAxiosError } from 'axios';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonTable } from '../../components/ui';
import {
  DEVICE_CATEGORY,
  DEVICE_CATEGORY_LABEL,
  precosApi,
  type CreatePriceEntryForm,
  type DeviceCategory,
  type ImportPriceTableResponse,
  type PriceTableEntry,
} from '../../lib/precos/api';
import { formatCents, parseEuros } from '../../lib/money';

export default function Precos() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [categoria, setCategoria] = useState<DeviceCategory | null>(null);
  const [marca, setMarca] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<PriceTableEntry | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<PriceTableEntry | null>(null);

  const list = useQuery({
    queryKey: ['precos', search, categoria, marca, page],
    queryFn: () => precosApi.list({ q: search, categoria, marca, page, pageSize: 50 }),
    placeholderData: keepPreviousData,
  });

  const marcas = useQuery({
    queryKey: ['precos-marcas'],
    queryFn: () => precosApi.marcas(),
    staleTime: 60_000,
  });

  const remove = useMutation({
    mutationFn: (e: PriceTableEntry) => precosApi.remove(e.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['precos'] });
      qc.invalidateQueries({ queryKey: ['precos-marcas'] });
      setConfirmDelete(null);
    },
  });

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / 50));

  function invalidate() {
    qc.invalidateQueries({ queryKey: ['precos'] });
    qc.invalidateQueries({ queryKey: ['precos-marcas'] });
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Tabela de precos"
        description="Base para orcamentos rapidos, margens e tempos previstos por equipamento."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'entrada' : 'entradas'} ? base para orcamentos rapidos</span>}
        actions={
          <>
            <Button type="button" variant="secondary" onClick={() => setImportOpen(true)} leftIcon={<FolderUp size={15} />}>
              Importar CSV
            </Button>
            <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>
              Novo
            </Button>
          </>
        }
      />

      <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
        <select
          value={categoria ?? ''}
          onChange={(e) => { setCategoria(e.target.value === '' ? null : (Number(e.target.value) as DeviceCategory)); setPage(1); }}
          className="min-h-11 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
        >
          <option value="">Todas categorias</option>
          {Object.entries(DEVICE_CATEGORY).map(([_, v]) => (
            <option key={v} value={v}>{DEVICE_CATEGORY_LABEL[v as DeviceCategory]}</option>
          ))}
        </select>
        <select
          value={marca ?? ''}
          onChange={(e) => { setMarca(e.target.value || null); setPage(1); }}
          className="min-h-11 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
        >
          <option value="">Todas marcas</option>
          {marcas.data?.map((m) => <option key={m} value={m}>{m}</option>)}
        </select>
        <div className="relative min-w-0 flex-1">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input
            type="search"
            placeholder="Pesquisar servico, modelo, notas..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </div>
      </div>

      {list.isLoading ? (
        <SkeletonTable columns={8} rows={6} minWidth="min-w-[900px]" />
      ) : (
        <section className="overflow-x-auto rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <table className="min-w-[900px] text-sm">
          <thead className="bg-zinc-50 text-left text-xs text-zinc-500 dark:bg-zinc-950">
            <tr>
              <th className="px-3 py-2">Categoria</th>
              <th className="px-3 py-2">Marca / Modelo</th>
              <th className="px-3 py-2">Serviço</th>
              <th className="px-3 py-2 text-right">Custo peça</th>
              <th className="px-3 py-2 text-right">PVP</th>
              <th className="px-3 py-2 text-right">Margem</th>
              <th className="px-3 py-2 text-right">Tempo</th>
              <th className="px-3 py-2"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {items.map((e) => (
              <tr key={e.id} className={`hover:bg-zinc-50 dark:hover:bg-zinc-800/50 ${!e.activo ? 'opacity-50' : ''}`}>
                <td className="px-3 py-2 text-xs text-zinc-500">{DEVICE_CATEGORY_LABEL[e.categoria]}</td>
                <td className="px-3 py-2">
                  <div className="font-medium">{e.marca}</div>
                  <div className="text-xs text-zinc-500">{e.modelo}</div>
                </td>
                <td className="px-3 py-2">
                  {e.servico}
                  {e.notas && <div className="text-xs text-zinc-500">{e.notas}</div>}
                </td>
                <td className="px-3 py-2 text-right tabular-nums">{e.custoPecaCents != null ? formatCents(e.custoPecaCents) : <span className="text-zinc-400">—</span>}</td>
                <td className="px-3 py-2 text-right font-semibold tabular-nums">{formatCents(e.pvpCents)}</td>
                <td className="px-3 py-2 text-right text-xs">
                  {e.margemPct != null ? (
                    <span className={e.margemPct >= 40 ? 'text-emerald-600 dark:text-emerald-400' : e.margemPct >= 20 ? 'text-amber-600 dark:text-amber-400' : 'text-rose-600 dark:text-rose-400'}>
                      {e.margemPct}%
                    </span>
                  ) : <span className="text-zinc-400">—</span>}
                </td>
                <td className="px-3 py-2 text-right text-xs text-zinc-500 tabular-nums">{e.tempoEstimadoMin ? `${e.tempoEstimadoMin}m` : '—'}</td>
                <td className="px-3 py-2 text-right">
                  <button type="button" onClick={() => setEditing(e)} aria-label="Editar" className="inline-grid h-10 w-10 place-items-center rounded-md text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"><Pencil size={14} strokeWidth={2} /></button>
                  <button type="button" onClick={() => setConfirmDelete(e)} aria-label="Apagar" className="ml-1 inline-grid h-10 w-10 place-items-center rounded-md text-zinc-400 hover:bg-rose-50 hover:text-rose-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-rose-950/40"><XIcon size={14} strokeWidth={2} /></button>
                </td>
              </tr>
            ))}
            {items.length === 0 && (
              <tr>
                <td colSpan={8} className="px-3 py-6">
                  <EmptyState
                    compact
                    icon={search || categoria != null || marca ? Search : Tags}
                    title={search || categoria != null || marca ? 'Nenhuma entrada encontrada' : 'Tabela de precos vazia'}
                    description={search || categoria != null || marca ? 'Ajusta os filtros para encontrares o servico certo.' : 'Importa um CSV ou cria a primeira entrada para acelerar orcamentos.'}
                    action={!(search || categoria != null || marca) ? <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>Criar entrada</Button> : undefined}
                  />
                </td>
              </tr>
            )}
          </tbody>
        </table>
        </section>
      )}

      {lastPage > 1 && (
        <div className="flex items-center justify-between text-xs text-zinc-500">
          <button disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">← Anterior</button>
          <span>{page} / {lastPage}</span>
          <button disabled={page >= lastPage} onClick={() => setPage((p) => p + 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">Seguinte →</button>
        </div>
      )}

      <PriceFormModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSaved={() => { invalidate(); setCreateOpen(false); }}
      />

      <PriceFormModal
        open={!!editing}
        editing={editing}
        onClose={() => setEditing(null)}
        onSaved={() => { invalidate(); setEditing(null); }}
      />

      <ImportCsvModal open={importOpen} onClose={() => setImportOpen(false)} onDone={invalidate} />

      <Modal
        open={!!confirmDelete}
        title="Apagar entrada"
        onClose={() => setConfirmDelete(null)}
        footer={<>
          <button type="button" onClick={() => setConfirmDelete(null)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={remove.isPending} onClick={() => confirmDelete && remove.mutate(confirmDelete)} className="rounded-md bg-rose-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {remove.isPending ? 'A apagar…' : 'Apagar'}
          </button>
        </>}
      >
        {confirmDelete && (
          <p className="text-sm">Apagar <strong>{confirmDelete.marca} {confirmDelete.modelo}</strong> · {confirmDelete.servico}?</p>
        )}
      </Modal>
    </div>
  );
}

function PriceFormModal({
  open,
  editing,
  onClose,
  onSaved,
}: {
  open: boolean;
  editing?: PriceTableEntry | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [form, setForm] = useState<CreatePriceEntryForm>({
    categoria: DEVICE_CATEGORY.Smartphone,
    marca: '',
    modelo: '',
    servico: '',
    custoPecaCents: null,
    pvpCents: 0,
    tempoEstimadoMin: null,
    notas: null,
  });
  const [activo, setActivo] = useState(true);
  const [custoStr, setCustoStr] = useState('');
  const [pvpStr, setPvpStr] = useState('');
  const [tempoStr, setTempoStr] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (editing) {
      setForm({
        categoria: editing.categoria,
        marca: editing.marca,
        modelo: editing.modelo,
        servico: editing.servico,
        custoPecaCents: editing.custoPecaCents,
        pvpCents: editing.pvpCents,
        tempoEstimadoMin: editing.tempoEstimadoMin,
        notas: editing.notas,
      });
      setActivo(editing.activo);
      setCustoStr(editing.custoPecaCents != null ? (editing.custoPecaCents / 100).toFixed(2) : '');
      setPvpStr((editing.pvpCents / 100).toFixed(2));
      setTempoStr(editing.tempoEstimadoMin?.toString() ?? '');
    } else if (open) {
      setForm({ categoria: DEVICE_CATEGORY.Smartphone, marca: '', modelo: '', servico: '', custoPecaCents: null, pvpCents: 0, tempoEstimadoMin: null, notas: null });
      setActivo(true);
      setCustoStr(''); setPvpStr(''); setTempoStr('');
    }
    setError(null);
  }, [editing, open]);

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        ...form,
        custoPecaCents: custoStr ? parseEuros(custoStr) : null,
        pvpCents: parseEuros(pvpStr) ?? 0,
        tempoEstimadoMin: tempoStr ? Number(tempoStr) : null,
      };
      if (editing) return precosApi.update(editing.id, { ...payload, activo });
      return precosApi.create(payload);
    },
    onSuccess: onSaved,
    onError: (err) => {
      if (isAxiosError(err)) setError(err.response?.data?.detail ?? err.response?.data?.title ?? 'Erro a guardar.');
    },
  });

  return (
    <Modal
      open={open}
      title={editing ? 'Editar entrada' : 'Nova entrada de preço'}
      onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button type="button" disabled={!form.marca || !form.modelo || !form.servico || !pvpStr || save.isPending} onClick={() => save.mutate()} className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
          {save.isPending ? 'A guardar…' : editing ? 'Guardar' : 'Criar'}
        </button>
      </>}
    >
      <div className="space-y-3">
        {error && <div className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700 dark:bg-rose-950/30 dark:text-rose-300">{error}</div>}
        <Field label="Categoria">
          <select value={form.categoria} onChange={(e) => setForm({ ...form, categoria: Number(e.target.value) as DeviceCategory })} className={inputCls}>
            {Object.entries(DEVICE_CATEGORY).map(([_, v]) => (
              <option key={v} value={v}>{DEVICE_CATEGORY_LABEL[v as DeviceCategory]}</option>
            ))}
          </select>
        </Field>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Marca *"><input value={form.marca} onChange={(e) => setForm({ ...form, marca: e.target.value })} placeholder="Apple" className={inputCls} /></Field>
          <Field label="Modelo *"><input value={form.modelo} onChange={(e) => setForm({ ...form, modelo: e.target.value })} placeholder="iPhone 13" className={inputCls} /></Field>
        </div>
        <Field label="Serviço *"><input value={form.servico} onChange={(e) => setForm({ ...form, servico: e.target.value })} placeholder="Substituição de ecrã" className={inputCls} /></Field>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <Field label="Custo peça (€)"><input inputMode="decimal" value={custoStr} onChange={(e) => setCustoStr(e.target.value)} placeholder="0,00" className={inputCls} /></Field>
          <Field label="PVP (€) *"><input inputMode="decimal" value={pvpStr} onChange={(e) => setPvpStr(e.target.value)} placeholder="0,00" className={inputCls} /></Field>
          <Field label="Tempo (min)"><input inputMode="numeric" value={tempoStr} onChange={(e) => setTempoStr(e.target.value)} placeholder="30" className={inputCls} /></Field>
        </div>
        <Field label="Notas"><textarea rows={2} value={form.notas ?? ''} onChange={(e) => setForm({ ...form, notas: e.target.value || null })} className={inputCls + ' resize-none'} /></Field>
        {editing && (
          <label className="flex items-center gap-2 text-xs">
            <input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} className="scale-125 sm:scale-100" />
            Activo (visível em sugestões/orçamentos)
          </label>
        )}
      </div>
    </Modal>
  );
}

function ImportCsvModal({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: () => void }) {
  const [csv, setCsv] = useState('');
  const [fileName, setFileName] = useState<string | null>(null);
  const [result, setResult] = useState<ImportPriceTableResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  const imp = useMutation({
    mutationFn: () => precosApi.importCsv(csv),
    onSuccess: (r) => { setResult(r); onDone(); },
    onError: (err) => {
      if (isAxiosError(err)) setError(err.response?.data?.detail ?? 'Erro ao importar.');
    },
  });

  function reset() { setCsv(''); setFileName(null); setResult(null); setError(null); }
  function handleFile(file: File) {
    if (file.size > 5 * 1024 * 1024) { setError('Ficheiro demasiado grande (5MB).'); return; }
    setFileName(file.name); setError(null);
    file.text().then((t) => setCsv(t));
  }

  const previewLines = csv ? csv.split(/\r?\n/).filter((l) => l.trim()).slice(0, 6) : [];

  return (
    <Modal
      open={open}
      title="Importar tabela de preços de CSV"
      onClose={() => { reset(); onClose(); }}
      footer={result ? (
        <button type="button" onClick={() => { reset(); onClose(); }} className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white">Fechar</button>
      ) : (
        <>
          <button type="button" onClick={() => { reset(); onClose(); }} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={!csv || imp.isPending} onClick={() => imp.mutate()} className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {imp.isPending ? 'A importar…' : 'Importar'}
          </button>
        </>
      )}
    >
      <div className="space-y-3">
        {error && <div className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700 dark:bg-rose-950/30 dark:text-rose-300">{error}</div>}

        {result ? (
          <div className="space-y-3 text-sm">
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
              <div className="rounded-lg border border-emerald-300 bg-emerald-50 p-3 text-center dark:border-emerald-800/60 dark:bg-emerald-950/30">
                <div className="text-2xl font-semibold text-emerald-700 dark:text-emerald-300">{result.criadas}</div>
                <div className="text-[10px] uppercase">Criadas</div>
              </div>
              <div className="rounded-lg border border-zinc-300 bg-zinc-50 p-3 text-center dark:border-zinc-700 dark:bg-zinc-900">
                <div className="text-2xl font-semibold">{result.ignoradas}</div>
                <div className="text-[10px] uppercase">Ignoradas (dup.)</div>
              </div>
              <div className="rounded-lg border border-rose-300 bg-rose-50 p-3 text-center dark:border-rose-800/60 dark:bg-rose-950/30">
                <div className="text-2xl font-semibold text-rose-700 dark:text-rose-300">{result.comErro}</div>
                <div className="text-[10px] uppercase">Com erro</div>
              </div>
            </div>
            {result.erros.length > 0 && (
              <ul className="max-h-48 space-y-1 overflow-y-auto rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
                {result.erros.map((e, i) => (
                  <li key={i} className="text-xs">
                    <span className="font-mono text-zinc-500">L{e.linha}</span> <strong>{e.campo}:</strong>{' '}
                    <span className="text-rose-700 dark:text-rose-300">{e.mensagem}</span>
                    {e.valorOriginal && <span className="text-zinc-500"> ({e.valorOriginal})</span>}
                  </li>
                ))}
              </ul>
            )}
          </div>
        ) : (
          <>
            <div className="rounded-lg bg-zinc-50 p-3 text-xs text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
              <p className="font-medium text-zinc-700 dark:text-zinc-300">Colunas reconhecidas:</p>
              <ul className="mt-1 space-y-0.5">
                <li>· <strong>marca, modelo, servico, pvp</strong> (obrigatórios)</li>
                <li>· categoria, custo, tempo, notas (opcionais)</li>
              </ul>
              <p className="mt-1">Aceita separador <code>,</code> <code>;</code> ou tab. Dedupe automático por (marca, modelo, serviço).</p>
              <p className="mt-1">💡 Tens uma tabela base PT 2026 em <code>Contexto/22-Tabela-Precos-PT.md</code>.</p>
            </div>
            <div
              onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
              onDragLeave={() => setDragging(false)}
              onDrop={(e) => { e.preventDefault(); setDragging(false); const f = e.dataTransfer.files[0]; if (f) handleFile(f); }}
              className={`rounded-xl border-2 border-dashed p-6 text-center text-sm transition ${dragging ? 'border-brand-500 bg-brand-50 dark:border-brand-400 dark:bg-brand-950/30' : 'border-zinc-300 bg-white dark:border-zinc-700 dark:bg-zinc-950'}`}
            >
              {fileName ? (
                <>
                  <div className="font-medium">📄 {fileName}</div>
                  <button type="button" onClick={() => { setCsv(''); setFileName(null); }} className="mt-1 text-xs text-zinc-500 underline">Outro</button>
                </>
              ) : (
                <>
                  <div className="text-zinc-500">Arrasta o ficheiro CSV para aqui</div>
                  <label className="mt-2 inline-flex min-h-11 cursor-pointer items-center justify-center rounded-md bg-brand-600 px-3 py-2 text-xs font-medium text-white hover:bg-brand-700">
                    Selecionar
                    <input type="file" accept=".csv,text/csv,text/plain" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f); }} />
                  </label>
                </>
              )}
            </div>
            {previewLines.length > 0 && (
              <pre className="max-h-32 overflow-auto rounded-lg border border-zinc-200 bg-zinc-50 p-2 text-[11px] dark:border-zinc-800 dark:bg-zinc-950">{previewLines.join('\n')}</pre>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">{label}</span>
      {children}
    </label>
  );
}

const inputCls = 'block min-h-11 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm shadow-sm focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-100 dark:border-zinc-800 dark:bg-zinc-950';
