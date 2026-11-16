import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Banknote, Boxes, CheckCircle2, FileText, PackageSearch, Plus, Save, Trash2, Wrench, XCircle } from 'lucide-react';
import { AutosaveStatus, BackButton, Button, PageHeader, SectionCard, SkeletonCard } from '../../components/ui';
import { useAutosave } from '../../lib/useAutosave';
import { useConfirm } from '../../components/ConfirmDialog';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { useAuth } from '../../lib/auth/AuthContext';
import { apiErrorMessage } from '../../lib/errors';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { toast } from '../../lib/toast';
import { formatEur, parseDecimal, previewVenda } from '../../lib/compras/format';
import type { InventarioLinha } from '../../lib/compras/types';
import { comprasApi } from '../../lib/compras/api';
import { vendasApi } from '../../lib/vendas/api';
import {
  PAYMENT_METHOD,
  PAYMENT_METHOD_LABEL,
  PECA_ENCOMENDA,
  PECA_ENCOMENDA_LABEL,
  type PecaEncomendaEstado,
  VENDA_ESTADO,
  VENDA_ESTADO_LABEL,
  VENDA_FLUXO,
  VENDA_TIPO,
  VENDA_TIPO_LABEL,
  type PaymentMethod,
  type Venda,
  type VendaEstado,
  type VendaTipo,
  type VendaWrite,
  estadoVendaVisivel,
} from '../../lib/vendas/types';
import ClienteSelect, { type ClienteEscolhido } from './ClienteSelect';
import DocumentoMoloni from './DocumentoMoloni';
import LotePicker from './LotePicker';
import ReparacaoExtras from './ReparacaoExtras';

const IVA_NORMAL = 23;

interface LinhaForm {
  key: string;
  id: string | null;
  compraLinhaId: string | null;
  descricao: string;
  quantidade: string;
  preco: string;
  iva: string;
  /** Snapshot (linhas gravadas) ou valores do lote escolhido — só para as contas na UI. */
  custoUnit: number;
  taxaCompra: number;
  maxStock: number | null;
  /** Sprint 594: peça que ainda não há (por encomendar/encomendada) — custo estimado c/ IVA e nota. */
  encomenda: PecaEncomendaEstado | null;
  custo: string;
  nota: string;
  /** Sprint 642: recebido por fora da fatura (sem IVA) e custo sem fatura (transporte, quem fez o trabalho…). */
  porFora: boolean;
  custoSemFatura: string;
  /** Sprint 680: preço unitário a FATURAR (vazio = fatura o preço todo); a diferença vai por fora sozinha. */
  faturar?: string;
}

/** Sprint 680: preço cobrado e faturado por unidade (c/ IVA) e quanto fica por fora na linha. */
function partesLinha(l: LinhaForm, semIva: boolean) {
  const qtd = Number(l.quantidade) || 0;
  const cobrado = l.porFora ? parseDecimal(l.preco) ?? 0 : precoComIva(l.preco, l.iva, semIva);
  const fat = !l.porFora && (l.faturar ?? '').trim() ? precoComIva(l.faturar!, l.iva, semIva) : cobrado;
  const faturado = Math.min(fat, cobrado);
  const porForaCents = l.porFora ? 0 : Math.max(0, Math.round(cobrado * 100) * qtd - Math.round(faturado * 100) * qtd);
  return { qtd, cobrado, faturado, porForaCents, faturarInvalido: fat > cobrado + 0.0001 };
}

const num = (cents: number) => (cents / 100).toFixed(2).replace('.', ',');
const fmt2 = (v: number) => (Math.round(v * 100) / 100).toFixed(2).replace('.', ',');

/**
 * Preço gravado é sempre c/ IVA (o que o cliente paga). Em modo "sem IVA" o utilizador escreve o valor
 * que é dele e o IVA é somado por cima — 300 € s/ IVA a 23% = 369 € a cobrar, 69 € para o Estado.
 */
function precoComIva(preco: string, ivaPct: string, semIva: boolean): number {
  const v = parseDecimal(preco) ?? 0;
  return semIva ? Math.round(v * (1 + (parseDecimal(ivaPct) ?? IVA_NORMAL) / 100) * 100) / 100 : v;
}

/** Linhas gravadas (preço c/ IVA) mostradas no modo escolhido. */
function linhasNoModo(v: Venda, semIva: boolean): LinhaForm[] {
  return linhasFrom(v).map((l) => {
    if (!semIva || l.porFora) return l;
    const f = 1 + (parseDecimal(l.iva) ?? IVA_NORMAL) / 100;
    return { ...l, preco: fmt2((parseDecimal(l.preco) ?? 0) / f), faturar: l.faturar ? fmt2((parseDecimal(l.faturar) ?? 0) / f) : '' };
  });
}

/** Serviços (sites, avenças) cotam-se sem IVA; balcão e reparações com IVA. */
const semIvaPorDefeito = (t: VendaTipo) => t === VENDA_TIPO.Servico;

function linhasFrom(v: Venda): LinhaForm[] {
  return v.items.map((i) => ({
    key: i.id,
    id: i.id,
    compraLinhaId: i.compraLinhaId,
    descricao: i.descricao,
    quantidade: String(i.quantidade),
    // Sprint 680: o preço mostrado é o cobrado; o faturado aparece em «Faturar a».
    preco: num(i.precoUnitarioCents + Math.round((i.porForaCents ?? 0) / Math.max(1, i.quantidade))),
    faturar: (i.porForaCents ?? 0) > 0 ? num(i.precoUnitarioCents) : '',
    iva: String(i.ivaRate),
    custoUnit: i.custoUnitarioPago ?? 0,
    taxaCompra: i.taxaIvaCompra ?? 0,
    maxStock: null,
    encomenda: i.encomenda ?? null,
    custo: i.encomenda != null && i.custoUnitarioPago != null ? fmt2(i.custoUnitarioPago) : '',
    nota: i.encomendaNota ?? '',
    porFora: i.porFora ?? false,
    custoSemFatura: !i.compraLinhaId && i.encomenda == null && i.taxaIvaCompra === 0 && i.custoUnitarioPago != null
      ? fmt2(i.custoUnitarioPago * i.quantidade) : '',
  }));
}


export default function VendaEditor() {
  const { id } = useParams<{ id: string }>();
  const isNew = !id;
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const isAdmin = useAuth().hasRole('Admin');

  const existente = useQuery({ queryKey: ['venda', id], queryFn: () => vendasApi.get(id!), enabled: !isNew });
  const venda = existente.data;

  const tipoInicial = Number(params.get('tipo') ?? VENDA_TIPO.Reparacao) as VendaTipo;
  const [tipo, setTipo] = useState<VendaTipo>(tipoInicial in VENDA_TIPO_LABEL ? tipoInicial : VENDA_TIPO.Reparacao);
  const [cliente, setCliente] = useState<ClienteEscolhido | null>(null);
  const [equipamento, setEquipamento] = useState('');
  const [problema, setProblema] = useState('');
  const [notas, setNotas] = useState('');
  const [previsto, setPrevisto] = useState('');
  // Sprint 679: adiantamento para encomendar a peça (€, texto do campo).
  const [adiant, setAdiant] = useState('');
  const [adiantFatura, setAdiantFatura] = useState('');
  const [linhas, setLinhas] = useState<LinhaForm[]>([]);
  const [semIva, setSemIva] = useState(() => semIvaPorDefeito(tipoInicial));
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pagamento, setPagamento] = useState<PaymentMethod>(PAYMENT_METHOD.MBWay);
  const [fatura, setFatura] = useState('');
  const [faturaData, setFaturaData] = useState(new Date().toISOString().slice(0, 10));
  const [confirmCancel, setConfirmCancel] = useState(false);

  // Sprint 675: a venda grava sozinha. `ignorarSync` evita que a resposta do autosave reescreva o que se está a
  // escrever; `rebase` marca o que veio do servidor como «já gravado» depois de o formulário o mostrar.
  const ignorarSync = useRef(false);
  const [rebase, setRebase] = useState(0);
  const idsPorKey = useRef(new Map<string, string>());

  useEffect(() => {
    if (!venda) return;
    if (ignorarSync.current) { ignorarSync.current = false; return; }
    idsPorKey.current = new Map(venda.items.map((i) => [i.id, i.id]));
    setRebase((n) => n + 1);
    setTipo(venda.tipo);
    const modoSemIva = semIvaPorDefeito(venda.tipo);
    setSemIva(modoSemIva);
    setCliente(venda.cliente ? { id: venda.cliente.id, nome: venda.cliente.nome } : null);
    setEquipamento(venda.equipamento ?? '');
    setProblema(venda.problema ?? '');
    setNotas(venda.notas ?? '');
    setPrevisto(venda.previstoPara ? venda.previstoPara.slice(0, 10) : '');
    setAdiant(venda.adiantamentoCents ? (venda.adiantamentoCents / 100).toFixed(2).replace('.', ',') : '');
    setLinhas(linhasNoModo(venda, modoSemIva));
    setFatura(venda.invoiceNumber ?? '');
    if (venda.invoiceEmittedAt) setFaturaData(venda.invoiceEmittedAt.slice(0, 10));
  }, [venda]);

  // Sprint 586: "Vender" numa compra → /vendas/nova?lotes=<linhaId>:<qtd>,… — as linhas entram já preenchidas.
  const lotesPedidos = isNew ? params.get('lotes') : null;
  const inventario = useQuery({ queryKey: ['compras', 'inventario'], queryFn: comprasApi.inventario, enabled: !!lotesPedidos });
  const lotesCarregados = useRef(false);
  const [vindoDaCompra, setVindoDaCompra] = useState(0);
  useEffect(() => {
    if (!lotesPedidos || !inventario.data || lotesCarregados.current) return;
    lotesCarregados.current = true;
    const porId = new Map(inventario.data.map((l) => [l.linhaId, l]));
    let n = 0;
    for (const par of lotesPedidos.split(',')) {
      // Sprint 590: <linhaId>:<qtd>[:<preço c/ IVA em cêntimos>] — o preço que se acertou na compra com o lucro.
      const [linhaId, q, cents] = par.split(':');
      const lote = porId.get(linhaId);
      if (!lote) continue;
      const preco = Number(cents) > 0 ? Number(cents) / 100 : undefined;
      addLote(lote, Math.min(Math.max(1, Number(q) || 1), lote.quantidadeEmStock), preco);
      n++;
    }
    setVindoDaCompra(n);
    if (n === 0) toast.error('Sem stock para vender', 'As linhas desta compra já não estão em stock.');
  // eslint-disable-next-line react-hooks/exhaustive-deps -- corre uma vez, quando o inventário chega
  }, [lotesPedidos, inventario.data]);

  const fechada = venda ? venda.estado === VENDA_ESTADO.Entregue || venda.estado === VENDA_ESTADO.Cancelada : false;

  const contas = useMemo(() => {
    const porLinha = linhas.map((l) => {
      // Sprint 642: por fora não tem IVA — o valor escrito é o que se recebe.
      // Sprint 680: «Faturar a» — o IVA e o custo vão no faturado; a diferença entra inteira no lucro.
      const p = partesLinha(l, semIva);
      const qtd = p.qtd;
      const total = qtd * p.cobrado;
      const faturadoTotal = qtd * p.faturado;
      const pfLinha = p.porForaCents / 100;
      // Linha de serviço: o único custo é o «custo s/ fatura» (vazio = 0) — apagar o campo tem de se ver logo no lucro.
      const servico = !l.compraLinhaId && l.encomenda == null;
      const [custo, taxa] = l.encomenda != null ? [parseDecimal(l.custo) ?? 0, IVA_NORMAL / 100]
        : servico ? [(parseDecimal(l.custoSemFatura) ?? 0) / Math.max(1, qtd), 0] : [l.custoUnit, l.taxaCompra];
      const ivaVenda = l.porFora || venda?.foraApuramento ? 0 : (parseDecimal(l.iva) ?? IVA_NORMAL) / 100;
      const pv = previewVenda(qtd, l.porFora ? total : faturadoTotal, custo, l.porFora ? 0 : taxa, ivaVenda);
      return { total, porFora: l.porFora, pfLinha, peca: !servico, ...pv, lucro: pv.lucro + pfLinha };
    });
    const soma = (k: 'total' | 'ivaVenda' | 'ivaAPagar' | 'lucro') => porLinha.reduce((s, c) => s + c[k], 0);
    const porFora = porLinha.filter((c) => c.porFora).reduce((s, c) => s + c.total, 0) + porLinha.reduce((s, c) => s + c.pfLinha, 0);
    const custoPecas = porLinha.filter((c) => c.peca).reduce((s, c) => s + c.custo, 0);
    return { porLinha, custoPecas, total: soma('total'), porFora, ivaVenda: soma('ivaVenda'), ivaAPagar: soma('ivaAPagar'), lucro: soma('lucro') };
  }, [linhas, semIva, venda?.foraApuramento]);

  const reservado = useMemo(() => {
    const r: Record<string, number> = {};
    // Linhas novas ainda não gravadas contam contra o stock mostrado no seletor.
    for (const l of linhas) if (l.compraLinhaId && !l.id) r[l.compraLinhaId] = (r[l.compraLinhaId] ?? 0) + (Number(l.quantidade) || 0);
    return r;
  }, [linhas]);

  function buildRequest(estado?: VendaEstado): VendaWrite {
    return {
      tipo,
      clienteId: cliente?.id ?? null,
      equipamento: equipamento.trim() || null,
      problema: problema.trim() || null,
      notas: notas.trim() || null,
      previstoPara: previsto ? new Date(`${previsto}T18:00:00`).toISOString() : null,
      adiantamentoCents: adiant.trim() ? Math.max(0, Math.round((parseDecimal(adiant) ?? 0) * 100)) : 0,
      linhas: linhas.map((l) => ({
        id: l.id,
        compraLinhaId: l.compraLinhaId,
        descricao: l.descricao.trim() || null,
        quantidade: Number(l.quantidade) || 0,
        precoUnitarioCents: Math.round(partesLinha(l, semIva).faturado * 100),
        porForaCents: partesLinha(l, semIva).porForaCents,
        ivaRate: l.porFora ? 0 : parseDecimal(l.iva) ?? IVA_NORMAL,
        porFora: l.porFora,
        custoSemFaturaCents: !l.compraLinhaId && l.encomenda == null && l.custoSemFatura.trim() !== ''
          ? Math.round((parseDecimal(l.custoSemFatura) ?? 0) * 100) : null,
        ...(l.encomenda != null && !l.compraLinhaId ? {
          encomenda: l.encomenda,
          custoEstimadoCents: Math.round((parseDecimal(l.custo) ?? 0) * 100),
          encomendaNota: l.nota.trim() || null,
        } : {}),
      })),
      ...(isNew ? { estado, paymentMethod: estado === VENDA_ESTADO.Entregue ? pagamento : null } : {}),
    };
  }

  function onSaved(v: Venda, msg: string) {
    qc.invalidateQueries({ queryKey: ['vendas'] });
    qc.invalidateQueries({ queryKey: ['compras'] });
    qc.setQueryData(['venda', v.id], v);
    toast.success(msg, `Venda #${v.numero} · ${formatEur(v.totalCents / 100)}`);
    if (isNew) navigate(`/vendas/${v.id}`, { replace: true });
  }

  // Sprint 675: autosave da venda já criada. Leva as chaves das linhas para dar o id certo às linhas novas,
  // mesmo que uma gravação fique em espera enquanto outra corre (sem linhas repetidas).
  const valida = !(tipo === VENDA_TIPO.Reparacao && !cliente)
    && linhas.every((l) => (l.compraLinhaId || l.descricao.trim()) && Number(l.quantidade) >= 1 && parseDecimal(l.preco) != null);
  const pedidoAuto = { req: isNew ? null : buildRequest(), keys: linhas.map((l) => l.key) };
  const auto = useAutosave(pedidoAuto, async ({ req, keys }) => {
    if (!req || !id) return;
    const linhasReq = req.linhas.map((l, i) => ({ ...l, id: l.id ?? idsPorKey.current.get(keys[i]) ?? null }));
    const v = await vendasApi.update(id, { ...req, linhas: linhasReq });
    const conhecidos = new Set(linhasReq.map((l) => l.id).filter(Boolean));
    const novos = v.items.filter((i) => !conhecidos.has(i.id));
    let k = 0;
    linhasReq.forEach((l, i) => { if (!l.id && novos[k]) idsPorKey.current.set(keys[i], novos[k++].id); });
    setLinhas((ls) => ls.map((l) => (l.id ? l : { ...l, id: idsPorKey.current.get(l.key) ?? null })));
    ignorarSync.current = true;
    qc.setQueryData(['venda', v.id], v);
    void qc.invalidateQueries({ queryKey: ['vendas'] });
    void qc.invalidateQueries({ queryKey: ['compras'] });
  }, { ativo: !isNew && rebase > 0 && !fechada && valida, atraso: 900 });
  useEffect(() => {
    if (rebase > 0) auto.reiniciar({ req: buildRequest(), keys: linhas.map((l) => l.key) });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rebase]);

  const guardar = useMutation({
    mutationFn: (estado?: VendaEstado) => (isNew ? vendasApi.create(buildRequest(estado)) : vendasApi.update(id!, buildRequest())),
    onSuccess: (v) => onSaved(v, isNew ? 'Venda criada' : 'Venda guardada'),
    onError: (err) => toast.fromError(err),
  });

  const confirm = useConfirm();
  /** Sprint 662: passar a Pronta/Em curso sem nada a cobrar quase sempre é engano — pergunta antes. */
  async function mudarComAviso(e: VendaEstado) {
    if (e !== VENDA_ESTADO.Orcamento && e !== VENDA_ESTADO.Cancelada && contas.total <= 0 && !(await confirm({
      title: `Passar a «${VENDA_ESTADO_LABEL[e]}» com 0 €?`,
      description: 'Ainda não há nada a cobrar nesta venda. Junta primeiro o valor (ex.: botão «Por fora» ou «Mão de obra»). Pronta não fecha nem cobra nada — só Entregue fecha.',
      confirmLabel: 'Mudar mesmo assim',
    }))) return;
    mudarEstado.mutate(e);
  }

  const mudarEstado = useMutation({
    mutationFn: async (estado: VendaEstado) => {
      // Grava alterações pendentes antes de mudar de estado (exceto ao cancelar).
      if (!fechada && estado !== VENDA_ESTADO.Cancelada) await auto.gravarJa();
      return vendasApi.mudarEstado(id!, estado, estado === VENDA_ESTADO.Entregue ? pagamento : undefined);
    },
    onSuccess: (v) => { setConfirmCancel(false); onSaved(v, VENDA_ESTADO_LABEL[v.estado]); },
    onError: (err) => toast.fromError(err),
  });

  // Sprint 679: o cliente pagou o adiantamento (grava primeiro o valor pendente).
  const adiantamentoRecebido = useMutation({
    mutationFn: async (recebido: boolean) => { await auto.gravarJa(); return vendasApi.adiantamento(id!, recebido, adiantFatura.trim() || null); },
    onSuccess: (v) => onSaved(v, v.adiantamentoRecebidoEm ? 'Adiantamento recebido' : 'Adiantamento marcado como não pago'),
    onError: (err) => toast.fromError(err),
  });
  const adiantValor = parseDecimal(adiant) ?? 0;
  const adiantRecebido = !!venda?.adiantamentoRecebidoEm;
  const eur2 = (n: number) => n.toFixed(2).replace('.', ',');

  // Botão vermelho, bem visível no cartão Estado; pede confirmação antes de cancelar.
  const cancelarUi = !confirmCancel ? (
    <Button type="button" variant="danger" leftIcon={<XCircle size={16} />} onClick={() => setConfirmCancel(true)}>Cancelar venda</Button>
  ) : (
    <div role="alert" className="flex flex-col gap-2 rounded-xl border border-rose-300 bg-rose-50 p-3 text-sm text-rose-900 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-200 sm:flex-row sm:items-center">
      <span className="flex-1">
        Cancelar esta venda? As peças voltam ao stock.
        {venda?.estado === VENDA_ESTADO.Entregue && ' Se a fatura já foi emitida, emite a nota de crédito no Moloni.'}
      </span>
      <div className="flex gap-2">
        <Button type="button" size="sm" variant="secondary" onClick={() => setConfirmCancel(false)}>Não</Button>
        <Button type="button" size="sm" variant="danger" loading={mudarEstado.isPending} onClick={() => mudarEstado.mutate(VENDA_ESTADO.Cancelada)}>Sim, cancelar</Button>
      </div>
    </div>
  );

  // Sprint 650: entregar sem receber já (o cliente paga depois) — fica nas cobranças até ser paga.
  const entregarPorReceber = useMutation({
    mutationFn: async () => {
      if (!fechada) await auto.gravarJa();
      return vendasApi.mudarEstado(id!, VENDA_ESTADO.Entregue, pagamento, true);
    },
    onSuccess: (v) => onSaved(v, 'Entregue — fica por receber'),
    onError: (err) => toast.fromError(err),
  });

  const marcarPagamento = useMutation({
    mutationFn: async (porReceber: boolean) => { await auto.gravarJa(); return vendasApi.pagamento(id!, porReceber); },
    onSuccess: (v) => onSaved(v, v.porReceber ? 'Marcada como por receber' : 'Marcada como paga'),
    onError: (err) => toast.fromError(err),
  });

  const semFatura = useMutation({
    mutationFn: async (ativo: boolean) => { await auto.gravarJa(); return vendasApi.foraApuramento(id!, ativo); },
    onSuccess: (v) => onSaved(v, v.foraApuramento ? 'Marcada como sem fatura' : 'Voltou a precisar de fatura'),
    onError: (err) => toast.fromError(err),
  });

  const registarFatura = useMutation({
    mutationFn: async () => { await auto.gravarJa(); return vendasApi.registarFatura(id!, fatura.trim() || null, faturaData); },
    onSuccess: (v) => onSaved(v, v.invoiceNumber ? 'Fatura registada' : 'Fatura removida'),
    onError: (err) => toast.fromError(err),
  });

  function mudarModoIva(novoSemIva: boolean) {
    if (novoSemIva === semIva) return;
    setLinhas((ls) => ls.map((l) => {
      const v = parseDecimal(l.preco);
      if (v == null) return l;
      const f = 1 + (parseDecimal(l.iva) ?? IVA_NORMAL) / 100;
      const fat = parseDecimal(l.faturar ?? '');
      return { ...l, preco: fmt2(novoSemIva ? v / f : v * f), faturar: fat == null ? '' : fmt2(novoSemIva ? fat / f : fat * f) };
    }));
    setSemIva(novoSemIva);
  }

  // Sprint 669: «combinei 75 € com o cliente» — escreve o total e o Mender acerta a linha principal
  // (a mão de obra, ou a última linha) para dar esse total; o lucro sai sozinho do custo real das peças.
  const [totalAlvo, setTotalAlvo] = useState('');
  // Sprint 673: «30 € ao todo, 10 € por fora» — a parte por fora vai para uma linha «Por fora» e as linhas com
  // fatura descem na mesma proporção até darem o resto (o lucro e o IVA recalculam-se sozinhos).
  const [porForaAlvo, setPorForaAlvo] = useState('');
  function aplicarTotal() {
    const pf = parseDecimal(porForaAlvo);
    if (pf != null) { aplicarComPorFora(pf); return; }
    const alvo = parseDecimal(totalAlvo);
    if (alvo == null || linhas.length === 0) return;
    const diff = Math.round((alvo - contas.total) * 100) / 100;
    if (Math.abs(diff) < 0.005) { setTotalAlvo(''); return; }
    const candidatas = linhas.filter((l) => !l.descricao.startsWith('Custo extra'));
    const alvoLinha = [...candidatas].reverse().find((l) => !l.compraLinhaId && l.encomenda == null) ?? candidatas[candidatas.length - 1] ?? linhas[linhas.length - 1];
    const qtd = Number(alvoLinha.quantidade) || 1;
    const atual = alvoLinha.porFora ? parseDecimal(alvoLinha.preco) ?? 0 : precoComIva(alvoLinha.preco, alvoLinha.iva, semIva);
    const novoComIva = Math.max(0, atual + diff / qtd);
    const taxa = (parseDecimal(alvoLinha.iva) ?? IVA_NORMAL) / 100;
    setLinha(alvoLinha.key, { preco: fmt2(semIva && !alvoLinha.porFora ? novoComIva / (1 + taxa) : novoComIva) });
    setTotalAlvo('');
  }

  // Sprint 681: «dos quais por fora» reparte-se pelas linhas («Faturar a»), sem linha «Por fora» à parte —
  // 2 cartões a 20 € e 10 € por fora = cada um cobrado a 20 € e faturado a 15 € (IVA só dos 15 €).
  function aplicarComPorFora(pf: number) {
    const alvo = parseDecimal(totalAlvo) ?? contas.total;
    if (pf < 0 || pf > alvo) return;
    const extra = (l: LinhaForm) => l.descricao.startsWith('Custo extra');
    const antigasPf = linhas.filter((l) => l.porFora && !extra(l));       // linha «Por fora» antiga: o valor volta às linhas
    const custosExtra = linhas.filter((l) => l.porFora && extra(l));
    const comFatura = linhas.filter((l) => !l.porFora && !extra(l));
    if (comFatura.length === 0) return;
    const cobradoUnit = (l: LinhaForm) => partesLinha(l, semIva).cobrado;
    const totalLinha = (l: LinhaForm) => (Number(l.quantidade) || 1) * cobradoUnit(l);
    const fixos = custosExtra.reduce((s2, l) => s2 + totalLinha(l), 0);
    const atual = comFatura.reduce((s2, l) => s2 + totalLinha(l), 0);
    const paraLinhas = Math.max(0, alvo - fixos);
    const fator = atual > 0 ? paraLinhas / atual : 0;
    const somaAlvo = comFatura.reduce((s2, l) => s2 + totalLinha(l) * fator, 0);
    let resto = Math.round(pf * 100);
    const novasPorKey = new Map<string, Partial<LinhaForm>>();
    comFatura.forEach((l, idx) => {
      const qtd = Number(l.quantidade) || 1;
      const taxa = (parseDecimal(l.iva) ?? IVA_NORMAL) / 100;
      const novoCobrado = Math.round(cobradoUnit(l) * fator * 100) / 100;
      const ultima = idx === comFatura.length - 1;
      const parte = ultima ? resto : Math.round((somaAlvo > 0 ? pf * (totalLinha(l) * fator) / somaAlvo : 0) * 100);
      const porUnidade = Math.min(Math.round(novoCobrado * 100), Math.round(parte / qtd));
      resto -= porUnidade * qtd;
      const faturarComIva = (Math.round(novoCobrado * 100) - porUnidade) / 100;
      const emModo = (v: number) => fmt2(semIva ? v / (1 + taxa) : v);
      novasPorKey.set(l.key, { preco: emModo(novoCobrado), faturar: porUnidade > 0 ? emModo(faturarComIva) : '' });
    });
    setLinhas((ls) => ls
      .filter((l) => !antigasPf.includes(l))
      .map((l) => (novasPorKey.has(l.key) ? { ...l, ...novasPorKey.get(l.key) } : l)));
    if (Math.abs(resto) > 0) toast.info(`Ficaram ${formatEur(Math.abs(resto) / 100)} por repartir (arredondamento) — acerta no «Faturar a» de uma linha.`);
    setTotalAlvo('');
    setPorForaAlvo('');
  }

  function setLinha(key: string, patch: Partial<LinhaForm>) {
    setLinhas((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  }

  // Sprint 594: "Chegou" numa peça por encomendar — o lote escolhido substitui-a (mantém a descrição e o preço).
  const [aSubstituir, setASubstituir] = useState<string | null>(null);
  function addLote(lote: InventarioLinha, quantidade = 1, precoComIvaDado?: number) {
    if (aSubstituir) {
      const key = aSubstituir;
      setASubstituir(null);
      setLinhas((ls) => ls.map((l) => (l.key === key ? {
        ...l, compraLinhaId: lote.linhaId, custoUnit: lote.precoUnitarioPago, taxaCompra: lote.taxaIvaCompra,
        maxStock: lote.quantidadeEmStock, encomenda: null, custo: '', nota: '', iva: String(IVA_NORMAL),
      } : l)));
      setPickerOpen(false);
      return;
    }
    const precoComIvaLote = precoComIvaDado ?? lote.precoVendaArtigo ?? lote.precoFinalComIva;
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(),
      id: null,
      compraLinhaId: lote.linhaId,
      // Sprint 585: o que sai na fatura, em português ("Ecrã iPhone 14 Pro Max") — editável; o texto do fornecedor fica no lote.
      descricao: lote.textoVenda ?? lote.descricao,
      quantidade: String(quantidade),
      // Sprint 570: lote de um artigo → preço do artigo (próprio ou da categoria, ex.: películas a 15 €).
      preco: fmt2(semIva ? precoComIvaLote / (1 + IVA_NORMAL / 100) : precoComIvaLote),
      iva: String(IVA_NORMAL),
      custoUnit: lote.precoUnitarioPago,
      taxaCompra: lote.taxaIvaCompra,
      maxStock: lote.quantidadeEmStock,
      encomenda: null, custo: '', nota: '', porFora: false, custoSemFatura: '',
    }]);
    setPickerOpen(false);
  }

  function addServico() {
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(), id: null, compraLinhaId: null,
      descricao: tipo === VENDA_TIPO.Reparacao ? 'Mão de obra' : '',
      quantidade: '1', preco: '', iva: String(IVA_NORMAL), custoUnit: 0, taxaCompra: 0, maxStock: null,
      encomenda: null, custo: '', nota: '', porFora: false, custoSemFatura: '',
    }]);
  }

  /** Sprint 662: valor cobrado por fora (sem fatura, sem IVA) — numa linha só, para «tudo por fora». */
  function addPorFora() {
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(), id: null, compraLinhaId: null,
      descricao: tipo === VENDA_TIPO.Reparacao ? 'Reparação' : tipo === VENDA_TIPO.Servico ? 'Serviço' : '',
      quantidade: '1', preco: '', iva: '0', custoUnit: 0, taxaCompra: 0, maxStock: null,
      encomenda: null, custo: '', nota: '', porFora: true, custoSemFatura: '',
    }]);
  }

  /** Sprint 642: custo sem fatura (transporte, quem fez o trabalho) — linha a 0 € por fora, só desconta no lucro. */
  function addCustoExtra() {
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(), id: null, compraLinhaId: null, descricao: 'Custo extra: ',
      quantidade: '1', preco: '0', iva: '0', custoUnit: 0, taxaCompra: 0, maxStock: null,
      encomenda: null, custo: '', nota: '', porFora: true, custoSemFatura: '',
    }]);
  }

  /** Sprint 594: peça que ainda não há em stock — fica anotada com o custo estimado e o preço, até chegar. */
  function addEncomenda() {
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(), id: null, compraLinhaId: null, descricao: '',
      quantidade: '1', preco: '', iva: String(IVA_NORMAL), custoUnit: 0, taxaCompra: 0, maxStock: null,
      encomenda: PECA_ENCOMENDA.PorEncomendar, custo: '', nota: '', porFora: false, custoSemFatura: '',
    }]);
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (isNew) guardar.mutate(VENDA_ESTADO.Orcamento);
    else void auto.gravarJa(); // Enter numa venda existente: grava já o que está pendente
  }

  const precisaCliente = tipo === VENDA_TIPO.Reparacao && !cliente;
  const pecasEmFalta = linhas.filter((l) => l.encomenda != null && !l.compraLinhaId).length;
  const linhasOk = linhas.every((l) => (l.compraLinhaId || l.descricao.trim()) && Number(l.quantidade) >= 1 && parseDecimal(l.preco) != null);
  const podeGravar = !precisaCliente && linhasOk && !guardar.isPending && !fechada;
  const busy = guardar.isPending || mudarEstado.isPending;

  if (!isNew && existente.isLoading) return <SkeletonCard />;
  if (!isNew && existente.isError) return <p className="text-sm text-rose-600">{apiErrorMessage(existente.error)}</p>;

  const estadoAtual = venda?.estado ?? VENDA_ESTADO.Orcamento;
  // Próximos passos na ordem natural (os números do enum não são a ordem — EmCurso = 5).
  const proximos = VENDA_FLUXO.slice(VENDA_FLUXO.indexOf(estadoAtual) + 1);

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      <BackButton to="/vendas" label="Vendas" />
      <PageHeader
        title={isNew ? `${tipo === VENDA_TIPO.Reparacao ? 'Nova' : 'Novo'} ${VENDA_TIPO_LABEL[tipo].toLowerCase()}` : `${VENDA_TIPO_LABEL[venda!.tipo]} #${venda!.numero}`}
        meta={venda && <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${estadoVendaVisivel(venda).color}`}>{estadoVendaVisivel(venda).label}</span>}
        actions={(
          <>
          {venda?.temFaturaPdf && (
            <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(vendasApi.faturaPdfPath(venda.id))}>
              PDF da fatura
            </Button>
          )}
          {venda?.estado === VENDA_ESTADO.Entregue && (
            <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(vendasApi.talaoPath(venda.id))}>
              Talão
            </Button>
          )}
          {venda && (
            <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(vendasApi.pdfPath(venda.id, 'orcamento'))}>
              Orçamento PDF
            </Button>
          )}
          {!fechada && isNew && (
          <Button type="submit" variant="secondary" loading={guardar.isPending && guardar.variables !== VENDA_ESTADO.Entregue} disabled={!podeGravar} leftIcon={<Save size={15} />}>
            Guardar orçamento
          </Button>
          )}
          {!fechada && !isNew && (
            <AutosaveStatus estado={valida ? auto.estado : 'erro'} onTentar={valida ? auto.tentarDeNovo : undefined}
              erro={valida ? auto.erro : precisaCliente ? 'Falta o cliente — não guardei' : 'Há uma linha incompleta — não guardei'} />
          )}
          </>
        )}
      />

      {venda?.faturaPorRegistar && (
        <div role="alert" className="flex flex-wrap items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          <AlertTriangle size={16} className="mt-0.5 shrink-0" />
          <span className="flex-1">Esta venda ainda não tem fatura — emite-a no Moloni ON abaixo (ou regista o nº se já a emitiste).</span>
          {isAdmin && (
            <Button type="button" size="sm" variant="secondary" loading={semFatura.isPending} onClick={() => semFatura.mutate(true)}>
              Registar sem fatura
            </Button>
          )}
        </div>
      )}

      {venda?.foraApuramento && (
        <div className="flex flex-wrap items-center gap-2 rounded-xl border border-zinc-200 bg-zinc-50 p-3 text-sm text-zinc-700 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300">
          <span className="rounded-full bg-zinc-200 px-2 py-0.5 text-xs font-medium text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200">Sem fatura</span>
          <span className="flex-1">{venda.foraApuramentoMotivo ?? 'Registo interno'} — não conta no IVA; o lucro conta o valor inteiro.</span>
          {isAdmin && (
            <Button type="button" size="sm" variant="ghost" loading={semFatura.isPending} onClick={() => semFatura.mutate(false)}>
              Desfazer
            </Button>
          )}
        </div>
      )}

      {venda?.estado === VENDA_ESTADO.Entregue && venda.atividade !== 1 && (
        venda.porReceber ? (
          <div className="flex flex-wrap items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
            <span className="rounded-full bg-amber-200 px-2 py-0.5 text-xs font-semibold text-amber-900 dark:bg-amber-900/60 dark:text-amber-100">Por receber</span>
            <span className="flex-1">Entregue — o cliente ainda não pagou.</span>
            <Button type="button" size="sm" loading={marcarPagamento.isPending} onClick={() => marcarPagamento.mutate(false)}>
              Marcar como paga
            </Button>
          </div>
        ) : (
          <div className="flex justify-end">
            <button type="button" className="text-xs text-zinc-500 underline-offset-2 hover:underline disabled:opacity-50"
              disabled={marcarPagamento.isPending} onClick={() => marcarPagamento.mutate(true)}>
              {venda.recebidoEm ? `Paga a ${new Date(venda.recebidoEm).toLocaleDateString('pt-PT')} · ` : ''}Ainda não pagou?
            </button>
          </div>
        )
      )}

      {isNew && vindoDaCompra > 0 && (
        <div className="flex items-start gap-2 rounded-xl border border-brand-200 bg-brand-50 p-3 text-sm text-brand-900 dark:border-brand-900/50 dark:bg-brand-950/30 dark:text-brand-200">
          <Boxes size={16} className="mt-0.5 shrink-0" />
          <span>{vindoDaCompra === 1 ? 'O artigo da compra já está' : `Os ${vindoDaCompra} artigos da compra já estão`} nas linhas, com o texto que sai na fatura.
            Põe o <strong>preço final</strong> de cada um (com IVA), escolhe o cliente se quiseres e grava.</span>
        </div>
      )}

      <SectionCard title="Venda">
        <div className="grid gap-3 md:grid-cols-2">
          <div>
            <span className={labelCls}>Tipo</span>
            <div className="flex gap-1 rounded-lg bg-zinc-100 p-1 dark:bg-zinc-800" role="radiogroup" aria-label="Tipo de venda">
              {Object.entries(VENDA_TIPO_LABEL).map(([v, label]) => (
                <button
                  key={v}
                  type="button"
                  role="radio"
                  aria-checked={tipo === Number(v)}
                  disabled={fechada}
                  onClick={() => {
                    const t = Number(v) as VendaTipo;
                    setTipo(t);
                    if (isNew) mudarModoIva(semIvaPorDefeito(t));
                  }}
                  className={`min-h-9 flex-1 rounded-md px-3 text-sm font-medium transition ${tipo === Number(v) ? 'bg-white shadow-sm dark:bg-zinc-950' : 'text-zinc-500'}`}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
          <div>
            <span className={labelCls}>Cliente{tipo === VENDA_TIPO.Reparacao ? ' *' : ' (opcional)'}</span>
            <ClienteSelect value={cliente} onChange={setCliente} disabled={fechada} />
          </div>
          {tipo === VENDA_TIPO.Reparacao && (
            <label className="block">
              <span className={labelCls}>Equipamento</span>
              <input value={equipamento} onChange={(e) => setEquipamento(e.target.value)} className={inputCls} placeholder="Samsung A15 · IMEI 35…" maxLength={200} disabled={fechada} />
            </label>
          )}
          {tipo !== VENDA_TIPO.Produto && (
            <label className="block">
              <span className={labelCls}>{tipo === VENDA_TIPO.Reparacao ? 'Avaria' : 'O que foi pedido'}</span>
              <input value={problema} onChange={(e) => setProblema(e.target.value)} className={inputCls} placeholder={tipo === VENDA_TIPO.Reparacao ? 'Ecrã partido, não carrega…' : 'Website institucional com 5 páginas'} maxLength={2000} disabled={fechada} />
            </label>
          )}
          {tipo !== VENDA_TIPO.Produto && (
            <label className="block">
              <span className={labelCls}>Previsão de entrega</span>
              <input type="date" value={previsto} onChange={(e) => setPrevisto(e.target.value)} className={inputCls} disabled={fechada} />
            </label>
          )}
          <label className={`block ${tipo === VENDA_TIPO.Produto ? 'md:col-span-2' : ''}`}>
            <span className={labelCls}>Notas</span>
            <input value={notas} onChange={(e) => setNotas(e.target.value)} className={inputCls} maxLength={2000} disabled={fechada} />
          </label>
        </div>
      </SectionCard>

      {tipo !== VENDA_TIPO.Produto && (!fechada || (venda?.adiantamentoCents ?? 0) > 0) && (
        <SectionCard title="Adiantamento">
          <div className="space-y-3 text-sm">
            <p className="text-zinc-500">Para encomendar a peça a quem não conheces. Sai no <strong>Orçamento PDF</strong> com o que falta pagar na entrega e a regra se o cliente desistir (sinal — art. 441.º do Código Civil).</p>
            <div className="flex flex-wrap items-end gap-2">
              <label className="block">
                <span className={labelCls}>Valor (€)</span>
                <input value={adiant} onChange={(e) => setAdiant(e.target.value)} inputMode="decimal" placeholder="sem adiantamento"
                  className={`${inputCls} w-36`} disabled={fechada || adiantRecebido} aria-label="Valor do adiantamento" />
              </label>
              {!fechada && !adiantRecebido && contas.total > 0 && (
                <>
                  <Button type="button" size="sm" variant="secondary" onClick={() => setAdiant(eur2(Math.ceil(contas.total * 0.25)))}>25% ({formatEur(Math.ceil(contas.total * 0.25))})</Button>
                  {contas.custoPecas > 0 && (
                    <Button type="button" size="sm" variant="secondary" onClick={() => setAdiant(eur2(Math.ceil(contas.custoPecas)))}>Custo das peças ({formatEur(Math.ceil(contas.custoPecas))})</Button>
                  )}
                  {adiant && <Button type="button" size="sm" variant="ghost" onClick={() => setAdiant('')}>Sem adiantamento</Button>}
                </>
              )}
            </div>
            {adiantValor > 0 && (
              <>
                {adiantValor > contas.total && contas.total > 0 && (
                  <p className="text-amber-700 dark:text-amber-300">O adiantamento é maior do que o total da reparação.</p>
                )}
                <p>Falta receber na entrega: <strong>{formatEur(Math.max(0, contas.total - adiantValor))}</strong></p>
                {!isNew && venda && (adiantRecebido ? (
                  <div className="flex flex-wrap items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 p-2 text-emerald-900 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-200">
                    <CheckCircle2 size={15} />
                    <span className="flex-1">Pago a {new Date(venda.adiantamentoRecebidoEm!).toLocaleDateString('pt-PT')}{venda.adiantamentoFatura ? ` · fatura ${venda.adiantamentoFatura}` : ' · sem nº de fatura registado'}</span>
                    {!fechada && <Button type="button" size="sm" variant="ghost" loading={adiantamentoRecebido.isPending} onClick={() => adiantamentoRecebido.mutate(false)}>Desfazer</Button>}
                  </div>
                ) : !fechada && (
                  <div className="flex flex-wrap items-end gap-2 rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
                    <label className="block">
                      <span className={labelCls}>Nº da fatura do adiantamento (opcional)</span>
                      <input value={adiantFatura} onChange={(e) => setAdiantFatura(e.target.value)} className={`${inputCls} w-48`} maxLength={60} placeholder="FT 2026/123" />
                    </label>
                    <Button type="button" size="sm" leftIcon={<Banknote size={15} />} loading={adiantamentoRecebido.isPending} onClick={() => adiantamentoRecebido.mutate(true)}>
                      Recebi o adiantamento
                    </Button>
                    <p className="w-full text-xs text-zinc-500">Passa a fatura do adiantamento no dia em que recebes (o IVA é devido nesse dia — CIVA art. 8.º n.º 2). Na fatura final desconta-se o adiantamento.</p>
                  </div>
                ))}
              </>
            )}
          </div>
        </SectionCard>
      )}

      <SectionCard
        title="Linhas"
        action={!fechada && (
          <div className="flex gap-2">
            <Button type="button" size="sm" variant="secondary" leftIcon={<Boxes size={15} />} onClick={() => setPickerOpen(true)}>Do stock</Button>
            <Button type="button" size="sm" variant="secondary" leftIcon={<Wrench size={15} />} onClick={addServico}>
              {tipo === VENDA_TIPO.Reparacao ? 'Mão de obra' : 'Serviço'}
            </Button>
            {tipo !== VENDA_TIPO.Servico && (
              <Button type="button" size="sm" variant="secondary" leftIcon={<PackageSearch size={15} />} onClick={addEncomenda}
                title="Uma peça que ainda não tens: anota quanto custa e por quanto a vendes — fica na lista de peças a encomendar">
                Peça a encomendar
              </Button>
            )}
            <Button type="button" size="sm" variant="secondary" leftIcon={<Banknote size={15} />} onClick={addPorFora}
              title="Valor cobrado sem fatura: sem IVA, conta inteiro no lucro">
              Por fora
            </Button>
            <Button type="button" size="sm" variant="secondary" leftIcon={<Plus size={15} />} onClick={addCustoExtra}
              title="Custo sem fatura (transporte, quem fez o trabalho…) — desconta no lucro, não mexe no IVA">
              Custo extra
            </Button>
          </div>
        )}
      >
        {/* Sprint 581: a peça usada tem de sair do stock — senão o custo das peças vendidas fica a 0 € e o resultado engana. */}
        {!fechada && tipo === VENDA_TIPO.Reparacao && linhas.length > 0 && !linhas.some((l) => l.compraLinhaId || l.encomenda != null) && (
          <p className="mb-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
            Usaste alguma peça (ecrã, bateria…)? Junta-a com <strong>Do stock</strong> — sai do lote e o custo entra no resultado.
          </p>
        )}
        <div className="mb-3 flex flex-wrap items-center gap-2 text-sm">
          <span className="text-zinc-500">Os preços que escrevo são</span>
          <div role="radiogroup" aria-label="Preços com ou sem IVA" className="inline-flex rounded-lg border border-zinc-200 p-0.5 dark:border-zinc-700">
            {[{ v: false, label: 'com IVA' }, { v: true, label: 'sem IVA' }].map((o) => (
              <button
                key={o.label}
                type="button"
                role="radio"
                aria-checked={semIva === o.v}
                disabled={fechada}
                onClick={() => mudarModoIva(o.v)}
                className={`min-h-9 rounded-md px-3 text-sm font-medium transition ${semIva === o.v ? 'bg-brand-600 text-white' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}
              >
                {o.label}
              </button>
            ))}
          </div>
          <span className="text-xs text-zinc-500">{semIva ? 'O IVA é somado por cima — o valor escrito é teu.' : 'O IVA já está dentro do valor escrito.'}</span>
        </div>
        {linhas.length === 0 ? (
          <p className="py-4 text-center text-sm text-zinc-500">
            Junta peças/artigos do stock ou {tipo === VENDA_TIPO.Reparacao ? 'mão de obra' : 'um serviço'}.
          </p>
        ) : (
          // Sprint 572: no telemóvel cada linha é um cartão (descrição; qtd/preço/IVA; totais) — a tabela só a partir de sm.
          <div className="sm:-mx-4 sm:overflow-x-auto sm:px-4">
            <table className="w-full text-sm sm:min-w-[46rem]">
              <thead className="hidden text-left text-xs text-zinc-500 sm:table-header-group">
                <tr>
                  <th className="py-2 pr-2 font-medium">Descrição</th>
                  <th className="w-16 py-2 pr-2 font-medium">Qtd</th>
                  <th className="w-28 py-2 pr-2 font-medium">Preço un. {semIva ? 's/ IVA' : 'c/ IVA'}</th>
                  <th className="w-20 py-2 pr-2 font-medium">IVA %</th>
                  <th className="w-24 py-2 pr-2 text-right font-medium">Total c/ IVA</th>
                  <th className="w-28 py-2 pr-2 text-right font-medium">IVA ao Estado</th>
                  <th className="w-24 py-2 pr-2 text-right font-medium">Lucro</th>
                  <th className="w-10" />
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {linhas.map((l, i) => {
                  const c = contas.porLinha[i];
                  return (
                    <tr key={l.key} className="grid grid-cols-3 gap-x-2 py-2 align-top sm:table-row sm:py-0">
                      <td className="col-span-3 py-1 sm:py-2 sm:pr-2">
                        <input aria-label="Descrição" value={l.descricao} onChange={(e) => setLinha(l.key, { descricao: e.target.value })} className={inputCls} disabled={fechada} maxLength={300} />
                        {l.encomenda != null && !l.compraLinhaId ? (
                          // Sprint 594: peça que falta — custo estimado, estado da encomenda, nota e "Chegou".
                          <div className="mt-1 space-y-1.5 rounded-lg border border-amber-200 bg-amber-50/60 p-2 dark:border-amber-900/50 dark:bg-amber-950/20">
                            <div className="flex flex-wrap items-center gap-1.5 text-xs">
                              <select aria-label="Estado da encomenda" value={l.encomenda} disabled={fechada}
                                onChange={(e) => setLinha(l.key, { encomenda: Number(e.target.value) as PecaEncomendaEstado })}
                                className="min-h-8 rounded-md border border-amber-300 bg-white px-2 text-xs font-medium text-amber-900 dark:border-amber-800 dark:bg-zinc-950 dark:text-amber-200">
                                {Object.entries(PECA_ENCOMENDA_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
                              </select>
                              <label className="flex items-center gap-1 text-zinc-600 dark:text-zinc-300">
                                custa
                                <input aria-label="Custo estimado da peça (c/ IVA)" inputMode="decimal" value={l.custo} disabled={fechada} placeholder="0,00"
                                  onChange={(e) => setLinha(l.key, { custo: e.target.value })}
                                  className="min-h-8 w-20 rounded-md border border-zinc-300 bg-white px-2 text-xs dark:border-zinc-700 dark:bg-zinc-950" />
                                €
                              </label>
                              <input aria-label="Nota da encomenda" value={l.nota} disabled={fechada} maxLength={300} placeholder="onde / quando chega"
                                onChange={(e) => setLinha(l.key, { nota: e.target.value })}
                                className="min-h-8 min-w-0 flex-1 rounded-md border border-zinc-300 bg-white px-2 text-xs dark:border-zinc-700 dark:bg-zinc-950" />
                              {!fechada && (
                                <Button type="button" size="sm" variant="secondary" leftIcon={<Boxes size={13} />}
                                  title="A peça chegou e está no stock: escolhe o lote — a linha passa a usar essa peça"
                                  onClick={() => { setASubstituir(l.key); setPickerOpen(true); }}>
                                  Chegou
                                </Button>
                              )}
                            </div>
                          </div>
                        ) : (
                          <span className="mt-1 block text-xs text-zinc-500">
                            {l.compraLinhaId ? `Stock · pago ${formatEur(l.custoUnit)}/un.` : l.porFora ? 'Por fora · sem IVA, fora da fatura' : semIva ? 'Serviço · IVA somado ao preço' : 'Serviço · IVA incluído no preço'}
                          </span>
                        )}
                        {/* Sprint 642: parte recebida por fora da fatura e custos sem fatura. */}
                        <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                          <label className="inline-flex min-h-8 items-center gap-1.5 text-zinc-600 dark:text-zinc-300"
                            title="Recebido fora da fatura (tratado à parte com a contabilista): sem IVA e não vai ao Moloni, mas conta no lucro">
                            <input type="checkbox" checked={l.porFora} disabled={fechada}
                              onChange={(e) => setLinha(l.key, { porFora: e.target.checked })} />
                            Por fora
                          </label>
                          {l.porFora && <span className="rounded-full bg-violet-100 px-2 py-0.5 font-medium text-violet-800 dark:bg-violet-950 dark:text-violet-200">por fora</span>}
                          {!l.compraLinhaId && l.encomenda == null && (
                            <label className="inline-flex items-center gap-1 text-zinc-600 dark:text-zinc-300"
                              title="Custo sem fatura desta linha (transporte, quem fez o trabalho, média do material) — desconta no lucro">
                              custo s/ fatura
                              <input aria-label="Custo sem fatura" inputMode="decimal" value={l.custoSemFatura} disabled={fechada} placeholder="0,00"
                                onChange={(e) => setLinha(l.key, { custoSemFatura: e.target.value })}
                                className="min-h-8 w-20 rounded-md border border-zinc-300 bg-white px-2 text-xs dark:border-zinc-700 dark:bg-zinc-950" />
                              €
                            </label>
                          )}
                        </div>
                      </td>
                      <td className="py-1 sm:py-2 sm:pr-2">
                        <span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">Qtd</span>
                        <input aria-label="Quantidade" inputMode="numeric" value={l.quantidade} onChange={(e) => setLinha(l.key, { quantidade: e.target.value.replace(/\D/g, '') })} className={inputCls} disabled={fechada} />
                        {l.maxStock != null && Number(l.quantidade) > l.maxStock && <span className="mt-1 block text-xs text-rose-600">só {l.maxStock}</span>}
                      </td>
                      <td className="py-1 sm:py-2 sm:pr-2"><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">{semIva ? 'Preço s/ IVA' : 'Preço c/ IVA'}</span><input aria-label={semIva ? 'Preço unitário sem IVA' : 'Preço unitário com IVA'} inputMode="decimal" value={l.preco} onChange={(e) => setLinha(l.key, { preco: e.target.value })} className={inputCls} placeholder="0,00" disabled={fechada} />
                        {/* Sprint 680: faturar abaixo do preço — a diferença vai por fora sozinha (IVA e lucro recalculam). */}
                        {!l.porFora && (() => {
                          const pt = partesLinha(l, semIva);
                          return (
                            <label className="mt-1 block text-[11px] text-zinc-500" title="Quanto faturas por unidade. O resto do preço fica por fora (sem IVA) e entra no lucro.">
                              Faturar a
                              <input aria-label="Preço a faturar por unidade" inputMode="decimal" value={l.faturar ?? ''} disabled={fechada}
                                onChange={(e) => setLinha(l.key, { faturar: e.target.value })} placeholder="tudo"
                                className={`mt-0.5 min-h-8 w-full rounded-md border bg-white px-2 text-xs dark:bg-zinc-950 ${pt.faturarInvalido ? 'border-rose-400' : 'border-zinc-300 dark:border-zinc-700'}`} />
                              {pt.faturarInvalido
                                ? <span className="text-rose-600">maior que o preço</span>
                                : pt.porForaCents > 0 && <span className="font-medium text-violet-700 dark:text-violet-300">{formatEur(pt.porForaCents / 100)} por fora</span>}
                            </label>
                          );
                        })()}
                      </td>
                      <td className="py-1 sm:py-2 sm:pr-2">
                        <span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">IVA</span>
                        <select aria-label="Taxa de IVA" value={l.iva} onChange={(e) => setLinha(l.key, { iva: e.target.value })} className={inputCls} disabled={fechada || !!l.compraLinhaId}>
                          {['23', '13', '6', '0'].map((t) => <option key={t} value={t}>{t}%</option>)}
                        </select>
                      </td>
                      <td className="py-1 font-medium tabular-nums sm:py-2 sm:pr-2 sm:pt-4 sm:text-right"><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">Total</span>{formatEur(c.total)}</td>
                      <td className="py-1 tabular-nums sm:py-2 sm:pr-2 sm:pt-4 sm:text-right"><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">IVA ao Estado</span>{formatEur(c.ivaAPagar)}</td>
                      <td className={`py-1 tabular-nums sm:py-2 sm:pr-2 sm:pt-4 sm:text-right ${c.lucro < 0 ? 'text-rose-600' : ''}`}><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">Lucro</span>{formatEur(c.lucro)}</td>
                      <td className="col-span-3 flex justify-end sm:table-cell sm:py-2">
                        {!fechada && (
                          <Button type="button" variant="icon" aria-label="Remover linha" onClick={() => setLinhas((ls) => ls.filter((x) => x.key !== l.key))}>
                            <Trash2 size={15} />
                          </Button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
        {!fechada && linhas.length > 0 && (
          <div className="mt-3 ml-auto flex max-w-sm flex-wrap items-end gap-2 rounded-xl bg-brand-50 p-3 dark:bg-brand-950/30">
            <label className="min-w-0 flex-1 text-xs font-semibold text-brand-900 dark:text-brand-100">
              Preço final combinado (€)
              <input inputMode="decimal" value={totalAlvo} onChange={(e) => setTotalAlvo(e.target.value)} placeholder={formatEur(contas.total)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); aplicarTotal(); } }}
                className={`${inputCls} mt-1`} aria-label="Preço final combinado com o cliente" />
            </label>
            <label className="w-24 text-xs font-semibold text-violet-800 dark:text-violet-200">
              dos quais por fora
              <input inputMode="decimal" value={porForaAlvo} onChange={(e) => setPorForaAlvo(e.target.value)} placeholder={contas.porFora > 0 ? formatEur(contas.porFora) : '0,00'}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); aplicarTotal(); } }}
                className={`${inputCls} mt-1`} aria-label="Parte do total paga por fora, sem fatura" />
            </label>
            <Button type="button" size="sm" onClick={aplicarTotal} disabled={!totalAlvo.trim() && !porForaAlvo.trim()}>Aplicar</Button>
            <p className="w-full text-[11px] text-brand-800/80 dark:text-brand-200/80">Escreves o total que vais cobrar e, se quiseres, quanto vai por fora (sem fatura). O por fora reparte-se pelas linhas («Faturar a») — o IVA é só do faturado e o lucro calcula-se sozinho.</p>
          </div>
        )}
        <dl className="mt-3 ml-auto grid max-w-xs grid-cols-[1fr_auto] gap-x-6 gap-y-1 text-sm tabular-nums">
          <dt className="font-medium">Total a cobrar</dt><dd className="text-right text-lg font-semibold">{formatEur(contas.total)}</dd>
          {contas.porFora > 0 && (
            <>
              <dt className="text-zinc-500">Na fatura</dt><dd className="text-right">{formatEur(contas.total - contas.porFora)}</dd>
              <dt className="text-violet-700 dark:text-violet-300" title="Recebido fora da fatura — sem IVA, conta no lucro">Por fora</dt><dd className="text-right text-violet-700 dark:text-violet-300">{formatEur(contas.porFora)}</dd>
            </>
          )}
          <dt className="text-zinc-500">IVA na fatura</dt><dd className="text-right">{formatEur(contas.ivaVenda)}</dd>
          <dt className="text-zinc-500" title="IVA da venda − IVA já pago ao fornecedor das peças">IVA a entregar ao Estado</dt><dd className="text-right">{formatEur(contas.ivaAPagar)}</dd>
          <dt className="text-zinc-500" title="Total − custo das peças − IVA a entregar">Lucro que sobra</dt><dd className={`text-right font-medium ${contas.lucro < 0 ? 'text-rose-600' : 'text-emerald-600'}`}>{formatEur(contas.lucro)}</dd>
        </dl>
      </SectionCard>

      {isNew && (
        <SectionCard title="Concluir">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
            <p className="text-sm text-zinc-500">"Guardar orçamento" não mexe no stock. Venda feita na hora? Escolhe o pagamento e regista como entregue.</p>
            <div className="flex gap-2">
              <select aria-label="Método de pagamento" value={pagamento} onChange={(e) => setPagamento(Number(e.target.value) as PaymentMethod)} className={`${inputCls} w-auto`}>
                {Object.entries(PAYMENT_METHOD_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
              </select>
              <Button type="button" leftIcon={<CheckCircle2 size={15} />} disabled={!podeGravar || linhas.length === 0} loading={guardar.isPending && guardar.variables === VENDA_ESTADO.Entregue} onClick={() => guardar.mutate(VENDA_ESTADO.Entregue)}>
                Entregue & paga
              </Button>
            </div>
          </div>
        </SectionCard>
      )}

      {venda && !fechada && (
        <SectionCard title="Estado">
          <div className="flex flex-wrap items-center gap-2">
            {proximos.filter((e) => e !== VENDA_ESTADO.Entregue).map((e) => (
              <Button key={e} type="button" variant="secondary" disabled={busy || !podeGravar} onClick={() => mudarComAviso(e)}>
                {VENDA_ESTADO_LABEL[e]}
              </Button>
            ))}
            <span className="mx-1 hidden h-6 w-px bg-zinc-200 sm:block dark:bg-zinc-700" />
            <select aria-label="Método de pagamento" value={pagamento} onChange={(e) => setPagamento(Number(e.target.value) as PaymentMethod)} className={`${inputCls} w-auto`}>
              {Object.entries(PAYMENT_METHOD_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
            </select>
            <Button type="button" variant="success" leftIcon={<CheckCircle2 size={15} />} disabled={busy || !podeGravar || linhas.length === 0} loading={mudarEstado.isPending && mudarEstado.variables === VENDA_ESTADO.Entregue} onClick={() => mudarEstado.mutate(VENDA_ESTADO.Entregue)}>
              Entregue & paga
            </Button>
            <Button type="button" variant="secondary" disabled={busy || !podeGravar || linhas.length === 0} loading={entregarPorReceber.isPending} onClick={() => entregarPorReceber.mutate()}
              title="O cliente leva já e paga depois — fica em Finanças → Cobranças até marcares como paga">
              Entregue, paga depois
            </Button>
          </div>
          {pecasEmFalta > 0 && (
            <p className="mt-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
              Falta{pecasEmFalta === 1 ? '' : 'm'} {pecasEmFalta} peça{pecasEmFalta === 1 ? '' : 's'} — quando chegar{pecasEmFalta === 1 ? '' : 'em'}, carrega em <strong>Chegou</strong> na linha para usar a peça do stock
              (o custo certo entra no lucro e a peça sai do stock).
            </p>
          )}
          <p className="mt-2 text-xs text-zinc-500">Ao sair de Orçamento as peças saem do stock. Entregue fecha a venda (as linhas deixam de se poder editar).</p>
          {isAdmin && <div className="mt-3 border-t border-zinc-200 pt-3 dark:border-zinc-800">{cancelarUi}</div>}
        </SectionCard>
      )}

      {/* Sprint 663: venda toda por fora (ou sem fatura) não tem documento fiscal — o bloco só confunde. */}
      {venda && !venda.foraApuramento && !(venda.items.length > 0 && venda.items.every((i) => i.porFora)) && (
        <DocumentoMoloni venda={venda} isAdmin={isAdmin} onChange={(v) => {
          qc.invalidateQueries({ queryKey: ['vendas'] });
          qc.setQueryData(['venda', v.id], v);
        }} />
      )}

      {venda && venda.estado === VENDA_ESTADO.Entregue && !venda.moloniDocumentoTipo && !venda.foraApuramento && !venda.items.every((i) => i.porFora) && (
        <SectionCard title="Nº da fatura (registo manual, se emitida fora do Mender)" collapsible defaultOpen={false}>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
            <label className="block flex-1">
              <span className={labelCls}>Nº da fatura (Moloni)</span>
              <input value={fatura} onChange={(e) => setFatura(e.target.value)} className={inputCls} placeholder="FR 2026/123" maxLength={120} />
            </label>
            <label className="block">
              <span className={labelCls}>Data</span>
              <input type="date" value={faturaData} onChange={(e) => setFaturaData(e.target.value)} className={inputCls} />
            </label>
            <Button type="button" loading={registarFatura.isPending} onClick={() => registarFatura.mutate()}>Registar</Button>
          </div>
          <p className="mt-2 text-xs text-zinc-500">
            Pago por {PAYMENT_METHOD_LABEL[venda.paymentMethod]} · {new Date(venda.data).toLocaleString('pt-PT')}
          </p>
        </SectionCard>
      )}

      {/* Venda entregue: cancelar continua possível (nota de crédito no Moloni), em vermelho e à vista. */}
      {venda && fechada && venda.estado !== VENDA_ESTADO.Cancelada && isAdmin && (
        <SectionCard title="Cancelar">{cancelarUi}</SectionCard>
      )}

      {venda && venda.tipo === VENDA_TIPO.Reparacao && <ReparacaoExtras venda={venda} />}

      <LotePicker open={pickerOpen} onClose={() => { setPickerOpen(false); setASubstituir(null); }} onPick={addLote} reservado={reservado} />
    </form>
  );
}
