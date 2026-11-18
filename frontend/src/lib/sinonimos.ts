/**
 * 18/11 (Bruno): «magnet» tem de encontrar «magnético», «gravity», «ventilação»… As faturas vêm em inglês e as
 * pessoas escrevem em português. MESMA lista que backend/src/RepairDesk.DAL/Persistence/Sinonimos.cs e o site
 * (lopestech-site/js/loja.js) — manter iguais.
 */
export const GRUPOS_SINONIMOS: string[][] = [
  ['magnetico', 'magnetic', 'magnet', 'iman', 'magsafe'],
  ['gravidade', 'gravity'],
  ['ventilacao', 'ventilador', 'grelha', 'vent', 'air vent'],
  ['carregador', 'charger', 'adaptador', 'adapter', 'power adapter'],
  ['isqueiro', 'car charger', 'car adapter'],
  ['cabo', 'cable'],
  ['capa', 'case', 'cover', 'bookcase', 'carteira', 'bolsa'],
  ['pelicula', 'vidro temperado', 'tempered glass', 'tempered', 'screen protector', 'protetor de ecra'],
  ['hidrogel', 'hydrogel', 'gel'],
  ['protetor de camara', 'camera protector', 'camera lens', 'lente', 'lens', 'metal shield'],
  ['suporte', 'holder', 'mount'],
  ['retrovisor', 'rearview', 'espelho', 'mirror'],
  ['auricular', 'auriculares', 'earphones', 'headphones', 'headset', 'fones', 'buds', 'earbuds'],
  ['bateria', 'battery'],
  ['ecra', 'display', 'lcd', 'oled', 'screen', 'touch', 'digitizer'],
  ['tampa', 'back cover', 'traseira', 'back glass', 'vidro traseiro', 'housing'],
  ['powerbank', 'power bank', 'bateria externa'],
  ['conector de carga', 'charging port', 'charge port', 'porta de carga', 'dock'],
  ['camara', 'camera'],
  ['altifalante', 'speaker', 'buzzer', 'coluna'],
  ['transparente', 'clear', 'transparent'],
  ['preto', 'preta', 'black', 'midnight'],
  ['branco', 'branca', 'white'],
  ['rosa', 'pink'],
  ['azul', 'blue'],
  ['dourado', 'dourada', 'gold'],
  ['verde', 'green'],
  ['cinzento', 'cinzenta', 'gray', 'grey'],
  ['titanio', 'titanium'],
];

/** O termo (já normalizado) e os sinónimos do(s) grupo(s) a que pertence. */
export function variantes(termo: string): string[] {
  const lista = [termo];
  if (termo.length < 3) return lista;
  for (const g of GRUPOS_SINONIMOS)
    if (g.some((w) => w.startsWith(termo) || (w.length >= 4 && termo.startsWith(w))))
      for (const w of g) if (!lista.includes(w)) lista.push(w);
  return lista;
}
