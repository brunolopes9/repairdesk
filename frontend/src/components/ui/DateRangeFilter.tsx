import { inputCls } from './formClasses';

/** Intervalo de datas (dias inclusivos, `yyyy-mm-dd`). Vazio = sem limite. */
export interface IntervaloDatas {
  de: string;
  ate: string;
}

export const SEM_DATAS: IntervaloDatas = { de: '', ate: '' };

type Atalho = 'mes' | 'mes-passado' | 'trimestre' | 'trimestre-passado' | 'ano' | 'ano-passado';

const ATALHOS: { key: Atalho; label: string }[] = [
  { key: 'mes', label: 'Este mês' },
  { key: 'mes-passado', label: 'Mês passado' },
  { key: 'trimestre', label: 'Este trimestre' },
  { key: 'trimestre-passado', label: 'Trimestre passado' },
  { key: 'ano', label: 'Este ano' },
  { key: 'ano-passado', label: 'Ano passado' },
];

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

function intervalo(a: Atalho): IntervaloDatas {
  const hoje = new Date();
  const y = hoje.getFullYear();
  const m = hoje.getMonth();
  const tri = Math.floor(m / 3) * 3;
  const r = (d1: Date, d2: Date) => ({ de: iso(d1), ate: iso(d2) });
  switch (a) {
    case 'mes': return r(new Date(y, m, 1), new Date(y, m + 1, 0));
    case 'mes-passado': return r(new Date(y, m - 1, 1), new Date(y, m, 0));
    case 'trimestre': return r(new Date(y, tri, 1), new Date(y, tri + 3, 0));
    case 'trimestre-passado': return r(new Date(y, tri - 3, 1), new Date(y, tri, 0));
    case 'ano': return r(new Date(y, 0, 1), new Date(y, 11, 31));
    case 'ano-passado': return r(new Date(y - 1, 0, 1), new Date(y - 1, 11, 31));
  }
}

/** Para APIs com limite superior exclusivo: dia seguinte ao "até", meia-noite local em ISO. */
export function intervaloParaInstantes(v: IntervaloDatas): { from?: string; to?: string } {
  const out: { from?: string; to?: string } = {};
  if (v.de) out.from = new Date(`${v.de}T00:00:00`).toISOString();
  if (v.ate) {
    const d = new Date(`${v.ate}T00:00:00`);
    d.setDate(d.getDate() + 1);
    out.to = d.toISOString();
  }
  return out;
}

/** Sprint 561: filtro "de / até" com atalhos (mês, trimestre — o período do IVA —, ano). */
export function DateRangeFilter({ value, onChange }: { value: IntervaloDatas; onChange: (v: IntervaloDatas) => void }) {
  const atalhoAtual = ATALHOS.find((a) => {
    const r = intervalo(a.key);
    return r.de === value.de && r.ate === value.ate;
  })?.key ?? '';
  const invalido = value.de && value.ate && value.de > value.ate;
  return (
    // Telemóvel: período numa linha e as duas datas lado a lado; a partir de sm tudo em linha.
    <div className="grid w-full grid-cols-[1fr_auto_1fr] items-center gap-2 sm:flex sm:w-auto sm:flex-wrap">
      <select
        aria-label="Período"
        value={atalhoAtual || (value.de || value.ate ? 'custom' : '')}
        onChange={(e) => {
          const k = e.target.value;
          if (k === '') onChange(SEM_DATAS);
          else if (k !== 'custom') onChange(intervalo(k as Atalho));
        }}
        className={`${inputCls} col-span-3 sm:w-auto`}
      >
        <option value="">Todas as datas</option>
        {ATALHOS.map((a) => <option key={a.key} value={a.key}>{a.label}</option>)}
        <option value="custom" disabled>Personalizado</option>
      </select>
      <input type="date" aria-label="De" value={value.de} max={value.ate || undefined} onChange={(e) => onChange({ ...value, de: e.target.value })} className={`${inputCls} min-w-0 sm:w-auto`} />
      <span className="text-xs text-zinc-500">até</span>
      <input type="date" aria-label="Até" value={value.ate} min={value.de || undefined} onChange={(e) => onChange({ ...value, ate: e.target.value })} className={`${inputCls} min-w-0 sm:w-auto`} />
      {invalido && <span className="col-span-3 text-xs text-rose-600">"De" depois de "até"</span>}
    </div>
  );
}
