import { useMemo, useRef } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { PageHeader, SkeletonCard, ViewTabs } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import { ConteudoEstado, type ConteudoPublicacao } from '../../lib/conteudo/types';
import { Agenda } from './Agenda';
import { BibliotecaVideos } from './BibliotecaVideos';
import { FilaAprovacao } from './FilaAprovacao';
import { GuioesGravar } from './GuioesGravar';

type Tab = 'aprovar' | 'guioes' | 'agenda' | 'videos' | 'historico';

/** Sprint 640: o estúdio marca os guiões para gravar com este prefixo no título — não se aprovam. */
const PREFIXO_GUIAO = '⚠️ NÃO APROVAR';
const ehGuiao = (p: ConteudoPublicacao) => p.titulo.trimStart().toUpperCase().startsWith(PREFIXO_GUIAO);
const diaLisboa = (d: Date) => d.toLocaleDateString('en-CA', { timeZone: 'Europe/Lisbon' });

// Sprint 628: a agenda mostra o dia inteiro — o que já saiu (Publicada) também, para saber «a quantos ando».
const ESTADOS_AGENDA = [ConteudoEstado.PorAprovar, ConteudoEstado.Aprovada, ConteudoEstado.APublicar, ConteudoEstado.ComFalhas, ConteudoEstado.Publicada];
const ESTADOS_HISTORICO = [ConteudoEstado.Publicada, ConteudoEstado.Rejeitada, ConteudoEstado.Cancelada, ConteudoEstado.Removida];

/**
 * Sprint 598 (Doc 95): módulo Conteúdo — o calendário das redes sociais da LopesTech.
 * O Claude prepara as publicações; o dono aprova aqui (pensado para o telemóvel, a notificação
 * push abre direto em «Por aprovar»), envia os vídeos das reparações e vê o que já saiu.
 * Sprint 640: «Por aprovar» mostra só hoje e amanhã (o dono aprova de manhã o que sai no dia;
 * «Ver tudo» para o resto) e os guiões para gravar têm separador próprio.
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
    enabled: tab === 'aprovar' || tab === 'guioes' || porAprovar > 0,
  });

  // Sprint 651: «Por aprovar» dia a dia — hoje por omissão; as atrasadas e as sem hora contam como hoje.
  const hoje = diaLisboa(new Date());
  const { porDia, dias, guioes } = useMemo(() => {
    const lista = fila.data ?? [];
    const mapa = new Map<string, ConteudoPublicacao[]>();
    for (const p of lista.filter((x) => !ehGuiao(x))) {
      const d = p.agendadoPara ? diaLisboa(new Date(p.agendadoPara)) : hoje;
      const chave = d < hoje ? hoje : d;
      mapa.set(chave, [...(mapa.get(chave) ?? []), p]);
    }
    const chaves = [...new Set([hoje, ...mapa.keys()])].sort();
    return { porDia: mapa, dias: chaves, guioes: lista.filter(ehGuiao) };
  }, [fila.data, hoje]);
  const diaParam = params.get('dia');
  const dia = diaParam && dias.includes(diaParam) ? diaParam : hoje;
  const setDia = (d: string) => setParams((p) => { if (d === hoje) p.delete('dia'); else p.set('dia', d); return p; }, { replace: true });
  const visiveis = porDia.get(dia) ?? [];
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

  const atual = tab === 'aprovar' || tab === 'guioes' ? fila : tab === 'agenda' ? agenda : tab === 'historico' ? historico : null;

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
          { key: 'aprovar', label: 'Por aprovar', meta: (fila.data ? (porDia.get(hoje)?.length ?? 0) : porAprovar) || undefined },
          { key: 'guioes', label: 'Guiões para gravar', meta: guioes.length || undefined },
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
        <DiaSwipe dias={dias} dia={dia} onDia={setDia}>
          <SeletorDia dias={dias} dia={dia} hoje={hoje} contar={(d) => porDia.get(d)?.length ?? 0} onDia={setDia} />
          <FilaAprovacao key={dia} itens={visiveis} hoje={hoje}
            vazio={dia === hoje ? 'Nada para aprovar hoje. Usa as setas para ver os próximos dias.' : 'Nada para aprovar neste dia.'} />
        </DiaSwipe>
      ) : tab === 'guioes' ? (
        <GuioesGravar itens={guioes} />
      ) : tab === 'agenda' ? (
        <Agenda itens={agenda.data ?? []} vazio="Nada agendado para os próximos 30 dias" contadores />
      ) : (
        <Agenda itens={[...(historico.data ?? [])].reverse()} vazio="Ainda não há histórico" />
      )}
    </div>
  );
}

const DIA_SEMANA = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];

function nomeDia(d: string, hoje: string) {
  const data = new Date(`${d}T12:00:00`);
  const amanha = diaLisboa(new Date(Date.now() + 86_400_000));
  const rotulo = d === hoje ? 'Hoje' : d === amanha ? 'Amanhã' : DIA_SEMANA[data.getDay()];
  return `${rotulo} ${String(data.getDate()).padStart(2, '0')}/${String(data.getMonth() + 1).padStart(2, '0')}`;
}

/** Sprint 651: ‹ Hoje 13/11 (8) › — só os dias com publicações por aprovar (e sempre hoje). */
function SeletorDia({ dias, dia, hoje, contar, onDia }: {
  dias: string[]; dia: string; hoje: string; contar: (d: string) => number; onDia: (d: string) => void;
}) {
  const i = dias.indexOf(dia);
  const ant = i > 0 ? dias[i - 1] : null;
  const seg = i < dias.length - 1 ? dias[i + 1] : null;
  const seta = 'grid h-10 w-10 shrink-0 place-items-center rounded-full border border-zinc-200 text-zinc-700 hover:bg-zinc-50 disabled:opacity-30 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800';
  return (
    <div className="flex items-center justify-between gap-2 rounded-xl border border-zinc-200 bg-white px-2 py-2 dark:border-zinc-800 dark:bg-zinc-900">
      <button type="button" className={seta} disabled={!ant} aria-label={ant ? `Dia anterior: ${nomeDia(ant, hoje)}` : 'Sem dia anterior'} onClick={() => ant && onDia(ant)}>
        <ChevronLeft size={20} />
      </button>
      <div className="min-w-0 text-center" aria-live="polite">
        <p className="font-semibold">{nomeDia(dia, hoje)} <span className="text-zinc-500">({contar(dia)})</span></p>
        {seg && <p className="truncate text-xs text-zinc-500">a seguir: {nomeDia(seg, hoje)} ({contar(seg)})</p>}
      </div>
      <button type="button" className={seta} disabled={!seg} aria-label={seg ? `Dia seguinte: ${nomeDia(seg, hoje)}` : 'Sem dia seguinte'} onClick={() => seg && onDia(seg)}>
        <ChevronRight size={20} />
      </button>
    </div>
  );
}

/** Deslizar para o lado no telemóvel muda de dia (só gestos claramente horizontais). */
function DiaSwipe({ dias, dia, onDia, children }: { dias: string[]; dia: string; onDia: (d: string) => void; children: React.ReactNode }) {
  const inicio = useRef<{ x: number; y: number } | null>(null);
  return (
    <div
      className="space-y-4"
      onTouchStart={(e) => { const t = e.touches[0]; inicio.current = { x: t.clientX, y: t.clientY }; }}
      onTouchEnd={(e) => {
        const a = inicio.current;
        inicio.current = null;
        if (!a) return;
        const t = e.changedTouches[0];
        const dx = t.clientX - a.x;
        const dy = t.clientY - a.y;
        if (Math.abs(dx) < 80 || Math.abs(dx) < Math.abs(dy) * 2) return;
        const i = dias.indexOf(dia) + (dx < 0 ? 1 : -1);
        if (i >= 0 && i < dias.length) onDia(dias[i]);
      }}
    >
      {children}
    </div>
  );
}
