import { useQuery } from '@tanstack/react-query';
import { Sparkles, FileText, Camera, AlertCircle } from 'lucide-react';
import { api } from '../../lib/api';

/**
 * Sprint 167a: dashboard de uso LLM Anthropic. Bruno vê:
 * - Gasto este mês (€ + nº chamadas)
 * - Comparação com mês anterior
 * - Breakdown por operação (parse-pdf, parse-image, …)
 * - Últimas 20 chamadas com tokens + custo
 *
 * Custos em microcêntimos (10000 µ¢ = 1€) para precisão sub-cêntimo.
 */
interface LlmUsageBreakdown {
  operation: string;
  calls: number;
  costMicrocents: number;
}
interface LlmUsageSummary {
  totalCalls: number;
  okCalls: number;
  errorCalls: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalCostMicrocents: number;
  byOperation: LlmUsageBreakdown[];
}
interface LlmUsageRecent {
  createdAt: string;
  operation: string;
  model: string;
  inputTokens: number;
  outputTokens: number;
  costMicrocents: number;
  outcome: string;
}
interface LlmUsageResponse {
  thisMonth: LlmUsageSummary;
  prevMonth: LlmUsageSummary;
  lifetime: LlmUsageSummary;
  recent: LlmUsageRecent[];
}

function microcentsToEuros(microcents: number): string {
  // 1€ = 100¢ = 100000 µ¢? Não — 1¢ = 1000 µ¢, 1€ = 100000 µ¢.
  // Mas no LlmUsageTracker, "microcents" significa 1¢ = 1000 µ¢ → 1€ = 100_000 µ¢.
  // Vamos formatar com 4 casas decimais para sub-cêntimo.
  const euros = microcents / 100_000;
  return new Intl.NumberFormat('pt-PT', {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 4,
    maximumFractionDigits: 4,
  }).format(euros);
}

function operationLabel(op: string): { label: string; icon: React.ComponentType<{ size?: number }> } {
  switch (op) {
    case 'parse-pdf': return { label: 'Parse PDF fatura', icon: FileText };
    case 'parse-image': return { label: 'Vision foto papel', icon: Camera };
    case 'alt-text': return { label: 'Alt text imagem', icon: Sparkles };
    default: return { label: op, icon: Sparkles };
  }
}

export default function LlmUsage() {
  const query = useQuery({
    queryKey: ['llm-usage'],
    queryFn: () => api.get<LlmUsageResponse>('/llm-usage/me').then((r) => r.data),
    refetchInterval: 60_000,
  });

  if (query.isLoading) return <div className="p-6 text-sm text-zinc-500">A carregar uso LLM…</div>;
  if (query.isError || !query.data) return <div className="p-6 text-sm text-rose-600">Erro a carregar.</div>;

  const { thisMonth, prevMonth, lifetime, recent } = query.data;
  const monthDelta = thisMonth.totalCostMicrocents - prevMonth.totalCostMicrocents;

  return (
    <div className="mx-auto max-w-5xl space-y-4 px-4 py-6">
      <header className="space-y-2">
        <h1 className="flex items-center gap-2 text-2xl font-semibold">
          <Sparkles size={24} strokeWidth={2} />
          Uso de IA (Anthropic Claude)
        </h1>
        <p className="text-sm text-zinc-500">
          Cada chamada à API Claude (parse de fatura, OCR de foto, alt text de imagem) regista-se aqui.
          Útil para conferir custo + escolher plano SaaS correcto.
        </p>
      </header>

      {/* KPIs principais */}
      <section className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="rounded-xl border border-brand-200 bg-brand-50/50 p-4 dark:border-brand-900/40 dark:bg-brand-950/20">
          <div className="text-xs uppercase tracking-wide text-brand-700 dark:text-brand-300">Este mês</div>
          <div className="mt-1 text-2xl font-bold text-brand-700 dark:text-brand-300">
            {microcentsToEuros(thisMonth.totalCostMicrocents)}
          </div>
          <div className="mt-1 text-xs text-zinc-500">
            {thisMonth.totalCalls} chamada(s) · {thisMonth.errorCalls > 0 && <span className="text-rose-600">{thisMonth.errorCalls} erro(s) · </span>}
            {(thisMonth.totalInputTokens + thisMonth.totalOutputTokens).toLocaleString()} tokens
          </div>
          {prevMonth.totalCostMicrocents > 0 && (
            <div className={`mt-1 text-[11px] ${monthDelta > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
              {monthDelta > 0 ? '↑' : '↓'} {microcentsToEuros(Math.abs(monthDelta))} vs mês anterior
            </div>
          )}
        </div>

        <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
          <div className="text-xs uppercase tracking-wide text-zinc-500">Mês anterior</div>
          <div className="mt-1 text-2xl font-semibold">{microcentsToEuros(prevMonth.totalCostMicrocents)}</div>
          <div className="mt-1 text-xs text-zinc-500">{prevMonth.totalCalls} chamada(s)</div>
        </div>

        <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
          <div className="text-xs uppercase tracking-wide text-zinc-500">Lifetime</div>
          <div className="mt-1 text-2xl font-semibold">{microcentsToEuros(lifetime.totalCostMicrocents)}</div>
          <div className="mt-1 text-xs text-zinc-500">{lifetime.totalCalls} chamada(s)</div>
        </div>
      </section>

      {/* Breakdown por operação */}
      {thisMonth.byOperation.length > 0 && (
        <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
          <h2 className="text-sm font-semibold">Este mês por operação</h2>
          <ul className="mt-2 space-y-2">
            {thisMonth.byOperation.map((b) => {
              const meta = operationLabel(b.operation);
              const Icon = meta.icon;
              const pct = thisMonth.totalCostMicrocents > 0
                ? (b.costMicrocents / thisMonth.totalCostMicrocents) * 100 : 0;
              return (
                <li key={b.operation} className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span className="flex items-center gap-2">
                      <Icon size={14} />
                      {meta.label}
                    </span>
                    <span className="font-mono">
                      {microcentsToEuros(b.costMicrocents)} <span className="text-zinc-400">· {b.calls}×</span>
                    </span>
                  </div>
                  <div className="h-1.5 overflow-hidden rounded bg-zinc-100 dark:bg-zinc-800">
                    <div className="h-full bg-brand-500" style={{ width: `${pct}%` }} />
                  </div>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      {/* Últimas chamadas */}
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h2 className="text-sm font-semibold">Últimas {recent.length} chamadas</h2>
        {recent.length === 0 ? (
          <div className="py-4 text-center text-sm text-zinc-500">Ainda sem uso registado.</div>
        ) : (
          <div className="mt-2 overflow-x-auto">
            <table className="w-full text-xs">
              <thead className="text-[10px] uppercase text-zinc-500">
                <tr>
                  <th className="px-2 py-1 text-left">Quando</th>
                  <th className="px-2 py-1 text-left">Operação</th>
                  <th className="px-2 py-1 text-right">In</th>
                  <th className="px-2 py-1 text-right">Out</th>
                  <th className="px-2 py-1 text-right">Custo</th>
                  <th className="px-2 py-1 text-left">Estado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {recent.map((r, i) => {
                  const meta = operationLabel(r.operation);
                  return (
                    <tr key={i}>
                      <td className="px-2 py-1 tabular-nums">{new Date(r.createdAt).toLocaleString('pt-PT')}</td>
                      <td className="px-2 py-1">{meta.label}</td>
                      <td className="px-2 py-1 text-right font-mono tabular-nums">{r.inputTokens.toLocaleString()}</td>
                      <td className="px-2 py-1 text-right font-mono tabular-nums">{r.outputTokens.toLocaleString()}</td>
                      <td className="px-2 py-1 text-right font-mono tabular-nums">{microcentsToEuros(r.costMicrocents)}</td>
                      <td className="px-2 py-1">
                        {r.outcome === 'ok' ? (
                          <span className="text-emerald-600">OK</span>
                        ) : (
                          <span className="flex items-center gap-1 text-rose-600"><AlertCircle size={11} />{r.outcome}</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <div className="rounded-md border border-zinc-200 bg-zinc-50 p-3 text-xs text-zinc-600 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-400">
        <strong>Preços snapshot (Maio 2026):</strong> Claude Haiku 4.5 = $1/M input, $5/M output ·
        prompt cache 90% off · 1 fatura típica ~0,5 cêntimos.
        Quota enforcement + tiers SaaS (free/pro/enterprise) virão em Sprint 167b.
      </div>
    </div>
  );
}
