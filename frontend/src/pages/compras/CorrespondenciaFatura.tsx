import { Link, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Link2 } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';
import { comprasApi } from '../../lib/compras/api';
import { formatEur } from '../../lib/compras/format';
import type { CandidatoCompra, CorrespondenciaFatura, SugestaoFatura } from '../../lib/compras/types';
import { formatDateOnly } from '../../lib/money';
import { toast } from '../../lib/toast';

/**
 * Sprint 560: fatura recebida ↔ compra existente. O servidor decide a sugestão (uma só fonte de
 * verdade); aqui só se mostra e se confirma.
 */
export function useCorrespondencia(importId: string, enabled = true) {
  return useQuery({
    queryKey: ['fatura-correspondencia', importId],
    queryFn: () => comprasApi.correspondencia(importId),
    enabled,
    staleTime: 30_000,
  });
}

const SUGESTAO: Record<SugestaoFatura, { label: string; cls: string }> = {
  associar: { label: 'Bate certo com uma compra', cls: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300' },
  rever: { label: 'Para rever', cls: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300' },
  nova: { label: 'Sem compra — criar nova', cls: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300' },
  duplicada: { label: 'Já registada', cls: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300' },
  ilegivel: { label: 'Leitura incompleta', cls: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300' },
};

const MOTIVO: Record<CandidatoCompra['motivo'], string> = {
  mesma_fatura: 'Mesmo nº de fatura',
  encomenda: 'Mesmo nº de encomenda',
  data: 'Fatura em falta, data próxima',
};

export function CorrespondenciaBadge({ data }: { data: CorrespondenciaFatura | undefined }) {
  if (!data) return <span className="text-xs text-zinc-400">…</span>;
  const s = SUGESTAO[data.sugestao];
  const alvo = data.candidatos.find((c) => c.compraId === data.compraSugeridaId);
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${s.cls}`}>
      {s.label}
      {alvo && data.sugestao !== 'nova' && <> · compra nº {alvo.numero}</>}
    </span>
  );
}

export function CorrespondenciaModal({ importId, onClose }: { importId: string; onClose: () => void }) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const corr = useCorrespondencia(importId);

  const associar = useMutation({
    mutationFn: (compraId: string) => comprasApi.associarFatura(compraId, importId),
    onSuccess: (doc) => {
      qc.invalidateQueries({ queryKey: ['supplier-invoices-pending'] });
      qc.invalidateQueries({ queryKey: ['supplier-invoices-history'] });
      qc.invalidateQueries({ queryKey: ['fatura-correspondencia'] });
      qc.invalidateQueries({ queryKey: ['compras'] });
      if (doc.temDiferenca) {
        toast.warning(`Fatura ligada à compra nº ${doc.numero}`,
          `Ficou uma diferença de ${formatEur(doc.diferenca)} — corrige as linhas da compra com o PDF à frente.`);
        navigate(`/compras/${doc.id}`);
      } else {
        toast.success(`Fatura ligada à compra nº ${doc.numero}`, 'Bate certo ao cêntimo.');
      }
      onClose();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível ligar a fatura.'),
  });

  const f = corr.data?.fatura;
  return (
    <Modal open wide title="Ligar fatura a uma compra" onClose={onClose}>
      {corr.isLoading || !corr.data || !f ? (
        <p className="py-6 text-center text-sm text-zinc-500">{corr.isError ? 'Não foi possível analisar a fatura.' : 'A comparar com as compras…'}</p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
          <section className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-zinc-500">Fatura lida</p>
            <p className="mt-1 font-semibold">{f.fornecedor ?? 'Fornecedor desconhecido'} · {f.numero ?? 'sem nº'}</p>
            <p className="text-xs text-zinc-500">{f.data ? formatDateOnly(f.data) : 'sem data'}</p>
            <ul className="mt-3 space-y-1 text-sm">
              {f.linhas.map((l, i) => (
                <li key={i} className={`flex justify-between gap-2 ${l.portes ? 'text-zinc-500' : ''}`}>
                  <span className="min-w-0">{l.quantidade}× {l.descricao}</span>
                  <span className="whitespace-nowrap tabular-nums">{formatEur(l.total)}</span>
                </li>
              ))}
            </ul>
            <div className="mt-3 flex justify-between border-t border-zinc-100 pt-2 text-sm font-semibold dark:border-zinc-800">
              <span>Total da fatura</span>
              <span className="tabular-nums">{f.total != null ? formatEur(f.total) : '—'}</span>
            </div>
          </section>

          <section className="space-y-3">
            <p className="text-sm text-zinc-600 dark:text-zinc-300">
              {corr.data.sugestao === 'nova' && 'Não há nenhuma compra deste fornecedor que corresponda. Cria uma compra nova a partir desta fatura.'}
              {corr.data.sugestao === 'duplicada' && 'Esta fatura já está registada e tem PDF. Provavelmente é uma segunda cópia — podes rejeitá-la.'}
              {corr.data.sugestao === 'ilegivel' && 'A leitura não tem fornecedor, data ou total. Abre o PDF e cria a compra à mão, ou reprocessa.'}
              {(corr.data.sugestao === 'associar' || corr.data.sugestao === 'rever') &&
                'Ligar a fatura a uma compra põe o nº, a data, os portes e o total da fatura na compra e guarda o PDF. As linhas e o stock não mudam.'}
            </p>
            {corr.data.candidatos.map((c) => (
              <CandidatoCard
                key={c.compraId}
                c={c}
                sugerida={c.compraId === corr.data!.compraSugeridaId}
                loading={associar.isPending && associar.variables === c.compraId}
                disabled={associar.isPending}
                onAssociar={() => associar.mutate(c.compraId)}
              />
            ))}
          </section>
        </div>
      )}
    </Modal>
  );
}

function CandidatoCard({ c, sugerida, loading, disabled, onAssociar }: {
  c: CandidatoCompra; sugerida: boolean; loading: boolean; disabled: boolean; onAssociar: () => void;
}) {
  return (
    <div className={`rounded-lg border p-3 ${sugerida ? 'border-brand-300 bg-brand-50/50 dark:border-brand-800 dark:bg-brand-950/20' : 'border-zinc-200 dark:border-zinc-800'}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <Link to={`/compras/${c.compraId}`} className="font-semibold text-brand-700 hover:underline dark:text-brand-300">
            Compra nº {c.numero} · {c.referencia}
          </Link>
          <p className="text-xs text-zinc-500">{formatDateOnly(c.data)} · {MOTIVO[c.motivo]}</p>
        </div>
        {c.jaDocumentada ? (
          <span className="text-xs text-zinc-500">Já tem fatura</span>
        ) : (
          <Button type="button" size="sm" leftIcon={<Link2 size={14} />} loading={loading} disabled={disabled} onClick={onAssociar}>
            Ligar a esta compra
          </Button>
        )}
      </div>
      <ul className="mt-2 space-y-0.5 text-sm">
        {c.linhas.map((l) => (
          <li key={l.numero} className="flex justify-between gap-2">
            <span className="min-w-0"><span className="mr-1 text-xs text-zinc-400">Lote {l.numero}</span>{l.quantidade}× {l.descricao}</span>
            <span className="whitespace-nowrap tabular-nums">{formatEur(l.totalPago)}</span>
          </li>
        ))}
      </ul>
      <div className={`mt-2 flex items-center gap-1.5 text-xs ${c.bateCerto ? 'text-emerald-700 dark:text-emerald-400' : 'text-amber-700 dark:text-amber-400'}`}>
        {c.bateCerto ? <CheckCircle2 size={14} /> : <AlertTriangle size={14} />}
        {c.bateCerto
          ? 'Linhas da compra + portes da fatura = total da fatura.'
          : `Linhas da compra + portes da fatura dão ${formatEur(Math.abs(c.diferencaComFatura))} ${c.diferencaComFatura > 0 ? 'a mais' : 'a menos'} do que a fatura — confirma as linhas no PDF depois de ligar.`}
      </div>
    </div>
  );
}
