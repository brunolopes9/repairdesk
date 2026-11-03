import { useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { FileText, Paperclip } from 'lucide-react';
import { despesasApi } from '../../lib/despesas/api';
import type { Despesa } from '../../lib/despesas/types';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import { toast } from '../../lib/toast';

/**
 * Sprint 586: a fatura da despesa — abre o PDF (para ver e imprimir, como nas compras) ou, se a despesa foi
 * criada à mão, deixa anexá-lo. `compact` = só o ícone (tabela); senão botão com texto (formulário).
 */
export function FaturaDespesa({ despesa, compact = false, onAnexada }: { despesa: Despesa; compact?: boolean; onAnexada?: (d: Despesa) => void }) {
  const qc = useQueryClient();
  const input = useRef<HTMLInputElement>(null);
  const anexar = useMutation({
    mutationFn: (file: File) => despesasApi.anexarPdf(despesa.id, file),
    onSuccess: (d) => {
      qc.invalidateQueries({ queryKey: ['despesas'] });
      qc.setQueryData(['despesa', d.id], d);
      toast.success('Fatura anexada', `Despesa nº ${d.numero ?? ''}`);
      onAnexada?.(d);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível anexar a fatura.'),
  });

  const base = compact
    ? 'grid h-8 w-8 place-items-center rounded-md focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400'
    : 'inline-flex min-h-9 items-center gap-1.5 rounded-md border px-3 text-sm font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400';

  if (despesa.supplierInvoiceImportId) {
    const importId = despesa.supplierInvoiceImportId;
    return (
      <button type="button" title="Abrir a fatura (PDF)" aria-label="Abrir a fatura"
        onClick={(e) => { e.stopPropagation(); openPdfInNewTab(supplierInvoicesApi.pdfPath(importId)).catch((err) => toast.fromError(err, 'Não foi possível abrir o PDF.')); }}
        className={`${base} ${compact ? 'text-emerald-600 hover:bg-emerald-50 dark:text-emerald-400 dark:hover:bg-emerald-950/40' : 'border-zinc-300 text-zinc-700 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800'}`}>
        <FileText size={compact ? 15 : 14} />{!compact && 'PDF da fatura'}
      </button>
    );
  }

  return (
    <>
      <input ref={input} type="file" accept="application/pdf,image/jpeg,image/png,image/webp" className="hidden"
        onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; if (f) anexar.mutate(f); }} />
      <button type="button" disabled={anexar.isPending} title="Anexar o PDF da fatura" aria-label="Anexar a fatura"
        onClick={(e) => { e.stopPropagation(); input.current?.click(); }}
        className={`${base} ${compact ? 'text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800' : 'border-dashed border-zinc-300 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800'} disabled:opacity-50`}>
        <Paperclip size={compact ? 15 : 14} />{!compact && (anexar.isPending ? 'A anexar…' : 'Anexar PDF da fatura')}
      </button>
    </>
  );
}
