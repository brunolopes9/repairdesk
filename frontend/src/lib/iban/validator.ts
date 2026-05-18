/**
 * Validador de IBAN — algoritmo ISO 13616.
 *
 * Foca-se em IBAN português (`PT50` + 21 dígitos = 25 chars total).
 * O algoritmo mod-97 é universal e funciona para qualquer país, mas
 * só validamos formato PT por agora (target de mercado).
 *
 * IBAN PT: PT50 NNNN NNNN NNNNNNNNNNN NN
 *  - 2 letras país (PT)
 *  - 2 dígitos check
 *  - 4 dígitos banco
 *  - 4 dígitos balcão
 *  - 11 dígitos conta
 *  - 2 dígitos check conta
 *  = 25 caracteres total (sem espaços)
 */

export interface IbanValidation {
  /** Só caracteres alfanuméricos, uppercase. */
  cleaned: string;
  /** Formatado em grupos de 4: 'PT50 0002 0123 1234 5678 9015 4'. */
  display: string;
  /** True se tem 25 chars e check-digit ISO 13616 válido. */
  isValid: boolean;
  /** Mensagem amigável para utilizador. */
  message: string;
}

export function validateIban(input: string | null | undefined): IbanValidation {
  const cleaned = (input ?? '').replace(/[^a-zA-Z0-9]/g, '').toUpperCase();

  if (!cleaned) {
    return { cleaned, display: '', isValid: false, message: '' };
  }

  if (!cleaned.startsWith('PT')) {
    return {
      cleaned,
      display: cleaned,
      isValid: false,
      message: 'IBAN deve começar com PT (para outros países, adapta depois).',
    };
  }

  if (cleaned.length < 25) {
    return {
      cleaned,
      display: groupFours(cleaned),
      isValid: false,
      message: `Falta${25 - cleaned.length === 1 ? '' : 'm'} ${25 - cleaned.length} ${25 - cleaned.length === 1 ? 'caracter' : 'caracteres'}.`,
    };
  }

  if (cleaned.length > 25) {
    return {
      cleaned,
      display: groupFours(cleaned),
      isValid: false,
      message: 'IBAN PT tem só 25 caracteres.',
    };
  }

  // mod-97 ISO 13616: mover 4 primeiros chars para o fim, converter letras para números (A=10, B=11, ...), mod 97 = 1
  const moved = cleaned.substring(4) + cleaned.substring(0, 4);
  const numeric = lettersToNumbers(moved);
  const isValid = mod97(numeric) === 1;

  return {
    cleaned,
    display: groupFours(cleaned),
    isValid,
    message: isValid ? 'IBAN válido' : 'IBAN inválido — verifica os 25 caracteres.',
  };
}

function lettersToNumbers(input: string): string {
  let result = '';
  for (const ch of input) {
    const code = ch.charCodeAt(0);
    if (code >= 48 && code <= 57) {
      result += ch;
    } else if (code >= 65 && code <= 90) {
      result += String(code - 55); // A=10, B=11, ..., Z=35
    } else {
      // Should not happen — input is already cleaned
      result += ch;
    }
  }
  return result;
}

function mod97(numeric: string): number {
  // Big numbers — processar em chunks de 9 dígitos
  let remainder = 0;
  for (let i = 0; i < numeric.length; i += 7) {
    const chunk = String(remainder) + numeric.substring(i, i + 7);
    remainder = parseInt(chunk, 10) % 97;
  }
  return remainder;
}

function groupFours(s: string): string {
  return s.replace(/(.{4})/g, '$1 ').trim();
}

export function displayIban(raw: string | null | undefined): string {
  if (!raw) return '';
  return validateIban(raw).display || raw;
}
