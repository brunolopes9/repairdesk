import { useMemo, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, CalendarDays, FileText, Film, Images, Send } from 'lucide-react';
import { useConfirm } from '../../components/ConfirmDialog';
import { Button, EmptyState } from '../../components/ui';
import { conteudoApi } from '../../lib/conteudo/api';
import {
  ConteudoEstado,
  ConteudoFicheiroTipo,
  ConteudoFormato,
  FORMATO_LABEL,
  RedeSocial,
  type ConteudoPublicacao,
  type HoraPublicacao,
} from '../../lib/conteudo/types';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { PublicacaoCard } from './PublicacaoCard';
import { EscolherHora } from './EscolherHora';
import { EstadoBadge, RedesChips, chaveDia, horaCurta, tituloDia } from './conteudoUi';

/**
 * Sprint 598 (Doc 95): agenda por dia com linhas compactas (hora, miniatura, redes). Tocar numa
 * linha abre a publicação inteira. Serve para ver de relance se as horas estão espalhadas
 * por manhã, tarde e noite.
 */
/**
 * Sprint 628: tipo para os contadores do dia — Threads (texto com destino Threads), Stories, Carrossel, Reels, Posts.
 */
function tipoDe(p: ConteudoPublicacao): string {
  if (p.formato === ConteudoFormato.Story) return 'Stories';
  if (p.formato === ConteudoFormato.Carrossel) return 'Carrossel';
  if (p.formato === ConteudoFormato.Reel) return 'Reels';
  if (p.formato === ConteudoFormato.Texto) return p.destinos.some((d) => d.rede === RedeSocial.Threads) ? 'Threads' : 'Texto';
  return 'Posts';
}
const ORDEM_TIPOS = ['Threads', 'Stories', 'Carrossel', 'Reels', 'Posts', 'Texto'];
const CONTA = new Set<ConteudoEstado>([ConteudoEstado.PorAprovar, ConteudoEstado.Aprovada, ConteudoEstado.APublicar, ConteudoEstado.ComFalhas, ConteudoEstado.Publicada]);

function ContadoresDia({ lista }: { lista: ConteudoPublicacao[] }) {
  const porTipo = new Map<string, { saiu: number; total: number }>();
  for (const p of lista) {
    if (!CONTA.has(p.estado)) continue;
    const t = tipoDe(p);
    const c = porTipo.get(t) ?? { saiu: 0, total: 0 };
    c.total++;
    if (p.estado === ConteudoEstado.Publicada) c.saiu++;
    porTipo.set(t, c);
  }
  const tipos = ORDEM_TIPOS.filter((t) => porTipo.has(t));
  if (tipos.length === 0) return null;
  return (
    <div className="mb-2 flex flex-wrap gap-1.5" aria-label="Publicado face ao planeado">
      {tipos.map((t) => {
        const c = porTipo.get(t)!;
        const completo = c.saiu >= c.total;
        return (
          <span key={t} className={`rounded-full px-2 py-0.5 text-xs font-medium ${completo ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/50 dark:text-emerald-300' : 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300'}`}>
            {t} {c.saiu}/{c.total}
          </span>
        );
      })}
    </div>
  );
}

export function Agenda({ itens, vazio, contadores = false }: { itens: ConteudoPublicacao[]; vazio: string; contadores?: boolean }) {
  const [aberta, setAberta] = useState<string | null>(null);
  const grupos = useMemo(() => {
    const m = new Map<string, ConteudoPublicacao[]>();
    for (const p of itens) {
      const k = chaveDia(p.agendadoPara);
      m.set(k, [...(m.get(k) ?? []), p]);
    }
    return [...m.entries()];
  }, [itens]);

  if (itens.length === 0) return <EmptyState icon={CalendarDays} title={vazio} />;

  return (
    <div className="space-y-5">
      {grupos.map(([dia, lista]) => (
        <section key={dia}>
          <h3 className="mb-2 flex items-center justify-between text-sm font-semibold text-zinc-700 dark:text-zinc-300">
            {tituloDia(dia)}
            <span className="text-xs font-normal text-zinc-500">{lista.length} {lista.length === 1 ? 'publicação' : 'publicações'}</span>
          </h3>
          {contadores && <ContadoresDia lista={lista} />}
          <ul className="divide-y divide-zinc-100 overflow-hidden rounded-lg border border-zinc-200 bg-white dark:divide-zinc-800 dark:border-zinc-800 dark:bg-zinc-900">
            {lista.map((p) => (
              <li key={p.id}>
                <button
                  type="button"
                  onClick={() => setAberta((v) => (v === p.id ? null : p.id))}
                  className="flex w-full items-center gap-3 px-3 py-2.5 text-left hover:bg-zinc-50 dark:hover:bg-zinc-800/60"
                  aria-expanded={aberta === p.id}
                >
                  <span className="w-12 shrink-0 text-sm font-semibold tabular-nums text-zinc-900 dark:text-zinc-100">{horaCurta(p.agendadoPara)}</span>
                  <Miniatura p={p} />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-medium text-zinc-900 dark:text-zinc-100">{p.titulo}</span>
                    <span className="mt-1 block"><RedesChips destinos={p.destinos} /></span>
                  </span>
                  <span className="hidden sm:block"><EstadoBadge estado={p.estado} /></span>
                </button>
                {aberta === p.id && (
                  <div className="bg-zinc-50 p-3 dark:bg-zinc-950">
                    <PublicacaoCard p={p} actions={<AcoesAgenda p={p} />} />
                  </div>
                )}
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function Miniatura({ p }: { p: ConteudoPublicacao }) {
  const f = p.ficheiros[0];
  const base = 'grid h-12 w-12 shrink-0 place-items-center overflow-hidden rounded-md bg-zinc-100 text-zinc-500 dark:bg-zinc-800';
  if (!f?.url) {
    const Icon = p.formato === ConteudoFormato.Texto ? FileText : p.formato === ConteudoFormato.Carrossel ? Images : Film;
    return (
      <span className={base} title={FORMATO_LABEL[p.formato]}>
        <Icon size={18} />
      </span>
    );
  }
  return (
    <span className={base} title={FORMATO_LABEL[p.formato]}>
      {f.tipo === ConteudoFicheiroTipo.Video ? (
        <video src={`${f.url}#t=0.5`} preload="metadata" muted playsInline className="h-full w-full object-cover" />
      ) : (
        <img src={f.url} alt="" loading="lazy" className="h-full w-full object-cover" />
      )}
    </span>
  );
}

function AcoesAgenda({ p }: { p: ConteudoPublicacao }) {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [escolher, setEscolher] = useState(false);
  const refrescar = () => qc.invalidateQueries({ queryKey: ['conteudo'] });

  const cancelar = useMutation({
    mutationFn: () => conteudoApi.cancelar(p.id),
    onSuccess: () => {
      toast.success(`#${p.numero} cancelada.`);
      refrescar();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Não foi possível cancelar.'),
  });

  // Sprint 610: mudar a hora não devolve à fila; «Publicar já» de uma por aprovar também a aprova.
  const hora = useMutation({
    mutationFn: (h: HoraPublicacao) =>
      h.agora && p.estado === ConteudoEstado.PorAprovar ? conteudoApi.aprovar(p.id, h) : conteudoApi.mudarHora(p.id, h),
    onSuccess: (r, h) => {
      toast.success(h.agora ? `#${r.numero} sai dentro de 1 minuto.` : `#${r.numero}: hora mudada.`);
      setEscolher(false);
      refrescar();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Não foi possível mudar a hora.'),
  });

  const removida = useMutation({
    mutationFn: () => conteudoApi.removida(p.id),
    onSuccess: () => {
      toast.success(`#${p.numero} marcada como removida.`);
      refrescar();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Não foi possível marcar.'),
  });

  const editavel = p.estado === ConteudoEstado.Aprovada || p.estado === ConteudoEstado.PorAprovar;
  if (p.estado === ConteudoEstado.Publicada || p.estado === ConteudoEstado.ComFalhas) {
    return (
      <div className="flex justify-end">
        <Button variant="ghost" loading={removida.isPending} onClick={async () => {
          const ok = await confirm({
            title: `Marcar #${p.numero} como removida?`,
            description: 'Usa isto quando a publicação foi apagada nas redes (ex.: substituída por uma corrigida). Deixa de contar no dia.',
            confirmLabel: 'Marcar como removida',
          });
          if (ok) removida.mutate();
        }}>
          Foi apagada nas redes
        </Button>
      </div>
    );
  }
  if (!editavel) return null;
  return (
    <div className="flex flex-wrap justify-end gap-2">
      <Button variant="ghost" loading={cancelar.isPending} onClick={async () => {
        const ok = await confirm({
          title: `Cancelar #${p.numero}?`,
          description: 'Deixa de sair. Se já estiver agendada no Business Suite, tenho de a tirar de lá.',
          confirmLabel: 'Cancelar publicação',
          destructive: true,
        });
        if (ok) cancelar.mutate();
      }}>
        Cancelar publicação
      </Button>
      <Button variant="secondary" leftIcon={<CalendarClock size={16} />} onClick={() => setEscolher(true)}>
        Mudar hora
      </Button>
      <Button leftIcon={<Send size={16} />} loading={hora.isPending && Boolean(hora.variables?.agora)} onClick={async () => {
        const ok = await confirm({
          title: `Publicar #${p.numero} já?`,
          description: p.estado === ConteudoEstado.PorAprovar
            ? 'Fica aprovada e sai em todas as redes dentro de 1 minuto.'
            : 'Sai em todas as redes dentro de 1 minuto.',
          confirmLabel: 'Publicar já',
        });
        if (ok) hora.mutate({ agendadoPara: null, agora: true });
      }}>
        Publicar já
      </Button>
      <EscolherHora
        open={escolher}
        titulo={`Nova hora para #${p.numero}`}
        inicial={p.agendadoPara}
        confirmar="Guardar hora"
        loading={hora.isPending}
        onClose={() => setEscolher(false)}
        onConfirm={(iso) => hora.mutate({ agendadoPara: iso, agora: false })}
      />
    </div>
  );
}
