import { useQuery } from '@tanstack/react-query';
import { FileDown, Lock, ReceiptText } from 'lucide-react';
import { cashApi, DAILY_CLOSING_STATUS, type DailyClosingDto } from '../../lib/cash/api';
import { formatDateShort, formatDateTime } from '../../lib/dates';
import { downloadFile } from '../../lib/downloadPdf';
import { formatCents } from '../../lib/money';
import { toast } from '../../lib/toast';
import { EmptyState, SectionCard } from '../../components/ui';

/**
 * Sprint 384 (Doc 86): tab "Fecho & Z-Reports" do Balcão. Histórico dos últimos fechos de caixa
 * com a diferença (esperado vs real) e download do Z-Report PDF de cada dia — antes só dava para
 * descarregar o do dia corrente. Read-only; o fecho em si faz-se no tab "Caixa de hoje".
 */
export default function FechoZReports() {
  const recent = useQuery({
    queryKey: ['cash', 'recent', 30],
    queryFn: () => cashApi.recent(30, null),
    staleTime: 30_000,
  });

  async function baixarZReport(c: DailyClosingDto) {
    try {
      await downloadFile(cashApi.zReportPdfPath(c.id), `ZReport_${c.date}.pdf`);
    } catch (err) {
      toast.fromError(err, 'Não foi possível descarregar o Z-Report.');
    }
  }

  const items = recent.data ?? [];
  const fechados = items.filter((c) => c.status === DAILY_CLOSING_STATUS.Closed);

  return (
    <SectionCard title="Fecho & Z-Reports" bodyClassName="p-0">
      {recent.isLoading ? (
        <div className="p-4 text-sm text-zinc-500">A carregar…</div>
      ) : items.length === 0 ? (
        <div className="p-4">
          <EmptyState icon={ReceiptText} title="Sem fechos registados" description="Quando fechares a caixa, os Z-Reports aparecem aqui para consulta e download." />
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-[640px] w-full text-sm">
            <thead className="border-b border-zinc-200 text-xs text-zinc-500 dark:border-zinc-800">
              <tr>
                <th className="px-4 py-2.5 text-left font-medium">Dia</th>
                <th className="px-3 py-2.5 text-center font-medium">Estado</th>
                <th className="px-3 py-2.5 text-right font-medium">Esperado</th>
                <th className="px-3 py-2.5 text-right font-medium">Real</th>
                <th className="px-3 py-2.5 text-right font-medium">Diferença</th>
                <th className="px-4 py-2.5 text-right font-medium">Z-Report</th>
              </tr>
            </thead>
            <tbody>
              {items.map((c) => {
                const aberta = c.status === DAILY_CLOSING_STATUS.Open || c.status === DAILY_CLOSING_STATUS.Reopened;
                const diff = c.diffCents ?? 0;
                return (
                  <tr key={c.id} className="border-b border-zinc-100 last:border-0 hover:bg-zinc-50 dark:border-zinc-900 dark:hover:bg-zinc-900/40">
                    <td className="px-4 py-2.5">
                      <div className="font-medium">{formatDateShort(c.date)}</div>
                      {c.closedAt && <div className="text-xs text-zinc-500">fechada {formatDateTime(c.closedAt)}</div>}
                    </td>
                    <td className="px-3 py-2.5 text-center">
                      <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${
                        aberta
                          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
                          : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'
                      }`}>
                        {aberta ? 'Aberta' : <><Lock size={10} /> Fechada</>}
                      </span>
                    </td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums">{formatCents(c.expectedClosingCents)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums">{c.actualClosingCents != null ? formatCents(c.actualClosingCents) : '—'}</td>
                    <td className={`px-3 py-2.5 text-right font-mono tabular-nums ${
                      c.actualClosingCents == null ? 'text-zinc-400' : diff >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400'
                    }`}>
                      {c.actualClosingCents == null ? '—' : `${diff > 0 ? '+' : diff < 0 ? '-' : ''}${formatCents(Math.abs(diff))}`}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      {c.status === DAILY_CLOSING_STATUS.Closed ? (
                        <button
                          type="button"
                          onClick={() => baixarZReport(c)}
                          className="inline-flex items-center gap-1.5 rounded-md border border-zinc-200 px-2.5 py-1.5 text-xs font-medium hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800"
                        >
                          <FileDown size={13} /> PDF
                        </button>
                      ) : (
                        <span className="text-xs text-zinc-400">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      {fechados.length > 0 && (
        <p className="border-t border-zinc-100 px-4 py-2.5 text-xs text-zinc-500 dark:border-zinc-800">
          {fechados.length} fecho(s) nos últimos registos. O Z-Report é o documento de fecho diário (corte de caixa).
        </p>
      )}
    </SectionCard>
  );
}
