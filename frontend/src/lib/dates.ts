/**
 * Sprint 254 (Doc 79): helpers de datas pt-PT — substitui 22 ocorrências de
 * `new Date(x).toLocaleDateString('pt-PT', {...})` espalhadas pelas páginas.
 *
 * Aceita ISO string, Date, ou number. Devolve string vazia se input for null/undefined
 * — convénio defensivo para evitar "Invalid Date" no render.
 */

export type DateInput = string | number | Date | null | undefined;

function parse(input: DateInput): Date | null {
  if (input == null) return null;
  const d = input instanceof Date ? input : new Date(input);
  return Number.isNaN(d.getTime()) ? null : d;
}

/** Curto: "24 mai" (sem ano se mesmo ano corrente; com ano caso contrário). */
export function formatDate(input: DateInput): string {
  const d = parse(input);
  if (!d) return '';
  const sameYear = d.getFullYear() === new Date().getFullYear();
  return d.toLocaleDateString('pt-PT', sameYear
    ? { day: '2-digit', month: 'short' }
    : { day: '2-digit', month: 'short', year: 'numeric' });
}

/** Numérico completo: "24/05/2026". Útil para tabelas + exports. */
export function formatDateShort(input: DateInput): string {
  const d = parse(input);
  if (!d) return '';
  return d.toLocaleDateString('pt-PT');
}

/** Data + hora: "24 mai 14:30". Usado em timelines e audit logs. */
export function formatDateTime(input: DateInput): string {
  const d = parse(input);
  if (!d) return '';
  return d.toLocaleString('pt-PT', {
    day: '2-digit', month: 'short',
    hour: '2-digit', minute: '2-digit',
  });
}

/** Relativo: "há 3min", "há 2h", "ontem", "há 5d". Para feeds e listas live. */
export function formatRelative(input: DateInput): string {
  const d = parse(input);
  if (!d) return '';
  const diffMs = Date.now() - d.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  if (diffSec < 60) return 'agora';
  const diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60) return `há ${diffMin}min`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `há ${diffHour}h`;
  const diffDay = Math.floor(diffHour / 24);
  if (diffDay === 1) return 'ontem';
  if (diffDay < 7) return `há ${diffDay}d`;
  return formatDate(input);
}
