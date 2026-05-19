/**
 * Validador IMEI client-side — espelha lógica do backend ImeiValidator.
 * Algoritmo Luhn standard, aceita 14/15/16 dígitos.
 */
export function normalizeImei(input: string | null | undefined): string {
  if (!input) return '';
  return input.replace(/\D/g, '');
}

export function isValidImei(input: string | null | undefined): boolean {
  const digits = normalizeImei(input);
  if (digits.length < 14 || digits.length > 16) return false;
  // Luhn check só obrigatório para 15 dígitos. 14/16 (legacy/IMEISV) aceites sem.
  if (digits.length !== 15) return true;
  return luhnCheck(digits);
}

function luhnCheck(digits: string): boolean {
  let sum = 0;
  let alternate = false;
  for (let i = digits.length - 1; i >= 0; i--) {
    let d = digits.charCodeAt(i) - 48;
    if (d < 0 || d > 9) return false;
    if (alternate) {
      d *= 2;
      if (d > 9) d -= 9;
    }
    sum += d;
    alternate = !alternate;
  }
  return sum % 10 === 0;
}
