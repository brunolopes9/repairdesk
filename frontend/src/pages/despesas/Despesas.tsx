import {
  DESPESA_CATEGORIA,
  OPEX_DESPESA_CATEGORIAS,
} from '../../lib/despesas/types';
import AprovadasTab from './AprovadasTab';

export default function Despesas() {
  return (
    <AprovadasTab
      title="Despesas"
      description="OpEx da operacao: renda, energia, SaaS, combustivel, comunicacoes, seguros e outras despesas gerais."
      categoriaIn={OPEX_DESPESA_CATEGORIAS}
      excludeSupplierInvoiceImports
      allowedCategorias={OPEX_DESPESA_CATEGORIAS}
      initialCategoria={DESPESA_CATEGORIA.Renda}
      createLabel="Nova despesa"
      emptyTitle="Ainda nao ha despesas OpEx"
      emptyDescription="Regista os custos recorrentes e gerais da oficina sem misturar compras de stock."
      showCategoriaFilter
      showRecurringToggle
    />
  );
}
