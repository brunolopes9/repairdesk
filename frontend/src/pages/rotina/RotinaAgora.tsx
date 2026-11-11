import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { TIPO_INFO, horaLocal, rotinaApi } from '../../lib/rotina/api';

/**
 * Sprint 621 (Doc 98): faixa do Dashboard — o bloco atual e o próximo da rotina, para responder a
 * «o que é que faço agora?» sem abrir outra página. Não aparece a quem não tem rotina.
 */
export function RotinaAgora() {
  const dia = useQuery({ queryKey: ['rotina', 'dia'], queryFn: () => rotinaApi.dia(), refetchInterval: 60_000 });
  const d = dia.data;
  if (!d || d.itens.length === 0) return null;
  const atual = d.itens.find((l) => l.item.id === d.atualId);
  const proximo = d.itens.find((l) => l.item.id === d.proximoId);
  const atrasados = d.itens.filter((l) => l.atrasado && l.estado == null).length;

  return (
    <Link
      to="/rotina"
      className="flex flex-wrap items-center gap-x-6 gap-y-1 rounded-2xl border border-brand-200 bg-brand-50 px-4 py-3 text-sm transition hover:border-brand-300 dark:border-brand-900/60 dark:bg-brand-950/30"
    >
      <span>
        <span className="text-xs font-medium uppercase tracking-wide text-zinc-500">Agora </span>
        <strong>{atual ? `${TIPO_INFO[atual.item.tipo].icone} ${atual.item.titulo}` : 'sem bloco'}</strong>
        {atual && atual.item.duracaoMin > 0 && <span className="text-zinc-500"> até {horaLocal(atual.fimUtc)}</span>}
      </span>
      {proximo && (
        <span>
          <span className="text-xs font-medium uppercase tracking-wide text-zinc-500">A seguir </span>
          {horaLocal(proximo.inicioUtc)} {TIPO_INFO[proximo.item.tipo].icone} {proximo.item.titulo}
        </span>
      )}
      <span className="ml-auto text-zinc-600 dark:text-zinc-300">
        {d.feitos}/{d.devidos} cumprido
        {atrasados > 0 && <span className="ml-2 font-semibold text-rose-600">{atrasados} por registar</span>}
      </span>
    </Link>
  );
}
