import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, PackageCheck, Truck } from 'lucide-react';
import { Button, EmptyState, PageHeader, SkeletonTable } from '../../components/ui';
import { useConfirm } from '../../components/ConfirmDialog';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { euro } from '../../lib/loja/api';
import { ENCOMENDA_ESTADO_LABEL, EncomendaEstado, encomendasApi, type Encomenda } from '../../lib/loja/encomendas';

const COR: Record<EncomendaEstado, string> = {
  0: 'bg-zinc-100 text-zinc-600', 1: 'bg-amber-100 text-amber-800', 2: 'bg-sky-100 text-sky-800', 3: 'bg-indigo-100 text-indigo-800',
  4: 'bg-emerald-100 text-emerald-800', 5: 'bg-rose-100 text-rose-700', 6: 'bg-zinc-100 text-zinc-500',
};

const data = (iso: string | null) => (iso ? new Date(iso).toLocaleString('pt-PT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : '');

/**
 * Sprint 656: encomendas pagas no site. Paga → a venda já existe (o stock saiu) e fica por faturar; aqui preparas,
 * envias pelos CTT (com o nº de seguimento, o cliente recebe email) e marcas entregue.
 */
export default function Encomendas() {
  const [todas, setTodas] = useState(false);
  const lista = useQuery({ queryKey: ['loja', 'encomendas'], queryFn: encomendasApi.list, refetchInterval: 60_000 });
  const itens = (lista.data ?? []).filter((e) => todas || (e.estado !== EncomendaEstado.Expirada && e.estado !== EncomendaEstado.AguardaPagamento));
  const porTratar = (lista.data ?? []).filter((e) => e.estado === EncomendaEstado.Paga || e.estado === EncomendaEstado.EmPreparacao).length;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Encomendas da loja"
        description={porTratar > 0 ? `${porTratar} por enviar.` : 'Encomendas pagas em lopestech.pt/loja.'}
        actions={<Link to="/loja" className="text-sm font-medium text-brand-600 hover:underline">Produtos</Link>}
      />
      <label className="flex items-center gap-2 text-sm text-zinc-600">
        <input type="checkbox" checked={todas} onChange={(e) => setTodas(e.target.checked)} />
        Mostrar também as não pagas (a aguardar e expiradas)
      </label>
      {lista.isLoading ? (
        <SkeletonTable columns={4} rows={3} />
      ) : lista.isError ? (
        <p className="text-sm text-rose-600">{apiErrorMessage(lista.error)}</p>
      ) : itens.length === 0 ? (
        <EmptyState icon={PackageCheck} title="Sem encomendas" description="Quando alguém pagar no site, aparece aqui e recebes um aviso." />
      ) : (
        <ul className="space-y-3">{itens.map((e) => <Cartao key={e.id} e={e} />)}</ul>
      )}
    </div>
  );
}

function Cartao({ e }: { e: Encomenda }) {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [seguimento, setSeguimento] = useState(e.numeroSeguimento ?? '');
  const mudar = useMutation({
    mutationFn: (estado: EncomendaEstado) => encomendasApi.mudarEstado(e.id, estado, seguimento.trim() || null),
    onSuccess: (r) => {
      qc.invalidateQueries({ queryKey: ['loja', 'encomendas'] });
      toast.success(`${r.referencia}: ${ENCOMENDA_ESTADO_LABEL[r.estado]}`);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível mudar o estado.'),
  });
  const cancelar = async () => {
    if (await confirm({
      title: `Cancelar ${e.referencia}?`,
      description: 'A venda é cancelada e o stock volta. O reembolso do pagamento faz-se no painel do Stripe.',
      confirmLabel: 'Cancelar encomenda',
      destructive: true,
    })) mudar.mutate(EncomendaEstado.Cancelada);
  };

  return (
    <li className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-semibold">#{e.numero} · {e.referencia}</span>
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${COR[e.estado]}`}>{ENCOMENDA_ESTADO_LABEL[e.estado]}</span>
        <span className="ml-auto font-semibold tabular-nums">{euro(e.total)}</span>
      </div>
      {e.problema && (
        <p className="mt-2 flex items-start gap-2 rounded-lg bg-rose-50 p-2 text-sm text-rose-700"><AlertTriangle size={16} className="mt-0.5 shrink-0" />{e.problema}</p>
      )}
      <div className="mt-3 grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <p className="font-medium">{e.nome}</p>
          <p className="text-zinc-600">{e.telefone} · {e.email}{e.nif ? ` · NIF ${e.nif}` : ''}</p>
          <p className="text-zinc-600">{e.morada}, {e.codigoPostal} {e.localidade}</p>
          {e.notas && <p className="mt-1 italic text-zinc-500">«{e.notas}»</p>}
        </div>
        <ul className="space-y-0.5">
          {e.linhas.map((l) => <li key={l.produtoLojaId}>{l.quantidade}× {l.titulo} <span className="text-zinc-500">{euro(l.precoUnitario)}</span></li>)}
          <li className="text-zinc-500">Envio {e.transportadora}: {euro(e.portes)}</li>
        </ul>
      </div>
      <p className="mt-2 text-xs text-zinc-500">
        Criada {data(e.criadaEm)}{e.pagaEm ? ` · paga ${data(e.pagaEm)}` : ''}{e.enviadaEm ? ` · enviada ${data(e.enviadaEm)}` : ''}{e.entregueEm ? ` · entregue ${data(e.entregueEm)}` : ''}
        {e.vendaId && <> · <Link className="text-brand-600 hover:underline" to={`/vendas/${e.vendaId}`}>ver venda (faturar)</Link></>}
      </p>
      {(e.estado === EncomendaEstado.Paga || e.estado === EncomendaEstado.EmPreparacao || e.estado === EncomendaEstado.Enviada) && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {e.estado === EncomendaEstado.Paga && <Button variant="secondary" loading={mudar.isPending} onClick={() => mudar.mutate(EncomendaEstado.EmPreparacao)}>A preparar</Button>}
          {e.estado !== EncomendaEstado.Enviada ? (
            <>
              <input className="min-h-10 rounded-lg border border-zinc-200 px-3 text-sm dark:border-zinc-700 dark:bg-zinc-950" placeholder="Nº de seguimento (opcional)"
                value={seguimento} onChange={(ev) => setSeguimento(ev.target.value)} aria-label="Número de seguimento" />
              <Button leftIcon={<Truck size={15} />} loading={mudar.isPending} onClick={() => mudar.mutate(EncomendaEstado.Enviada)}>Enviada</Button>
            </>
          ) : (
            <Button leftIcon={<PackageCheck size={15} />} loading={mudar.isPending} onClick={() => mudar.mutate(EncomendaEstado.Entregue)}>Entregue</Button>
          )}
          {e.estado !== EncomendaEstado.Enviada && <Button variant="ghost" onClick={cancelar}>Cancelar</Button>}
        </div>
      )}
    </li>
  );
}
