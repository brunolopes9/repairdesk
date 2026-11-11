import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '../../components/ui';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { rotinaApi, type GuardarRotinaItem } from '../../lib/rotina/api';

/** Sprint 615: importar vários itens de uma vez (lista JSON) — modelos de rotina e cópias de segurança. */
export default function ImportarRotina({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient();
  const [texto, setTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const importar = useMutation({
    mutationFn: (itens: GuardarRotinaItem[]) => rotinaApi.importar(itens),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['rotina'] });
      toast.success(`${r.length} itens importados`, 'Os avisos começam já a seguir este plano.');
      onClose();
    },
    onError: (e) => setErro(apiErrorMessage(e)),
  });

  function confirmar() {
    setErro(null);
    try {
      const dados = JSON.parse(texto) as unknown;
      if (!Array.isArray(dados)) throw new Error('Tem de ser uma lista [ … ].');
      importar.mutate(dados as GuardarRotinaItem[]);
    } catch (e) {
      setErro(e instanceof Error ? `JSON inválido: ${e.message}` : 'JSON inválido.');
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-labelledby="importar-titulo"
        className="w-full rounded-t-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900 sm:max-w-2xl sm:rounded-2xl"
        onClick={(e) => e.stopPropagation()}>
        <h2 id="importar-titulo" className="text-lg font-semibold">Importar rotina</h2>
        <p className="mt-1 text-sm text-zinc-500">Cola a lista de itens (JSON). Os itens são acrescentados aos que já tens; se algum estiver errado, nada é importado.</p>
        <textarea aria-label="Itens em JSON" rows={12} value={texto} onChange={(e) => setTexto(e.target.value)}
          className="mt-3 w-full rounded-lg border border-zinc-300 p-3 font-mono text-xs dark:border-zinc-700 dark:bg-zinc-950" />
        {erro && <p role="alert" className="mt-2 text-sm text-rose-600">{erro}</p>}
        <div className="mt-4 flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>Cancelar</Button>
          <Button disabled={!texto.trim()} loading={importar.isPending} onClick={confirmar}>Importar</Button>
        </div>
      </div>
    </div>
  );
}
