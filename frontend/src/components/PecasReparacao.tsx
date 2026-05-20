import { Package } from 'lucide-react';
import PecasUsadas from './PecasUsadas';
import DespesasImputadas from './DespesasImputadas';

/**
 * Sprint 116: Wrapper visual que junta "Peças do stock" + "Compras ao fornecedor"
 * sob 1 cabeçalho único — Bruno pediu unificação para deixar de parecer duplicado.
 *
 * Cada subsecção mantém o seu botão "Adicionar" próprio porque o backend tem entidades
 * distintas (PartMovimento vs Despesa) com flows diferentes. A unificação é visual,
 * não estrutural.
 */
export default function PecasReparacao({ reparacaoId, readOnly }: { reparacaoId: string; readOnly?: boolean }) {
  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <header className="flex items-center gap-2 border-b border-zinc-100 pb-3 dark:border-zinc-800">
        <Package size={16} strokeWidth={2} className="text-zinc-500" />
        <div>
          <h2 className="text-sm font-semibold">Peças desta reparação</h2>
          <p className="text-[11px] text-zinc-500">
            Stock que gastas + compras específicas ao fornecedor. Tudo entra no cálculo de custo da reparação.
          </p>
        </div>
      </header>
      <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="space-y-1">
          <div className="text-[11px] font-medium uppercase tracking-wider text-zinc-500">Do stock interno</div>
          <PecasUsadas reparacaoId={reparacaoId} readOnly={readOnly} />
        </div>
        <div className="space-y-1">
          <div className="text-[11px] font-medium uppercase tracking-wider text-zinc-500">Encomendado ao fornecedor</div>
          <DespesasImputadas reparacaoId={reparacaoId} invalidateKeys={[['reparacao', reparacaoId]]} readOnly={readOnly} />
        </div>
      </div>
    </section>
  );
}
