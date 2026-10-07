import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';
import { toast } from '../../lib/toast';
import { comprasApi } from '../../lib/compras/api';
import type { ImportComprasResultado } from '../../lib/compras/types';

/** Importa o Excel de compras (folhas Fornecedores / Faturas / Compras). O backend é idempotente. */
export default function ImportarExcelModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const qc = useQueryClient();
  const [ficheiro, setFicheiro] = useState<File | null>(null);
  const [resultado, setResultado] = useState<ImportComprasResultado | null>(null);

  const importar = useMutation({
    mutationFn: (f: File) => comprasApi.importarExcel(f),
    onSuccess: (r) => {
      setResultado(r);
      qc.invalidateQueries({ queryKey: ['compras'] });
      qc.invalidateQueries({ queryKey: ['fornecedores'] });
    },
    onError: (err) => toast.fromError(err),
  });

  function fechar() {
    setFicheiro(null);
    setResultado(null);
    onClose();
  }

  return (
    <Modal
      open={open}
      title="Importar Excel de compras"
      onClose={fechar}
      footer={
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={fechar}>{resultado ? 'Fechar' : 'Cancelar'}</Button>
          {!resultado && (
            <Button disabled={!ficheiro} loading={importar.isPending} onClick={() => ficheiro && importar.mutate(ficheiro)}>Importar</Button>
          )}
        </div>
      }
    >
      {!resultado ? (
        <div className="space-y-3 text-sm">
          <p className="text-zinc-600 dark:text-zinc-300">
            Ficheiro .xlsx com as folhas <strong>Fornecedores</strong>, <strong>Faturas</strong> e <strong>Compras</strong>.
            Documentos que já existam (mesmo fornecedor + nº fatura ou encomenda) são ignorados — podes importar o mesmo ficheiro várias vezes.
          </p>
          <input
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            onChange={(e) => setFicheiro(e.target.files?.[0] ?? null)}
            className="block w-full text-sm file:mr-3 file:rounded-lg file:border-0 file:bg-zinc-100 file:px-3 file:py-2 file:text-sm file:font-medium dark:file:bg-zinc-800"
          />
        </div>
      ) : (
        <div className="space-y-3 text-sm">
          <dl className="grid grid-cols-[1fr_auto] gap-y-1 tabular-nums">
            <dt>Documentos criados</dt><dd className="text-right font-medium">{resultado.documentosCriados}</dd>
            <dt>Linhas (lotes)</dt><dd className="text-right font-medium">{resultado.linhas}</dd>
            <dt>Já existiam (ignorados)</dt><dd className="text-right">{resultado.documentosJaExistentes}</dd>
            <dt>Fornecedores criados / atualizados</dt><dd className="text-right">{resultado.fornecedoresCriados} / {resultado.fornecedoresAtualizados}</dd>
          </dl>
          {resultado.avisos.length > 0 && (
            <ul className="max-h-48 list-disc space-y-1 overflow-y-auto rounded-lg border border-amber-200 bg-amber-50 p-3 pl-7 text-xs text-amber-900 dark:border-amber-900/40 dark:bg-amber-950/20 dark:text-amber-200">
              {resultado.avisos.map((a, i) => <li key={i}>{a}</li>)}
            </ul>
          )}
        </div>
      )}
    </Modal>
  );
}
