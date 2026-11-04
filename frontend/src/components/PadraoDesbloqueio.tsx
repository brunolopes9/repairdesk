import { useRef, useState } from 'react';

const PONTOS = [1, 2, 3, 4, 5, 6, 7, 8, 9];
const pos = (n: number) => ({ x: 20 + ((n - 1) % 3) * 40, y: 20 + Math.floor((n - 1) / 3) * 40 });

/**
 * Sprint 595: o padrão de desbloqueio (9 pontos). Arrasta-se o dedo (ou toca-se nos pontos por ordem) tal como no
 * telemóvel; fica guardado como a sequência "1-5-9-6". Só leitura quando `onChange` não é dado.
 */
export function PadraoDesbloqueio({ value, onChange, size = 132 }: { value: string; onChange?: (v: string) => void; size?: number }) {
  const seq = value ? value.split('-').map(Number).filter((n) => n >= 1 && n <= 9) : [];
  const [aDesenhar, setADesenhar] = useState(false);
  const svg = useRef<SVGSVGElement | null>(null);

  function pontoEm(e: React.PointerEvent): number | null {
    const r = svg.current?.getBoundingClientRect();
    if (!r) return null;
    const x = ((e.clientX - r.left) / r.width) * 120;
    const y = ((e.clientY - r.top) / r.height) * 120;
    return PONTOS.find((n) => Math.hypot(pos(n).x - x, pos(n).y - y) < 14) ?? null;
  }

  function juntar(n: number | null, base: number[]) {
    if (n == null || base.includes(n) || !onChange) return;
    onChange([...base, n].join('-'));
  }

  return (
    <div className="inline-flex flex-col items-center gap-1">
      <svg ref={svg} viewBox="0 0 120 120" width={size} height={size}
        className={`rounded-lg bg-zinc-50 dark:bg-zinc-950 ${onChange ? 'cursor-pointer touch-none' : ''}`}
        onPointerDown={(e) => { if (!onChange) return; e.currentTarget.setPointerCapture(e.pointerId); setADesenhar(true); const n = pontoEm(e); if (n) onChange(String(n)); }}
        onPointerMove={(e) => { if (aDesenhar) juntar(pontoEm(e), seq); }}
        onPointerUp={() => setADesenhar(false)}
        aria-label={seq.length ? `Padrão ${seq.join('-')}` : 'Desenhar o padrão'}>
        {seq.length > 1 && (
          <polyline points={seq.map((n) => `${pos(n).x},${pos(n).y}`).join(' ')} fill="none" stroke="currentColor"
            className="text-brand-600" strokeWidth={4} strokeLinecap="round" strokeLinejoin="round" />
        )}
        {PONTOS.map((n) => {
          const i = seq.indexOf(n);
          return (
            <g key={n}>
              <circle cx={pos(n).x} cy={pos(n).y} r={i === 0 ? 8 : 6} className={i === 0 ? 'fill-emerald-500' : i > 0 ? 'fill-brand-600' : 'fill-zinc-300 dark:fill-zinc-600'} />
              {i >= 0 && <text x={pos(n).x + 9} y={pos(n).y - 7} fontSize="9" className="fill-zinc-500">{i + 1}</text>}
            </g>
          );
        })}
      </svg>
      {onChange && (
        <span className="text-[11px] text-zinc-500">
          {seq.length ? `${seq.join('-')} · ` : 'Arrasta o dedo pelos pontos · '}
          <button type="button" className="underline" onClick={() => onChange('')}>limpar</button>
        </span>
      )}
    </div>
  );
}
