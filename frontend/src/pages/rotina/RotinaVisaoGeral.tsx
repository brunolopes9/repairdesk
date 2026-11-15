import { useQuery } from '@tanstack/react-query';
import { TIPO_INFO, rotinaApi, type RotinaDiaResumo } from '../../lib/rotina/api';

const cor = (d: RotinaDiaResumo) => {
  if (d.total === 0) return 'bg-zinc-100 dark:bg-zinc-800';
  if (d.percent >= 80) return 'bg-emerald-500';
  if (d.percent >= 50) return 'bg-emerald-300 dark:bg-emerald-700';
  if (d.percent > 0) return 'bg-amber-200 dark:bg-amber-800';
  return 'bg-zinc-200 dark:bg-zinc-700';
};

/**
 * Sprint 626: visão panorâmica — 13 semanas, % por dia, dias cumpridos seguidos e sequência de cada hábito.
 * Feito a qualquer hora conta; «saltado» com motivo não quebra. O objetivo é ver padrões, não castigar.
 */
export default function RotinaVisaoGeral() {
  const h = useQuery({ queryKey: ['rotina', 'historico'], queryFn: () => rotinaApi.historico(91) });
  const d = h.data;
  if (!d) return null;

  // Grelha por semanas (segunda a domingo), como o GitHub.
  const primeiro = new Date(d.dias[0].dia);
  const offset = (primeiro.getDay() + 6) % 7;
  const celulas: (RotinaDiaResumo | null)[] = [...Array(offset).fill(null), ...d.dias];
  const semanas: (RotinaDiaResumo | null)[][] = [];
  for (let i = 0; i < celulas.length; i += 7) semanas.push(celulas.slice(i, i + 7));

  return (
    <section className="space-y-4 rounded-2xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="font-semibold">Visão geral</h2>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Kpi label="Dias cumpridos seguidos" valor={`🔥 ${d.diasCumpridosSeguidos}`} />
        <Kpi label="Melhor sequência" valor={String(d.melhorSequenciaDias)} />
        <Kpi label="Últimos 7 dias" valor={`${d.percentSemana}%`} />
        <Kpi label="Últimos 30 dias" valor={`${d.percentMes}%`} />
      </div>
      <div className="overflow-x-auto">
        <div className="flex gap-1" role="img" aria-label="Cumprimento por dia nas últimas 13 semanas">
          {semanas.map((s, i) => (
            <div key={i} className="flex flex-col gap-1">
              {s.map((dia, j) => (
                <div key={j} title={dia ? `${new Date(dia.dia).toLocaleDateString('pt-PT')}: ${dia.feitos}/${dia.total - dia.saltados} (${dia.percent}%)` : ''}
                  className={`h-3.5 w-3.5 rounded-sm ${dia ? cor(dia) : 'bg-transparent'}`} />
              ))}
            </div>
          ))}
        </div>
        <p className="mt-2 text-xs text-zinc-500">Verde forte = dia cumprido (≥ 80%). Fazer fora de horas conta; saltar com motivo não é falha.</p>
      </div>
      <div>
        <h3 className="mb-2 text-sm font-semibold">Hábitos</h3>
        {/* Sprint 663: o mesmo hábito em dias diferentes (semana/fim de semana) aparece uma vez só. */}
        <ul className="grid gap-2 text-sm sm:grid-cols-2">
          {habitosUnicos(d.habitos).map((x) => {
            const pct = x.previstos30 ? Math.round((100 * x.feitos30) / x.previstos30) : 0;
            return (
              <li key={x.titulo} className="rounded-xl border border-zinc-200 p-3 dark:border-zinc-800">
                <div className="flex items-center gap-2">
                  <span className="min-w-0 flex-1 truncate font-medium">{TIPO_INFO[x.tipo].icone} {x.titulo}</span>
                  <span className="tabular-nums font-semibold" title="Sequência atual (máx.)">🔥 {x.sequenciaAtual}<span className="text-xs font-normal text-zinc-500"> / {x.melhorSequencia}</span></span>
                </div>
                <div className="mt-2 flex items-center gap-2">
                  <div className="h-2 flex-1 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                    <div className={`h-full rounded-full ${pct >= 80 ? 'bg-emerald-500' : pct >= 40 ? 'bg-amber-400' : 'bg-rose-400'}`} style={{ width: `${Math.max(pct, 3)}%` }} />
                  </div>
                  <span className="w-16 text-right text-xs tabular-nums text-zinc-500">{pct}% · 30d</span>
                </div>
              </li>
            );
          })}
        </ul>
      </div>
    </section>
  );
}

function Kpi({ label, valor }: { label: string; valor: string }) {
  return (
    <div className="rounded-xl border border-zinc-200 p-3 dark:border-zinc-800">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="text-xl font-bold tabular-nums">{valor}</p>
    </div>
  );
}

/** Junta hábitos com o mesmo título (versões de dias diferentes): soma os 30 dias, fica a melhor sequência. */
function habitosUnicos<T extends { titulo: string; tipo: keyof typeof TIPO_INFO; sequenciaAtual: number; melhorSequencia: number; feitos30: number; previstos30: number }>(lista: T[]): T[] {
  const m = new Map<string, T>();
  for (const h of lista) {
    const k = h.titulo.trim().toLowerCase();
    const a = m.get(k);
    m.set(k, a ? { ...a, sequenciaAtual: Math.max(a.sequenciaAtual, h.sequenciaAtual), melhorSequencia: Math.max(a.melhorSequencia, h.melhorSequencia),
      feitos30: a.feitos30 + h.feitos30, previstos30: a.previstos30 + h.previstos30 } : h);
  }
  return [...m.values()];
}
