import { DESPESA_CATEGORIA } from '../../lib/despesas/types';
import AprovadasTab from './AprovadasTab';

/**
 * Doc 94: as compras de stock vivem em Compras (CompraDocumento), por isso aqui aparecem TODAS as
 * despesas — incluindo as aprovadas a partir de faturas recebidas (ex.: RMA, equipamento).
 * Antes esta página filtrava só categorias "OpEx" e escondia as vindas de faturas (herança de quando
 * as compras de peças também eram Despesas).
 */
export default function Despesas() {
  return (
    <AprovadasTab
      title="Despesas"
      description="Custos do negócio que não são stock: renda, software, transporte, ferramentas, comunicações, seguros…"
      initialCategoria={DESPESA_CATEGORIA.Outro}
      createLabel="Nova despesa"
      emptyTitle="Ainda não há despesas"
      emptyDescription="Regista aqui os custos do negócio que não são peças para revenda."
      showCategoriaFilter
      showRecurringToggle
    />
  );
}
