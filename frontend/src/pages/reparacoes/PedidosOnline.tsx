import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRight, X, Phone, Mail } from 'lucide-react';
import { repairRequestsApi, REPAIR_REQUEST_ESTADO, type RepairRequestEstado } from '../../lib/repairRequests/api';
import { toast } from '../../lib/toast';
import { useConfirm } from '../../components/ConfirmDialog';
import { formatDate } from '../../lib/money';
import { liveListOptions } from '../../lib/queryOptions';

/**
 * Sprint 354 (Doc 83 Pillar 9): backoffice dos pedidos de reparação submetidos
 * via widget público. Converter cria a reparação (lookup-or-create cliente).
 */
export default function PedidosOnline() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const confirm = useConfirm();
  const [filtro, setFiltro] = useState<RepairRequestEstado>(REPAIR_REQUEST_ESTADO.Pendente);

  const list = useQuery({
    queryKey: ['repair-requests', filtro],
    queryFn: () => repairRequestsApi.list(filtro),
    ...liveListOptions,
  });

  const converterMut = useMutation({
    mutationFn: (id: string) => repairRequestsApi.converter(id),
    onSuccess: (req) => {
      toast.success('Pedido convertido em reparação.');
      qc.invalidateQueries({ queryKey: ['repair-requests'] });
      qc.invalidateQueries({ queryKey: ['repair-requests-count'] });
      if (req.reparacaoId) navigate(`/reparacoes/${req.reparacaoId}`);
    },
    onError: (err) => toast.fromError(err, 'Erro a converter pedido.'),
  });

  const rejeitarMut = useMutation({
    mutationFn: (id: string) => repairRequestsApi.rejeitar(id),
    onSuccess: () => {
      toast.success('Pedido rejeitado.');
      qc.invalidateQueries({ queryKey: ['repair-requests'] });
      qc.invalidateQueries({ queryKey: ['repair-requests-count'] });
    },
    onError: (err) => toast.fromError(err, 'Erro a rejeitar pedido.'),
  });

  async function askRejeitar(id: string) {
    const ok = await confirm({
      title: 'Rejeitar pedido',
      description: 'Marcar este pedido como rejeitado? Não cria reparação.',
      confirmLabel: 'Rejeitar',
      destructive: true,
    });
    if (ok) rejeitarMut.mutate(id);
  }

  const tabs: { label: string; value: RepairRequestEstado }[] = [
    { label: 'Pendentes', value: REPAIR_REQUEST_ESTADO.Pendente },
    { label: 'Convertidos', value: REPAIR_REQUEST_ESTADO.Convertido },
    { label: 'Rejeitados', value: REPAIR_REQUEST_ESTADO.Rejeitado },
  ];

  return (
    <div className="space-y-4">
      <header>
        <h1 className="text-xl font-semibold">Pedidos online</h1>
        <p className="text-sm text-zinc-500">Pedidos de reparação submetidos pelos clientes através do widget no website.</p>
      </header>

      <div className="flex gap-1 border-b border-zinc-200 dark:border-zinc-800">
        {tabs.map((t) => (
          <button
            key={t.value} type="button" onClick={() => setFiltro(t.value)}
            className={`px-3 py-1.5 text-sm ${filtro === t.value ? 'border-b-2 border-brand-600 font-medium text-brand-700 dark:text-brand-400' : 'text-zinc-500'}`}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="grid gap-2">
        {list.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
        {list.data?.length === 0 && <p className="text-sm text-zinc-500">Sem pedidos nesta categoria.</p>}
        {(list.data ?? []).map((r) => (
          <div key={r.id} className="rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-700 dark:bg-zinc-900">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="font-medium">{r.nome} · <span className="font-normal text-zinc-600 dark:text-zinc-400">{r.equipamento}</span></div>
                <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-[11px] text-zinc-500">
                  {r.telefone && <span className="inline-flex items-center gap-1"><Phone size={10} /> {r.telefone}</span>}
                  {r.email && <span className="inline-flex items-center gap-1"><Mail size={10} /> {r.email}</span>}
                  <span>{formatDate(r.createdAt)}</span>
                </div>
                <p className="mt-1.5 whitespace-pre-line text-sm text-zinc-700 dark:text-zinc-300">{r.descricao}</p>
                {r.motivoRejeicao && <p className="mt-1 text-xs italic text-rose-600">Rejeitado: {r.motivoRejeicao}</p>}
              </div>
              {r.estado === REPAIR_REQUEST_ESTADO.Pendente && (
                <div className="flex shrink-0 flex-col gap-1">
                  <button
                    type="button" disabled={converterMut.isPending}
                    onClick={() => converterMut.mutate(r.id)}
                    className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-2.5 py-1.5 text-xs font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                  >
                    Converter <ArrowRight size={12} />
                  </button>
                  <button
                    type="button" onClick={() => askRejeitar(r.id)}
                    className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 px-2.5 py-1.5 text-xs text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-400 dark:hover:bg-zinc-800"
                  >
                    <X size={12} /> Rejeitar
                  </button>
                </div>
              )}
              {r.estado === REPAIR_REQUEST_ESTADO.Convertido && r.reparacaoId && (
                <button
                  type="button" onClick={() => navigate(`/reparacoes/${r.reparacaoId}`)}
                  className="shrink-0 text-xs text-brand-600 hover:underline"
                >
                  Ver reparação →
                </button>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
