/** Minúsculas e sem acentos: "Câmara" → "camara". */
export function normalizar(texto: string): string {
  return texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
}

/**
 * Sprint 562: pesquisa por palavras, igual à do servidor — cada palavra tem de aparecer, em qualquer ordem,
 * sem distinguir maiúsculas nem acentos. "xiaomi 13t pro" encontra "Xiaomi 13T / 13T Pro".
 */
export function correspondeTermos(texto: string, query: string): boolean {
  const termos = normalizar(query).split(/[\s/,;+()|]+/).filter(Boolean);
  if (termos.length === 0) return true;
  const alvo = normalizar(texto);
  return termos.every((t) => alvo.includes(t));
}
