import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download, FileText, ReceiptText } from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonCard, SkeletonTable } from '../../components/ui';
import { downloadFile, openPdfInNewTab } from '../../lib/downloadPdf';
import { formatCents, parseEuros } from '../../lib/money';
import { relatoriosApi, type RelatorioIvaDocumento } from '../../lib/relatorios/api';

const QUARTERS = [1, 2, 3, 4] as const;
const PAGE_SIZE = 20;

export default function RelatorioIva() {
  const now = new Date();
  const [ano, setAno] = useState(now.getFullYear());
  const [trimestre, setTrimestre] = useState<1 | 2 | 3 | 4>((Math.floor(now.getMonth() / 3) + 1) as 1 | 2 | 3 | 4);
  const [ivaCompras, setIvaCompras] = useState('');
  const [page, setPage] = useState(1);

  const ivaComprasCents = Math.max(0, parseEuros(ivaCompras) ?? 0);
  const report = useQuery({
    queryKey: ['relatorio-iva', ano, trimestre, ivaComprasCents],
    queryFn: () => relatoriosApi.iva(ano, trimestre, ivaComprasCents),
  });

  const docs = report.data?.documentos ?? [];
  const totalPages = Math.max(1, Math.ceil(docs.length / PAGE_SIZE));
  const pageDocs = useMemo(() => docs.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE), [docs, page]);
  const query = `ano=${ano}&trimestre=${trimestre}&ivaComprasCents=${ivaComprasCents}`;

  function setQuarter(q: 1 | 2 | 3 | 4) {
    setTrimestre(q);
    setPage(1);
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Relatorio IVA"
        description="Resumo trimestral para conferir com SAFT/Moloni antes da declaracao na AT."
        meta={<span className="text-sm text-zinc-500">{docs.length} {docs.length === 1 ? 'documento' : 'documentos'}</span>}
        actions={
          <>
            <Button type="button" variant="secondary" leftIcon={<Download size={15} />} onClick={() => downloadFile(`/relatorios/iva/export.csv?${query}`, `relatorio_iva_${ano}_T${trimestre}.csv`)}>
              Exportar CSV
            </Button>
            <Button type="button" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(`/relatorios/iva/export.pdf?${query}`)}>
              Exportar PDF
            </Button>
          </>
        }
      />

      <section className="flex flex-wrap items-end gap-3">
        <div className="flex min-h-11 rounded-lg border border-zinc-200 bg-white p-1 dark:border-zinc-800 dark:bg-zinc-900">
          {QUARTERS.map((q) => (
            <button
              key={q}
              type="button"
              onClick={() => setQuarter(q)}
              className={`min-h-9 rounded-md px-3 text-sm font-medium transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${
                trimestre === q ? 'bg-brand-600 text-white' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'
              }`}
            >
              T{q}
            </button>
          ))}
        </div>

        <label className="block">
          <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">Ano</span>
          <input
            type="number"
            min={2000}
            max={2100}
            value={ano}
            onChange={(e) => { setAno(Number(e.target.value)); setPage(1); }}
            className="min-h-11 w-28 rounded-lg border border-zinc-300 bg-white px-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>

        <label className="block min-w-56">
          <span className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">IVA compras intra-UE manual</span>
          <input
            type="text"
            inputMode="decimal"
            value={ivaCompras}
            onChange={(e) => setIvaCompras(e.target.value)}
            placeholder="0,00"
            className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
      </section>

      {report.isLoading ? (
        <>
          <section className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <SkeletonCard />
            <SkeletonCard />
            <SkeletonCard />
          </section>
          <SkeletonTable columns={7} rows={8} />
        </>
      ) : report.isError ? (
        <EmptyState icon={ReceiptText} title="Nao foi possivel carregar o relatorio" description="Confirma a ligacao ao servidor e tenta novamente." />
      ) : report.data ? (
        <>
          <section className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <Kpi title="Total sem IVA" value={report.data.totalSemIvaCents} />
            <Kpi title="IVA liquidado" value={report.data.ivaLiquidadoCents} />
            <Kpi title="IVA a entregar" value={report.data.ivaAEntregarCents} tone="brand" />
          </section>

          <ComparisonChart
            current={report.data.totalSemIvaCents}
            previous={report.data.trimestreAnteriorTotalSemIvaCents}
            currentVat={report.data.ivaLiquidadoCents}
            previousVat={report.data.trimestreAnteriorIvaLiquidadoCents}
          />

          {docs.length === 0 ? (
            <EmptyState icon={ReceiptText} title="Sem documentos faturados neste trimestre" description="Quando houver reparacoes ou trabalhos com fatura emitida, aparecem aqui." />
          ) : (
            <>
              <DocumentsTable docs={pageDocs} />
              {totalPages > 1 && (
                <div className="flex items-center justify-between text-xs text-zinc-500">
                  <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">
                    Anterior
                  </button>
                  <span>{page} / {totalPages}</span>
                  <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">
                    Seguinte
                  </button>
                </div>
              )}
            </>
          )}
        </>
      ) : null}
    </div>
  );
}

function Kpi({ title, value, tone }: { title: string; value: number; tone?: 'brand' }) {
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="text-xs uppercase tracking-wide text-zinc-500">{title}</div>
      <div className={`mt-1 text-2xl font-semibold ${tone === 'brand' ? 'text-brand-600 dark:text-brand-400' : ''}`}>{formatCents(value)}</div>
    </div>
  );
}

function ComparisonChart({ current, previous, currentVat, previousVat }: { current: number; previous: number; currentVat: number; previousVat: number }) {
  const max = Math.max(1, current, previous);
  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-sm font-semibold">Comparacao com trimestre anterior</h2>
        <span className="text-xs text-zinc-500">receita sem IVA</span>
      </div>
      <div className="space-y-3">
        <Bar label="Trimestre atual" value={current} vat={currentVat} pct={Math.max(4, Math.round((current / max) * 100))} className="bg-brand-500" />
        <Bar label="Trimestre anterior" value={previous} vat={previousVat} pct={Math.max(4, Math.round((previous / max) * 100))} className="bg-zinc-400" />
      </div>
    </section>
  );
}

function Bar({ label, value, vat, pct, className }: { label: string; value: number; vat: number; pct: number; className: string }) {
  return (
    <div className="grid gap-2 sm:grid-cols-[140px_1fr_180px] sm:items-center">
      <div className="text-sm font-medium">{label}</div>
      <div className="h-3 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
        <div className={`h-full rounded-full ${className}`} style={{ width: `${pct}%` }} />
      </div>
      <div className="text-sm text-zinc-600 dark:text-zinc-300">{formatCents(value)} <span className="text-xs text-zinc-500">IVA {formatCents(vat)}</span></div>
    </div>
  );
}

function DocumentsTable({ docs }: { docs: RelatorioIvaDocumento[] }) {
  return (
    <section className="overflow-x-auto rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
      <table className="min-w-[760px] divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
        <thead className="bg-zinc-50 text-xs uppercase tracking-wide text-zinc-500 dark:bg-zinc-950">
          <tr>
            <th className="px-3 py-2 text-left">Data</th>
            <th className="px-3 py-2 text-left">Tipo</th>
            <th className="px-3 py-2 text-left">Numero</th>
            <th className="px-3 py-2 text-left">Cliente</th>
            <th className="px-3 py-2 text-right">Base</th>
            <th className="px-3 py-2 text-right">IVA</th>
            <th className="px-3 py-2 text-right">Total</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {docs.map((d) => (
            <tr key={`${d.tipo}-${d.id}`}>
              <td className="px-3 py-2 text-zinc-500">{new Date(d.data).toLocaleDateString('pt-PT', { timeZone: 'Europe/Lisbon' })}</td>
              <td className="px-3 py-2">{d.tipo}</td>
              <td className="px-3 py-2 font-mono text-xs">{d.numeroDocumento}</td>
              <td className="px-3 py-2">{d.cliente}</td>
              <td className="px-3 py-2 text-right tabular-nums">{formatCents(d.baseCents)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{formatCents(d.ivaCents)}</td>
              <td className="px-3 py-2 text-right font-semibold tabular-nums">{formatCents(d.totalCents)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
