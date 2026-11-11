import { useQuery } from '@tanstack/react-query';
import { rotinaApi } from '../../lib/rotina/api';

/** Sprint 615: evolução do peso (registos de 14 em 14 dias) — últimos valores e diferença. */
export default function PesoHistorico({ itemId }: { itemId: string }) {
  const valores = useQuery({ queryKey: ['rotina', 'valores', itemId], queryFn: () => rotinaApi.valores(itemId) });
  const lista = valores.data ?? [];
  if (lista.length === 0) return null;
  const ultimos = lista.slice(-8);
  const primeiro = lista[0].valor;
  const ultimo = lista[lista.length - 1].valor;
  const diff = Math.round((ultimo - primeiro) * 10) / 10;
  const min = Math.min(...ultimos.map((v) => v.valor));
  const max = Math.max(...ultimos.map((v) => v.valor));
  const altura = (v: number) => (max === min ? 50 : 15 + ((v - min) / (max - min)) * 85);

  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-baseline justify-between">
        <h2 className="font-semibold">⚖️ Peso</h2>
        <p className="text-sm text-zinc-500">
          {ultimo.toLocaleString('pt-PT')} kg
          {lista.length > 1 && ` · ${diff > 0 ? '+' : ''}${diff.toLocaleString('pt-PT')} kg desde ${new Date(lista[0].dia).toLocaleDateString('pt-PT')}`}
        </p>
      </div>
      <div className="mt-3 flex h-24 items-end gap-2" role="img" aria-label={`Peso: ${ultimos.map((v) => `${v.valor} kg`).join(', ')}`}>
        {ultimos.map((v) => (
          <div key={v.dia} className="flex flex-1 flex-col items-center gap-1">
            <span className="text-[10px] tabular-nums text-zinc-500">{v.valor.toLocaleString('pt-PT')}</span>
            <div className="w-full max-w-8 rounded-t bg-teal-500/80" style={{ height: `${altura(v.valor)}%` }} />
            <span className="text-[10px] text-zinc-400">{new Date(v.dia).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit' })}</span>
          </div>
        ))}
      </div>
    </section>
  );
}
