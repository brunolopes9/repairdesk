import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Copy, CreditCard, KeyRound, ShieldCheck, Trash2 } from 'lucide-react';
import { BackButton, Button, PageHeader, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { useConfirm } from '../../components/ConfirmDialog';
import { api } from '../../lib/api';
import { toast } from '../../lib/toast';

interface EstadoPagamentos { gatewayKeyDefinida: boolean; antiPhishingDefinida: boolean; ativo: boolean; callbackUrl: string | null }

const pagamentosApi = {
  estado: () => api.get<EstadoPagamentos>('/loja/pagamentos').then((r) => r.data),
  guardar: (b: { gatewayKey: string | null; antiPhishingKey: string | null; remover?: boolean }) =>
    api.put<EstadoPagamentos>('/loja/pagamentos', b).then((r) => r.data),
};

/**
 * Sprint 690: a loja online cobra pelo ifthenpay (MB WAY, Multibanco, cartão, Google Pay, Apple Pay).
 * As chaves ficam cifradas no servidor e nunca voltam ao browser — aqui só se vê «definida».
 */
export default function PagamentosLoja() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const estado = useQuery({ queryKey: ['loja', 'pagamentos'], queryFn: pagamentosApi.estado });
  const [gateway, setGateway] = useState('');
  const [anti, setAnti] = useState('');

  const guardar = useMutation({
    mutationFn: (remover?: boolean) => pagamentosApi.guardar({ gatewayKey: gateway.trim() || null, antiPhishingKey: anti.trim() || null, remover }),
    onSuccess: (e, remover) => {
      qc.setQueryData(['loja', 'pagamentos'], e);
      setGateway(''); setAnti('');
      if (remover) toast.success('Pagamentos desligados', 'A loja deixa de aceitar pagamentos pelo ifthenpay.');
      else if (e.ativo) toast.success('Pagamentos da loja ativos', 'Os clientes já podem pagar com MB WAY, Multibanco, cartão, Google Pay e Apple Pay.');
      else toast.success('Chave guardada', 'Falta a outra chave para ativar os pagamentos.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar as chaves.'),
  });

  const e = estado.data;
  const copiar = async (txt: string) => {
    try { await navigator.clipboard.writeText(txt); toast.success('Copiado', 'Cola-o no pedido ao ifthenpay.'); }
    catch { toast.error('Não foi possível copiar', 'Seleciona o texto e copia à mão.'); }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-4 p-4">
      <BackButton to="/definicoes" />
      <PageHeader title="Pagamentos da loja" subtitle="ifthenpay — MB WAY, Multibanco, cartão, Google Pay e Apple Pay no lopestech.pt" />

      {estado.isError ? (
        <SectionCard title="Não foi possível carregar">
          <p role="alert" className="text-sm text-rose-700 dark:text-rose-300">Não consegui ler o estado dos pagamentos. Atualiza a página; se continuar, avisa.</p>
        </SectionCard>
      ) : (
        <>
          <SectionCard title={<span className="flex items-center gap-2"><CreditCard size={16} /> Estado</span>}>
            {estado.isLoading ? <p className="text-sm text-zinc-500">A carregar…</p> : e?.ativo ? (
              <p className="flex items-center gap-2 text-sm font-medium text-emerald-700 dark:text-emerald-400"><CheckCircle2 size={16} /> Ativo — a loja cobra pelo ifthenpay.</p>
            ) : (
              <p role="status" className="flex items-start gap-2 text-sm font-medium text-amber-700 dark:text-amber-400">
                <AlertTriangle size={16} className="mt-0.5 flex-none" />
                Desligado — os clientes veem «pagamentos ainda não ativos» ao finalizar a encomenda. Cola as duas chaves abaixo.
              </p>
            )}
            <ul className="mt-3 space-y-1 text-sm">
              <li>Gateway Key: {e?.gatewayKeyDefinida ? <strong className="text-emerald-700 dark:text-emerald-400">definida</strong> : <span className="text-zinc-500">em falta</span>}</li>
              <li>Chave anti-phishing: {e?.antiPhishingDefinida ? <strong className="text-emerald-700 dark:text-emerald-400">definida</strong> : <span className="text-zinc-500">em falta</span>}</li>
            </ul>
          </SectionCard>

          <SectionCard title={<span className="flex items-center gap-2"><KeyRound size={16} /> Chaves do ifthenpay</span>}>
            <form className="space-y-3" onSubmit={(ev) => { ev.preventDefault(); guardar.mutate(false); }}>
              <label className="block">
                <span className={labelCls}>Gateway Key (Pay by Link)</span>
                <input className={inputCls} value={gateway} onChange={(ev) => setGateway(ev.target.value)} autoComplete="off" spellCheck={false}
                  placeholder={e?.gatewayKeyDefinida ? '•••••••• (definida — escreve só para trocar)' : 'ex.: ABC-123456'} maxLength={50} />
              </label>
              <label className="block">
                <span className={labelCls}>Chave anti-phishing (a que combinaste com o ifthenpay para o callback)</span>
                <input className={inputCls} type="password" value={anti} onChange={(ev) => setAnti(ev.target.value)} autoComplete="new-password" spellCheck={false}
                  placeholder={e?.antiPhishingDefinida ? '•••••••• (definida — escreve só para trocar)' : 'até 50 caracteres'} maxLength={50} />
              </label>
              <p className="text-xs text-zinc-500">As chaves ficam cifradas no servidor e não voltam a aparecer aqui.</p>
              <div className="flex flex-wrap gap-2">
                <Button type="submit" loading={guardar.isPending} disabled={!gateway.trim() && !anti.trim()}>Guardar</Button>
                {(e?.gatewayKeyDefinida || e?.antiPhishingDefinida) && (
                  <Button type="button" variant="danger" leftIcon={<Trash2 size={14} />} disabled={guardar.isPending}
                    onClick={async () => {
                      if (await confirm({ title: 'Desligar os pagamentos da loja?', description: 'As chaves são apagadas e a loja deixa de aceitar pagamentos até as voltares a colar.', confirmLabel: 'Desligar', destructive: true })) guardar.mutate(true);
                    }}>Desligar</Button>
                )}
              </div>
            </form>
          </SectionCard>

          <SectionCard title={<span className="flex items-center gap-2"><ShieldCheck size={16} /> Callback (confirmação do pagamento)</span>}>
            <p className="text-sm text-zinc-600 dark:text-zinc-400">
              O pagamento só fica confirmado quando o ifthenpay chama este endereço com a chave anti-phishing — nunca só porque o cliente voltou ao site.
              Pede ao ifthenpay (suporte@ifthenpay.com) para ativar o callback da tua Gateway Key com este URL:
            </p>
            {e?.callbackUrl ? (
              <div className="mt-2 flex items-start gap-2">
                <code className="flex-1 break-all rounded-lg bg-zinc-100 p-2 text-xs dark:bg-zinc-800">{e.callbackUrl}</code>
                <Button type="button" size="sm" variant="secondary" leftIcon={<Copy size={13} />} onClick={() => copiar(e.callbackUrl!)}>Copiar</Button>
              </div>
            ) : (
              <p className="mt-2 text-sm text-amber-700 dark:text-amber-400">A empresa ainda não tem endereço público — abre Definições → Automações uma vez para o criar.</p>
            )}
          </SectionCard>
        </>
      )}
    </div>
  );
}
