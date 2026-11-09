import { useState } from 'react';
import { ChevronLeft, ChevronRight, ImageOff } from 'lucide-react';
import { ConteudoFicheiroTipo, ConteudoFormato, type ConteudoFicheiro } from '../../lib/conteudo/types';

/**
 * Sprint 598 (Doc 95): pré-visualização como vai sair — slides do carrossel com setas e pontos,
 * vídeo com controlos (playsInline para não abrir em ecrã inteiro no iPhone). A caixa usa a
 * proporção real do ficheiro para o dono ver cortes ou espaços vazios antes de aprovar.
 */
export function MediaPreview({ ficheiros, formato }: { ficheiros: ConteudoFicheiro[]; formato: ConteudoFormato }) {
  const [i, setI] = useState(0);
  if (ficheiros.length === 0) return null;
  const atual = ficheiros[Math.min(i, ficheiros.length - 1)];
  const vertical = formato === ConteudoFormato.Reel || formato === ConteudoFormato.Story;
  const ratio = atual.largura && atual.altura ? `${atual.largura} / ${atual.altura}` : vertical ? '9 / 16' : '4 / 5';
  const varios = ficheiros.length > 1;

  return (
    <div className="relative mx-auto w-full max-w-sm overflow-hidden rounded-lg bg-zinc-900 ring-1 ring-zinc-200 dark:ring-zinc-800">
      <div className="relative w-full" style={{ aspectRatio: ratio }}>
        {!atual.url ? (
          <div className="absolute inset-0 grid place-items-center text-zinc-400">
            <ImageOff size={28} />
          </div>
        ) : atual.tipo === ConteudoFicheiroTipo.Video ? (
          <video
            key={atual.id}
            src={atual.url}
            controls
            playsInline
            preload="metadata"
            className="absolute inset-0 h-full w-full object-contain"
          />
        ) : (
          <img key={atual.id} src={atual.url} alt={atual.nomeOriginal} loading="lazy" className="absolute inset-0 h-full w-full object-contain" />
        )}
      </div>

      {varios && (
        <>
          <button
            type="button"
            aria-label="Slide anterior"
            onClick={() => setI((v) => Math.max(0, v - 1))}
            disabled={i === 0}
            className="absolute left-2 top-1/2 grid h-9 w-9 -translate-y-1/2 place-items-center rounded-full bg-white/90 text-zinc-800 shadow disabled:opacity-0"
          >
            <ChevronLeft size={18} />
          </button>
          <button
            type="button"
            aria-label="Slide seguinte"
            onClick={() => setI((v) => Math.min(ficheiros.length - 1, v + 1))}
            disabled={i === ficheiros.length - 1}
            className="absolute right-2 top-1/2 grid h-9 w-9 -translate-y-1/2 place-items-center rounded-full bg-white/90 text-zinc-800 shadow disabled:opacity-0"
          >
            <ChevronRight size={18} />
          </button>
          <div className="absolute inset-x-0 bottom-2 flex justify-center gap-1.5">
            {ficheiros.map((f, idx) => (
              <button
                key={f.id}
                type="button"
                aria-label={`Slide ${idx + 1}`}
                onClick={() => setI(idx)}
                className={`h-1.5 rounded-full transition-all ${idx === i ? 'w-4 bg-white' : 'w-1.5 bg-white/50'}`}
              />
            ))}
          </div>
          <span className="absolute right-2 top-2 rounded-full bg-black/60 px-2 py-0.5 text-xs font-medium text-white">
            {i + 1}/{ficheiros.length}
          </span>
        </>
      )}
    </div>
  );
}
