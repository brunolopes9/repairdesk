import { Package } from 'lucide-react';
import PecasUsadas from './PecasUsadas';
import DespesasImputadas from './DespesasImputadas';

/**
 * Sprint 116: Wrapper visual que junta "Peças do stock" + "Compras ao fornecedor"
 * sob 1 cabeçalho único.
 *
 * Sprint 179: stack vertical (não mais 2 colunas) — Bruno pediu unificação visual.
 * Backend mantém 2 entidades distintas (PartMovimento vs Despesa) com flows
 * diferentes mas UI agora apresenta-os sequencialmente como variações da mesma
 * coisa: "peças que entraram na reparação". Cada sub-secção tem o seu botão
 * "Adicionar" próprio para clarificar a fonte.
 */
export default function PecasReparacao({ reparacaoId, readOnly }: { reparacaoId: string; readOnly?: boolean }) {
  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <header className="flex items-start gap-2 border-b border-zinc-100 pb-3 dark:border-zinc-800">
        <Package size={16} strokeWidth={2} className="mt-0.5 text-zinc-500" />
        <div>
          <h2 className="text-sm font-semibold">Peças desta reparação</h2>
          <p className="text-[11px] text-zinc-500">
            Soma de tudo o que foi usado nesta reparação. <strong>Do stock</strong> = peças que já tinhas;
            <strong> compra ao fornecedor</strong> = encomendas específicas com nº de encomenda e portes.
            Ambas contam para o custo total da reparação.
          </p>
        </div>
      </header>
      <div className="mt-4 space-y-4">
        <PecasUsadas reparacaoId={reparacaoId} readOnly={readOnly} />
        <DespesasImputadas reparacaoId={reparacaoId} invalidateKeys={[['reparacao', reparacaoId]]} readOnly={readOnly} />
      </div>
    </section>
  );
}
