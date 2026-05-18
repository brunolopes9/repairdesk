import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { AlertTriangle, Download, FolderUp, Pencil, Search, UserPlus, Users } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { isAxiosError } from 'axios';
import { clientesApi, type ImportClientesResponse } from '../../lib/clientes/api';
import { downloadFile } from '../../lib/downloadPdf';
import { displayPhone } from '../../lib/phone/formatter';
import { validateNif } from '../../lib/nif/validator';
import type { Cliente, ClienteForm } from '../../lib/clientes/types';
import ClienteFormView from './ClienteForm';

export default function Clientes() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [editing, setEditing] = useState<Cliente | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<Cliente | null>(null);
  const [importOpen, setImportOpen] = useState(false);

  const list = useQuery({
    queryKey: ['clientes', search, page],
    queryFn: () => clientesApi.list(search, page, pageSize),
    placeholderData: keepPreviousData,
  });

  const upsert = useMutation({
    mutationFn: async (form: ClienteForm) => {
      if (editing) return clientesApi.update(editing.id, form);
      return clientesApi.create(form);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clientes'] });
      setModalOpen(false);
      setEditing(null);
    },
  });

  const remove = useMutation({
    mutationFn: (c: Cliente) => clientesApi.remove(c.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['clientes'] });
      setConfirmDelete(null);
    },
  });

  function openCreate() {
    setEditing(null);
    setModalOpen(true);
  }

  function openEdit(c: Cliente) {
    setEditing(c);
    setModalOpen(true);
  }

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Clientes"
        description="Contactos, historico e dados de faturacao das pessoas que entram na loja."
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'cliente' : 'clientes'}</span>}
        actions={
          <>
            <Button
              type="button"
              variant="secondary"
              onClick={() => downloadFile('/clientes/export.csv', `clientes_${new Date().toISOString().slice(0,10)}.csv`)}
              leftIcon={<Download size={15} />}
              title="Exportar todos os clientes para CSV"
            >
              Exportar
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={() => setImportOpen(true)}
              leftIcon={<FolderUp size={15} />}
              title="Importar clientes em massa de CSV (Excel/Google Sheets)"
            >
              Importar CSV
            </Button>
            <Button type="button" onClick={openCreate} leftIcon={<UserPlus size={15} />}>
              Novo
            </Button>
          </>
        }
      />

      <div className="relative">
        <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input
          type="search"
          inputMode="search"
          placeholder="Pesquisar nome, telefone, email ou NIF..."
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
          className="w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
        />
      </div>

      {list.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
          Não foi possível carregar a lista.
        </div>
      )}

      <ul className="space-y-2">
        {list.isLoading && Array.from({ length: 4 }).map((_, index) => <SkeletonCard key={index} />)}
        {items.map((c) => (
          <li
            key={c.id}
            className="flex items-center justify-between gap-3 rounded-xl border border-zinc-200 bg-white px-4 py-3 dark:border-zinc-800 dark:bg-zinc-900"
          >
            <Link to={`/clientes/${c.id}`} className="flex-1 rounded-md text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">
              <div className="font-medium">{c.nome}</div>
              <div className="flex flex-wrap items-center gap-x-1 text-xs text-zinc-500">
                {c.telefone ? displayPhone(c.telefone) : <em className="opacity-60">sem telefone</em>}
                {c.email && <span>· {c.email}</span>}
                {c.nif && (
                  <span className="inline-flex items-center gap-1">
                    · NIF {c.nif}
                    {!validateNif(c.nif).isValid && (
                      <span className="inline-flex items-center gap-0.5 rounded-sm bg-amber-100 px-1 text-[10px] text-amber-700 dark:bg-amber-900/40 dark:text-amber-300" title="NIF com check-digit inválido — verifica em editar">
                        <AlertTriangle size={9} strokeWidth={2.5} />
                        inválido
                      </span>
                    )}
                  </span>
                )}
              </div>
            </Link>
            <div className="flex items-center gap-1">
              <button
                type="button"
                onClick={() => openEdit(c)}
                className="rounded-md p-1.5 text-zinc-500 transition hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800"
                title="Editar"
                aria-label="Editar"
              >
                <Pencil size={13} strokeWidth={2} />
              </button>
              <button
                type="button"
                onClick={() => setConfirmDelete(c)}
                className="rounded-md px-2 py-1 text-xs text-red-600 transition hover:bg-red-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-red-950/40"
              >
                Apagar
              </button>
            </div>
          </li>
        ))}
        {items.length === 0 && !list.isLoading && (
          <li>
            <EmptyState
              icon={search ? Search : Users}
              title={search ? 'Nenhum cliente encontrado' : 'Ainda nao ha clientes'}
              description={search ? 'Ajusta a pesquisa ou limpa o campo para voltar a ver todos os clientes.' : 'Cria o primeiro cliente para associar reparacoes, trabalhos e historico.'}
              action={!search ? <Button type="button" onClick={openCreate} leftIcon={<UserPlus size={15} />}>Criar cliente</Button> : undefined}
            />
          </li>
        )}
      </ul>

      {lastPage > 1 && (
        <div className="flex items-center justify-between text-xs text-zinc-500">
          <button
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            className="rounded-md px-2 py-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40"
          >
            ← Anterior
          </button>
          <span>
            {page} / {lastPage}
          </span>
          <button
            disabled={page >= lastPage}
            onClick={() => setPage((p) => Math.min(lastPage, p + 1))}
            className="rounded-md px-2 py-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40"
          >
            Seguinte →
          </button>
        </div>
      )}

      <Modal
        open={modalOpen}
        title={editing ? 'Editar cliente' : 'Novo cliente'}
        onClose={() => setModalOpen(false)}
        footer={
          <>
            <button
              type="button"
              onClick={() => setModalOpen(false)}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Cancelar
            </button>
            <button
              type="submit"
              form="cliente-form"
              disabled={upsert.isPending}
              className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-60"
            >
              {upsert.isPending ? 'A guardar…' : 'Guardar'}
            </button>
          </>
        }
      >
        <ClienteFormView
          initial={editing}
          submitting={upsert.isPending}
          onCancel={() => setModalOpen(false)}
          onSubmit={async (form) => {
            await upsert.mutateAsync(form);
          }}
        />
      </Modal>

      <Modal
        open={!!confirmDelete}
        title="Apagar cliente"
        onClose={() => setConfirmDelete(null)}
        footer={
          <>
            <button
              type="button"
              onClick={() => setConfirmDelete(null)}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={remove.isPending}
              onClick={() => confirmDelete && remove.mutate(confirmDelete)}
              className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white transition hover:bg-red-700 disabled:opacity-60"
            >
              {remove.isPending ? 'A apagar…' : 'Apagar'}
            </button>
          </>
        }
      >
        <p className="text-sm">
          Tens a certeza que queres apagar <strong>{confirmDelete?.nome}</strong>? Esta ação pode
          ser revertida pelo admin (soft delete).
        </p>
      </Modal>

      <ImportCsvModal
        open={importOpen}
        onClose={() => setImportOpen(false)}
        onDone={() => { qc.invalidateQueries({ queryKey: ['clientes'] }); }}
      />
    </div>
  );
}

function ImportCsvModal({
  open,
  onClose,
  onDone,
}: {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
}) {
  const [csv, setCsv] = useState('');
  const [fileName, setFileName] = useState<string | null>(null);
  const [result, setResult] = useState<ImportClientesResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  const imp = useMutation({
    mutationFn: () => clientesApi.importCsv(csv),
    onSuccess: (r) => { setResult(r); onDone(); },
    onError: (err) => {
      if (isAxiosError(err)) {
        const d = err.response?.data as { detail?: string; title?: string } | undefined;
        setError(d?.detail ?? d?.title ?? 'Erro ao importar.');
      } else setError('Erro ao importar.');
    },
  });

  function reset() {
    setCsv('');
    setFileName(null);
    setResult(null);
    setError(null);
  }

  function handleFile(file: File) {
    if (file.size > 5 * 1024 * 1024) {
      setError('Ficheiro demasiado grande (máximo 5 MB).');
      return;
    }
    setFileName(file.name);
    setError(null);
    file.text().then((t) => setCsv(t));
  }

  // Preview: primeiras 5 linhas com vírgula/ponto-e-vírgula como separadores comuns
  const previewLines = csv ? csv.split(/\r?\n/).filter((l) => l.trim()).slice(0, 6) : [];

  return (
    <Modal
      open={open}
      title="Importar clientes de CSV"
      onClose={() => { reset(); onClose(); }}
      footer={
        result ? (
          <button
            type="button"
            onClick={() => { reset(); onClose(); }}
            className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white"
          >
            Fechar
          </button>
        ) : (
          <>
            <button
              type="button"
              onClick={() => { reset(); onClose(); }}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={!csv || imp.isPending}
              onClick={() => imp.mutate()}
              className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
            >
              {imp.isPending ? 'A importar…' : 'Importar'}
            </button>
          </>
        )
      }
    >
      <div className="space-y-3">
        {error && (
          <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700 dark:border-rose-900/60 dark:bg-rose-950/30 dark:text-rose-300">
            {error}
          </div>
        )}

        {result ? (
          <div className="space-y-3 text-sm">
            <div className="grid grid-cols-3 gap-2">
              <div className="rounded-lg border border-emerald-300 bg-emerald-50 p-3 text-center dark:border-emerald-800/60 dark:bg-emerald-950/30">
                <div className="text-2xl font-semibold text-emerald-700 dark:text-emerald-300">{result.criados}</div>
                <div className="text-[11px] uppercase text-emerald-700/80 dark:text-emerald-300/80">Criados</div>
              </div>
              <div className="rounded-lg border border-zinc-300 bg-zinc-50 p-3 text-center dark:border-zinc-700 dark:bg-zinc-900">
                <div className="text-2xl font-semibold text-zinc-600 dark:text-zinc-300">{result.ignorados}</div>
                <div className="text-[11px] uppercase text-zinc-500">Ignorados (dup. NIF)</div>
              </div>
              <div className="rounded-lg border border-rose-300 bg-rose-50 p-3 text-center dark:border-rose-800/60 dark:bg-rose-950/30">
                <div className="text-2xl font-semibold text-rose-700 dark:text-rose-300">{result.comErro}</div>
                <div className="text-[11px] uppercase text-rose-700/80 dark:text-rose-300/80">Com erro</div>
              </div>
            </div>
            {result.erros.length > 0 && (
              <div>
                <h4 className="mb-1 text-xs font-semibold text-zinc-600 dark:text-zinc-400">Erros por linha:</h4>
                <ul className="max-h-48 space-y-1 overflow-y-auto rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
                  {result.erros.map((e, i) => (
                    <li key={i} className="text-xs">
                      <span className="font-mono text-zinc-500">L{e.linha}</span>{' '}
                      <span className="font-medium">{e.campo}:</span>{' '}
                      <span className="text-rose-700 dark:text-rose-300">{e.mensagem}</span>
                      {e.valorOriginal && <span className="text-zinc-500"> ({e.valorOriginal})</span>}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        ) : (
          <>
            <div className="rounded-lg bg-zinc-50 p-3 text-xs text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400">
              <p className="font-medium text-zinc-700 dark:text-zinc-300">Formato esperado:</p>
              <p className="mt-1">Header obrigatório: <code className="font-mono">nome,telefone,email,nif,notas</code></p>
              <p className="mt-1">Aceito separador <code>,</code>, <code>;</code> ou tab. Vindo de Excel? Guarda como <strong>CSV UTF-8</strong>. Dedupe automático por NIF.</p>
            </div>

            <div
              onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
              onDragLeave={() => setDragging(false)}
              onDrop={(e) => {
                e.preventDefault();
                setDragging(false);
                const f = e.dataTransfer.files[0];
                if (f) handleFile(f);
              }}
              className={`rounded-xl border-2 border-dashed p-6 text-center text-sm transition ${
                dragging
                  ? 'border-brand-500 bg-brand-50 dark:border-brand-400 dark:bg-brand-950/30'
                  : 'border-zinc-300 bg-white dark:border-zinc-700 dark:bg-zinc-950'
              }`}
            >
              {fileName ? (
                <>
                  <div className="font-medium">📄 {fileName}</div>
                  <button
                    type="button"
                    onClick={() => { setCsv(''); setFileName(null); }}
                    className="mt-1 text-xs text-zinc-500 underline"
                  >Escolher outro</button>
                </>
              ) : (
                <>
                  <div className="text-zinc-500">Arrasta o ficheiro CSV para aqui</div>
                  <div className="mt-1 text-xs text-zinc-400">ou</div>
                  <label className="mt-2 inline-block cursor-pointer rounded-md bg-brand-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-700">
                    Selecionar ficheiro
                    <input
                      type="file"
                      accept=".csv,text/csv,text/plain"
                      className="hidden"
                      onChange={(e) => { const f = e.target.files?.[0]; if (f) handleFile(f); }}
                    />
                  </label>
                </>
              )}
            </div>

            {previewLines.length > 0 && (
              <div>
                <h4 className="mb-1 text-xs font-semibold text-zinc-600 dark:text-zinc-400">
                  Preview (primeiras {previewLines.length - 1} {previewLines.length - 1 === 1 ? 'linha' : 'linhas'}):
                </h4>
                <pre className="max-h-32 overflow-auto rounded-lg border border-zinc-200 bg-zinc-50 p-2 text-[11px] dark:border-zinc-800 dark:bg-zinc-950">
                  {previewLines.join('\n')}
                </pre>
              </div>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}
