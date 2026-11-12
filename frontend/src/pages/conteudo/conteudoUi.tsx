import { StatusBadge } from '../../components/ui';
import {
  ConteudoDestinoEstado,
  ConteudoEstado,
  REDE_LABEL,
  RedeSocial,
  type ConteudoDestino,
} from '../../lib/conteudo/types';

// Sprint 598 (Doc 95): peças visuais partilhadas pelas vistas do módulo Conteúdo.

type Tone = Parameters<typeof StatusBadge>[0]['tone'];

const ESTADO: Record<ConteudoEstado, { label: string; tone: Tone }> = {
  [ConteudoEstado.Rascunho]: { label: 'Rascunho', tone: 'zinc' },
  [ConteudoEstado.PorAprovar]: { label: 'Por aprovar', tone: 'amber' },
  [ConteudoEstado.Aprovada]: { label: 'Aprovada', tone: 'sky' },
  [ConteudoEstado.APublicar]: { label: 'A publicar', tone: 'violet' },
  [ConteudoEstado.Publicada]: { label: 'Publicada', tone: 'emerald' },
  [ConteudoEstado.ComFalhas]: { label: 'Com falhas', tone: 'rose' },
  [ConteudoEstado.Rejeitada]: { label: 'Rejeitada', tone: 'zinc' },
  [ConteudoEstado.Cancelada]: { label: 'Cancelada', tone: 'zinc' },
  [ConteudoEstado.Removida]: { label: 'Removida', tone: 'zinc' },
};

export function EstadoBadge({ estado }: { estado: ConteudoEstado }) {
  const e = ESTADO[estado];
  return <StatusBadge tone={e.tone} size="md">{e.label}</StatusBadge>;
}

// Sprint 631: cores que se leem de relance — verde = saiu (com link), vermelho = falhou (com o erro), neutro = à espera.
const DESTINO_COR: Record<ConteudoDestinoEstado, string> = {
  [ConteudoDestinoEstado.Pendente]: 'bg-zinc-100 text-zinc-700 ring-zinc-200 dark:bg-zinc-800 dark:text-zinc-200 dark:ring-zinc-700',
  [ConteudoDestinoEstado.AgendadoNaRede]: 'bg-zinc-100 text-zinc-700 ring-zinc-300 dark:bg-zinc-800 dark:text-zinc-200 dark:ring-zinc-600',
  [ConteudoDestinoEstado.Publicado]: 'bg-emerald-600 text-white ring-emerald-700',
  [ConteudoDestinoEstado.Falhou]: 'bg-rose-600 text-white ring-rose-700',
};

const DESTINO_SUFIXO: Record<ConteudoDestinoEstado, string> = {
  [ConteudoDestinoEstado.Pendente]: '',
  [ConteudoDestinoEstado.AgendadoNaRede]: ' · agendado',
  [ConteudoDestinoEstado.Publicado]: ' · saiu',
  [ConteudoDestinoEstado.Falhou]: ' · falhou',
};

/**
 * Sprint 631: o TikTok fica «à espera da app» enquanto a app não estiver aprovada pelo TikTok — não é uma falha
 * e não impede a peça de contar como «saiu» se já saiu noutra rede.
 */
export function RedesChips({ destinos, passou = false }: { destinos: ConteudoDestino[]; passou?: boolean }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {destinos.map((d) => {
        const chip = (
          <span
            className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ${DESTINO_COR[d.estado]}`}
            title={d.ultimoErro ?? undefined}
          >
            {REDE_LABEL[d.rede]}
            {d.rede === RedeSocial.TikTok && d.estado === ConteudoDestinoEstado.Pendente && passou
              ? ' · à espera da app'
              : DESTINO_SUFIXO[d.estado]}
          </span>
        );
        return d.permalink ? (
          <a key={d.id} href={d.permalink} target="_blank" rel="noreferrer" className="hover:opacity-80">
            {chip}
          </a>
        ) : (
          <span key={d.id}>{chip}</span>
        );
      })}
    </div>
  );
}

const diaFmt = new Intl.DateTimeFormat('pt-PT', { weekday: 'short', day: '2-digit', month: '2-digit' });
const horaFmt = new Intl.DateTimeFormat('pt-PT', { hour: '2-digit', minute: '2-digit' });

/** «ter., 10/11 · 14:00» na hora de Portugal do browser. */
export function quando(iso: string | null): string {
  if (!iso) return 'Sem hora marcada';
  const d = new Date(iso);
  return `${diaFmt.format(d)} · ${horaFmt.format(d)}`;
}

export function horaCurta(iso: string | null): string {
  return iso ? horaFmt.format(new Date(iso)) : '—';
}

/** Chave do dia local (AAAA-MM-DD) para agrupar a agenda. */
export function chaveDia(iso: string | null): string {
  if (!iso) return 'sem-hora';
  const d = new Date(iso);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function tituloDia(chave: string): string {
  if (chave === 'sem-hora') return 'Sem hora marcada';
  const [a, m, d] = chave.split('-').map(Number);
  const dia = new Date(a, m - 1, d);
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  const diff = Math.round((dia.getTime() - hoje.getTime()) / 86_400_000);
  const nome = new Intl.DateTimeFormat('pt-PT', { weekday: 'long', day: 'numeric', month: 'long' }).format(dia);
  if (diff === 0) return `Hoje · ${nome}`;
  if (diff === 1) return `Amanhã · ${nome}`;
  if (diff === -1) return `Ontem · ${nome}`;
  return nome.charAt(0).toUpperCase() + nome.slice(1);
}

export function tamanho(bytes: number): string {
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(1)} GB`;
  if (bytes >= 1024 ** 2) return `${Math.round(bytes / 1024 ** 2)} MB`;
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export function duracao(seg: number | null): string | null {
  if (!seg) return null;
  const m = Math.floor(seg / 60);
  const s = Math.round(seg % 60);
  return `${m}:${String(s).padStart(2, '0')}`;
}

/**
 * Sprint 612: a API do Facebook não publica em perfis pessoais. Este link abre a janela de partilha
 * com a publicação da página — o dono escolhe «Com…» → a página e publica no perfil.
 */
export function linkPartilhaFacebook(permalink: string): string {
  return `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(permalink)}`;
}
