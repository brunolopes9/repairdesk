import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Building2 } from 'lucide-react';
import { Button } from '../../components/ui';
import { api } from '../../lib/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';

/**
 * Sprint 604: o dono da plataforma cria outra empresa (ex.: Cortex Automation). A conta nova
 * recebe por email o link para escolher a palavra-passe — ninguém a vê. Outras empresas recebem
 * 403 do servidor, que é quem decide.
 */
export function NovaEmpresa() {
  const [nome, setNome] = useState('');
  const [email, setEmail] = useState('');
  const criar = useMutation({
    mutationFn: () => api.post<{ tenantId: string; nome: string; email: string }>('/plataforma/empresas', { nome, email }).then((r) => r.data),
    onSuccess: (e) => {
      toast.success(`${e.nome} criada. Vê o email ${e.email} para escolher a palavra-passe.`);
      setNome('');
      setEmail('');
    },
    onError: (err) => toast.error(apiErrorMessage(err)),
  });

  const input = 'mt-1 block min-h-10 w-full rounded-md border border-zinc-200 bg-white px-3 text-sm dark:border-zinc-700 dark:bg-zinc-950';
  return (
    <section className="rounded-2xl border border-zinc-200 bg-white p-6 shadow-sm dark:border-zinc-800 dark:bg-zinc-900">
      <h2 className="flex items-center gap-2 text-base font-semibold"><Building2 size={18} /> Nova empresa</h2>
      <p className="mt-1 text-sm text-zinc-500">
        Cria outra empresa no Mender, separada desta (clientes, conteúdo e redes próprios). Usa um email que ainda não
        tenha conta — por exemplo <code>teuemail+empresa@gmail.com</code>, que chega à mesma caixa do Gmail.
      </p>
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <label className="text-sm font-medium">
          Nome da empresa
          <input className={input} value={nome} onChange={(e) => setNome(e.target.value)} placeholder="Cortex Automation" />
        </label>
        <label className="text-sm font-medium">
          Email do administrador
          <input className={input} type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="nome+cortex@gmail.com" />
        </label>
      </div>
      <div className="mt-4 flex justify-end">
        <Button loading={criar.isPending} disabled={nome.trim().length < 2 || !email.includes('@')} onClick={() => criar.mutate()}>
          Criar empresa
        </Button>
      </div>
    </section>
  );
}
