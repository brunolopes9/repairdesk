import { useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { PageHeader, SkeletonCard, ViewTabs } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import { ConteudoEstado } from '../../lib/conteudo/types';
import { Agenda } from './Agenda';
import { BibliotecaVideos } from './BibliotecaVideos';
import { FilaAprovacao } from './FilaAprovacao';

type Tab = 'aprovar' | 'agenda' | 'videos' | 'historico';

// Sprint 628: a agenda mostra o dia inteiro — o que já saiu (Publicada) também, para saber «a quantos ando».
const ESTADOS_AGENDA = [ConteudoEstado.PorAprovar, ConteudoEstado.Aprovada, ConteudoEstado.APublicar, ConteudoEstado.ComFalhas, ConteudoEstado.Publicada];
const ESTADOS_HISTORICO = [ConteudoEstado.Publicada, ConteudoEstado.Rejeitada, ConteudoEstado.Cancelada, ConteudoEstado.Removida];

/**
 * Sprint 598 (Doc 95): módulo Conteúdo — o calendário das redes sociais da LopesTech.
 * O Claude prepara as publicações; o dono aprova aqui (pensado para o telemóvel, a notificação
 * push abre direto em «Por aprovar»), envia os vídeos das reparações e vê o que já saiu.
 */
export default function Conteudo() {
  const [params, setParams] = useSearchParams();

  const resumo = useQuery({ queryKey: ['conteudo', 'resumo'], queryFn: conteudoApi.resumo, refetchInterval: 60_000 });
  const porAprovar = resumo.data?.porAprovar ?? 0;
  const tab = (params.get('tab') as Tab | null) ?? (porAprovar > 0 || resumo.isLoading ? 'aprovar' : 'agenda');
  const setTab = (t: Tab) => setParams((p) => { p.set('tab', t); return p; }, { replace: true });

  // Janelas fixas por vista: agenda = de ontem a +30 dias; histórico = últimos 45 dias.
  const janela = useMemo(() => {
    const dia = 86_400_000;
    const agora = Date.now();
    return {
      agendaDe: new Date(agora - dia).toISOString(),
      agendaAte: new Date(agora + 30 * dia).toISOString(),
      historicoDe: new Date(agora - 45 * dia).toISOString(),
    };
  }, []);

  const fila = useQuery({
    queryKey: ['conteudo', 'lista', 'aprovar'],
    queryFn: () => conteudoApi.list({ estados: [ConteudoEstado.PorAprovar] }),
    enabled: tab === 'aprovar',
  });
  const agenda = useQuery({
    queryKey: ['conteudo', 'lista', 'agenda', janela.agendaDe],
    queryFn: () => conteudoApi.list({ estados: ESTADOS_AGENDA, de: janela.agendaDe, ate: janela.agendaAte }),
    enabled: tab === 'agenda',
  });
  const historico = useQuery({
    queryKey: ['conteudo', 'lista', 'historico', janela.historicoDe],
    queryFn: () => conteudoApi.list({ estados: ESTADOS_HISTORICO, de: janela.historicoDe }),
    enabled: tab === 'historico',
  });

  const atual = tab === 'aprovar' ? fila : tab === 'agenda' ? agenda : tab === 'historico' ? historico : null;

  return (
    <div className="space-y-5">
      <PageHeader
        title="Conteúdo"
        description="Facebook, Instagram, TikTok e Threads da LopesTech: aprovas aqui, as publicações saem às horas marcadas."
      />

      <ViewTabs
        value={tab}
        onChange={(t) => setTab(t as Tab)}
        tabs={[
          { key: 'aprovar', label: 'Por aprovar', meta: porAprovar || undefined },
          { key: 'agenda', label: 'Agenda' },
          { key: 'videos', label: 'Vídeos', meta: resumo.data?.bibliotecaPorProcessar || undefined },
          { key: 'historico', label: 'Histórico' },
        ]}
      />

      {tab === 'videos' ? (
        <BibliotecaVideos />
      ) : atual?.isLoading ? (
        <div className="grid gap-4 xl:grid-cols-2">
          <SkeletonCard />
          <SkeletonCard />
        </div>
      ) : atual?.isError ? (
        <p className="rounded-md bg-rose-50 p-3 text-sm text-rose-800 dark:bg-rose-950 dark:text-rose-200">
          Não consegui carregar as publicações. Puxa para atualizar ou tenta daqui a pouco.
        </p>
      ) : tab === 'aprovar' ? (
        <FilaAprovacao itens={fila.data ?? []} />
      ) : tab === 'agenda' ? (
        <Agenda itens={agenda.data ?? []} vazio="Nada agendado para os próximos 30 dias" contadores />
      ) : (
        <Agenda itens={[...(historico.data ?? [])].reverse()} vazio="Ainda não há histórico" />
      )}
    </div>
  );
}
