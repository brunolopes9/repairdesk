const eur = new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' });

/** Euros (decimal) → "12,34 €". Arredonda só na apresentação, como o motor de IVA. */
export function formatEur(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return '—';
  return eur.format(value);
}

export function formatPct(rate: number): string {
  return `${Math.round(rate * 10000) / 100}%`;
}

/** "12,5" / "12.5" → 12.5; vazio/ inválido → null. */
export function parseDecimal(input: string): number | null {
  const t = input.replace(/\s/g, '').replace(',', '.');
  if (!t) return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/**
 * Pré-visualização por unidade enquanto se escreve (SPEC compras §3.1). Espelha IvaEngine.Unidade;
 * o valor que conta é sempre o que o servidor devolve depois de gravar.
 */
export function previewUnidade(precoPago: number, taxaCompra: number, lucro: number, taxaVenda: number) {
  const custoSemIva = precoPago / (1 + taxaCompra);
  const ivaPago = precoPago - custoSemIva;
  const vendaSemIva = custoSemIva + lucro;
  const ivaVenda = vendaSemIva * taxaVenda;
  return { custoSemIva, precoFinalComIva: vendaSemIva + ivaVenda, ivaAPagarEstado: ivaVenda - ivaPago };
}

const PALAVRAS_ACESSORIO = ['vidro', 'pelicula', 'película', 'temperado', 'hidrogel', 'protetor', 'protector', 'capa '];

/** Espelha FiscalDefaults.LucroPorDefeito (só para mostrar o valor que o servidor vai assumir). */
export function lucroPorDefeito(descricao: string): number {
  const d = descricao.toLowerCase();
  return PALAVRAS_ACESSORIO.some((p) => d.includes(p)) ? 1 : 5;
}
