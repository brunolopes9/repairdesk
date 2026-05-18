/**
 * Formatador e validador de telefone português.
 *
 * Formatos aceites:
 *   - 9 dígitos: telemóvel ou fixo. Formato canónico: 'xxx xxx xxx'
 *   - +351 9 dígitos: idem (prefixo é descartado para formatação)
 *
 * Telemóveis começam por 9 (91, 92, 93, 96). Fixos começam por 2.
 *
 * NÃO normaliza para E.164 — esse cuidado fica para a chamada wa.me que
 * usa o número como veio. Aqui é só para display amigável.
 */

export interface PhoneInfo {
  /** Só os dígitos do número (sem espaços, sem +351). */
  digits: string;
  /** Formatado para humano: 'xxx xxx xxx' ou original se não 9 dígitos. */
  display: string;
  /** True se tem exactamente 9 dígitos e começa por 2 ou 9. */
  isValid: boolean;
  /** 'mobile' (9), 'fixed' (2), 'unknown' (outro), 'incomplete' (<9 dígitos). */
  kind: 'mobile' | 'fixed' | 'unknown' | 'incomplete';
}

export function formatPhonePT(raw: string | null | undefined): PhoneInfo {
  const input = (raw ?? '').toString();
  // Tira tudo excepto dígitos. Preserva possível prefixo +351 antes de tirar.
  const cleanedAll = input.replace(/[^\d+]/g, '');
  let digits = cleanedAll.replace(/\D/g, '');

  // Se começa por 351 ou +351, descarta.
  if (digits.startsWith('351') && digits.length === 12) {
    digits = digits.substring(3);
  }

  if (digits.length === 0) {
    return { digits: '', display: '', isValid: false, kind: 'incomplete' };
  }

  if (digits.length < 9) {
    return { digits, display: digits, isValid: false, kind: 'incomplete' };
  }

  if (digits.length > 9) {
    // Mais de 9 dígitos = formato inesperado, deixa como está
    return { digits, display: digits, isValid: false, kind: 'unknown' };
  }

  // 9 dígitos exactos
  const display = `${digits.substring(0, 3)} ${digits.substring(3, 6)} ${digits.substring(6, 9)}`;
  const first = digits[0];
  const kind: PhoneInfo['kind'] =
    first === '9' ? 'mobile' : first === '2' ? 'fixed' : 'unknown';
  const isValid = kind === 'mobile' || kind === 'fixed';

  return { digits, display, isValid, kind };
}

/** Versão curta — só retorna a string formatada (ou o original se inválido). */
export function displayPhone(raw: string | null | undefined): string {
  if (!raw) return '';
  return formatPhonePT(raw).display || raw;
}
