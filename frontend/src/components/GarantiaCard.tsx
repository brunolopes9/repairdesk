import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, ExternalLink, FileDown, ShieldCheck, ShieldOff, ShieldX } from 'lucide-react';
import { openPdfInNewTab } from '../lib/downloadPdf';
import { garantiasApi, type GarantiaAdminDto } from '../lib/garantias/api';
import { toast } from '../lib/toast';
import Modal from './Modal';

type Props =
  | { kind: 'reparacao'; reparacaoId: string }
  | { kind: 'venda'; vendaId: string };

/// Card reutilizável que mostra a garantia digital de uma Reparação ou Venda.
/// Permite copiar o link público e anular com motivo (audit log).
export default function GarantiaCard(props: Props) {
  const qc = useQueryClient();
  const queryKey = props.kind === 'reparacao'
    ? ['garantia-reparacao', props.reparacaoId]
    : ['garantia-venda', props.vendaId];

  const garantia = useQuery({
    queryKey,
    queryFn: () => props.kind === 'reparacao'
      ? garantiasApi.byReparacao(props.reparacaoId)
      : garantiasApi.byVenda(props.vendaId),
  });

  const [anularOpen, setAnularOpen] = useState(false);
  const [motivo, setMotivo] = useState('');

  const anular = useMutation({
    mutationFn: (g: GarantiaAdminDto) => garantiasApi.anular(g.id, motivo),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey });
      qc.invalidateQueries({ queryKey: ['dashboard-garantias-resumo'] });
      toast.success('Garantia anulada');
      setAnularOpen(false);
      setMotivo('');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível anular a garantia.'),
  });

  if (garantia.isLoading) {
    return (
      <div className="rounded-xl border border-zinc-200 bg-white p-4 text-sm text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900">
        A carregar garantia…
      </div>
    );
  }

  const g = garantia.data;
  if (!g) {
    return (
      <div className="rounded-xl border border-dashed border-zinc-300 bg-white p-4 text-sm text-zinc-500 dark:border-zinc-700 dark:bg-zinc-900">
        Sem garantia digital emitida.
      </div>
    );
  }

  const StatusIcon = g.anulada ? ShieldX : g.activa ? ShieldCheck : ShieldOff;
  const statusCls = g.anulada
    ? 'text-rose-600 dark:text-rose-400'
    : g.activa
      ? 'text-emerald-600 dark:text-emerald-400'
      : 'text-zinc-500 dark:text-zinc-400';
  const statusLabel = g.anulada
    ? 'Anulada'
    : g.activa
      ? `Activa · ${g.diasRestantes} ${g.diasRestantes === 1 ? 'dia restante' : 'dias restantes'}`
      : 'Expirada';

  const portalUrl = `${window.location.origin}/g/${g.slug}`;

  return (
    <>
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2 text-[10px] uppercase tracking-wider text-zinc-500">
              <StatusIcon size={12} strokeWidth={2} className={statusCls} />
              Garantia · {g.sourceType === 1 ? 'Venda (DL 84/2021)' : 'Reparação'}
            </div>
            <div className={`mt-1 inline-flex items-center gap-1.5 text-sm font-medium ${statusCls}`}>
              {g.activa && !g.anulada && <Check size={14} strokeWidth={2.5} />}
              {statusLabel}
            </div>
            <div className="mt-2 grid grid-cols-2 gap-2 text-xs text-zinc-600 dark:text-zinc-400">
              <div>
                <div className="text-[10px] uppercase">Início</div>
                <div className="font-medium text-zinc-900 dark:text-zinc-100">
                  {new Date(g.dataInicio).toLocaleDateString('pt-PT')}
                </div>
              </div>
              <div>
                <div className="text-[10px] uppercase">Fim</div>
                <div className="font-medium text-zinc-900 dark:text-zinc-100">
                  {new Date(g.dataFim).toLocaleDateString('pt-PT')}
                </div>
              </div>
            </div>
          </div>
          <div className="flex flex-col items-end gap-1">
            <a
              href={`/g/${g.slug}`}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1 text-xs text-brand-600 hover:underline"
            >
              Portal <ExternalLink size={11} strokeWidth={2} />
            </a>
            <button
              type="button"
              onClick={() => openPdfInNewTab(garantiasApi.pdfUrl(g.id))}
              className="inline-flex items-center gap-1 text-xs text-brand-600 hover:underline"
              title="Abrir PDF imprimível com QR code para o portal"
            >
              <FileDown size={11} strokeWidth={2} /> PDF
            </button>
            <button
              type="button"
              onClick={() => navigator.clipboard.writeText(portalUrl).then(() => toast.success('Link copiado'))}
              className="text-[10px] text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
            >
              copiar link
            </button>
            {!g.anulada && (
              <button
                type="button"
                onClick={() => setAnularOpen(true)}
                className="text-[10px] text-rose-600 hover:underline dark:text-rose-400"
              >
                anular
              </button>
            )}
          </div>
        </div>
        {g.anulada && g.motivoAnulacao && (
          <div className="mt-3 rounded-md bg-rose-50 p-2 text-[11px] text-rose-800 dark:bg-rose-950/30 dark:text-rose-200">
            <strong>Motivo:</strong> {g.motivoAnulacao}
          </div>
        )}
      </section>

      <Modal
        open={anularOpen}
        title="Anular garantia"
        onClose={() => { if (!anular.isPending) setAnularOpen(false); }}
        footer={<>
          <button
            type="button"
            disabled={anular.isPending}
            onClick={() => { setAnularOpen(false); setMotivo(''); }}
            className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 disabled:opacity-60 dark:text-zinc-300"
          >
            Cancelar
          </button>
          <button
            type="button"
            disabled={!motivo.trim() || anular.isPending}
            onClick={() => anular.mutate(g)}
            className="rounded-md bg-rose-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {anular.isPending ? 'A anular…' : 'Anular garantia'}
          </button>
        </>}
      >
        <div className="space-y-3 text-sm">
          <p className="text-zinc-600 dark:text-zinc-300">
            A anulação fica registada no audit log. Indica o motivo (ex: cliente abriu equipamento, dano por água, etc.).
          </p>
          <label className="block">
            <span className="text-xs font-medium text-zinc-600 dark:text-zinc-300">Motivo *</span>
            <textarea
              value={motivo}
              onChange={(e) => setMotivo(e.target.value)}
              rows={3}
              maxLength={500}
              className="mt-1 block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-rose-500 focus:ring-2 focus:ring-rose-200 dark:border-zinc-700 dark:bg-zinc-950"
              placeholder="Ex: Cliente abriu o equipamento — violação dos termos da garantia."
            />
          </label>
        </div>
      </Modal>
    </>
  );
}
