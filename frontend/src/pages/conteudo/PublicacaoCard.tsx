import { useState, type ReactNode } from 'react';
import { CalendarClock, ChevronDown } from 'lucide-react';
import {
  ConteudoFormato,
  ConteudoOrigem,
  FORMATO_LABEL,
  RedeSocial,
  LINHA_LABEL,
  REDE_LABEL,
  type ConteudoPublicacao,
} from '../../lib/conteudo/types';
import { MediaPreview } from './MediaPreview';
import { EstadoBadge, RedesChips, quando } from './conteudoUi';
import { TikTokPainel } from './TikTokPainel';

/**
 * Sprint 598 (Doc 95): uma publicação tal como vai sair — media, legenda e, quando uma rede tem
 * texto próprio (ex.: Threads mais curto), essa variante à parte. As ações vêm de fora.
 */
export function PublicacaoCard({ p, actions, tiktokEditavel = false }: { p: ConteudoPublicacao; actions?: ReactNode; tiktokEditavel?: boolean }) {
  const vaiParaTikTok = p.destinos.some((d) => d.rede === RedeSocial.TikTok) && p.formato === ConteudoFormato.Reel;
  const variantes = p.destinos.filter((d) => d.legendaPropria);
  const [verVariantes, setVerVariantes] = useState(false);

  return (
    <article className="overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
      <header className="flex flex-wrap items-start justify-between gap-2 border-b border-zinc-100 px-4 py-3 dark:border-zinc-800">
        <div className="min-w-0">
          <p className="text-xs font-medium text-zinc-500">
            #{p.numero} · {FORMATO_LABEL[p.formato]} · {LINHA_LABEL[p.linha]}
            {p.origem === ConteudoOrigem.Claude && ' · preparado pelo Claude'}
            {p.autoAprovada && ' · promo automática'}
          </p>
          <h3 className="mt-0.5 text-base font-semibold text-zinc-950 dark:text-zinc-50">{p.titulo}</h3>
          <p className="mt-1 inline-flex items-center gap-1.5 text-sm font-medium text-brand-700 dark:text-brand-300">
            <CalendarClock size={15} />
            {quando(p.agendadoPara)}
          </p>
        </div>
        <EstadoBadge estado={p.estado} />
      </header>

      <div className="space-y-3 p-4">
        <RedesChips destinos={p.destinos} />

        <MediaPreview ficheiros={p.ficheiros} formato={p.formato} />

        {p.legenda && (
          <p className="whitespace-pre-wrap break-words text-sm leading-6 text-zinc-800 dark:text-zinc-200">{p.legenda}</p>
        )}

        {variantes.length > 0 && (
          <div className="rounded-md border border-zinc-200 dark:border-zinc-800">
            <button
              type="button"
              onClick={() => setVerVariantes((v) => !v)}
              className="flex min-h-10 w-full items-center justify-between px-3 text-sm font-medium text-zinc-700 dark:text-zinc-300"
            >
              Texto próprio em {variantes.map((d) => REDE_LABEL[d.rede]).join(', ')}
              <ChevronDown size={16} className={`transition ${verVariantes ? 'rotate-180' : ''}`} />
            </button>
            {verVariantes && (
              <div className="space-y-3 border-t border-zinc-200 p-3 dark:border-zinc-800">
                {variantes.map((d) => (
                  <div key={d.id}>
                    <p className="text-xs font-semibold uppercase tracking-wide text-zinc-500">{REDE_LABEL[d.rede]}</p>
                    <p className="mt-1 whitespace-pre-wrap break-words text-sm leading-6 text-zinc-800 dark:text-zinc-200">{d.legendaPropria}</p>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {vaiParaTikTok && tiktokEditavel && <TikTokPainel p={p} />}

        {p.notas && <Notas texto={p.notas} />}

        {p.motivoRejeicao && (
          <p className="rounded-md bg-rose-50 px-3 py-2 text-sm text-rose-800 dark:bg-rose-950 dark:text-rose-200">
            <span className="font-semibold">Rejeitada: </span>
            {p.motivoRejeicao}
          </p>
        )}

        {p.destinos.some((d) => d.ultimoErro) && (
          <ul className="space-y-1 text-xs text-rose-700 dark:text-rose-300">
            {p.destinos.filter((d) => d.ultimoErro).map((d) => (
              <li key={d.id}>
                {REDE_LABEL[d.rede]}: {d.ultimoErro}
              </li>
            ))}
          </ul>
        )}
      </div>

      {actions && <footer className="border-t border-zinc-100 px-4 py-3 dark:border-zinc-800">{actions}</footer>}
    </article>
  );
}

/** Notas da publicação — muitas vezes o guião do reel: mantém as quebras de linha e abre-se por inteiro. */
function Notas({ texto }: { texto: string }) {
  const longo = texto.length > 280 || texto.split('\n').length > 4;
  const [aberto, setAberto] = useState(false);
  return (
    <div className="rounded-md bg-zinc-50 px-3 py-2 text-xs leading-5 text-zinc-600 dark:bg-zinc-950 dark:text-zinc-400">
      <p className={`whitespace-pre-wrap break-words ${longo && !aberto ? 'line-clamp-4' : ''}`}>
        <span className="font-semibold">Nota: </span>
        {texto}
      </p>
      {longo && (
        <button
          type="button"
          onClick={() => setAberto((v) => !v)}
          aria-expanded={aberto}
          className="mt-1 inline-flex min-h-8 items-center gap-1 font-semibold text-brand-700 dark:text-brand-300"
        >
          {aberto ? 'Esconder' : 'Ver tudo (guião)'}
          <ChevronDown size={14} className={`transition ${aberto ? 'rotate-180' : ''}`} />
        </button>
      )}
    </div>
  );
}
