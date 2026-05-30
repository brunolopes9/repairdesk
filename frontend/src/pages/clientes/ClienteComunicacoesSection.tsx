import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { MessageCircle, Phone, Mail, MessageSquare, StickyNote, MapPin, ArrowDownLeft, ArrowUpRight, ArrowRight } from 'lucide-react';
import { clientesApi } from '../../lib/clientes/api';
import {
  ComunicacaoDirecao,
  ComunicacaoTipo,
  COMUNICACAO_DIRECAO_LABEL,
  COMUNICACAO_TIPO_LABEL,
  type ReparacaoComunicacao,
} from '../../lib/comunicacoes/api';

/**
 * Sprint 453 (extensão eixo cliente do S452): vista read-only de todas as comunicações
 * com este cliente, agregadas das várias reparações. Útil para "ver o que se passou
 * com este cliente nos últimos contactos" sem ter que abrir cada reparação.
 *
 * Adicionar comunicações continua a ser feito na reparação (eixo onde o problema vive).
 */
const TIPO_ICON: Record<ComunicacaoTipo, typeof MessageCircle> = {
  [ComunicacaoTipo.Nota]: StickyNote,
  [ComunicacaoTipo.Telefone]: Phone,
  [ComunicacaoTipo.WhatsApp]: MessageCircle,
  [ComunicacaoTipo.Email]: Mail,
  [ComunicacaoTipo.Sms]: MessageSquare,
  [ComunicacaoTipo.Visita]: MapPin,
  // Sprint 480.
  [ComunicacaoTipo.PortalCliente]: MessageCircle,
};

const TIPO_COR: Record<ComunicacaoTipo, string> = {
  [ComunicacaoTipo.Nota]: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
  [ComunicacaoTipo.Telefone]: 'bg-sky-100 text-sky-800 dark:bg-sky-950/40 dark:text-sky-300',
  [ComunicacaoTipo.WhatsApp]: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300',
  [ComunicacaoTipo.Email]: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-950/40 dark:text-indigo-300',
  [ComunicacaoTipo.Sms]: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
  [ComunicacaoTipo.Visita]: 'bg-purple-100 text-purple-800 dark:bg-purple-950/40 dark:text-purple-300',
  [ComunicacaoTipo.PortalCliente]: 'bg-rose-100 text-rose-800 dark:bg-rose-950/40 dark:text-rose-300',
};

export function ClienteComunicacoesSection({ clienteId }: { clienteId: string }) {
  const list = useQuery({
    queryKey: ['cliente-comunicacoes', clienteId],
    queryFn: () => clientesApi.comunicacoes(clienteId, 50),
    staleTime: 30_000,
  });
  const items = list.data ?? [];

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="flex items-center gap-2 text-sm font-semibold">
        <MessageCircle size={16} strokeWidth={2} className="text-brand-600" />
        Comunicações <span className="text-zinc-500">— {items.length}</span>
      </h2>
      <p className="mt-1 text-xs text-zinc-500">
        Histórico de contactos com este cliente (todas as reparações). Para registar nova
        comunicação, abre a reparação respectiva.
      </p>

      {items.length === 0 && !list.isLoading && (
        <div className="mt-3 rounded-lg border border-dashed border-zinc-200 px-3 py-6 text-center text-xs text-zinc-500 dark:border-zinc-800">
          Sem comunicações registadas para este cliente.
        </div>
      )}

      {items.length > 0 && (
        <ul className="mt-3 space-y-2">
          {items.map((c) => <Row key={c.id} entry={c} />)}
        </ul>
      )}
    </section>
  );
}

function Row({ entry }: { entry: ReparacaoComunicacao }) {
  const Icon = TIPO_ICON[entry.tipo];
  const isInbound = entry.direcao === ComunicacaoDirecao.Inbound;
  const isInterna = entry.direcao === ComunicacaoDirecao.Interna;
  return (
    <li className="rounded-lg border border-zinc-100 bg-zinc-50/50 p-3 text-sm dark:border-zinc-800 dark:bg-zinc-950/40">
      <div className="flex items-start gap-2.5">
        <span className={`mt-0.5 inline-flex h-7 w-7 flex-none items-center justify-center rounded-full ${TIPO_COR[entry.tipo]}`}>
          <Icon size={14} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="mb-0.5 flex flex-wrap items-center gap-1.5 text-[11px] text-zinc-500">
            <span className="font-semibold text-zinc-700 dark:text-zinc-300">{COMUNICACAO_TIPO_LABEL[entry.tipo]}</span>
            {!isInterna && (
              <span className="inline-flex items-center gap-0.5 text-zinc-500">
                {isInbound ? <ArrowDownLeft size={11} /> : <ArrowUpRight size={11} />}
                {COMUNICACAO_DIRECAO_LABEL[entry.direcao]}
              </span>
            )}
            {isInterna && <span className="rounded bg-zinc-200 px-1 py-0.5 text-[10px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">Nota</span>}
            <span>·</span>
            <span>{new Date(entry.createdAt).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}</span>
            <span>·</span>
            <Link
              to={`/reparacoes/${entry.reparacaoId}`}
              className="inline-flex items-center gap-0.5 text-brand-600 hover:underline dark:text-brand-400"
            >
              reparação <ArrowRight size={10} />
            </Link>
          </div>
          <p className="whitespace-pre-wrap break-words">{entry.texto}</p>
        </div>
      </div>
    </li>
  );
}
