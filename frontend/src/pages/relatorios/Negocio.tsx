import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, BarChart3, Building2, PackageSearch, ReceiptText, TrendingUp, Wrench } from 'lucide-react';
import { EmptyState, PageHeader, SkeletonCard, SkeletonTable } from '../../components/ui';
import { formatCents } from '../../lib/money';
import { relatoriosApi, type FornecedorDefeito, type TopFornecedor, type TopPecaUsada, type TopReparacaoLucrativa } from '../../lib/relatorios/api';

const QUARTERS = [1, 2, 3, 4] as const;

export default function RelatorioNegocio() {
  const now = new Date();
  const [ano, setAno] = useState(now.getFullYear());
  const [trimestre, setTrimestre] = useState<1 | 2 | 3 | 4>((Math.floor(now.getMonth() / 3) + 1) as 1 | 2 | 3 | 4);

  const report = useQuery({
    queryKey: ['relatorio-negocio', ano, trimestre],
    queryFn: () => relatoriosApi.negocio(ano, trimestre),
  });

  // Sprint 187: análise B2B independente do trimestre — janela de 12 meses corridos por defeito.
  // Calcula-se sobre toda a base histórica para que defeitos manifestos tarde também contem.
  const [mesesDefeito, setMesesDefeito] = useState(12);
  const defeito = useQuery({
    queryKey: ['relatorio-defeito-fornecedor', mesesDefeito],
    queryFn: () => relatoriosApi.taxaDefeitoFornecedor(mesesDefeito),
  });

  return (
    <div className="space-y-5">
      <PageHeader
        title="Dashboard Negocio"
        description="Receita, custos e margem operacional sem misturar regras fiscais de IVA."
        meta={<span className="text-sm text-zinc-500">T{trimestre} {ano}</span>}
      />

      <section className="flex flex-wrap items-end gap-3">
        <div className="flex min-h-11 rounded-lg border border-zinc-200 bg-white p-1 dark:border-zinc-800 dark:bg-zinc-900">
          {QUARTERS.map((q) => (
            <button
              key={q}
              type="button"
              onClick={() => setTrimestre(q)}
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
            onChange={(e) => setAno(Number(e.target.value))}
            className="min-h-11 w-28 rounded-lg border border-zinc-300 bg-white px-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </label>
      </section>

      {report.isLoading ? (
        <>
          <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {[0, 1, 2, 3, 4, 5].map((i) => <SkeletonCard key={i} />)}
          </section>
          <SkeletonTable columns={4} rows={5} />
        </>
      ) : report.isError ? (
        <EmptyState icon={ReceiptText} title="Nao foi possivel carregar o dashboard" description="Confirma a ligacao ao servidor e tenta novamente." />
      ) : report.data ? (
        <>
          <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <KpiCard title="Receita total" value={formatCents(report.data.receitaTotalCents)} hint={`${formatCents(report.data.receitaReparacoesCents)} reparacoes · ${formatCents(report.data.receitaTrabalhosCents)} trabalhos · ${formatCents(report.data.receitaVendasCents)} vendas`} icon={TrendingUp} tone="brand" />
            <KpiCard title="Custo pecas" value={formatCents(report.data.custoPecasCents)} hint="Stock consumido em reparacoes no periodo" icon={PackageSearch} />
            <KpiCard title="OpEx" value={formatCents(report.data.opexCents)} hint="Despesas operacionais, exclui pecas/material" icon={ReceiptText} />
            <KpiCard title="Lucro bruto" value={formatCents(report.data.lucroBrutoCents)} hint="Receita - pecas - OpEx" icon={BarChart3} tone={report.data.lucroBrutoCents < 0 ? 'danger' : 'success'} />
            <KpiCard title="Margem media" value={`${report.data.margemMedia.toFixed(2)}%`} hint="Lucro bruto / receita" icon={TrendingUp} />
            <KpiCard title="Ticket medio" value={formatCents(report.data.ticketMedioCents)} hint={`${report.data.reparacoesPagasCount} reparacoes pagas`} icon={Wrench} />
          </section>

          <section className="grid grid-cols-1 gap-4 lg:grid-cols-3">
            <TopReparacoes rows={report.data.topReparacoesLucrativas} />
            <TopPecas rows={report.data.topPecasUsadas} />
            <TopFornecedores rows={report.data.topFornecedores} />
          </section>

          <TaxaDefeitoFornecedores
            data={defeito.data}
            loading={defeito.isLoading}
            meses={mesesDefeito}
            onChangeMeses={setMesesDefeito}
          />
        </>
      ) : null}
    </div>
  );
}

function KpiCard({
  title,
  value,
  hint,
  icon: Icon,
  tone,
}: {
  title: string;
  value: string;
  hint: string;
  icon: typeof BarChart3;
  tone?: 'brand' | 'success' | 'danger';
}) {
  const valueClass = tone === 'brand'
    ? 'text-brand-600 dark:text-brand-400'
    : tone === 'success'
      ? 'text-emerald-600 dark:text-emerald-400'
      : tone === 'danger'
        ? 'text-red-600 dark:text-red-400'
        : '';

  return (
    <article className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-xs uppercase tracking-wide text-zinc-500">{title}</div>
          <div className={`mt-1 text-2xl font-semibold tabular-nums ${valueClass}`}>{value}</div>
        </div>
        <span className="grid h-10 w-10 place-items-center rounded-lg bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-300">
          <Icon size={18} strokeWidth={1.75} aria-hidden />
        </span>
      </div>
      <p className="mt-2 text-xs leading-5 text-zinc-500">{hint}</p>
    </article>
  );
}

function TopReparacoes({ rows }: { rows: TopReparacaoLucrativa[] }) {
  return (
    <TopPanel title="Top reparacoes lucrativas" empty="Sem reparacoes pagas neste periodo.">
      {rows.map((row) => (
        <li key={row.id} className="space-y-1 rounded-lg border border-zinc-100 p-3 dark:border-zinc-800">
          <div className="flex items-start justify-between gap-3">
            <div>
              <div className="text-sm font-medium">#{row.numero} · {row.equipamento}</div>
              <div className="text-xs text-zinc-500">{row.clienteNome ?? 'Cliente nao identificado'}</div>
            </div>
            <span className="font-mono text-sm font-semibold tabular-nums text-emerald-600 dark:text-emerald-400">{formatCents(row.lucroCents)}</span>
          </div>
          <div className="text-xs text-zinc-500">{formatCents(row.receitaCents)} receita · {formatCents(row.custoPecasCents)} pecas</div>
        </li>
      ))}
    </TopPanel>
  );
}

function TopPecas({ rows }: { rows: TopPecaUsada[] }) {
  return (
    <TopPanel title="Top pecas usadas" empty="Sem pecas consumidas neste periodo.">
      {rows.map((row) => (
        <li key={row.partId} className="flex items-center justify-between gap-3 rounded-lg border border-zinc-100 p-3 dark:border-zinc-800">
          <div>
            <div className="text-sm font-medium">{row.nome}</div>
            <div className="text-xs text-zinc-500">{row.sku ?? 'Sem SKU'}</div>
          </div>
          <span className="rounded-full bg-zinc-100 px-2 py-1 text-xs font-semibold tabular-nums dark:bg-zinc-800">{row.quantidade} un.</span>
        </li>
      ))}
    </TopPanel>
  );
}

function TopFornecedores({ rows }: { rows: TopFornecedor[] }) {
  return (
    <TopPanel title="Top fornecedores" empty="Sem compras de stock neste periodo.">
      {rows.map((row) => (
        <li key={row.nome} className="flex items-center justify-between gap-3 rounded-lg border border-zinc-100 p-3 dark:border-zinc-800">
          <div className="flex items-center gap-2">
            <Building2 size={16} className="text-zinc-400" aria-hidden />
            <span className="text-sm font-medium">{row.nome}</span>
          </div>
          <span className="font-mono text-sm font-semibold tabular-nums">{formatCents(row.totalCompradoCents)}</span>
        </li>
      ))}
    </TopPanel>
  );
}

// Sprint 187: cruzamento Vendas (IMEI + Fornecedor) × Reparações (mesmo IMEI, criada depois).
// Mostra fornecedores com mais volume vendido primeiro quando empate em taxa.
function TaxaDefeitoFornecedores({
  data,
  loading,
  meses,
  onChangeMeses,
}: {
  data: { fornecedores: FornecedorDefeito[] } | undefined;
  loading: boolean;
  meses: number;
  onChangeMeses: (m: number) => void;
}) {
  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <AlertTriangle size={15} className="text-amber-500" aria-hidden />
            Taxa de defeito por fornecedor
          </h2>
          <p className="mt-1 text-xs text-zinc-500">
            % de equipamentos vendidos (com IMEI + fornecedor) que voltaram para reparação. Útil
            para decidir se mantér ou trocar de fornecedor B2B.
          </p>
        </div>
        <label className="flex items-center gap-2 text-xs text-zinc-500">
          Janela:
          <select
            value={meses}
            onChange={(e) => onChangeMeses(Number(e.target.value))}
            className="min-h-9 rounded-md border border-zinc-300 bg-white px-2 py-1 text-xs dark:border-zinc-700 dark:bg-zinc-950"
          >
            <option value={3}>3 meses</option>
            <option value={6}>6 meses</option>
            <option value={12}>12 meses</option>
            <option value={24}>24 meses</option>
          </select>
        </label>
      </div>

      {loading ? (
        <div className="mt-3"><SkeletonTable columns={4} rows={3} /></div>
      ) : !data || data.fornecedores.length === 0 ? (
        <p className="mt-3 rounded-lg bg-zinc-50 px-3 py-4 text-sm text-zinc-500 dark:bg-zinc-950">
          Sem vendas com IMEI + fornecedor identificado nesta janela.
        </p>
      ) : (
        <div className="mt-3 overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-[11px] uppercase text-zinc-500">
              <tr>
                <th className="py-2 pr-3">Fornecedor</th>
                <th className="py-2 pr-3 text-right">Vendidos</th>
                <th className="py-2 pr-3 text-right">Voltaram</th>
                <th className="py-2 pl-3 text-right">Taxa defeito</th>
              </tr>
            </thead>
            <tbody>
              {data.fornecedores.map((f) => (
                <tr key={f.nome} className="border-t border-zinc-100 dark:border-zinc-800">
                  <td className="py-2 pr-3">
                    <span className="flex items-center gap-2">
                      <Building2 size={14} className="text-zinc-400" aria-hidden />
                      <span className="font-medium">{f.nome}</span>
                    </span>
                  </td>
                  <td className="py-2 pr-3 text-right font-mono tabular-nums">{f.itemsVendidos}</td>
                  <td className="py-2 pr-3 text-right font-mono tabular-nums">{f.itemsComReparacao}</td>
                  <td className="py-2 pl-3 text-right">
                    <TaxaBadge pct={f.taxaDefeitoPct} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function TaxaBadge({ pct }: { pct: number }) {
  // Thresholds informais — refinar com base no histórico real. < 5% verde, 5-15 amarelo, > 15 vermelho.
  const cls = pct < 5
    ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300'
    : pct < 15
      ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300'
      : 'bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300';
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums ${cls}`}>
      {pct.toFixed(1)}%
    </span>
  );
}

function TopPanel({ title, empty, children }: { title: string; empty: string; children: React.ReactNode }) {
  const hasRows = Array.isArray(children) ? children.length > 0 : Boolean(children);
  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="text-sm font-semibold">{title}</h2>
      {hasRows ? (
        <ol className="mt-3 space-y-2">{children}</ol>
      ) : (
        <p className="mt-3 rounded-lg bg-zinc-50 px-3 py-4 text-sm text-zinc-500 dark:bg-zinc-950">{empty}</p>
      )}
    </section>
  );
}
