/**
 * Validador de NIF português — algoritmo mod 11 oficial AT (Autoridade Tributária).
 *
 * Sprint 121C: alinhado com a implementação C# em RepairDesk.Common.Helpers.NifValidator
 * para garantir que loja online valida com os mesmos critérios que o backend. Se loja
 * passar um NIF que aqui é válido, o backend não devolve `customer_invalid_nif` — se
 * houver desincronização, é bug em algum dos lados (alert P1 conforme acordado).
 *
 * Regras (DL 463/79):
 * 1. Exactamente 9 dígitos decimais
 * 2. Primeiro dígito ∈ {1, 2, 3, 5, 6, 8, 9} (4 e 7 não atribuídos a singulares/colectivos PT)
 *    Mas como há excepções históricas, aceitamos {1, 2, 3, 5, 6, 7, 8, 9} (7 = entidades específicas).
 * 3. Soma ponderada: d1*9 + d2*8 + d3*7 + d4*6 + d5*5 + d6*4 + d7*3 + d8*2
 * 4. resto = soma mod 11
 * 5. checkDigit = resto < 2 ? 0 : 11 - resto
 * 6. checkDigit deve ser igual ao 9º dígito
 *
 * Referência: https://info.portaldasfinancas.gov.pt/ (algoritmo público desde DL 463/79)
 */

/** Primeiros dígitos válidos para NIFs PT. */
const VALID_FIRST_DIGITS = new Set(['1', '2', '3', '5', '6', '7', '8', '9']);

/**
 * Valida um NIF português. Espaços e pontos são removidos.
 *
 * @example
 * isValidPortugueseNIF('123 456 789')  // false
 * isValidPortugueseNIF('501123456')    // true
 * isValidPortugueseNIF('999999990')    // true (Consumidor Final)
 */
export function isValidPortugueseNIF(nif: string | null | undefined): boolean {
  if (!nif) return false;
  const clean = nif.replace(/[\s.]/g, '');
  if (!/^\d{9}$/.test(clean)) return false;
  if (!VALID_FIRST_DIGITS.has(clean[0])) return false;

  const weights = [9, 8, 7, 6, 5, 4, 3, 2];
  let sum = 0;
  for (let i = 0; i < 8; i++) sum += Number(clean[i]) * weights[i];
  const remainder = sum % 11;
  const checkDigit = remainder < 2 ? 0 : 11 - remainder;
  return checkDigit === Number(clean[8]);
}

/**
 * Versão "soft" que devolve normalizado (sem espaços/pontos) quando válido, null caso contrário.
 * Útil para guardar a versão canónica em BD.
 */
export function normalizePortugueseNIF(nif: string | null | undefined): string | null {
  if (!nif) return null;
  const clean = nif.replace(/[\s.]/g, '');
  return isValidPortugueseNIF(clean) ? clean : null;
}
