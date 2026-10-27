import { useState } from 'react';
import { Link } from 'react-router-dom';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Ban, FileText, Receipt, Search } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { formatEur } from '../../lib/compras/format';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { moloniApi, type MoloniDocumentoLista, type MoloniTipoDocumento } from '../../lib/moloni/api';
import { toast } from '../../lib/toast';
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL, type PaymentMethod } from '../../lib/vendas/types';

const TIPOS: { key: MoloniTipoDocumento; label: string }[] = [
  { key: 'Fatura', label: 'Faturas' },
  { key: 'FaturaRecibo', label: 'Faturas-recibo' },
  { key: 'Recibo', label: 'Recibos' },
  { key: 'Orcamento', label: 'Orçamentos' },
];

/**
 * Sprint 571 (antes página Faturação, Sprint 568): os documentos tal como estão no Moloni ON — incluindo os
 * emitidos fora do Mender. As vendas ligadas abrem com um clique; pagar ou anular aqui atualiza a venda.
 */
export default function DocumentosMoloniVista({ isAdmin }: { isAdmin: boolean }) {
  const estado = useQuery({ queryKey: ['moloni', 'estado'], queryFn: moloniApi.estado, staleTime: 5 * 60_000 });
  const [tipo, setTipo] = useState<MoloniTipoDocumento>('Fatura');
  if (estado.data && !estado.data.configurado) {
    return <EmptyState icon={FileText} title="Moloni ON por ligar" description="Liga-o em Definições → Moloni ON para veres aqui as faturas, recibos e orçamentos." />;
  }
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Tipo de documento">
        {TIPOS.map((t) => (
          <button key={t.key} type="button" role="tab" aria-selected={tipo === t.key} onClick={() => setTipo(t.key)}
            className={`rounded-full border px-3 py-1 text-sm ${tipo === t.key ? 'border-brand-500 bg-brand-50 font-medium text-brand-700 dark:bg-brand-950/40 dark:text-brand-300' : 'border-zinc-200 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}>
            {t.label}
          </button>
        ))}
      </div>
      <DocumentosMoloni key={tipo} tipo={tipo} isAdmin={isAdmin} />
    </div>
  );
}

function DocumentosMoloni({ tipo, isAdmin }: { tipo: MoloniTipoDocumento; isAdmin: boolean }) {
  const qc = useQueryClient();
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [pagar, setPagar] = useState<MoloniDocumentoLista | null>(null);
  const [metodo, setMetodo] = useState<PaymentMethod>(PAYMENT_METHOD.TransferenciaBancaria);
  const [anular, setAnular] = useState<MoloniDocumentoLista | null>(null);
  const [motivo, setMotivo] = useState('');
  const lista = useQuery({ queryKey: ['moloni', 'documentos', tipo, q, page], queryFn: () => moloniApi.documentos(tipo, q, page), placeholderData: keepPreviousData });

  const refrescar = () => { qc.invalidateQueries({ queryKey: ['moloni', 'documentos'] }); qc.invalidateQueries({ queryKey: ['vendas'] }); };
  const recibo = useMutation({
    mutationFn: () => moloniApi.reciboDeFatura(pagar!.documentId, metodo),
    onSuccess: () => { toast.success('Recibo emitido', pagar?.numero); setPagar(null); refrescar(); },
    onError: (err) => toast.fromError(err, 'Não foi possível emitir o recibo.'),
  });
  const anulacao = useMutation({
    mutationFn: () => moloniApi.anularDocumento(anular!.tipo, anular!.documentId, motivo.trim()),
    onSuccess: () => { toast.success('Documento anulado', anular?.numero); setAnular(null); setMotivo(''); refrescar(); },
    onError: (err) => toast.fromError(err, 'Não foi possível anular.'),
  });

  const docs = lista.data?.items ?? [];
  const paginas = Math.max(1, Math.ceil((lista.data?.total ?? 0) / 25));
  return (
    <SectionCard bodyClassName="p-0" title={
      <label className="relative block flex-1">
        <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} className={`${inputCls} pl-9`} placeholder="Cliente, NIF, nº…" aria-label="Procurar documentos" />
      </label>
    }>
      {lista.isLoading ? <p className="p-4 text-sm text-zinc-500">A ler do Moloni ON…</p>
        : docs.length === 0 ? <p className="p-4 text-sm text-zinc-500">Sem documentos.</p> : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[44rem] text-sm">
              <thead className="bg-zinc-50 text-left text-xs uppercase text-zinc-500 dark:bg-zinc-900">
                <tr><th className="px-3 py-2">Nº</th><th className="px-3 py-2">Data</th><th className="px-3 py-2">Cliente</th><th className="px-3 py-2 text-right">Total</th><th className="px-3 py-2">Estado</th><th className="px-3 py-2" /></tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {docs.map((d) => (
                  <tr key={d.documentId} className={d.anulado ? 'opacity-50' : ''}>
                    <td className="px-3 py-2 font-mono text-xs">
                      {d.numero}
                      {d.vendaId && <Link to={`/vendas/${d.vendaId}`} className="ml-2 font-sans text-brand-600 hover:underline">venda #{d.vendaNumero}</Link>}
                    </td>
                    <td className="px-3 py-2 tabular-nums">{d.data ? new Date(d.data).toLocaleDateString('pt-PT') : '—'}</td>
                    <td className="px-3 py-2">{d.cliente ?? '—'}{d.nif && <span className="block text-xs text-zinc-500">{d.nif}</span>}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{formatEur(d.total)}</td>
                    <td className="px-3 py-2">
                      {d.anulado ? <span className="text-xs text-rose-600">Anulado</span>
                        : d.rascunho ? <span className="text-xs text-zinc-500">Rascunho</span>
                        : d.pagoPercentagem == null ? <span className="text-xs text-emerald-700 dark:text-emerald-400">Finalizado</span>
                        : d.pagoPercentagem >= 100 ? <span className="text-xs text-emerald-700 dark:text-emerald-400">Paga</span>
                        : <span className="text-xs font-medium text-amber-700 dark:text-amber-400">Por pagar{d.pagoPercentagem > 0 ? ` (${d.pagoPercentagem}%)` : ''}</span>}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2 text-right">
                      <button type="button" title="PDF" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800"
                        onClick={() => openPdfInNewTab(moloniApi.documentoPdfPath(d.tipo, d.documentId)).catch((e) => toast.fromError(e, 'PDF ainda não disponível.'))}>
                        <FileText size={15} />
                      </button>
                      {d.tipo === 'Fatura' && !d.anulado && !d.rascunho && (d.pagoPercentagem ?? 0) < 100 && (
                        <button type="button" title="Registar pagamento (recibo)" className="rounded p-1 text-emerald-700 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => setPagar(d)}>
                          <Receipt size={15} />
                        </button>
                      )}
                      {isAdmin && !d.anulado && !d.rascunho && (
                        <button type="button" title="Anular" className="rounded p-1 text-rose-600 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => { setAnular(d); setMotivo(''); }}>
                          <Ban size={15} />
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      {paginas > 1 && (
        <div className="flex items-center justify-end gap-2 border-t border-zinc-100 p-3 text-sm dark:border-zinc-800">
          <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
          <span className="tabular-nums text-zinc-500">{page} / {paginas}</span>
          <Button type="button" variant="secondary" disabled={page >= paginas} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
        </div>
      )}

      <Modal open={pagar !== null} onClose={() => setPagar(null)} title={`Registar pagamento — ${pagar?.numero ?? ''}`}
        footer={<Button type="button" leftIcon={<Receipt size={15} />} loading={recibo.isPending} onClick={() => recibo.mutate()}>Emitir recibo</Button>}>
        <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">Emite no Moloni ON o recibo do valor em falta ({pagar ? formatEur(pagar.total) : ''}) — a fatura fica paga.</p>
        <label className="block"><span className={labelCls}>Pago por</span>
          <select value={metodo} onChange={(e) => setMetodo(Number(e.target.value) as PaymentMethod)} className={inputCls}>
            {Object.entries(PAYMENT_METHOD_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
        </label>
      </Modal>

      <Modal open={anular !== null} onClose={() => setAnular(null)} title={`Anular ${anular?.numero ?? ''}`}
        footer={<Button type="button" leftIcon={<Ban size={15} />} loading={anulacao.isPending} disabled={motivo.trim().length < 5} onClick={() => anulacao.mutate()}>Anular no Moloni ON</Button>}>
        <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
          A anulação é comunicada à AT e não se desfaz. Uma fatura já paga só se anula depois de anular o recibo
          {anular?.vendaId ? ' (sendo de uma venda do Mender, o recibo é anulado primeiro automaticamente)' : ''}.
        </p>
        <input value={motivo} onChange={(e) => setMotivo(e.target.value)} className={inputCls} placeholder="Motivo (ex.: emitida com o NIF errado)" autoFocus />
      </Modal>
    </SectionCard>
  );
}
