import { useState } from 'react';
import { SignaturePadModal } from '../../components/SignaturePadModal';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from '../../lib/toast';
import { PenLine, Check } from 'lucide-react';
import { vendasApi } from '../../lib/vendas/api';

/**
 * Sprint 551 (Doc 80 pilar assinaturas / Doc 93 #5): recolha da assinatura do cliente no
 * balcão — vira-se o telemóvel/tablet para o cliente, ele assina no canvas, e a assinatura
 * fica estampada no PDF de entrada/entrega. Recapturar substitui a anterior.
 */
// Sprint 595: também a do técnico, para a ordem de reparação.
type Tipo = 'entrada' | 'entrega' | 'tecnico';
const ROTULO: Record<Tipo, string> = { entrada: 'Cliente (entrada)', tecnico: 'Técnico', entrega: 'Cliente (entrega)' };
const TITULO: Record<Tipo, string> = {
  entrada: 'Assinatura do cliente — entrada do equipamento',
  tecnico: 'Assinatura do técnico',
  entrega: 'Assinatura do cliente — entrega do equipamento',
};
const SUBTITULO: Record<Tipo, string> = {
  entrada: 'O cliente confirma os dados da ordem de reparação e aceita os termos.',
  tecnico: 'Quem recebeu o equipamento assina a ordem de reparação.',
  entrega: 'O cliente confirma o levantamento do equipamento.',
};

export function AssinaturasVenda({ vendaId }: { vendaId: string }) {
  const qc = useQueryClient();
  const [capturing, setCapturing] = useState<Tipo | null>(null);

  const assinaturas = useQuery({
    queryKey: ['venda-assinaturas', vendaId],
    queryFn: () => vendasApi.listAssinaturas(vendaId),
    staleTime: 60_000,
  });

  const save = useMutation({
    mutationFn: ({ tipo, dataUrl }: { tipo: Tipo; dataUrl: string }) =>
      vendasApi.saveAssinatura(vendaId, tipo, dataUrl),
    onSuccess: () => {
      toast.success('Assinatura guardada — já aparece no PDF.');
      setCapturing(null);
      void qc.invalidateQueries({ queryKey: ['venda-assinaturas', vendaId] });
    },
    onError: (e) => toast.fromError(e, 'Não foi possível guardar a assinatura.'),
  });

  const info = (tipo: string) => assinaturas.data?.find((a) => a.tipo === tipo);

  return (
    <div className="border-t border-zinc-200 pt-3 dark:border-zinc-800">
      <p className="text-[11px] font-medium uppercase tracking-wide text-zinc-500">Assinaturas</p>
      <div className="mt-2 grid grid-cols-3 gap-2">
        {(['entrada', 'tecnico', 'entrega'] as const).map((tipo) => {
          const a = info(tipo);
          return (
            <button
              key={tipo}
              type="button"
              onClick={() => setCapturing(tipo)}
              className={
                a
                  ? 'flex flex-col items-center gap-0.5 rounded-md border border-emerald-300 px-2 py-2 text-xs text-emerald-700 hover:bg-emerald-50 dark:border-emerald-800/60 dark:text-emerald-300 dark:hover:bg-emerald-950/30'
                  : 'flex flex-col items-center gap-0.5 rounded-md border border-zinc-300 px-2 py-2 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800'
              }
            >
              <span className="flex items-center gap-1 font-medium capitalize">
                {a ? <Check size={12} /> : <PenLine size={12} />} {ROTULO[tipo]}
              </span>
              <span className="text-[10px] text-zinc-500">
                {a ? new Date(a.assinadaEm).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : 'recolher'}
              </span>
            </button>
          );
        })}
      </div>

      {capturing && (
        <SignaturePadModal
          title={TITULO[capturing]}
          subtitle={SUBTITULO[capturing]}
          saving={save.isPending}
          onClose={() => setCapturing(null)}
          onSave={(dataUrl) => save.mutate({ tipo: capturing, dataUrl })}
        />
      )}
    </div>
  );
}
