import { formatEur } from '../../lib/compras/format';
import { PagamentoPicker } from '../../components/PagamentoPicker';
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
import DadosFatura, { escolhaInicial, pedidoFatura, problemaFatura, type EscolhaFatura } from './DadosFatura';

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
  // Sprint 683: o pagamento escolhido em «Entregue & paga» já vem daqui — não se escolhe duas vezes.
  const [mudarPagamento, setMudarPagamento] = useState(false);
  useEffect(() => { setPagamento(venda.paymentMethod); }, [venda.paymentMethod]);
  const pagamentoDaVenda = venda.estado === VENDA_ESTADO.Entregue && !venda.porReceber;
  const [vencimento, setVencimento] = useState(0); // 18/11 (Bruno): pronto pagamento é a realidade
  const [outras, setOutras] = useState(false);
  const [motivo, setMotivo] = useState<string>('');
  const [anular, setAnular] = useState<string | null>(null);
  const [mudarSerie, setMudarSerie] = useState(false);

  // Sprint 587: uma série que não está ativa na AT não emite — começa numa que emite.
  const serieInicial = e?.series.some((s) => s.id === serieDefault && s.ativaNaAt === false)
    ? (e.series.find((s) => s.ativaNaAt !== false)?.id ?? serieDefault)
    : serieDefault;
  useEffect(() => { setSerie(serieInicial ?? ''); }, [serieInicial]);
  // Sprint 571: sem NIF / com NIF — proposto a partir do cliente da venda, decidido no momento.
  const [fatura, setFatura] = useState<EscolhaFatura>(() => escolhaInicial(venda.cliente));
  const chaveCliente = `${venda.cliente?.id ?? ""}|${venda.cliente?.nif ?? ""}`;
  // eslint-disable-next-line react-hooks/exhaustive-deps -- só volta a propor quando muda o cliente (não a cada refetch)
  useEffect(() => { setFatura(escolhaInicial(venda.cliente)); }, [chaveCliente]);
  const problema = problemaFatura(fatura);
  // Sprint 686: fatura simplificada (preços c/ IVA) — CIVA art. 40.º: bens até 1000 € s/ IVA; com serviços até 100 €.
  const faturaveis = venda.items.filter((i) => !i.porFora);
  const baseSemIva = faturaveis.reduce((s, i) => s + i.totalCents / 100 / (1 + i.ivaRate / 100), 0);
  // CIVA art. 40.º: serviços 100 €, bens a retalho 1000 € (sem IVA). Reparação/Serviço = prestação de serviços.
  const limiteFs = venda.tipo !== VENDA_TIPO.Produto || faturaveis.some((i) => !i.compraLinhaId) ? 100 : 1000;
  const fsPossivel = faturaveis.length > 0 && baseSemIva <= limiteFs;

  const ok = (titulo: string) => (v: Venda) => { onChange(v); toast.success(titulo, v.invoiceNumber ?? v.moloniOrcamentoNumero ?? undefined); };
  const erro = (msg: string) => (err: unknown) => toast.fromError(err, msg);

  const emitir = useMutation({
    mutationFn: (tipo: MoloniDocumento) => moloniApi.emitir(venda.id, {
      tipo,
      serieId: serie === '' ? null : serie,
      paymentMethod: tipo === 'FaturaRecibo' || tipo === 'FaturaSimplificada' ? pagamento : null,
      vencimentoDias: tipo === 'Fatura' ? vencimento : 0,
      motivoIsencao: motivo || null,
      ...pedidoFatura(fatura),
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
      <SectionCard title="Documento fiscal" collapsible defaultOpen={false}>
        <p className="text-sm text-zinc-500">
          Para emitir faturas e orçamentos daqui, liga o Moloni ON {isAdmin ? <Link to="/definicoes/moloni" className="text-brand-600 underline">em Definições → Moloni ON</Link> : 'nas Definições (administrador)'}.
        </p>
      </SectionCard>
    );
  }

  const entregue = venda.estado === VENDA_ESTADO.Entregue;
  const comMoloni = venda.moloniDocumentoTipo != null;
  const serieDoTipo = e.series.find((s) => s.id === serieDefault && s.ativaNaAt !== false);
  const opcoesSerie = serieDoTipo && !mudarSerie ? (
    <p className="text-sm text-zinc-600 dark:text-zinc-400">
      Série <strong>{serieDoTipo.nome}</strong> (pelo tipo da venda) ·{' '}
      <button type="button" className="text-brand-600 underline" onClick={() => setMudarSerie(true)}>mudar</button>
    </p>
  ) : (
    <label className="block">
      <span className={labelCls}>Série</span>
      <select value={serie} onChange={(ev) => setSerie(ev.target.value ? Number(ev.target.value) : '')} className={inputCls}>
        {e.series.map((s) => (
          <option key={s.id} value={s.id} disabled={s.ativaNaAt === false}>
            {s.nome}{s.ativaNaAt === false ? ' — não comunicada à AT' : ''}
          </option>
        ))}
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
    <SectionCard title={comMoloni ? `Documento fiscal · ${venda.invoiceNumber ?? 'emitido'}` : 'Documento fiscal (Moloni ON)'} collapsible defaultOpen={comMoloni}>
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
                <PagamentoPicker compacto value={pagamento} onChange={setPagamento} />
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
          <DadosFatura value={fatura} onChange={setFatura} nomeInterno={venda.cliente?.nome} />
          <div className="grid gap-3 sm:grid-cols-3">
            {opcoesSerie}
            <div className="sm:col-span-3">
              <span className={labelCls}>Pago com (fatura-recibo)</span>
              {pagamentoDaVenda && !mudarPagamento ? (
                <p className="text-sm">
                  <strong>{PAYMENT_METHOD_LABEL[pagamento]}</strong> <span className="text-zinc-500">— o que escolheste ao entregar.</span>{' '}
                  <button type="button" className="text-xs text-brand-700 underline dark:text-brand-300" onClick={() => setMudarPagamento(true)}>mudar</button>
                </p>
              ) : (
                <PagamentoPicker compacto value={pagamento} onChange={setPagamento} />
              )}
            </div>
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
          {/* 18/11 (Bruno): o Mender escolhe o documento — paga na hora → fatura simplificada até ao limite legal, acima dele fatura-recibo. */}
          <div className="space-y-2">
            <p className="text-xs text-zinc-600 dark:text-zinc-400">
              {fsPossivel
                ? <>Recomendado: <strong>fatura simplificada</strong> — {formatEur(baseSemIva)} sem IVA está dentro do limite de {formatEur(limiteFs)} ({limiteFs === 100 ? 'prestação de serviços' : 'venda de bens'}).</>
                : <>Recomendado: <strong>fatura-recibo</strong> — {formatEur(baseSemIva)} sem IVA passa o limite de {formatEur(limiteFs)} da fatura simplificada.</>}
            </p>
            <Button type="button" variant="success" leftIcon={<FileCheck2 size={15} />} disabled={ocupado || !!problema}
              loading={emitir.isPending && emitir.variables === (fsPossivel ? 'FaturaSimplificada' : 'FaturaRecibo')}
              onClick={() => emitir.mutate(fsPossivel ? 'FaturaSimplificada' : 'FaturaRecibo')}
              title={fsPossivel ? 'Fatura simplificada: cada linha aparece com o IVA incluído, paga na hora' : 'Fatura-recibo: fatura e recibo num só documento, paga na hora'}>
              {fsPossivel ? 'Emitir fatura simplificada (paga)' : 'Emitir fatura-recibo (paga)'}
            </Button>
            <button type="button" className="block text-xs font-medium text-brand-600 hover:underline dark:text-brand-300" onClick={() => setOutras((v) => !v)} aria-expanded={outras}>
              {outras ? 'Esconder outros documentos' : 'Outro documento…'}
            </button>
            {outras && (
              <div className="flex flex-wrap gap-2">
                {fsPossivel && (
                  <Button type="button" variant="secondary" leftIcon={<FileCheck2 size={15} />} loading={emitir.isPending && emitir.variables === 'FaturaRecibo'} disabled={ocupado || !!problema}
                    onClick={() => emitir.mutate('FaturaRecibo')}>
                    Emitir fatura-recibo (paga)
                  </Button>
                )}
                <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} loading={emitir.isPending && emitir.variables === 'Fatura'} disabled={ocupado || !!problema}
                  onClick={() => emitir.mutate('Fatura')}>
                  Emitir fatura (por pagar)
                </Button>
              </div>
            )}
          </div>
          {problema && <p className="text-xs font-medium text-amber-700 dark:text-amber-400">{problema}</p>}
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
