import PorAprovarTab from '../despesas/PorAprovarTab';

/**
 * Sprint 657: todas as faturas que chegam a faturas@lopestech.pt (ou por upload), num só sítio. Cada uma mostra se o
 * fornecedor é de compras (peças para stock) ou de despesas, e o botão certo vem destacado. Também aparecem filtradas
 * em Compras → «Faturas recebidas» e em Despesas → «Por aprovar».
 */
export default function FaturasRecebidas() {
  return <PorAprovarTab destino="todas" />;
}
