import { useEffect, useState } from 'react';
import { api } from '../lib/api';

interface HealthResponse {
  status: string;
  utc: string;
  version: string;
}

export default function Dashboard() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .get<HealthResponse>('/health')
      .then((r) => setHealth(r.data))
      .catch((e) => setError(e.message ?? 'unknown error'));
  }, []);

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Dashboard</h1>
        <p className="text-sm text-zinc-500">Visão geral do dia.</p>
      </header>

      <section className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[
          { label: 'Pedidos hoje', value: '—' },
          { label: 'Em curso', value: '—' },
          { label: 'Faturação dia', value: '—' },
          { label: 'Lucro mês', value: '—' },
        ].map((kpi) => (
          <div
            key={kpi.label}
            className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900"
          >
            <div className="text-xs uppercase tracking-wide text-zinc-500">{kpi.label}</div>
            <div className="mt-1 text-2xl font-semibold">{kpi.value}</div>
          </div>
        ))}
      </section>

      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="text-xs uppercase tracking-wide text-zinc-500">API health</div>
        {error && <div className="mt-2 text-sm text-red-600">⚠ {error}</div>}
        {health && (
          <pre className="mt-2 overflow-auto text-xs">
            {JSON.stringify(health, null, 2)}
          </pre>
        )}
        {!error && !health && <div className="mt-2 text-sm text-zinc-500">A carregar…</div>}
      </section>
    </div>
  );
}
