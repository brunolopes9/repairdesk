import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Sparkles, FileText, Camera, AlertCircle, Key, ExternalLink, CheckCircle2, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { api } from '../../lib/api';
import { toast } from '../../lib/toast';

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
interface LlmQuotaInfo {
  plan: 'Free' | 'Pro' | 'Enterprise';
  used: number;
  limit: number | null;
  allowed: boolean;
  reason: string | null;
}
interface LlmUsageResponse {
  thisMonth: LlmUsageSummary;
  prevMonth: LlmUsageSummary;
  lifetime: LlmUsageSummary;
  quota: LlmQuotaInfo;
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
  const qc = useQueryClient();
  const query = useQuery({
    queryKey: ['llm-usage'],
    queryFn: () => api.get<LlmUsageResponse>('/llm-usage/me').then((r) => r.data),
    refetchInterval: 60_000,
  });

  // Sprint 168: estado da Anthropic key per-tenant.
  const keyStatus = useQuery({
    queryKey: ['anthropic-key-status'],
    queryFn: () => api.get<{ configured: boolean; validatedAt: string | null }>('/llm-usage/anthropic-key/status').then((r) => r.data),
  });
  const [showKeyInput, setShowKeyInput] = useState(false);
  const [apiKeyInput, setApiKeyInput] = useState('');
  const setKey = useMutation({
    mutationFn: (apiKey: string) => api.post('/llm-usage/anthropic-key', { apiKey }).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['anthropic-key-status'] });
      qc.invalidateQueries({ queryKey: ['llm-usage'] });
      toast.success('Anthropic key configurada e validada.');
      setShowKeyInput(false);
      setApiKeyInput('');
    },
    onError: (err) => toast.fromError(err, 'Key inválida ou rejeitada pela Anthropic.'),
  });
  const removeKey = useMutation({
    mutationFn: () => api.delete('/llm-usage/anthropic-key').then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['anthropic-key-status'] });
      toast.warning('Anthropic key removida — features IA desactivadas.');
    },
    onError: (err) => toast.fromError(err, 'Falhou remover key.'),
  });

  if (query.isLoading) return <div className="p-6 text-sm text-zinc-500">A carregar uso LLM…</div>;
  if (query.isError || !query.data) return <div className="p-6 text-sm text-rose-600">Erro a carregar.</div>;

  const { thisMonth, prevMonth, lifetime, quota, recent } = query.data;
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

      {/* Sprint 172: Anthropic key BYOK opcional — central key (Reparo) é o default. */}
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex-1">
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <Key size={16} />
              Anthropic API key — avançado (opcional)
              {keyStatus.data?.configured ? (
                <span className="flex items-center gap-1 rounded bg-emerald-600 px-2 py-0.5 text-[10px] text-white">
                  <CheckCircle2 size={11} /> BYOK activo
                </span>
              ) : (
                <span className="rounded bg-zinc-500 px-2 py-0.5 text-[10px] text-white">Usa Reparo</span>
              )}
            </h2>
            <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
              {keyStatus.data?.configured ? (
                <>BYOK activo desde {keyStatus.data.validatedAt ? new Date(keyStatus.data.validatedAt).toLocaleString('pt-PT') : '?'}. Pagas Anthropic directamente — Reparo não conta para a quota do plano. Útil para grandes volumes ou requisitos RGPD estritos.</>
              ) : (
                <>Por defeito IA usa a infraestrutura Reparo — não precisas de fazer nada. Se preferires <strong>pagar Anthropic directamente</strong> (Bring Your Own Key) e remover quota do plano, cola aqui a tua key.</>
              )}
            </p>
            {!keyStatus.data?.configured && (
              <a href="https://console.anthropic.com/settings/keys" target="_blank" rel="noreferrer" className="mt-2 inline-flex items-center gap-1 text-xs text-brand-600 underline dark:text-brand-400">
                Criar key na Anthropic <ExternalLink size={11} />
              </a>
            )}
          </div>
          <div className="flex gap-2">
            {keyStatus.data?.configured ? (
              <button
                type="button"
                onClick={() => { if (confirm('Remover a key? Features IA ficam desactivadas.')) removeKey.mutate(); }}
                disabled={removeKey.isPending}
                className="flex items-center gap-1 rounded-md border border-rose-300 bg-white px-3 py-1.5 text-xs font-medium text-rose-700 hover:bg-rose-50 disabled:opacity-60"
              >
                <Trash2 size={12} /> Remover
              </button>
            ) : (
              <button
                type="button"
                onClick={() => setShowKeyInput(true)}
                className="rounded-md bg-brand-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-700"
              >
                Configurar key
              </button>
            )}
          </div>
        </div>
        {showKeyInput && !keyStatus.data?.configured && (
          <div className="mt-3 space-y-2 border-t border-zinc-200 pt-3 dark:border-zinc-700">
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300">API key Anthropic</label>
            <input
              type="password"
              value={apiKeyInput}
              onChange={(e) => setApiKeyInput(e.target.value)}
              placeholder="sk-ant-api03-..."
              className="w-full rounded-md border border-zinc-300 px-3 py-2 text-sm font-mono dark:border-zinc-700 dark:bg-zinc-950"
              autoFocus
            />
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setKey.mutate(apiKeyInput)}
                disabled={!apiKeyInput.startsWith('sk-ant-') || setKey.isPending}
                className="rounded-md bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white disabled:opacity-60"
              >
                {setKey.isPending ? 'A validar…' : 'Validar e gravar'}
              </button>
              <button type="button" onClick={() => { setShowKeyInput(false); setApiKeyInput(''); }} className="rounded-md px-3 py-1.5 text-xs text-zinc-600">
                Cancelar
              </button>
            </div>
            <div className="text-[11px] text-zinc-500">
              Encriptada no servidor (DataProtection). Validada com chamada a Anthropic /v1/models antes de gravar.
            </div>
          </div>
        )}
      </section>

      {/* Sprint 167b: card plano + quota */}
      <section className={`rounded-xl border p-4 ${quota.allowed ? 'border-emerald-200 bg-emerald-50/40 dark:border-emerald-900/40 dark:bg-emerald-950/20' : 'border-rose-300 bg-rose-50 dark:border-rose-800/40 dark:bg-rose-950/30'}`}>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="flex items-center gap-2 text-sm font-semibold">
              Plano: <span className="rounded bg-zinc-900 px-2 py-0.5 text-xs uppercase text-white dark:bg-zinc-100 dark:text-zinc-900">{quota.plan}</span>
              {!quota.allowed && <span className="rounded bg-rose-600 px-2 py-0.5 text-xs uppercase text-white">Quota esgotada</span>}
            </div>
            <div className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
              {quota.limit === null ? (
                <>Sem limite — Enterprise paga LLM com key própria.</>
              ) : (
                <>{quota.used} de {quota.limit} chamadas usadas este mês</>
              )}
            </div>
          </div>
          {quota.limit && (
            <div className="w-full sm:w-64">
              <div className="h-2 overflow-hidden rounded-full bg-zinc-200 dark:bg-zinc-800">
                <div
                  className={`h-full transition-all ${quota.used / quota.limit > 0.9 ? 'bg-rose-500' : quota.used / quota.limit > 0.75 ? 'bg-amber-500' : 'bg-emerald-500'}`}
                  style={{ width: `${Math.min(100, (quota.used / quota.limit) * 100)}%` }}
                />
              </div>
              <div className="mt-1 text-right text-[10px] text-zinc-500">
                {Math.round((quota.used / quota.limit) * 100)}% usado
              </div>
            </div>
          )}
        </div>
        {!quota.allowed && (
          <div className="mt-2 text-xs text-rose-700 dark:text-rose-300">
            Upload de PDF e foto papel via Claude estão bloqueados até ao próximo mês ou upgrade de plano.
          </div>
        )}
      </section>

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
        <strong>Planos:</strong> Free = 100 chamadas/mês · Pro = 1000/mês · Enterprise = ilimitado (key própria).
        Para mudar plano, contacta o admin LopesTech (UI de upgrade em sprint futuro).
        <br /><strong>Preços snapshot:</strong> Claude Haiku 4.5 = $1/M input + $5/M output · prompt cache 90% off · ~0,5¢/fatura.
      </div>
    </div>
  );
}
