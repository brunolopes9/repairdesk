import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { TrendingUp } from 'lucide-react';
import { SectionCard } from '../../components/ui';
import { formatEur } from '../../lib/compras/format';
import type { PainelDia } from '../../lib/dashboard/api';

const diaCurto = new Intl.DateTimeFormat('pt-PT', { day: 'numeric', month: 'short', timeZone: 'UTC' });
const diaLongo = new Intl.DateTimeFormat('pt-PT', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC' });

/** Faturado por dia nos últimos 30 dias (vendas entregues). O dia de hoje fica com a cor forte. */
export function Vendas30Dias({ dias }: { dias: PainelDia[] }) {
  const total = dias.reduce((s, d) => s + d.faturado, 0);
  const vendas = dias.reduce((s, d) => s + d.vendas, 0);
  const dados = dias.map((d, i) => ({ ...d, rotulo: diaCurto.format(new Date(d.dia)), hoje: i === dias.length - 1 }));

  return (
    <SectionCard
      title={
        <span className="flex items-center gap-2 text-sm font-semibold">
          <TrendingUp size={15} className="text-brand-600" /> Últimos 30 dias
        </span>
      }
      action={
        <span className="text-sm tabular-nums text-zinc-500">
          <strong className="font-semibold text-zinc-900 dark:text-zinc-100">{formatEur(total)}</strong> · {vendas} venda(s)
        </span>
      }
    >
      <div className="h-48 w-full" role="img" aria-label={`Faturado por dia nos últimos 30 dias: ${formatEur(total)} no total`}>
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={dados} margin={{ top: 4, right: 4, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="currentColor" className="text-zinc-100 dark:text-zinc-800" />
            <XAxis dataKey="rotulo" tickLine={false} axisLine={false} interval={6} tick={{ fontSize: 11, fill: '#71717a' }} />
            <YAxis
              tickLine={false}
              axisLine={false}
              width={48}
              tick={{ fontSize: 11, fill: '#71717a' }}
              tickFormatter={(v: number) => (v >= 1000 ? `${Math.round(v / 100) / 10}k` : `${v}`)}
            />
            <Tooltip
              cursor={{ fill: 'rgba(14,165,233,0.08)' }}
              content={({ active, payload }) => {
                const p = active && payload?.[0]?.payload as (PainelDia & { hoje: boolean }) | undefined;
                if (!p) return null;
                return (
                  <div className="rounded-lg border border-zinc-200 bg-white px-3 py-2 text-xs shadow-md dark:border-zinc-700 dark:bg-zinc-900">
                    <p className="font-medium capitalize">{diaLongo.format(new Date(p.dia))}</p>
                    <p className="tabular-nums text-zinc-600 dark:text-zinc-300">
                      {formatEur(p.faturado)} · {p.vendas} venda(s)
                    </p>
                  </div>
                );
              }}
            />
            <Bar dataKey="faturado" radius={[4, 4, 0, 0]} maxBarSize={22}>
              {dados.map((d) => (
                <Cell key={d.dia} fill={d.hoje ? 'var(--color-brand-600)' : 'var(--color-brand-300)'} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </SectionCard>
  );
}
