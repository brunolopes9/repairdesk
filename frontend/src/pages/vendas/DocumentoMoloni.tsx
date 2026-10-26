import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Ban, FileCheck2, FileText, Receipt } from 'lucide-react';
import { Button, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { moloniApi, MOTIVOS_ISENCAO, type MoloniDocumento } from '../../lib/moloni/api';
import { toast } from '../../lib/toast';
import { vendasApi } from '../../lib/vendas/api';
import { PAYMENT_METHOD_LABEL, VENDA_ESTADO, VENDA_TIPO, type PaymentMethod, type Venda } from '../../lib/vendas/types';

interface Props {
  venda: Venda;
  isAdmin: boolean;
  onChange: (v: Venda) => void;
}

/**
 * Sprint 567: documento fiscal da venda no Moloni ON — emitir fatura (por pagar) ou fatura-recibo (paga),
 * registar o pagamento (recibo), anular, e orçamentos. O Moloni é a fonte legal; o Mender guarda o nº e o PDF.
 */
export default function DocumentoMoloni({ venda, isAdmin, onChange }: Props) {
  const estado = useQuery({ queryKey: ['moloni', 'estado'], queryFn: moloniApi.estado, staleTime: 5 * 60_000 });
  const e = estado.data;
  const serieDefault = venda.tipo === VENDA_TIPO.Servico ? e?.serieServico : venda.tipo === VENDA_TIPO.Reparacao ? e?.serieReparacao : e?.serieProduto;
  const temIsentas = venda.items.some((i) => i.ivaRate === 0);

  const [serie, setSerie] = useState<number | ''>('');
  const [pagamento, setPagamento] = useState<PaymentMethod>(venda.paymentMethod);
  const [vencimento, setVencimento] = useState(30);
  const [motivo, setMotivo] = useState<string>('');
  const [anular, setAnular] = useState<string | null>(null);

  useEffect(() => { setSerie(serieDefault ?? ''); }, [serieDefault]);

  const ok = (titulo: string) => (v: Venda) => { onChange(v); toast.success(titulo, v.invoiceNumber ?? v.moloniOrcamentoNumero ?? undefined); };
  const erro = (msg: string) => (err: unknown) => toast.fromError(err, msg);

  const emitir = useMutation({
    mutationFn: (tipo: MoloniDocumento) => moloniApi.emitir(venda.id, {
      tipo,
      serieId: serie === '' ? null : serie,
      paymentMethod: tipo === 'FaturaRecibo' ? pagamento : null,
      vencimentoDias: tipo === 'Fatura' ? vencimento : 0,
      motivoIsencao: motivo || null,
    }),
    onSuccess: ok('Documento emitido no Moloni ON'),
    onError: erro('Não foi possível emitir no Moloni ON.'),
  });
  const recibo = useMutation({
    mutationFn: () => moloniApi.recibo(venda.id, pagamento),
    onSuccess: (v) => { onChange(v); toast.success('Pagamento registado', v.moloniReciboNumero ?? undefined); },
    onError: erro('Não foi possível emitir o recibo.'),
  });
  const anulacao = useMutation({
    mutationFn: (m: string) => moloniApi.anular(venda.id, m),
    onSuccess: (v) => { onChange(v); setAnular(null); toast.success('Documento anulado no Moloni ON', 'A venda voltou a "Fatura por registar".'); },
    onError: erro('Não foi possível anular.'),
  });
  const orcamento = useMutation({
    mutationFn: () => moloniApi.orcamento(venda.id, { serieId: serie === '' ? null : serie, motivoIsencao: motivo || null }),
    onSuccess: (v) => { onChange(v); toast.success('Orçamento emitido no Moloni ON', v.moloniOrcamentoNumero ?? undefined); },
    onError: erro('Não foi possível emitir o orçamento.'),
  });
  const ocupado = emitir.isPending || recibo.isPending || anulacao.isPending || orcamento.isPending;

  if (venda.estado === VENDA_ESTADO.Cancelada) return null;
  if (!e?.configurado) {
    return (
      <SectionCard title="Documento fiscal">
        <p className="text-sm text-zinc-500">
          Para emitir faturas e orçamentos daqui, liga o Moloni ON {isAdmin ? <Link to="/definicoes/moloni" className="text-brand-600 underline">em Definições → Moloni ON</Link> : 'nas Definições (administrador)'}.
        </p>
      </SectionCard>
    );
  }

  const entregue = venda.estado === VENDA_ESTADO.Entregue;
  const comMoloni = venda.moloniDocumentoTipo != null;
  const opcoesSerie = (
    <label className="block">
      <span className={labelCls}>Série</span>
      <select value={serie} onChange={(ev) => setSerie(ev.target.value ? Number(ev.target.value) : '')} className={inputCls}>
        {e.series.map((s) => <option key={s.id} value={s.id}>{s.nome}</option>)}
      </select>
    </label>
  );
  const opcoesIsencao = temIsentas && (
    <label className="block sm:col-span-2">
      <span className={labelCls}>Motivo de isenção (há linhas a 0%)</span>
      <select value={motivo} onChange={(ev) => setMotivo(ev.target.value)} className={inputCls}>
        <option value="">Escolher…</option>
        {MOTIVOS_ISENCAO.map((m) => <option key={m.codigo} value={m.codigo}>{m.label}</option>)}
      </select>
    </label>
  );

  return (
    <SectionCard title="Documento fiscal (Moloni ON)">
      {comMoloni ? (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="font-mono font-semibold">{venda.invoiceNumber}</span>
            {venda.paga
              ? <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">Paga{venda.moloniReciboNumero ? ` · ${venda.moloniReciboNumero}` : ''}</span>
              : <span className="rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">Por pagar</span>}
          </div>
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="secondary" leftIcon={<FileText size={15} />}
              onClick={() => openPdfInNewTab(venda.temFaturaPdf ? vendasApi.faturaPdfPath(venda.id) : moloniApi.pdfPath(venda.id, 'fatura')).catch(erro('PDF ainda não disponível.'))}>
              PDF da fatura
            </Button>
            {venda.moloniReciboNumero && (
              <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(moloniApi.pdfPath(venda.id, 'recibo')).catch(erro('PDF ainda não disponível.'))}>
                PDF do recibo
              </Button>
            )}
          </div>
          {venda.paga === false && (
            <div className="flex flex-wrap items-end gap-2 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
              <label className="block">
                <span className={labelCls}>Pago por</span>
                <select value={pagamento} onChange={(ev) => setPagamento(Number(ev.target.value) as PaymentMethod)} className={`${inputCls} w-auto`}>
                  {Object.entries(PAYMENT_METHOD_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
                </select>
              </label>
              <Button type="button" leftIcon={<Receipt size={15} />} loading={recibo.isPending} disabled={ocupado} onClick={() => recibo.mutate()}>
                Registar pagamento (recibo)
              </Button>
            </div>
          )}
          {isAdmin && (anular === null ? (
            <Button type="button" variant="ghost" leftIcon={<Ban size={15} />} disabled={ocupado} onClick={() => setAnular('')}>Anular documento</Button>
          ) : (
            <div className="space-y-2 rounded-lg border border-rose-300 bg-rose-50 p-3 text-sm dark:border-rose-900/50 dark:bg-rose-950/30">
              <p className="text-rose-900 dark:text-rose-200">
                Anula {venda.moloniReciboNumero ? 'o recibo e depois ' : ''}a fatura no Moloni ON (fica comunicado à AT). A venda fica por faturar — podes emitir outra.
              </p>
              <input value={anular} onChange={(ev) => setAnular(ev.target.value)} className={inputCls} placeholder="Motivo (ex.: emitida com o NIF errado)" autoFocus />
              <div className="flex gap-2">
                <Button type="button" variant="secondary" onClick={() => setAnular(null)}>Voltar</Button>
                <Button type="button" leftIcon={<Ban size={15} />} loading={anulacao.isPending} disabled={anular.trim().length < 5} onClick={() => anulacao.mutate(anular.trim())}>Anular</Button>
              </div>
            </div>
          ))}
        </div>
      ) : entregue && !venda.invoiceNumber ? (
        <div className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-3">
            {opcoesSerie}
            <label className="block">
              <span className={labelCls}>Pago por (fatura-recibo)</span>
              <select value={pagamento} onChange={(ev) => setPagamento(Number(ev.target.value) as PaymentMethod)} className={inputCls}>
                {Object.entries(PAYMENT_METHOD_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>
            <label className="block">
              <span className={labelCls}>Vencimento (fatura)</span>
              <select value={vencimento} onChange={(ev) => setVencimento(Number(ev.target.value))} className={inputCls}>
                <option value={0}>Pronto pagamento</option>
                <option value={30}>30 dias</option>
                <option value={60}>60 dias</option>
              </select>
            </label>
            {opcoesIsencao}
          </div>
          <div className="flex flex-wrap gap-2">
            <Button type="button" leftIcon={<FileCheck2 size={15} />} loading={emitir.isPending && emitir.variables === 'FaturaRecibo'} disabled={ocupado}
              onClick={() => emitir.mutate('FaturaRecibo')}>
              Emitir fatura-recibo (pago)
            </Button>
            <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} loading={emitir.isPending && emitir.variables === 'Fatura'} disabled={ocupado}
              onClick={() => emitir.mutate('Fatura')}>
              Emitir fatura (por pagar)
            </Button>
          </div>
          <p className="text-xs text-zinc-500">O Mender cria primeiro um rascunho no Moloni e só o finaliza se o total bater certo com esta venda.</p>
        </div>
      ) : entregue ? (
        <p className="text-sm text-zinc-500">Fatura {venda.invoiceNumber} registada à mão (emitida fora do Mender).</p>
      ) : (
        <div className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">{opcoesSerie}{opcoesIsencao}</div>
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} loading={orcamento.isPending} disabled={ocupado || venda.items.length === 0}
              onClick={() => orcamento.mutate()}>
              {venda.moloniOrcamentoNumero ? 'Emitir novo orçamento' : 'Emitir orçamento no Moloni'}
            </Button>
            {venda.moloniOrcamentoNumero && (
              <Button type="button" variant="ghost" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(moloniApi.pdfPath(venda.id, 'orcamento')).catch(erro('PDF ainda não disponível.'))}>
                {venda.moloniOrcamentoNumero} (PDF)
              </Button>
            )}
          </div>
          <p className="text-xs text-zinc-500">A fatura emite-se quando a venda estiver entregue.</p>
        </div>
      )}
    </SectionCard>
  );
}
