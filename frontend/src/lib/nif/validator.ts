/**
 * Validador de NIF (Número de Identificação Fiscal) português.
 * Algoritmo oficial AT: 9 dígitos, último é check-digit.
 *
 * Primeiro dígito:
 *  1, 2, 3 — pessoa singular
 *  45 — não-residente
 *  5 — pessoa colectiva
 *  6 — administração pública
 *  70, 74, 75 — heranças indivisas / fundos
 *  71 — não-residentes colectivos
 *  77 — sociedade civil sem personalidade jurídica
 *  79 — regime especial
 *  8 — empresário em nome individual (descontinuado)
 *  9 — pessoa colectiva irregular ou número provisório
 *
 * Algoritmo do check-digit (9.º dígito):
 *  - somar primeiros 8 dígitos × pesos [9,8,7,6,5,4,3,2]
 *  - mod 11 do resultado
 *  - se resto < 2 → check = 0; senão → check = 11 - resto
 */

const VALID_FIRST_DIGITS = ['1', '2', '3', '5', '6', '8', '9'];
const VALID_FIRST_TWO = ['45', '70', '71', '72', '74', '75', '77', '79'];

export interface NifValidation {
  /** Limpa para mostrar (só dígitos). */
  cleaned: string;
  /** True se tem 9 dígitos numéricos. */
  hasNineDigits: boolean;
  /** True se primeiro dígito (ou primeiros 2) são válidos por AT. */
  firstDigitValid: boolean;
  /** True se check-digit corresponde ao algoritmo. */
  checkDigitValid: boolean;
  /** True se tudo OK. */
  isValid: boolean;
  /** Mensagem amigável para utilizador. */
  message: string;
}

export function validateNif(input: string | null | undefined): NifValidation {
  const cleaned = (input ?? '').replace(/\D/g, '');

  if (!cleaned) {
    return {
      cleaned,
      hasNineDigits: false,
      firstDigitValid: false,
      checkDigitValid: false,
      isValid: false,
      message: '',
    };
  }

  if (cleaned.length < 9) {
    return {
      cleaned,
      hasNineDigits: false,
      firstDigitValid: false,
      checkDigitValid: false,
      isValid: false,
      message: `Falta${9 - cleaned.length === 1 ? '' : 'm'} ${9 - cleaned.length} ${9 - cleaned.length === 1 ? 'dígito' : 'dígitos'}.`,
    };
  }

  if (cleaned.length > 9) {
    return {
      cleaned,
      hasNineDigits: false,
      firstDigitValid: false,
      checkDigitValid: false,
      isValid: false,
      message: 'NIF tem só 9 dígitos.',
    };
  }

  const firstTwo = cleaned.substring(0, 2);
  const firstDigitValid =
    VALID_FIRST_DIGITS.includes(cleaned[0]) || VALID_FIRST_TWO.includes(firstTwo);

  if (!firstDigitValid) {
    return {
      cleaned,
      hasNineDigits: true,
      firstDigitValid: false,
      checkDigitValid: false,
      isValid: false,
      message: 'Primeiro dígito inválido para NIF português.',
    };
  }

  // Calcular check-digit
  const weights = [9, 8, 7, 6, 5, 4, 3, 2];
  let sum = 0;
  for (let i = 0; i < 8; i++) {
    sum += parseInt(cleaned[i], 10) * weights[i];
  }
  const remainder = sum % 11;
  const expected = remainder < 2 ? 0 : 11 - remainder;
  const actual = parseInt(cleaned[8], 10);
  const checkDigitValid = expected === actual;

  if (!checkDigitValid) {
    return {
      cleaned,
      hasNineDigits: true,
      firstDigitValid: true,
      checkDigitValid: false,
      isValid: false,
      message: 'Check-digit inválido — verifica se digitaste o NIF correctamente.',
    };
  }

  return {
    cleaned,
    hasNineDigits: true,
    firstDigitValid: true,
    checkDigitValid: true,
    isValid: true,
    message: nifTypeLabel(cleaned),
  };
}

function nifTypeLabel(nif: string): string {
  const first = nif[0];
  const firstTwo = nif.substring(0, 2);
  if (['1', '2', '3'].includes(first)) return 'Pessoa singular';
  if (firstTwo === '45') return 'Não-residente';
  if (first === '5') return 'Pessoa colectiva';
  if (first === '6') return 'Administração pública';
  if (['70', '74', '75'].includes(firstTwo)) return 'Herança indivisa';
  if (firstTwo === '71') return 'Não-residente colectivo';
  if (firstTwo === '77') return 'Sociedade civil';
  if (first === '8') return 'Empresário individual';
  if (first === '9') return 'Pessoa colectiva irregular';
  return 'NIF válido';
}
