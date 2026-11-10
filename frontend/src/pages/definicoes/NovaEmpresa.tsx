import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Building2 } from 'lucide-react';
import { Button } from '../../components/ui';
import { api } from '../../lib/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

/**
 * Sprint 604/606: o dono da plataforma cria outra empresa (ex.: Cortex Automation) na sua própria
 * conta — depois troca de empresa no botão do topo. Os dados de cada empresa ficam separados.
 * Outras empresas recebem 403 do servidor, que é quem decide.
 */
export function NovaEmpresa() {
  const qc = useQueryClient();
  const [nome, setNome] = useState('');
  const criar = useMutation({
    mutationFn: () => api.post<{ tenantId: string; nome: string }>('/plataforma/empresas', { nome }).then((r) => r.data),
    onSuccess: (e) => {
      toast.success(`${e.nome} criada. Troca para ela no botão de empresa, no topo.`);
      setNome('');
      qc.invalidateQueries({ queryKey: ['auth', 'empresas'] });
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });

  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="flex items-center gap-2 text-base font-semibold"><Building2 size={18} /> Nova empresa</h2>
      <p className="mt-1 text-sm text-zinc-500">
        Cria outra empresa no Mender, com clientes, conteúdo e redes próprios, na tua conta de sempre. Depois trocas
        entre empresas no botão do topo (ao lado do logótipo).
      </p>
      <label className="mt-4 block text-sm font-medium sm:max-w-md">
        Nome da empresa
        <input
          className="mt-1 block min-h-10 w-full rounded-md border border-zinc-200 bg-white px-3 text-sm dark:border-zinc-700 dark:bg-zinc-950"
          value={nome}
          onChange={(e) => setNome(e.target.value)}
          placeholder="Cortex Automation"
        />
      </label>
      <div className="mt-4 flex justify-end">
        <Button loading={criar.isPending} disabled={nome.trim().length < 2} onClick={() => criar.mutate()}>
          Criar empresa
        </Button>
      </div>
    </section>
  );
}
