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
        <ul className="divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
          {d.habitos.map((x) => (
            <li key={x.itemId} className="flex items-center gap-3 py-1.5">
              <span className="min-w-0 flex-1 truncate">{TIPO_INFO[x.tipo].icone} {x.titulo}</span>
              <span className="tabular-nums" title="Sequência atual">🔥 {x.sequenciaAtual}</span>
              <span className="w-20 text-right tabular-nums text-zinc-500" title="Melhor sequência">máx. {x.melhorSequencia}</span>
              <span className="w-20 text-right tabular-nums text-zinc-500" title="Últimos 30 dias">{x.previstos30 ? Math.round((100 * x.feitos30) / x.previstos30) : 0}% (30d)</span>
            </li>
          ))}
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
