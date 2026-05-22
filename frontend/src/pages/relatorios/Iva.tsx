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
          {/* Sprint 178: Relatório só fiscal. Lucro/margem/COGS = página de Negócio (futuro).
              IVA dedutível nasce na COMPRA (entrada em stock ou despesa), não no consumo.
              Foi o erro conceptual da Sprint 159 que Bruno+ChatGPT identificaram. */}
          <section className="space-y-4">
            <div className="rounded-md border border-zinc-200 bg-zinc-50 px-3 py-2 text-xs text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400">
              <strong>Apenas fiscal.</strong> IVA dedutível nasce no momento da compra (entrada
              em stock ou despesa registada), <em>não</em> no consumo. Para lucro real, margem por
              reparação e custo de peças, consulta o Dashboard ou página de Negócio (futuro).
            </div>
            {/* Bloco Vendas */}
            <div className="rounded-xl border border-blue-200 bg-blue-50/50 p-4 dark:border-blue-900/40 dark:bg-blue-950/20">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-blue-700 dark:text-blue-300">
                🟦 Vendas — IVA liquidado (cobrado aos clientes)
              </div>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <Kpi title="Total facturado sem IVA" value={report.data.totalSemIvaCents} />
                <Kpi title="IVA liquidado" value={report.data.ivaLiquidadoCents} tone="brand" />
              </div>
            </div>

            {/* Bloco Compras dedutíveis */}
            <div className="rounded-xl border border-emerald-200 bg-emerald-50/50 p-4 dark:border-emerald-900/40 dark:bg-emerald-950/20">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-emerald-700 dark:text-emerald-300">
                🟩 Compras — IVA dedutível (auto + manual)
              </div>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <KpiSmall title="Compras de stock (auto)" value={report.data.ivaDedutivelPecasCents}
                  hint="IVA pago nas peças que entraram em stock no período (PartMovimento Entrada)" />
                <KpiSmall title="Despesas operacionais (auto)" value={report.data.ivaDedutivelDespesasCents}
                  hint="IVA das despesas overhead (rent, ferramentas, …) imputadas a trabalhos pagos ou sem imputação" />
                <KpiSmall title="Compras manuais (input)" value={report.data.ivaComprasCents}
                  hint="Campo acima — fornecedores fora do RepairDesk (ex: portes pagos por fora)" />
              </div>
              <div className="mt-3 rounded bg-emerald-100/60 px-3 py-2 text-sm dark:bg-emerald-900/30">
                <strong>Total dedutível:</strong> {formatCents(report.data.ivaDedutivelTotalCents)}
              </div>
            </div>

            {/* Bloco a entregar */}
            <div className="rounded-xl border-2 border-amber-300 bg-amber-50 p-4 dark:border-amber-700/60 dark:bg-amber-950/30">
              <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-amber-800 dark:text-amber-200">
                🟥 A entregar à AT — trimestre {report.data.trimestre} {report.data.ano}
              </div>
              <div className="text-3xl font-bold text-amber-900 dark:text-amber-100">
                {formatCents(report.data.ivaAEntregarCents)}
              </div>
              <div className="mt-1 text-xs text-amber-800/80 dark:text-amber-200/80">
                {formatCents(report.data.ivaLiquidadoCents)} liquidado − {formatCents(report.data.ivaDedutivelTotalCents)} dedutível
              </div>
              <div className="mt-2 text-[11px] italic text-amber-700 dark:text-amber-300">
                ⚠️ Este valor é uma <strong>estimativa de controlo interno</strong>. O valor oficial é o do SAF-T Moloni que o contabilista entrega à AT.
              </div>
            </div>
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

function KpiSmall({ title, value, hint }: { title: string; value: number; hint?: string }) {
  return (
    <div className="rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-700 dark:bg-zinc-900">
      <div className="text-[11px] uppercase tracking-wide text-zinc-500">{title}</div>
      <div className="mt-0.5 text-lg font-semibold tabular-nums">{formatCents(value)}</div>
      {hint && <div className="mt-0.5 text-[10px] text-zinc-500">{hint}</div>}
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
