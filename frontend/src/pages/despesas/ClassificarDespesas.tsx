import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '../../components/ui';
import { api } from '../../lib/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

/**
 * Sprint 646 (livros fiscal/gestão): classificar várias despesas de uma vez pelo nº visível —
 * «só gestão», pertence a (0 Negócio, 1 Pessoal, 2 Obrigação), atividade (0 LopesTech, 1 Trading, 2 Mista) e nota.
 */
export function ClassificarDespesas({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const classificar = useMutation({
    mutationFn: (itens: unknown[]) =>
      api.post<{ atualizadas: number; naoEncontradas: number[] }>('/despesas/classificar', itens).then((r) => r.data),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['despesas'] });
      qc.invalidateQueries({ queryKey: ['apuramento'] });
      toast.success(`${r.atualizadas} despesa(s) classificadas`, r.naoEncontradas.length ? `Não encontradas: ${r.naoEncontradas.join(', ')}` : undefined);
      onClose();
    },
    onError: (e) => setErro(apiErrorMessage(e)),
  });
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-labelledby="classificar-despesas" onClick={(e) => e.stopPropagation()}
        className="w-full rounded-t-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900 sm:max-w-2xl sm:rounded-2xl">
        <h2 id="classificar-despesas" className="text-lg font-semibold">Classificar em lote</h2>
        <p className="mt-1 text-sm text-zinc-500">
          Lista JSON pelo nº da despesa: <code>{'[{"numero":92,"soGestao":true,"ambito":0,"afetacao":1,"nota":"…"}]'}</code>.
          Só muda o que vier preenchido.
        </p>
        <textarea aria-label="Classificação em JSON" rows={12} value={texto} onChange={(e) => setTexto(e.target.value)}
          className="mt-3 w-full rounded-lg border border-zinc-300 p-3 font-mono text-xs dark:border-zinc-700 dark:bg-zinc-950" />
        {erro && <p role="alert" className="mt-2 text-sm text-rose-600">{erro}</p>}
        <div className="mt-4 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button disabled={!texto.trim()} loading={classificar.isPending} onClick={() => {
            setErro(null);
            try {
              const d = JSON.parse(texto) as unknown;
              if (!Array.isArray(d)) throw new Error('Tem de ser uma lista [ … ].');
              classificar.mutate(d);
            } catch (e) { setErro(e instanceof Error ? `JSON inválido: ${e.message}` : 'JSON inválido.'); }
          }}>Classificar</Button>
        </div>
      </div>
    </div>
  );
}
