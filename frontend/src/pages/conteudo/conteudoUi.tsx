import { StatusBadge } from '../../components/ui';
import {
  ConteudoDestinoEstado,
  ConteudoEstado,
  REDE_LABEL,
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
};

export function EstadoBadge({ estado }: { estado: ConteudoEstado }) {
  const e = ESTADO[estado];
  return <StatusBadge tone={e.tone} size="md">{e.label}</StatusBadge>;
}

const DESTINO_COR: Record<ConteudoDestinoEstado, string> = {
  [ConteudoDestinoEstado.Pendente]: 'bg-zinc-100 text-zinc-700 ring-zinc-200 dark:bg-zinc-800 dark:text-zinc-200 dark:ring-zinc-700',
  [ConteudoDestinoEstado.AgendadoNaRede]: 'bg-sky-50 text-sky-800 ring-sky-200 dark:bg-sky-950 dark:text-sky-200 dark:ring-sky-800',
  [ConteudoDestinoEstado.Publicado]: 'bg-emerald-50 text-emerald-800 ring-emerald-200 dark:bg-emerald-950 dark:text-emerald-200 dark:ring-emerald-800',
  [ConteudoDestinoEstado.Falhou]: 'bg-rose-50 text-rose-800 ring-rose-200 dark:bg-rose-950 dark:text-rose-200 dark:ring-rose-800',
};

const DESTINO_SUFIXO: Record<ConteudoDestinoEstado, string> = {
  [ConteudoDestinoEstado.Pendente]: '',
  [ConteudoDestinoEstado.AgendadoNaRede]: ' · agendado',
  [ConteudoDestinoEstado.Publicado]: ' · saiu',
  [ConteudoDestinoEstado.Falhou]: ' · falhou',
};

export function RedesChips({ destinos }: { destinos: ConteudoDestino[] }) {
  return (
    <div className="flex flex-wrap gap-1.5">
      {destinos.map((d) => {
        const chip = (
          <span
            className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ${DESTINO_COR[d.estado]}`}
            title={d.ultimoErro ?? undefined}
          >
            {REDE_LABEL[d.rede]}
            {DESTINO_SUFIXO[d.estado]}
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
