import { Check, CloudOff, Loader2 } from 'lucide-react';
import type { EstadoAutosave } from '../../lib/useAutosave';

/** Sprint 671: o que o autosave está a fazer — substitui o botão «Guardar». */
export function AutosaveStatus({ estado, erro, onTentar }: { estado: EstadoAutosave; erro?: string | null; onTentar?: () => void }) {
  if (estado === 'idle') return <span className="text-xs text-zinc-400">Guarda sozinho</span>;
  if (estado === 'pendente' || estado === 'a-guardar')
    return <span className="inline-flex items-center gap-1 text-xs text-zinc-500" aria-live="polite"><Loader2 size={12} className="animate-spin" />A guardar…</span>;
  if (estado === 'guardado')
    return <span className="inline-flex items-center gap-1 text-xs text-emerald-600 dark:text-emerald-400" aria-live="polite"><Check size={12} />Guardado</span>;
  return (
    <span className="inline-flex items-center gap-1 text-xs text-red-600 dark:text-red-400" role="alert">
      <CloudOff size={12} />{erro ?? 'Não guardou'}
      {onTentar && <button type="button" onClick={onTentar} className="ml-1 font-medium underline">Tentar de novo</button>}
    </span>
  );
}
