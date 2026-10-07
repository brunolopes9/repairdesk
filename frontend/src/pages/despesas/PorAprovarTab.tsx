import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Camera, Download, FileText, Inbox, PackagePlus, ReceiptText, Upload, XCircle } from 'lucide-react';
import { api } from '../../lib/api';
import { supplierInvoicesApi, type SupplierInvoiceImport, type ApproveSupplierInvoiceRequest} from '../../lib/supplierInvoices/api';
import { formatCents } from '../../lib/money';
import { toast } from '../../lib/toast';
import { DESPESA_CATEGORIA, DESPESA_LABEL, type DespesaCategoria } from '../../lib/despesas/types';
import Modal from '../../components/Modal';
import { Button, DetailWorkspace, InspectorRail, ViewTabs } from '../../components/ui';

const inputCls = 'mt-1 min-h-11 w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950';

export default function PorAprovarTab() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const pending = useQuery({
    queryKey: ['supplier-invoices-pending'],
    queryFn: () => supplierInvoicesApi.pending(100),
    refetchInterval: 30_000,
  });

  const [approveTarget, setApproveTarget] = useState<SupplierInvoiceImport | null>(null);
  const [rejectTarget, setRejectTarget] = useState<SupplierInvoiceImport | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [exportFrom, setExportFrom] = useState(() => {
    const d = new Date();
    d.setMonth(d.getMonth() - 3);
    return d.toISOString().slice(0, 10);
  });
  const [exportTo, setExportTo] = useState(() => new Date().toISOString().slice(0, 10));

  const reject = useMutation({
    mutationFn: (req: { id: string; reason: string }) => supplierInvoicesApi.reject(req.id, req.reason),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['supplier-invoices-pending'] });
      toast.success('Importação rejeitada');
      setRejectTarget(null);
      setRejectReason('');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível rejeitar.'),
  });

  // Sprint 160c: upload manual de PDF (sem n8n IMAP).
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const photoInputRef = useRef<HTMLInputElement | null>(null);
  const upload = useMutation({
    mutationFn: (file: File) => supplierInvoicesApi.uploadPdf(file),
    onSuccess: (result) => {
      qc.invalidateQueries({ queryKey: ['supplier-invoices-pending'] });
      qc.invalidateQueries({ queryKey: ['supplier-invoices-history'] });
      // Sprint 163b: distingue duplicate de novo.
      if (result.wasDuplicate) {
        toast.warning('PDF já tinha sido processado', 'Verifica o separador "Histórico" abaixo.');
      } else {
        toast.success('PDF processado — vê a importação na lista pendente.');
      }
      if (fileInputRef.current) fileInputRef.current.value = '';
    },
    onError: (err) => toast.fromError(err, 'Falhou upload do PDF.'),
  });

  // Sprint 164: upload foto papel via Claude Vision.
  const uploadPhoto = useMutation({
    mutationFn: (file: File) => supplierInvoicesApi.uploadPhoto(file),
    onSuccess: (result) => {
      qc.invalidateQueries({ queryKey: ['supplier-invoices-pending'] });
      qc.invalidateQueries({ queryKey: ['supplier-invoices-history'] });
      if (result.wasDuplicate) {
        toast.warning('Esta foto já tinha sido processada', 'Verifica o separador "Histórico".');
      } else {
        toast.success('Foto processada por Claude Vision — vê a importação na lista pendente.');
      }
      if (photoInputRef.current) photoInputRef.current.value = '';
    },
    onError: (err) => toast.fromError(err, 'Falhou OCR da foto.'),
  });

  // Sprint 163b: re-corre pipeline parser+fingerprint+LLM numa importação.
  const reprocess = useMutation({
    mutationFn: (id: string) => supplierInvoicesApi.reprocess(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['supplier-invoices-pending'] });
      qc.invalidateQueries({ queryKey: ['supplier-invoices-history'] });
      toast.success('Reprocessado — verifica items na lista pendente.');
    },
    onError: (err) => toast.fromError(err, 'Falhou reprocesso.'),
  });

  // Sprint 163b: histórico (Approved/Rejected).
  const [tab, setTab] = useState<'pending' | 'history'>('pending');
  const history = useQuery({
    queryKey: ['supplier-invoices-history'],
    queryFn: () => supplierInvoicesApi.history(100),
    enabled: tab === 'history',
  });

  async function openPdf(id: string) {
    try {
      const res = await api.get<Blob>(supplierInvoicesApi.pdfPath(id), { responseType: 'blob' });
      const url = URL.createObjectURL(res.data);
      window.open(url, '_blank', 'noopener,noreferrer');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (err) {
      toast.fromError(err, 'Não foi possível abrir o PDF.');
    }
  }

  async function downloadZip() {
    try {
      const res = await api.get<Blob>(supplierInvoicesApi.exportZipPath(exportFrom, exportTo), { responseType: 'blob' });
      const url = URL.createObjectURL(res.data);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Faturas-fornecedor_${exportFrom}_a_${exportTo}.zip`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
      toast.success('ZIP descarregado', 'Pronto para entregar ao contabilista.');
    } catch (err) {
      toast.fromError(err, 'Não foi possível gerar o ZIP.');
    }
  }

  const data = pending.data ?? [];
  const failed = data.filter((d) => d.status === 'Failed');
  const ready = data.filter((d) => d.status === 'Pending');
  const pendingTotalCents = data.reduce((sum, item) => sum + (item.totalCents ?? 0), 0);
  const inboxRail = (
    <InspectorRail>
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-zinc-500">Inbox fornecedor</p>
        <h3 className="mt-1 text-base font-semibold text-zinc-950 dark:text-zinc-50">Triagem de faturas</h3>
        <p className="mt-1 text-sm text-zinc-500">
          Prioriza falhas do parser, depois aprova linhas para stock ou despesa sem perder o rasto do PDF.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
          <Inbox size={16} className="mb-3 text-brand-600" />
          <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-zinc-500">Pendentes</p>
          <p className="mt-1 text-lg font-semibold text-zinc-950 dark:text-zinc-50">{ready.length}</p>
        </div>
        <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
          <AlertTriangle size={16} className="mb-3 text-amber-600" />
          <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-zinc-500">Com falha</p>
          <p className="mt-1 text-lg font-semibold text-zinc-950 dark:text-zinc-50">{failed.length}</p>
        </div>
        <div className="col-span-2 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
          <Download size={16} className="mb-3 text-emerald-600" />
          <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-zinc-500">Valor detetado</p>
          <p className="mt-1 text-lg font-semibold text-zinc-950 dark:text-zinc-50">{formatCents(pendingTotalCents)}</p>
          <p className="mt-1 text-xs text-zinc-500">Soma apenas os PDFs com total extraido.</p>
        </div>
      </div>

      <div className="rounded-lg bg-zinc-950 p-3 text-sm text-white dark:bg-zinc-50 dark:text-zinc-950">
        Usa esta inbox como mesa de revisao: PDF, parser, classificacao, aprovacao e export para contabilista.
      </div>
    </InspectorRail>
  );

  return (
    <div className="space-y-4">
      <header className="rounded-lg border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-zinc-500">Compras</p>
            <h1 className="mt-1 flex items-center gap-2 text-xl font-semibold text-zinc-950 dark:text-zinc-50">
              <Inbox size={22} strokeWidth={2} className="text-brand-600" />
              Importações de Fornecedor
            </h1>
          </div>
          {/* Sprint 160c + 164: upload manual PDF / foto papel. */}
          <div className="flex flex-wrap gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept=".pdf,application/pdf"
              multiple
              className="hidden"
              onChange={(e) => {
                // Sprint 543: multi-fatura — seleciona vários PDFs de uma vez (ex: mês inteiro de
                // faturas Anthropic/CTT); cada um vira uma importação própria.
                const files = Array.from(e.target.files ?? []);
                for (const file of files) upload.mutate(file);
                e.target.value = '';
              }}
            />
            <input
              ref={photoInputRef}
              type="file"
              accept="image/jpeg,image/jpg,image/png,image/webp,image/gif"
              capture="environment"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) uploadPhoto.mutate(file);
              }}
            />
            <Button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              loading={upload.isPending}
              variant="secondary"
              leftIcon={<Upload size={15} />}
              title="Upload manual de PDF (sem precisar de n8n IMAP)"
            >
              PDF
            </Button>
            <Button
              type="button"
              onClick={() => photoInputRef.current?.click()}
              loading={uploadPhoto.isPending}
              variant="primary"
              leftIcon={<Camera size={15} />}
              title="Foto papel — Claude Vision faz OCR (mobile: usa câmara directamente)"
            >
              Foto papel
            </Button>
          </div>
        </div>
        <p className="text-sm text-zinc-500">
          Facturas que chegaram via n8n IMAP automation ou upload manual. Revê os valores extraídos pelo parser e aprova
          (cria Despesa ou adiciona ao stock) ou rejeita. Os PDFs ficam guardados em <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">/data/supplier-invoices/{`{tenant}`}/{`{ano}`}/{`{mês}`}/{`{fornecedor}`}/</code>.
        </p>
      </header>

      {/* Export ZIP para contabilista */}
      <section className="rounded-lg border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div className="min-w-0">
            <h2 className="flex items-center gap-2 text-sm font-semibold text-zinc-950 dark:text-zinc-50">
              <Download size={16} strokeWidth={2} className="text-emerald-600" />
              Export trimestral para contabilista
            </h2>
            <p className="mt-1 text-xs text-zinc-500">
              ZIP com todas as facturas aprovadas no período. Estrutura: <code>ano/mês/fornecedor/fatura.pdf</code>.
            </p>
          </div>
          <div className="grid gap-2 sm:grid-cols-[150px_150px_auto]">
            <label className="text-xs">
              <span className="block text-zinc-500">De</span>
              <input type="date" value={exportFrom} onChange={(e) => setExportFrom(e.target.value)} className={inputCls} />
            </label>
            <label className="text-xs">
              <span className="block text-zinc-500">Até</span>
              <input type="date" value={exportTo} onChange={(e) => setExportTo(e.target.value)} className={inputCls} />
            </label>
            <Button type="button" onClick={downloadZip} leftIcon={<Download size={15} />} className="self-end">
              Descarregar ZIP
            </Button>
          </div>
        </div>
      </section>

      <DetailWorkspace rail={inboxRail}>
        {failed.length > 0 && (
          <section className="rounded-lg border border-amber-200 bg-amber-50 p-4 dark:border-amber-900/40 dark:bg-amber-950/30">
            <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-900 dark:text-amber-200">
              <AlertTriangle size={16} strokeWidth={2} />
              {failed.length} fatura(s) onde o parser falhou
            </h2>
            <p className="mt-1 text-xs text-amber-800 dark:text-amber-300">
              Confidence "None" — Bruno precisa de abrir o PDF e meter valores manuais antes de aprovar.
            </p>
            <ImportsTable data={failed} onPdf={openPdf} onCompra={(x) => navigate(`/compras/nova?fatura=${x.id}`)} onDespesa={setApproveTarget} onReject={(x) => { setRejectTarget(x); setRejectReason(''); }} />
          </section>
        )}

        {/* Sprint 163b: tabs Pendentes vs Histórico (Approved/Rejected). */}
        <section className="rounded-lg border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
          <div className="border-b border-zinc-200 p-2 dark:border-zinc-800">
            <ViewTabs
              value={tab}
              onChange={(value) => setTab(value as 'pending' | 'history')}
              tabs={[
                { key: 'pending', label: 'Pendentes', meta: ready.length },
                { key: 'history', label: 'Histórico', meta: history.data?.length },
              ]}
              className="border-0 bg-transparent p-0 dark:bg-transparent"
            />
          </div>

          <div className="p-4">
            {tab === 'pending' ? (
              pending.isLoading ? (
                <div className="py-8 text-center text-sm text-zinc-500">A carregar…</div>
              ) : ready.length === 0 && failed.length === 0 ? (
                <div className="py-8 text-center text-sm text-zinc-500">
                  Sem importações pendentes. Faz upload manual ou aguarda n8n IMAP.
                </div>
              ) : ready.length > 0 ? (
                <ImportsTable data={ready} onPdf={openPdf} onCompra={(x) => navigate(`/compras/nova?fatura=${x.id}`)} onDespesa={setApproveTarget} onReject={(x) => { setRejectTarget(x); setRejectReason(''); }} />
              ) : null
            ) : (
              history.isLoading ? (
                <div className="py-8 text-center text-sm text-zinc-500">A carregar histórico…</div>
              ) : (history.data?.length ?? 0) === 0 ? (
                <div className="py-8 text-center text-sm text-zinc-500">Sem histórico ainda.</div>
              ) : (
                <HistoryTable
                  data={history.data!}
                  onPdf={openPdf}
                  onReprocess={(id) => reprocess.mutate(id)}
                  reprocessing={reprocess.isPending}
                />
              )
            )}
          </div>
        </section>
      </DetailWorkspace>

      {approveTarget && (
        <ApproveModal
          target={approveTarget}
          onClose={() => setApproveTarget(null)}
          onSuccess={() => {
            qc.invalidateQueries({ queryKey: ['supplier-invoices-pending'] });
            qc.invalidateQueries({ queryKey: ['despesas'] });
            setApproveTarget(null);
          }}
        />
      )}

      <Modal
        open={!!rejectTarget}
        title="Rejeitar importação"
        onClose={() => setRejectTarget(null)}
        footer={<>
          <button type="button" onClick={() => setRejectTarget(null)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100">Cancelar</button>
          <button
            type="button"
            disabled={reject.isPending}
            onClick={() => rejectTarget && reject.mutate({ id: rejectTarget.id, reason: rejectReason })}
            className="rounded-md bg-rose-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {reject.isPending ? 'A rejeitar…' : 'Rejeitar'}
          </button>
        </>}
      >
        <div className="space-y-3 text-sm">
          <p>Vais rejeitar a importação de <strong>{rejectTarget?.fornecedorName ?? 'fornecedor desconhecido'}</strong>{rejectTarget?.documentNumber ? ` (${rejectTarget.documentNumber})` : ''}.</p>
          <p className="text-xs text-zinc-500">O PDF mantém-se no filesystem; só o registo é marcado Rejected.</p>
          <label className="block text-xs font-medium text-zinc-500">Motivo (opcional)</label>
          <textarea
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
            placeholder="ex: duplicado mal-detectado, fatura para outro tenant…"
            rows={3}
            className={inputCls}
          />
        </div>
      </Modal>
    </div>
  );
}

function ImportsTable({
  data, onPdf, onCompra, onDespesa, onReject,
}: {
  data: SupplierInvoiceImport[];
  onPdf: (id: string) => void;
  onCompra: (x: SupplierInvoiceImport) => void;
  onDespesa: (x: SupplierInvoiceImport) => void;
  onReject: (x: SupplierInvoiceImport) => void;
}) {
  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="text-xs uppercase text-zinc-500">
          <tr>
            <th className="px-2 py-2 text-left">Fornecedor</th>
            <th className="px-2 py-2 text-left">Documento</th>
            <th className="px-2 py-2 text-left">Data</th>
            <th className="px-2 py-2 text-right">Total</th>
            <th className="px-2 py-2 text-left">Confidence</th>
            <th className="px-2 py-2 text-right">Acções</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {data.map((x) => (
            <ImportRow key={x.id} x={x} onPdf={onPdf} onCompra={onCompra} onDespesa={onDespesa} onReject={onReject} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ImportRow({
  x, onPdf, onCompra, onDespesa, onReject,
}: {
  x: SupplierInvoiceImport;
  onPdf: (id: string) => void;
  onCompra: (x: SupplierInvoiceImport) => void;
  onDespesa: (x: SupplierInvoiceImport) => void;
  onReject: (x: SupplierInvoiceImport) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const hasItems = x.items && x.items.length > 0;
  return (
    <>
      <tr className={hasItems ? 'cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-900' : ''} onClick={() => hasItems && setExpanded(!expanded)}>
        <td className="px-2 py-2 font-medium">
          {hasItems && <span className="mr-1 text-zinc-400">{expanded ? '▾' : '▸'}</span>}
          {x.fornecedorName ?? <span className="text-zinc-400">(não detectado)</span>}
        </td>
        <td className="px-2 py-2">{x.documentNumber ?? <span className="text-zinc-400">—</span>}</td>
        <td className="px-2 py-2">{x.documentDate ? new Date(x.documentDate).toLocaleDateString('pt-PT') : <span className="text-zinc-400">—</span>}</td>
        <td className="px-2 py-2 text-right">{x.totalCents != null ? formatCents(x.totalCents) : <span className="text-zinc-400">—</span>}</td>
        <td className="px-2 py-2"><ConfidenceBadge value={x.parseConfidence} /></td>
        <td className="px-2 py-2" onClick={(e) => e.stopPropagation()}>
          <div className="flex justify-end gap-1">
            <button type="button" onClick={() => onPdf(x.id)} className="rounded-md border border-zinc-300 bg-white px-2 py-1 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:hover:bg-zinc-800" title="Abrir PDF">
              <FileText size={14} />
            </button>
            {/* Doc 94 Fase 3: peças/artigos → Compra (lotes de stock, editor pré-preenchido);
                serviços, ferramentas, contas → Despesa. */}
            <button
              type="button"
              onClick={() => onCompra(x)}
              className="flex items-center gap-1 rounded-md bg-emerald-600 px-3 py-1 text-xs font-medium text-white hover:bg-emerald-700"
              title="Peças/artigos para revenda: abre a compra já preenchida para rever"
            >
              <PackagePlus size={14} /> Compra
            </button>
            <button
              type="button"
              onClick={() => onDespesa(x)}
              className="flex items-center gap-1 rounded-md border border-zinc-300 bg-white px-2 py-1 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:hover:bg-zinc-800"
              title="Serviços, ferramentas, contas: regista como despesa"
            >
              <ReceiptText size={14} /> Despesa
            </button>
            <button type="button" onClick={() => onReject(x)} className="rounded-md border border-rose-300 bg-white px-2 py-1 text-xs text-rose-700 hover:bg-rose-50 dark:border-rose-800/40 dark:bg-zinc-900 dark:text-rose-300" title="Rejeitar">
              <XCircle size={14} />
            </button>
          </div>
        </td>
      </tr>
      {expanded && hasItems && (
        <tr className="bg-zinc-50/50 dark:bg-zinc-900/50">
          <td colSpan={6} className="px-4 py-3">
            <div className="text-xs font-semibold uppercase text-zinc-500 mb-2">Items detectados ({x.items!.length})</div>
            <ul className="space-y-2">
              {x.items!.map((item, i) => (
                <li key={i} className="rounded-md border border-zinc-200 bg-white p-2 dark:border-zinc-700 dark:bg-zinc-900">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex-1">
                      <div className="text-sm font-medium">{item.description}</div>
                      <div className="text-xs text-zinc-500">
                        {item.quantity}× · {formatCents(item.lineTotalCents)}
                        {item.brand && <> · {item.brand}{item.model && ` ${item.model}`}</>}
                      </div>
                    </div>
                  </div>
                  {/\b(shipping|portes?|envio|transport|chronopost|dpd|ups|fedex|dhl|frete)\b/i.test(item.description) ? (
                    <div className="mt-2 text-[11px] italic text-zinc-400">🚚 Custo de transporte — não importa para stock (skip por defeito ao aprovar).</div>
                  ) : item.suggestions.length > 0 ? (
                    <div className="mt-2 space-y-1 border-t border-zinc-100 pt-2 dark:border-zinc-800">
                      <div className="text-[10px] uppercase text-zinc-500">Sugestões de match (Sprint 157 fuzzy por nome similar)</div>
                      {item.suggestions.map((s, si) => (
                        <div key={si} className="flex items-center justify-between rounded bg-zinc-50 px-2 py-1 text-xs dark:bg-zinc-800/50">
                          <span className="font-mono text-zinc-600 dark:text-zinc-300">{s.partSku}</span>
                          <span className="flex-1 truncate px-2">{s.partName}</span>
                          <ScoreBadge score={s.score} matchType={s.matchType} />
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="mt-2 text-[11px] italic text-zinc-400">Sem Parts no stock com nome similar — vai criar Part nova ao aprovar (SKU auto-gerado).</div>
                  )}
                </li>
              ))}
            </ul>
          </td>
        </tr>
      )}
    </>
  );
}

// Sprint 163b: tabela histórico — Approved/Rejected com botão Reprocess.
// Sprint 520: linhas expansíveis — clica para ver o que o parser importou de cada fatura.
function HistoryTable({
  data, onPdf, onReprocess, reprocessing,
}: {
  data: SupplierInvoiceImport[];
  onPdf: (id: string) => void;
  onReprocess: (id: string) => void;
  reprocessing: boolean;
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead className="text-xs uppercase text-zinc-500">
          <tr>
            <th className="px-2 py-2 text-left">Fornecedor</th>
            <th className="px-2 py-2 text-left">Documento</th>
            <th className="px-2 py-2 text-left">Estado</th>
            <th className="px-2 py-2 text-right">Total</th>
            <th className="px-2 py-2 text-right">Acções</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {data.map((x) => (
            <HistoryRow key={x.id} x={x} onPdf={onPdf} onReprocess={onReprocess} reprocessing={reprocessing} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

// Sprint 520: linha de histórico expansível — mostra os items que o parser extraiu da fatura
// (o pedido do Bruno: "clique abre dropdown em cada artigo para mostrar o que importou").
function HistoryRow({
  x, onPdf, onReprocess, reprocessing,
}: {
  x: SupplierInvoiceImport;
  onPdf: (id: string) => void;
  onReprocess: (id: string) => void;
  reprocessing: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const hasItems = !!x.items && x.items.length > 0;
  return (
    <>
      <tr className={hasItems ? 'cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-900' : ''} onClick={() => hasItems && setExpanded(!expanded)}>
        <td className="px-2 py-2 font-medium">
          {hasItems && <span className="mr-1 text-zinc-400">{expanded ? '▾' : '▸'}</span>}
          {x.fornecedorName ?? <span className="text-zinc-400">—</span>}
        </td>
        <td className="px-2 py-2">{x.documentNumber ?? <span className="text-zinc-400">—</span>}</td>
        <td className="px-2 py-2">
          <span className={`rounded px-1.5 py-0.5 text-xs font-medium ${x.status === 'Approved' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300' : 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-300'}`}>
            {x.status}
          </span>
        </td>
        <td className="px-2 py-2 text-right">{x.totalCents != null ? formatCents(x.totalCents) : <span className="text-zinc-400">—</span>}</td>
        <td className="px-2 py-2" onClick={(e) => e.stopPropagation()}>
          <div className="flex justify-end gap-1">
            <button type="button" onClick={() => onPdf(x.id)} className="rounded-md border border-zinc-300 bg-white px-2 py-1 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-900 dark:hover:bg-zinc-800" title="Abrir PDF">
              <FileText size={14} />
            </button>
            <button
              type="button"
              onClick={() => {
                if (confirm('Re-correr pipeline de parsing? A importação vai voltar para pendente.')) onReprocess(x.id);
              }}
              disabled={reprocessing}
              className="rounded-md border border-blue-300 bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 hover:bg-blue-100 disabled:opacity-60 dark:border-blue-800/40 dark:bg-blue-950/30 dark:text-blue-300"
              title="Re-correr parser+LLM (útil se Anthropic key foi adicionada depois)"
            >
              🔄 Reprocessar
            </button>
          </div>
        </td>
      </tr>
      {expanded && hasItems && (
        <tr className="bg-zinc-50/50 dark:bg-zinc-900/50">
          <td colSpan={5} className="px-4 py-3">
            <div className="mb-2 text-xs font-semibold uppercase text-zinc-500">O que o parser importou desta fatura ({x.items!.length})</div>
            <ul className="space-y-1.5">
              {x.items!.map((item, i) => {
                const isShipping = /\b(shipping|portes?|envio|transport|chronopost|dpd|ups|fedex|dhl|frete)\b/i.test(item.description);
                return (
                  <li key={i} className="flex items-start justify-between gap-3 rounded-md border border-zinc-200 bg-white px-2.5 py-1.5 dark:border-zinc-700 dark:bg-zinc-900">
                    <div className="flex-1">
                      <div className="text-sm">{item.description}</div>
                      {item.brand && <div className="text-[11px] text-zinc-500">{item.brand}{item.model ? ` ${item.model}` : ''}</div>}
                      {isShipping && <div className="text-[11px] italic text-zinc-400">🚚 Transporte — não entra em stock.</div>}
                    </div>
                    <div className="whitespace-nowrap text-xs text-zinc-500">
                      {item.quantity}× · <span className="font-medium text-zinc-700 dark:text-zinc-300">{formatCents(item.lineTotalCents)}</span>
                    </div>
                  </li>
                );
              })}
            </ul>
            <p className="mt-2 text-[11px] text-zinc-400">
              Para veres onde cada linha ficou (stock vs despesa), confirma em <strong>Stock (peças)</strong> ou <strong>Despesas &amp; custos</strong>.
            </p>
          </td>
        </tr>
      )}
    </>
  );
}

function ScoreBadge({ score, matchType }: { score: number; matchType: string }) {
  const pct = Math.round(score * 100);
  const tone = pct >= 70 ? 'emerald' : pct >= 50 ? 'amber' : 'zinc';
  const colors: Record<string, string> = {
    emerald: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
    amber: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
    zinc: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
  };
  return (
    <span className={`rounded px-1.5 py-0.5 text-[10px] font-medium ${colors[tone]}`} title={`${matchType} match`}>
      {pct}%
    </span>
  );
}

function ConfidenceBadge({ value }: { value: string | null }) {
  const map: Record<string, { label: string; cls: string }> = {
    High: { label: 'Alta', cls: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200' },
    Medium: { label: 'Média', cls: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200' },
    Low: { label: 'Baixa', cls: 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200' },
    None: { label: 'Falhou', cls: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300' },
  };
  const { label, cls } = map[value ?? 'None'] ?? map.None;
  return <span className={`rounded px-1.5 py-0.5 text-[11px] font-medium ${cls}`}>{label}</span>;
}

function ApproveModal({
  target, onClose, onSuccess,
}: {
  target: SupplierInvoiceImport;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [descricao, setDescricao] = useState(target.fornecedorName
    ? `${target.fornecedorName}${target.documentNumber ? ` · ${target.documentNumber}` : ''}`
    : 'Compra a fornecedor');
  // Sprint 543: pré-seleciona a categoria aprendida/conhecida do fornecedor (Anthropic→Software).
  const [categoria, setCategoria] = useState<DespesaCategoria>(
    (target.fornecedorDefaultDespesaCategoria as DespesaCategoria | null | undefined) ?? DESPESA_CATEGORIA.Pecas,
  );
  const [valor, setValor] = useState((target.totalCents ?? 0) / 100);
  const [data, setData] = useState(target.documentDate ? target.documentDate.slice(0, 10) : new Date().toISOString().slice(0, 10));
  const [fornecedor, setFornecedor] = useState(target.fornecedorName ?? '');
  const [numeroEncomenda, setNumeroEncomenda] = useState(target.documentNumber ?? '');
  const [notas, setNotas] = useState('');

  const approve = useMutation({
    mutationFn: (req: ApproveSupplierInvoiceRequest) => supplierInvoicesApi.approve(target.id, req),
    onSuccess: () => {
      toast.success('Importação aprovada', 'Despesa criada e disponível em Despesas.');
      onSuccess();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível aprovar.'),
  });

  return (
    <Modal
      open
      title="Aprovar importação → criar Despesa"
      onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100">Cancelar</button>
        <button
          type="button"
          disabled={approve.isPending || valor <= 0}
          onClick={() => approve.mutate({
            descricao,
            categoria,
            valorCents: Math.round(valor * 100),
            data,
            fornecedor: fornecedor || null,
            numeroEncomenda: numeroEncomenda || null,
            notas: notas || null,
          })}
          className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
        >
          {approve.isPending ? 'A criar…' : 'Aprovar + criar Despesa'}
        </button>
      </>}
    >
      <div className="space-y-3 text-sm">
        <p className="text-xs text-zinc-500">
          Confirma os dados antes de criar a Despesa. Valores extraídos pelo parser estão preenchidos —
          edita o que estiver mal.
        </p>
        <label className="block text-xs">
          <span className="text-zinc-500">Descrição</span>
          <input value={descricao} onChange={(e) => setDescricao(e.target.value)} className={inputCls} />
        </label>
        <div className="grid grid-cols-2 gap-2">
          <label className="block text-xs">
            <span className="text-zinc-500">Categoria</span>
            <select value={categoria} onChange={(e) => setCategoria(Number(e.target.value) as DespesaCategoria)} className={inputCls}>
              {Object.entries(DESPESA_CATEGORIA).map(([key, value]) => (
                <option key={key} value={value}>{DESPESA_LABEL[value as DespesaCategoria]}</option>
              ))}
            </select>
          </label>
          <label className="block text-xs">
            <span className="text-zinc-500">Valor (€)</span>
            <input
              type="number"
              step="0.01"
              value={valor}
              onChange={(e) => setValor(Number(e.target.value))}
              className={inputCls}
            />
          </label>
          <label className="block text-xs">
            <span className="text-zinc-500">Data</span>
            <input type="date" value={data} onChange={(e) => setData(e.target.value)} className={inputCls} />
          </label>
          <label className="block text-xs">
            <span className="text-zinc-500">Fornecedor</span>
            <input value={fornecedor} onChange={(e) => setFornecedor(e.target.value)} className={inputCls} />
          </label>
          <label className="block text-xs col-span-2">
            <span className="text-zinc-500">Nº encomenda / fatura</span>
            <input value={numeroEncomenda} onChange={(e) => setNumeroEncomenda(e.target.value)} className={inputCls} />
          </label>
          <label className="block text-xs col-span-2">
            <span className="text-zinc-500">Notas (opcional)</span>
            <textarea value={notas} onChange={(e) => setNotas(e.target.value)} rows={2} className={inputCls} />
          </label>
        </div>
      </div>
    </Modal>
  );
}

