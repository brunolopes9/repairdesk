import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '../../components/ui';
import { api } from '../../lib/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

/** Sprint 636: criar vários produtos de uma vez (lista JSON) — ficam como rascunho; títulos repetidos são saltados. */
export function ImportarProdutos({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const importar = useMutation({
    mutationFn: (dados: unknown[]) => api.post<{ criados: number; jaExistiam: number }>('/loja/produtos/importar', dados).then((r) => r.data),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['loja'] });
      toast.success(`${r.criados} produto(s) criados como rascunho`, r.jaExistiam ? `${r.jaExistiam} já existiam.` : 'Revê preços e fotos antes de publicar.');
      onClose();
    },
    onError: (e) => setErro(apiErrorMessage(e)),
  });
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-labelledby="importar-produtos" onClick={(e) => e.stopPropagation()}
        className="w-full rounded-t-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900 sm:max-w-2xl sm:rounded-2xl">
        <h2 id="importar-produtos" className="text-lg font-semibold">Importar produtos</h2>
        <p className="mt-1 text-sm text-zinc-500">Lista JSON de produtos. Ficam como rascunho — nada vai para o site sem publicares.</p>
        <textarea aria-label="Produtos em JSON" rows={12} value={texto} onChange={(e) => setTexto(e.target.value)}
          className="mt-3 w-full rounded-lg border border-zinc-300 p-3 font-mono text-xs dark:border-zinc-700 dark:bg-zinc-950" />
        {erro && <p role="alert" className="mt-2 text-sm text-rose-600">{erro}</p>}
        <div className="mt-4 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button disabled={!texto.trim()} loading={importar.isPending} onClick={() => {
            setErro(null);
            try {
              const d = JSON.parse(texto) as unknown;
              if (!Array.isArray(d)) throw new Error('Tem de ser uma lista [ … ].');
              importar.mutate(d);
            } catch (e) { setErro(e instanceof Error ? `JSON inválido: ${e.message}` : 'JSON inválido.'); }
          }}>Importar</Button>
        </div>
      </div>
    </div>
  );
}
