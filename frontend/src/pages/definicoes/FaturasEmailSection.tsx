import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Inbox } from 'lucide-react';
import { Button } from '../../components/ui';
import { api } from '../../lib/api';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { FaturasCloudflare } from './FaturasCloudflare';

interface Config {
  configurado: boolean;
  host: string | null;
  port: number | null;
  user: string | null;
  ultimaLeitura: string | null;
  ultimoErro: string | null;
  endereco?: string | null;
  temSegredo?: boolean;
  ultimoEmailEm?: string | null;
}

const campo = 'mt-1 w-full rounded-lg border border-zinc-300 px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-950';

/**
 * Sprint 620 (Doc 98 F5): caixa dedicada às faturas. O Mender lê-a de 5 em 5 minutos e cada PDF fica
 * «por aprovar» em Compras/Despesas. As faturas chegam lá por regras de reencaminhamento no Gmail/Outlook.
 */
export function FaturasEmailSection() {
  const qc = useQueryClient();
  const cfg = useQuery({ queryKey: ['faturas-email'], queryFn: () => api.get<Config>('/faturas-email').then((r) => r.data) });
  const [host, setHost] = useState('mail.lopestech.pt');
  const [user, setUser] = useState('faturas@lopestech.pt');
  const [password, setPassword] = useState('');
  const [editar, setEditar] = useState(false);

  const guardar = useMutation({
    mutationFn: () => api.put<Config>('/faturas-email', { host, port: 993, user, password }).then((r) => r.data),
    onSuccess: (d) => {
      qc.setQueryData(['faturas-email'], d);
      setPassword('');
      setEditar(false);
      toast.success('Caixa ligada', 'O Mender lê-a de 5 em 5 minutos.');
    },
    onError: (e) => toast.error('Não ficou ligada', apiErrorMessage(e)),
  });
  const lerAgora = useMutation({
    mutationFn: () => api.post<{ importados: number; config: Config }>('/faturas-email/ler-agora').then((r) => r.data),
    onSuccess: (r) => {
      qc.setQueryData(['faturas-email'], r.config);
      if (r.config.ultimoErro) toast.error('A leitura falhou', r.config.ultimoErro);
      else toast.success(r.importados === 0 ? 'Sem faturas novas' : `${r.importados} fatura(s) nova(s) por aprovar`);
    },
    onError: (e) => toast.error('A leitura falhou', apiErrorMessage(e)),
  });
  const desligar = useMutation({
    mutationFn: () => api.delete('/faturas-email'),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['faturas-email'] }),
    onError: (e) => toast.error('Não foi desligada', apiErrorMessage(e)),
  });

  const c = cfg.data;
  const mostrarForm = editar;

  return (
    <section className="space-y-4 rounded-2xl border border-zinc-200 bg-white p-5 dark:border-zinc-800 dark:bg-zinc-900">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-semibold"><Inbox size={18} /> Faturas por email</h2>
        <p className="mt-1 text-sm text-zinc-500">
          Uma caixa só para faturas. O Mender lê-a de 5 em 5 minutos e cada PDF fica «por aprovar» em Compras/Despesas (duplicados são ignorados).
          Nada se apaga: os emails são movidos para as pastas «Mender-Processadas», «Mender-Sem-PDF» ou «Mender-Erros».
        </p>
      </div>

      {c && <FaturasCloudflare endereco={c.endereco ?? null} temSegredo={!!c.temSegredo} ultimoEmailEm={c.ultimoEmailEm ?? null} />}

      <h3 className="pt-2 text-sm font-semibold">Alternativa: caixa IMAP</h3>
      {c && !c.configurado && !editar && (
        <Button size="sm" variant="ghost" onClick={() => setEditar(true)}>Ligar uma caixa IMAP</Button>
      )}
      {c?.configurado && !editar && (
        <div className="rounded-xl border border-zinc-200 p-3 text-sm dark:border-zinc-800">
          <p><strong>{c.user}</strong> <span className="text-zinc-500">em {c.host}</span></p>
          <p className="text-xs text-zinc-500">
            {c.ultimaLeitura ? `Última leitura: ${new Date(c.ultimaLeitura).toLocaleString('pt-PT')}` : 'Ainda não foi lida.'}
          </p>
          {c.ultimoErro && <p className="mt-1 text-xs text-rose-600">{c.ultimoErro}</p>}
          <div className="mt-3 flex flex-wrap gap-2">
            <Button size="sm" loading={lerAgora.isPending} onClick={() => lerAgora.mutate()}>Ler agora</Button>
            <Button size="sm" variant="secondary" onClick={() => { setHost(c.host ?? host); setUser(c.user ?? user); setEditar(true); }}>Mudar</Button>
            <Button size="sm" variant="ghost" loading={desligar.isPending} onClick={() => desligar.mutate()}>Desligar</Button>
          </div>
        </div>
      )}

      {mostrarForm && (
        <form className="grid gap-3 sm:grid-cols-2" onSubmit={(e) => { e.preventDefault(); guardar.mutate(); }}>
          <label className="block text-sm">Servidor IMAP
            <input value={host} onChange={(e) => setHost(e.target.value)} className={campo} placeholder="mail.lopestech.pt" />
          </label>
          <label className="block text-sm">Endereço da caixa
            <input value={user} onChange={(e) => setUser(e.target.value)} className={campo} autoComplete="off" placeholder="faturas@lopestech.pt" />
          </label>
          <label className="block text-sm sm:col-span-2">Palavra-passe da caixa
            <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} className={campo} autoComplete="new-password" />
            <span className="mt-1 block text-xs text-zinc-500">Testo a ligação antes de guardar. Fica cifrada e não volta a aparecer.</span>
          </label>
          <div className="flex gap-2 sm:col-span-2">
            <Button type="submit" loading={guardar.isPending} disabled={!host.trim() || !user.trim() || !password}>Testar e ligar</Button>
            {editar && <Button type="button" variant="ghost" onClick={() => setEditar(false)}>Cancelar</Button>}
          </div>
        </form>
      )}

      <details className="rounded-xl bg-zinc-50 p-3 text-sm dark:bg-zinc-950">
        <summary className="cursor-pointer font-medium">Como pôr as faturas a chegar sozinhas</summary>
        <ol className="mt-2 list-decimal space-y-1 pl-5 text-zinc-600 dark:text-zinc-300">
          <li>No cPanel do lopestech.pt cria a caixa <strong>faturas@lopestech.pt</strong> (Contas de email → Criar).</li>
          <li>Liga-a aqui em cima (servidor <code>mail.lopestech.pt</code>, porta 993).</li>
          <li><strong>Gmail:</strong> Definições → Encaminhamento e POP/IMAP → «Adicionar endereço de encaminhamento» → faturas@lopestech.pt. O código de confirmação aparece no webmail, na pasta «Mender-Sem-PDF».</li>
          <li><strong>Gmail:</strong> cria um filtro por fornecedor (ex.: <code>from:(tudo4mobile OR microwire OR vodafone) has:attachment</code>) → «Reencaminhar para» faturas@lopestech.pt.</li>
          <li><strong>Outlook:</strong> Definições → Correio → Regras → nova regra «De» o fornecedor → «Reencaminhar para» faturas@lopestech.pt.</li>
          <li>Também podes reencaminhar à mão qualquer fatura para esse endereço.</li>
        </ol>
      </details>
    </section>
  );
}
