import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Inbox, FileText, CheckCircle2, XCircle, AlertTriangle, Download } from 'lucide-react';
import { api } from '../../lib/api';
import { supplierInvoicesApi, type SupplierInvoiceImport, type ApproveSupplierInvoiceRequest, type ApproveAsStockItem } from '../../lib/supplierInvoices/api';
import { formatCents } from '../../lib/money';
import { toast } from '../../lib/toast';
import { DESPESA_CATEGORIA, DESPESA_LABEL, type DespesaCategoria } from '../../lib/despesas/types';
import Modal from '../../components/Modal';

const inputCls = 'mt-1 min-h-11 w-full rounded-md border border-zinc-300 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950';

export default function Importacoes() {
  const qc = useQueryClient();
  const pending = useQuery({
    queryKey: ['supplier-invoices-pending'],
    queryFn: () => supplierInvoicesApi.pending(100),
    refetchInterval: 30_000,
  });

  const [approveTarget, setApproveTarget] = useState<SupplierInvoiceImport | null>(null);
  const [stockTarget, setStockTarget] = useState<SupplierInvoiceImport | null>(null);
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

  // Sprint 160b: aprovar como stock — cria Parts + PartMovimentos + SkuMapping.
  const approveStock = useMutation({
    mutationFn: (req: { id: string; items: ApproveAsStockItem[]; learnDefaultAction?: boolean }) =>
      supplierInvoicesApi.approveAsStock(req.id, { items: req.items, learnDefaultAction: req.learnDefaultAction }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['supplier-invoices-pending'] });
      qc.invalidateQueries({ queryKey: ['parts'] });
      toast.success('Aprovada — items adicionados ao stock.');
      setStockTarget(null);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível aprovar como stock.'),
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

  return (
    <div className="mx-auto max-w-6xl space-y-4 px-4 py-6">
      <header className="space-y-2">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <h1 className="flex items-center gap-2 text-2xl font-semibold">
            <Inbox size={24} strokeWidth={2} />
            Importações de Fornecedor
          </h1>
          {/* Sprint 160c + 164: upload manual PDF / foto papel. */}
          <div className="flex gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept=".pdf,application/pdf"
              className="hidden"
              onChange={(e) => {
                const file = e.target.files?.[0];
                if (file) upload.mutate(file);
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
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              disabled={upload.isPending}
              className="rounded-md bg-zinc-900 px-3 py-2 text-sm font-medium text-white hover:bg-zinc-700 disabled:opacity-60 dark:bg-zinc-100 dark:text-zinc-900 dark:hover:bg-zinc-300"
              title="Upload manual de PDF (sem precisar de n8n IMAP)"
            >
              {upload.isPending ? 'A processar…' : '📎 PDF'}
            </button>
            <button
              type="button"
              onClick={() => photoInputRef.current?.click()}
              disabled={uploadPhoto.isPending}
              className="rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-60"
              title="Foto papel — Claude Vision faz OCR (mobile: usa câmara directamente)"
            >
              {uploadPhoto.isPending ? 'OCR…' : '📷 Foto papel'}
            </button>
          </div>
        </div>
        <p className="text-sm text-zinc-500">
          Facturas que chegaram via n8n IMAP automation ou upload manual. Revê os valores extraídos pelo parser e aprova
          (cria Despesa ou adiciona ao stock) ou rejeita. Os PDFs ficam guardados em <code className="rounded bg-zinc-100 px-1 dark:bg-zinc-800">/data/supplier-invoices/{`{tenant}`}/{`{ano}`}/{`{mês}`}/{`{fornecedor}`}/</code>.
        </p>
      </header>

      {/* Export ZIP para contabilista */}
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="flex items-center gap-2 text-sm font-semibold">
          <Download size={16} strokeWidth={2} />
          Export trimestral para contabilista
        </h2>
        <p className="mt-1 text-xs text-zinc-500">
          ZIP com todas as facturas aprovadas no período. Estrutura: <code>ano/mês/fornecedor/fatura.pdf</code>.
        </p>
        <div className="mt-3 flex flex-wrap items-end gap-2">
          <label className="text-xs">
            <span className="block text-zinc-500">De</span>
            <input type="date" value={exportFrom} onChange={(e) => setExportFrom(e.target.value)} className={inputCls} />
          </label>
          <label className="text-xs">
            <span className="block text-zinc-500">Até</span>
            <input type="date" value={exportTo} onChange={(e) => setExportTo(e.target.value)} className={inputCls} />
          </label>
          <button
            type="button"
            onClick={downloadZip}
            className="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
          >
            Descarregar ZIP
          </button>
        </div>
      </section>

      {/* Falhadas — destaque vermelho */}
      {failed.length > 0 && (
        <section className="rounded-xl border border-amber-200 bg-amber-50 p-4 dark:border-amber-900/40 dark:bg-amber-950/30">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-amber-900 dark:text-amber-200">
            <AlertTriangle size={16} strokeWidth={2} />
            {failed.length} fatura(s) onde o parser falhou
          </h2>
          <p className="mt-1 text-xs text-amber-800 dark:text-amber-300">
            Confidence "None" — Bruno precisa de abrir o PDF e meter valores manuais antes de aprovar.
          </p>
          <ImportsTable data={failed} onPdf={openPdf} onApproveStock={setStockTarget} onReject={(x) => { setRejectTarget(x); setRejectReason(''); }} />
        </section>
      )}

      {/* Sprint 163b: tabs Pendentes vs Histórico (Approved/Rejected). */}
      <section className="rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-center gap-1 border-b border-zinc-200 p-2 dark:border-zinc-800">
          <button
            type="button"
            onClick={() => setTab('pending')}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${tab === 'pending' ? 'bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100' : 'text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300'}`}
          >
            Pendentes ({ready.length})
          </button>
          <button
            type="button"
            onClick={() => setTab('history')}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${tab === 'history' ? 'bg-zinc-100 text-zinc-900 dark:bg-zinc-800 dark:text-zinc-100' : 'text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300'}`}
          >
            Histórico {history.data ? `(${history.data.length})` : ''}
          </button>
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
              <ImportsTable data={ready} onPdf={openPdf} onApproveStock={setStockTarget} onReject={(x) => { setRejectTarget(x); setRejectReason(''); }} />
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

      {stockTarget && (
        <ApproveStockModal
          target={stockTarget}
          onClose={() => setStockTarget(null)}
          onSubmit={(items, learnRule) => approveStock.mutate({ id: stockTarget.id, items, learnDefaultAction: learnRule })}
          submitting={approveStock.isPending}
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
  data, onPdf, onApproveStock, onReject,
}: {
  data: SupplierInvoiceImport[];
  onPdf: (id: string) => void;
  onApproveStock: (x: SupplierInvoiceImport) => void;
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
            <ImportRow key={x.id} x={x} onPdf={onPdf} onApproveStock={onApproveStock} onReject={onReject} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ImportRow({
  x, onPdf, onApproveStock, onReject,
}: {
  x: SupplierInvoiceImport;
  onPdf: (id: string) => void;
  onApproveStock: (x: SupplierInvoiceImport) => void;
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
            {/* Sprint 181: 1 botão único 'Aprovar'. Modal classifica items automáticamente
                (stock/despesa/skip) e Bruno só override se necessário. Removido o '🧾 Despesa
                overhead' que duplicava IVA no relatório. */}
            <button
              type="button"
              onClick={() => onApproveStock(x)}
              className="rounded-md bg-emerald-600 px-3 py-1 text-xs font-medium text-white hover:bg-emerald-700 flex items-center gap-1"
              title="Revê items e confirma — sistema classifica automáticamente"
              disabled={!x.items || x.items.length === 0}
            >
              <CheckCircle2 size={14} /> Aprovar
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
            <tr key={x.id}>
              <td className="px-2 py-2 font-medium">{x.fornecedorName ?? <span className="text-zinc-400">—</span>}</td>
              <td className="px-2 py-2">{x.documentNumber ?? <span className="text-zinc-400">—</span>}</td>
              <td className="px-2 py-2">
                <span className={`rounded px-1.5 py-0.5 text-xs font-medium ${x.status === 'Approved' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300' : 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-300'}`}>
                  {x.status}
                </span>
              </td>
              <td className="px-2 py-2 text-right">{x.totalCents != null ? formatCents(x.totalCents) : <span className="text-zinc-400">—</span>}</td>
              <td className="px-2 py-2">
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
          ))}
        </tbody>
      </table>
    </div>
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
  const [categoria, setCategoria] = useState<DespesaCategoria>(DESPESA_CATEGORIA.Pecas);
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

// Sprint 160b: modal para aprovar items como stock (Parts).
// Cada linha: dropdown action (existing partId / new sku+name / skip).
// Para "existing", mostra fuzzy match top 1 como sugestão default (do Sprint 158).
function ApproveStockModal({
  target, onClose, onSubmit, submitting,
}: {
  target: SupplierInvoiceImport;
  onClose: () => void;
  onSubmit: (items: ApproveAsStockItem[], learnRule: boolean) => void;
  submitting: boolean;
}) {
  // Sprint 163c: detecta items de transporte/portes — default action=skip.
  // Bruno não cria stock para shipping costs, é overhead.
  const SHIPPING_RX = /\b(shipping|portes?|envio|transport|chronopost|dpd|ups|fedex|dhl|frete)\b/i;

  // Estado inicial: default action conforme heurística + regra aprendida do fornecedor (Sprint 184).
  const supplierRule = target.fornecedorDefaultAction ?? 'auto';
  const initial: ApproveAsStockItem[] = (target.items ?? []).map((it) => {
    const top = it.suggestions[0];
    const lineUnit = it.quantity > 0 ? Math.round(it.lineTotalCents / it.quantity) : it.lineTotalCents;
    const isShipping = SHIPPING_RX.test(it.description);
    let action: ApproveAsStockItem['action'];
    if (isShipping) action = 'skip';
    else if (supplierRule === 'despesa') action = 'despesa';
    else if (top && top.score >= 0.7) action = 'existing';
    else action = 'new';
    return {
      description: it.description,
      quantity: it.quantity,
      unitCostCents: lineUnit,
      action,
      existingPartId: top && top.score >= 0.7 ? top.partId : null,
      newSku: '',
      newName: it.description.slice(0, 100),
      newMarca: it.brand ?? null,
      newModelo: it.model ?? null,
      supplierSku: null,
    };
  });
  const [items, setItems] = useState<ApproveAsStockItem[]>(initial);
  const [learnRule, setLearnRule] = useState(false);

  function patch(i: number, p: Partial<ApproveAsStockItem>) {
    setItems((arr) => arr.map((x, j) => (j === i ? { ...x, ...p } : x)));
  }

  const validItems = items.filter((x) => x.action !== 'skip');
  // Sprint 163d+181: SKU opcional (auto-gera no backend) + suporte despesa (não exige Part).
  const canSubmit = validItems.length > 0
    && validItems.every((x) =>
      (x.action === 'existing' && x.existingPartId)
      || (x.action === 'new' && (x.newName ?? '').trim().length > 0)
      || x.action === 'despesa');

  return (
    <Modal
      open
      title={`Confirmar importação — ${target.fornecedorName ?? 'fornecedor'}`}
      onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100">Cancelar</button>
        <button
          type="button"
          disabled={!canSubmit || submitting}
          onClick={() => onSubmit(items, learnRule)}
          className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
        >
          {submitting ? 'A confirmar…' : `Confirmar ${validItems.length} item(s)`}
        </button>
      </>}
    >
      <div className="space-y-3 text-sm">
        <p className="text-xs text-zinc-500">
          Cada linha tem classificação automática (Stock / Despesa / Skip). Revê o dropdown
          e ajusta se necessário. Stock cria PartMovimento Entrada (entra no inventário);
          Despesa cria Despesa avulsa Categoria=Peças (não vai para stock).
        </p>
        {/* Sprint 184: regra aprendida (se existe) + checkbox para aprender nova. */}
        {supplierRule !== 'auto' && supplierRule !== null && (
          <div className="rounded bg-blue-50 px-3 py-2 text-[11px] text-blue-700 dark:bg-blue-950/30 dark:text-blue-300">
            ℹ️ Regra aprendida para <strong>{target.fornecedorName}</strong>:
            items defaultam a <strong>{supplierRule === 'stock' ? '📦 Stock' : '🧾 Despesa avulsa'}</strong>.
            Podes editar abaixo.
          </div>
        )}
        {target.fornecedorId && (
          <label className="flex cursor-pointer items-center gap-2 text-[11px] text-zinc-600 dark:text-zinc-400">
            <input type="checkbox" checked={learnRule} onChange={(e) => setLearnRule(e.target.checked)} />
            <span>
              Lembrar regra para próximas faturas de <strong>{target.fornecedorName}</strong>
              {' '}(actualiza o default action baseado na maioria dos items abaixo)
            </span>
          </label>
        )}
        <ul className="space-y-3">
          {items.map((it, i) => {
            const original = target.items![i];
            return (
              <li key={i} className={`rounded-md border p-3 ${it.action === 'skip' ? 'border-zinc-200 bg-zinc-50/50 dark:border-zinc-800 dark:bg-zinc-900/50' : 'border-zinc-200 dark:border-zinc-700'}`}>
                <div className="mb-2 flex items-start justify-between gap-2">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 text-sm font-medium">
                      {it.description}
                      {SHIPPING_RX.test(it.description) && (
                        <span className="rounded bg-zinc-200 px-1.5 py-0.5 text-[10px] font-medium text-zinc-700 dark:bg-zinc-700 dark:text-zinc-300">
                          🚚 transporte
                        </span>
                      )}
                    </div>
                    <div className="mt-1 flex items-center gap-2 text-xs text-zinc-500">
                      <span>{it.quantity}× a</span>
                      <input
                        type="number"
                        step="0.01"
                        value={(it.unitCostCents / 100).toFixed(2)}
                        onChange={(e) => {
                          const euros = Number.parseFloat(e.target.value);
                          if (Number.isFinite(euros) && euros >= 0) patch(i, { unitCostCents: Math.round(euros * 100) });
                        }}
                        className="w-20 rounded border border-zinc-300 px-1 py-0.5 text-right text-xs dark:border-zinc-700 dark:bg-zinc-900"
                        title="Edita se o LLM extraiu o valor errado. Default é o total da linha (com IVA), que é o que pagaste."
                      />
                      <span>€ = {formatCents(it.quantity * it.unitCostCents)}</span>
                    </div>
                  </div>
                  <select
                    value={it.action}
                    onChange={(e) => patch(i, { action: e.target.value as 'existing' | 'new' | 'despesa' | 'skip' })}
                    className="rounded-md border border-zinc-300 bg-white px-2 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-900"
                  >
                    <option value="existing">📦 Stock — ligar a Part existente</option>
                    <option value="new">📦 Stock — criar Part nova</option>
                    <option value="despesa">🧾 Despesa avulsa (não cria stock)</option>
                    <option value="skip">⊘ Skip (não importar)</option>
                  </select>
                </div>

                {it.action === 'existing' && (
                  <div className="space-y-1">
                    <label className="block text-[11px] font-medium text-zinc-500">Sugestões fuzzy:</label>
                    {original.suggestions.length === 0 ? (
                      <div className="text-xs italic text-rose-600">Sem matches — usa "Criar Part nova" ou cola PartId manualmente.</div>
                    ) : (
                      <div className="space-y-1">
                        {original.suggestions.map((s) => (
                          <label key={s.partId} className="flex cursor-pointer items-center gap-2 rounded bg-zinc-50 px-2 py-1 text-xs dark:bg-zinc-800/50">
                            <input
                              type="radio"
                              name={`part-${i}`}
                              checked={it.existingPartId === s.partId}
                              onChange={() => patch(i, { existingPartId: s.partId })}
                            />
                            <span className="font-mono text-zinc-600 dark:text-zinc-300">{s.partSku}</span>
                            <span className="flex-1 truncate">{s.partName}</span>
                            <span className="rounded bg-zinc-200 px-1.5 py-0.5 text-[10px] font-medium dark:bg-zinc-700">
                              {Math.round(s.score * 100)}%
                            </span>
                          </label>
                        ))}
                      </div>
                    )}
                  </div>
                )}

                {it.action === 'new' && (
                  <div className="grid grid-cols-2 gap-2 text-xs">
                    <label className="block">
                      <span className="text-zinc-500">SKU <span className="text-[10px] text-zinc-400">(opcional · auto-gera)</span></span>
                      <input
                        value={it.newSku ?? ''}
                        onChange={(e) => patch(i, { newSku: e.target.value })}
                        placeholder="Auto · ou ex: LCD-HUA-P20L"
                        className={inputCls}
                      />
                    </label>
                    <label className="block">
                      <span className="text-zinc-500">Nome *</span>
                      <input
                        value={it.newName ?? ''}
                        onChange={(e) => patch(i, { newName: e.target.value })}
                        className={inputCls}
                      />
                    </label>
                    <label className="block">
                      <span className="text-zinc-500">Marca</span>
                      <input
                        value={it.newMarca ?? ''}
                        onChange={(e) => patch(i, { newMarca: e.target.value })}
                        className={inputCls}
                      />
                    </label>
                    <label className="block">
                      <span className="text-zinc-500">Modelo</span>
                      <input
                        value={it.newModelo ?? ''}
                        onChange={(e) => patch(i, { newModelo: e.target.value })}
                        className={inputCls}
                      />
                    </label>
                  </div>
                )}

                {it.action !== 'skip' && (
                  <label className="mt-2 block text-[11px]">
                    <span className="text-zinc-500">SKU do fornecedor (opcional — para o sistema aprender mapping):</span>
                    <input
                      value={it.supplierSku ?? ''}
                      onChange={(e) => patch(i, { supplierSku: e.target.value })}
                      placeholder="ex: 137491 (T4M), MLN-ABC123 (Molano)"
                      className={inputCls}
                    />
                  </label>
                )}
              </li>
            );
          })}
        </ul>
      </div>
    </Modal>
  );
}
