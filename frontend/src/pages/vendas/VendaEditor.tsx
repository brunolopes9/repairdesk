import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Banknote, Boxes, CheckCircle2, FileText, PackageSearch, Save, Trash2, Wrench, XCircle } from 'lucide-react';
import { AutosaveStatus, BackButton, Button, PageHeader, SectionCard, SkeletonCard } from '../../components/ui';
import { useAutosave } from '../../lib/useAutosave';
import { PagamentoPicker } from '../../components/PagamentoPicker';
import { useConfirm } from '../../components/ConfirmDialog';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { useAuth } from '../../lib/auth/AuthContext';
import { apiErrorInfo, apiErrorMessage } from '../../lib/errors';
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

/** 19/11 (Bruno: «números redondos»): 4,24 → 4,90; 50,37 → 50,90; 18,95 → 19,90. Igual a ArtigoService.PrecoRedondo. */
function precoRedondo(eur: number): number {
  if (eur <= 0) return eur;
  const c = Math.round(eur * 100);
  const e = Math.floor(c / 100);
  return (c % 100 <= 90 ? e * 100 + 90 : e * 100 + 190) / 100;
}

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
  /** 18/11: portes da compra por unidade (só lotes escolhidos agora — informativo). */
  portesUnit?: number;
  /** 19/11: lote de fornecedor da UE (autoliquidação) — a fatura não traz IVA, o custo é o mesmo com e sem IVA. */
  autoliqUe?: boolean;
  maxStock: number | null;
  /** Sprint 594: peça que ainda não há (por encomendar/encomendada) — custo estimado c/ IVA e nota. */
  encomenda: PecaEncomendaEstado | null;
  custo: string;
  nota: string;
  /** Linhas antigas (histórico): só leitura. As vendas novas faturam sempre o valor total. */
  porFora: boolean;
  /** Custo desta linha de serviço (desconta no lucro). */
  custoSemFatura: string;
}

/** Preço por unidade com IVA (o que vai à fatura). */
function partesLinha(l: LinhaForm, semIva: boolean) {
  const qtd = Number(l.quantidade) || 0;
  const cobrado = l.porFora ? parseDecimal(l.preco) ?? 0 : precoComIva(l.preco, l.iva, semIva);
  return { qtd, cobrado };
}

/** Custo de UMA unidade (c/ IVA, o que pagaste) e a taxa de IVA dessa compra. Serviço: o «custo» da linha a dividir pela qtd, sem IVA. */
function custoDaLinha(l: LinhaForm): { custoUnit: number; taxaCompra: number } {
  const qtd = Math.max(1, Number(l.quantidade) || 1);
  if (l.encomenda != null && !l.compraLinhaId) return { custoUnit: parseDecimal(l.custo) ?? 0, taxaCompra: IVA_NORMAL / 100 };
  if (!l.compraLinhaId && l.encomenda == null) return { custoUnit: (parseDecimal(l.custoSemFatura) ?? 0) / qtd, taxaCompra: 0 };
  return { custoUnit: l.custoUnit, taxaCompra: l.taxaCompra };
}

/**
 * 18/11 (Bruno): numa Reparação ou num Serviço a peça do stock faz parte do serviço — a linha leva «Reparação — »
 * ou «Serviço — » à frente (e vai para o Moloni como serviço). Numa venda de Produto fica só o nome do artigo.
 */
const PREFIXOS = /^(Reparação|Serviço)\s+[—-]\s+/;

/**
 * 20/11 (Bruno): o nome do fornecedor não vai assim para a fatura do cliente — «Ecrã Motorola Moto G05 XT2523 / 2025
 * Refurbished) All Colors)» → «Ecrã Motorola Moto G05 XT2523». Tira anos, «Refurbished», «All Colors» e parênteses soltos.
 */
function limparNomeFatura(nome: string): string {
  let t = nome
    .replace(/\b(refurbished|all colou?rs?|original|remaded|oem|service pack)\b\)?/gi, ' ')
    .replace(/[/(]\s*20\d{2}\s*\)?/g, ' ');
  // parênteses sem par
  let abertos = 0;
  t = [...t].filter((c) => { if (c === '(') { abertos++; return true; } if (c === ')') { if (abertos === 0) return false; abertos--; } return true; }).join('');
  while (abertos-- > 0) t = t.replace(/\((?![^(]*\))/, ' ');
  return t.replace(/\(\s*\)/g, ' ').replace(/\s+([/,])\s*$/g, '').replace(/\s{2,}/g, ' ').replace(/[\s/–—-]+$/, '').trim();
}

function comPrefixo(descricao: string, tipo: VendaTipo): string {
  const base = limparNomeFatura(descricao.replace(PREFIXOS, ''));
  return tipo === VENDA_TIPO.Reparacao ? `Reparação — ${base}` : tipo === VENDA_TIPO.Servico ? `Serviço — ${base}` : base;
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
    return { ...l, preco: fmt2((parseDecimal(l.preco) ?? 0) / f) };
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
    preco: num(i.precoUnitarioCents),
    iva: String(i.ivaRate),
    custoUnit: i.custoUnitarioPago ?? 0,
    taxaCompra: i.taxaIvaCompra ?? 0,
    portesUnit: i.portesUnitario ?? 0,
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
  // Sprint 685: o total c/ IVA de cada linha também se escreve (o preço unitário acerta-se sozinho).
  const [totEdit, setTotEdit] = useState<Record<string, string>>({});
  function escreverTotal(l: LinhaForm, txt: string) {
    setTotEdit((t) => ({ ...t, [l.key]: txt }));
    const v = parseDecimal(txt);
    if (v == null || v < 0) return;
    const unit = v / Math.max(1, Number(l.quantidade) || 1);
    const taxa = (parseDecimal(l.iva) ?? IVA_NORMAL) / 100;
    setLinha(l.key, { preco: fmt2(l.porFora || !semIva ? unit : unit / (1 + taxa)) });
  }
  // Pedido do Bruno (18/11): escrever o lucro que quer por unidade (s/ IVA) e o preço acerta-se sozinho.
  const [lucroEdit, setLucroEdit] = useState<Record<string, string>>({});
  function escreverLucroUn(l: LinhaForm, txt: string) {
    setLucroEdit((t) => ({ ...t, [l.key]: txt }));
    const v = parseDecimal(txt);
    if (v == null) return;
    const { custoUnit, taxaCompra } = custoDaLinha(l);
    const custoSemIva = custoUnit / (1 + (l.porFora ? 0 : taxaCompra));
    const taxaVenda = l.porFora ? 0 : (parseDecimal(l.iva) ?? IVA_NORMAL) / 100;
    // 22/11 (Bruno): os portes da compra fazem parte do custo — lucro 0 = vender exatamente ao custo (com portes).
    const portesSemIva = l.compraLinhaId ? (l.portesUnit ?? 0) / (1 + (l.porFora ? 0 : taxaCompra)) : 0;
    const semIvaUn = Math.max(0, custoSemIva + portesSemIva + v);
    setLinha(l.key, { preco: fmt2(l.porFora || semIva ? semIvaUn : semIvaUn * (1 + taxaVenda)) });
  }
  // Sprint 683: o erro de uma ação aparece a vermelho no sítio (cartão Estado e linha com o problema), não só num aviso.
  const [erroAcao, setErroAcao] = useState<{ msg: string; loteId?: string; disponivel?: number } | null>(null);
  function mostrarErro(err: unknown) {
    const msg = apiErrorMessage(err);
    const { code, detalhes } = apiErrorInfo(err);
    const loteId = code === 'stock_insuficiente' && typeof detalhes?.compraLinhaId === 'string' ? detalhes.compraLinhaId : undefined;
    setErroAcao({ msg, loteId, disponivel: typeof detalhes?.disponivel === 'number' ? detalhes.disponivel : undefined });
    toast.error(msg);
    // leva o olhar até à linha com o problema (ou ao cartão Estado)
    setTimeout(() => document.querySelector(loteId ? `[data-lote-erro="${loteId}"]` : '#venda-erro-acao')?.scrollIntoView({ behavior: 'smooth', block: 'center' }), 50);
  }

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
  const inventario = useQuery({ queryKey: ['compras', 'inventario'], queryFn: () => comprasApi.inventario(), enabled: !!lotesPedidos });
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
      const p = partesLinha(l, semIva);
      const qtd = p.qtd;
      const total = qtd * p.cobrado;
      // Linha de serviço: o único custo é o «custo s/ fatura» (vazio = 0) — apagar o campo tem de se ver logo no lucro.
      const servico = !l.compraLinhaId && l.encomenda == null;
      const { custoUnit: custo, taxaCompra: taxa } = custoDaLinha(l);
      const ivaVenda = l.porFora ? 0 : (parseDecimal(l.iva) ?? IVA_NORMAL) / 100;
      const pv = previewVenda(qtd, total, custo, l.porFora ? 0 : taxa, ivaVenda);
      // 20/11 (Bruno): os portes da compra (divididos por igual) contam no custo; no lucro sai a parte sem IVA dedutível.
      const portes = l.compraLinhaId ? qtd * (l.portesUnit ?? 0) : 0;
      const portesSemIva = portes / (1 + (l.porFora ? 0 : taxa));
      return { total, peca: !servico, ...pv, custo: pv.custo + portes, lucro: pv.lucro - portesSemIva, portes };
    });
    const soma = (k: 'total' | 'ivaVenda' | 'ivaAPagar' | 'lucro') => porLinha.reduce((s, c) => s + c[k], 0);
    const custoPecas = porLinha.filter((c) => c.peca).reduce((s, c) => s + c.custo, 0);
    const custoComIva = porLinha.reduce((s, c) => s + c.custo, 0);
    const portes = porLinha.reduce((s, c) => s + c.portes, 0);
    return { porLinha, custoPecas, custoComIva, portes, total: soma('total'), ivaVenda: soma('ivaVenda'), ivaAPagar: soma('ivaAPagar'), lucro: soma('lucro') };
  }, [linhas, semIva]);

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
        precoUnitarioCents: Math.round(partesLinha(l, semIva).cobrado * 100),
        ivaRate: l.porFora ? 0 : parseDecimal(l.iva) ?? IVA_NORMAL,
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
  // 20/11 (Bruno): venda nova também grava sozinha — com cliente ou a 1.ª linha cria-se como orçamento (não mexe no
  // stock) e passa a ser uma venda normal com autosave. Antes, voltar atrás perdia tudo.
  const rascunhoCriado = useRef(false);
  const temConteudo = !!cliente || linhas.some((l) => l.compraLinhaId || l.descricao.trim());
  const chaveRascunho = isNew && valida && temConteudo ? JSON.stringify(buildRequest(VENDA_ESTADO.Orcamento)) : '';
  useEffect(() => {
    if (!chaveRascunho || rascunhoCriado.current || guardar.isPending) return;
    const t = window.setTimeout(() => {
      if (rascunhoCriado.current) return;
      rascunhoCriado.current = true;
      vendasApi.create(buildRequest(VENDA_ESTADO.Orcamento))
        .then((v) => { qc.setQueryData(['venda', v.id], v); void qc.invalidateQueries({ queryKey: ['vendas'] }); navigate(`/vendas/${v.id}`, { replace: true }); })
        .catch(() => { rascunhoCriado.current = false; });
    }, 1500);
    return () => window.clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- só quando o conteúdo muda
  }, [chaveRascunho]);

  useEffect(() => {
    if (rebase > 0) auto.reiniciar({ req: buildRequest(), keys: linhas.map((l) => l.key) });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rebase]);

  const guardar = useMutation({
    mutationFn: (estado?: VendaEstado) => { if (isNew) rascunhoCriado.current = true; return isNew ? vendasApi.create(buildRequest(estado)) : vendasApi.update(id!, buildRequest()); },
    onSuccess: (v) => { setErroAcao(null); onSaved(v, isNew ? 'Venda criada' : 'Venda guardada'); },
    onError: (err) => mostrarErro(err),
  });

  const confirm = useConfirm();
  /** Sprint 662: passar a Pronta/Em curso sem nada a cobrar quase sempre é engano — pergunta antes. */
  async function mudarComAviso(e: VendaEstado) {
    if (e !== VENDA_ESTADO.Orcamento && e !== VENDA_ESTADO.Cancelada && contas.total <= 0 && !(await confirm({
      title: `Passar a «${VENDA_ESTADO_LABEL[e]}» com 0 €?`,
      description: 'Ainda não há nada a cobrar nesta venda. Junta primeiro o valor (ex.: botão «Mão de obra»). Pronta não fecha nem cobra nada — só Entregue fecha.',
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
    onSuccess: (v) => { setConfirmCancel(false); setErroAcao(null); onSaved(v, VENDA_ESTADO_LABEL[v.estado]); },
    onError: (err) => mostrarErro(err),
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
    onSuccess: (v) => { setErroAcao(null); onSaved(v, 'Entregue — fica por receber'); },
    onError: (err) => mostrarErro(err),
  });

  /** 20/11 (Bruno, Junta de Freguesia): venda nova a uma empresa que paga depois — cria e fica «entregue, por receber»,
   *  para emitir já a fatura (por pagar) e o recibo quando pagarem. Antes era preciso guardar orçamento primeiro. */
  const criarPorReceber = useMutation({
    mutationFn: async () => {
      rascunhoCriado.current = true;
      const v = await vendasApi.create(buildRequest(VENDA_ESTADO.Orcamento));
      return vendasApi.mudarEstado(v.id, VENDA_ESTADO.Entregue, pagamento, true);
    },
    onSuccess: (v) => { setErroAcao(null); onSaved(v, 'Entregue — fica por receber. Emite a fatura (por pagar) em baixo.'); },
    onError: (err) => mostrarErro(err),
  });

  const marcarPagamento = useMutation({
    mutationFn: async (porReceber: boolean) => { await auto.gravarJa(); return vendasApi.pagamento(id!, porReceber); },
    onSuccess: (v) => onSaved(v, v.porReceber ? 'Marcada como por receber' : 'Marcada como paga'),
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
      return { ...l, preco: fmt2(novoSemIva ? v / f : v * f) };
    }));
    setSemIva(novoSemIva);
  }

  // Sprint 669: «combinei 75 € com o cliente» — escreve o total e o Mender acerta a linha principal
  // (a mão de obra, ou a última linha) para dar esse total; o lucro sai sozinho do custo real das peças.
  const [totalAlvo, setTotalAlvo] = useState('');
  function aplicarTotal() {
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

  function setLinha(key: string, patch: Partial<LinhaForm>) {
    setLinhas((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  }

  // Sprint 594: "Chegou" numa peça por encomendar — o lote escolhido substitui-a (mantém a descrição e o preço).
  const [aSubstituir, setASubstituir] = useState<string | null>(null);
  // 18/11: «Trocar peça» — a peça que saiu foi outra (ex.: dois suportes ao mesmo preço). O stock acerta-se; a fatura não muda.
  const [trocarItem, setTrocarItem] = useState<string | null>(null);
  async function trocarPara(lote: InventarioLinha) {
    const itemId = trocarItem;
    setTrocarItem(null);
    setPickerOpen(false);
    if (!itemId || !id) return;
    try {
      const v = await vendasApi.trocarLote(id, itemId, lote.linhaId);
      qc.setQueryData(['venda', v.id], v);
      void qc.invalidateQueries({ queryKey: ['compras'] });
      void qc.invalidateQueries({ queryKey: ['artigos'] });
      toast.success('Peça trocada', `Agora sai do lote ${lote.numero} — o stock foi acertado e a fatura fica igual.`);
    } catch (e) {
      toast.fromError(e, 'Não foi possível trocar a peça.');
    }
  }
  function addLote(lote: InventarioLinha, quantidade = 1, precoComIvaDado?: number) {
    if (aSubstituir) {
      const key = aSubstituir;
      setASubstituir(null);
      setLinhas((ls) => ls.map((l) => (l.key === key ? {
        ...l, compraLinhaId: lote.linhaId, custoUnit: lote.precoUnitarioPago, taxaCompra: lote.taxaIvaCompra, portesUnit: lote.portesUnidade ?? 0, autoliqUe: lote.autoliquidacaoUe,
        maxStock: lote.quantidadeEmStock, encomenda: null, custo: '', nota: '', iva: String(IVA_NORMAL),
      } : l)));
      setPickerOpen(false);
      return;
    }
    // 19/11: sem preço no artigo, a proposta (custo + lucro do lote) sobe para o ,90 seguinte — igual às etiquetas.
    const precoComIvaLote = precoComIvaDado ?? lote.precoVendaArtigo ?? precoRedondo(lote.precoFinalComIva);
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(),
      id: null,
      compraLinhaId: lote.linhaId,
      portesUnit: lote.portesUnidade ?? 0,
      autoliqUe: lote.autoliquidacaoUe,
      // Sprint 585: o que sai na fatura, em português ("Ecrã iPhone 14 Pro Max") — editável; o texto do fornecedor fica no lote.
      descricao: comPrefixo(lote.textoVenda ?? lote.descricao, tipo),
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
        </div>
      )}

      {venda?.estado === VENDA_ESTADO.Entregue && venda.atividade !== 1 && (
        venda.porReceber ? (
          <div className="flex flex-wrap items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
            <span className="rounded-full bg-amber-200 px-2 py-0.5 text-xs font-semibold text-amber-900 dark:bg-amber-900/60 dark:text-amber-100">Por receber</span>
            <span className="flex-1">Entregue — o cliente ainda não pagou.</span>
            {venda.moloniDocumentoTipo != null && !venda.moloniReciboNumero ? (
              // 20/11: com fatura do Moloni, «paga» = passar o recibo (senão a fatura fica por pagar no Moloni).
              <Button type="button" size="sm" onClick={() => document.getElementById('documento-fiscal')?.scrollIntoView({ behavior: 'smooth', block: 'center' })}>
                Pagaram? Passar recibo ↓
              </Button>
            ) : (
              <Button type="button" size="sm" loading={marcarPagamento.isPending} onClick={() => marcarPagamento.mutate(false)}>
                Marcar como paga
              </Button>
            )}
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
                    // As linhas do stock acompanham o tipo («Reparação — …», «Serviço — …» ou só o artigo).
                    setLinhas((ls) => ls.map((l) => (l.compraLinhaId ? { ...l, descricao: comPrefixo(l.descricao, t) } : l)));
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
                  <th className="w-28 py-2 pr-2 font-medium" title="Peças do stock: o teu custo (fixo, já pago ao fornecedor). Mão de obra/serviço: o preço por unidade.">Custo / preço un.</th>
                  <th className="w-20 py-2 pr-2 font-medium">IVA %</th>
                  <th className="w-28 py-2 pr-2 text-right font-medium" title="O teu preço de venda desta linha (com IVA) — escreve aqui; o lucro é a diferença para o custo">Venda c/ IVA ✎</th>
                  <th className="w-28 py-2 pr-2 text-right font-medium">IVA ao Estado</th>
                  <th className="w-28 py-2 pr-2 text-right font-medium" title="Lucro por unidade, sem IVA — escreve o que queres ganhar e o preço acerta-se">Lucro un. ✎</th>
                  <th className="w-10" />
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {linhas.map((l, i) => {
                  const c = contas.porLinha[i];
                  return (
                    <tr key={l.key} data-lote-erro={l.compraLinhaId ?? undefined}
                      className={`grid grid-cols-3 gap-x-2 py-2 align-top sm:table-row sm:py-0 ${erroAcao?.loteId && l.compraLinhaId === erroAcao.loteId ? 'bg-rose-50 outline outline-2 outline-rose-400 dark:bg-rose-950/30' : ''}`}>
                      <td className="col-span-3 py-1 sm:py-2 sm:pr-2">
                        <input aria-label="Descrição" value={l.descricao} onChange={(e) => setLinha(l.key, { descricao: e.target.value })} className={inputCls} disabled={fechada} maxLength={300} />
                        {erroAcao?.loteId && l.compraLinhaId === erroAcao.loteId && (
                          <p role="alert" className="mt-1 text-xs font-semibold text-rose-700 dark:text-rose-300">
                            ⚠ Sem stock suficiente: só há {erroAcao.disponivel ?? 0} deste lote. Baixa a quantidade, tira uma das linhas iguais ou troca por outro lote em «Do stock».
                          </p>
                        )}
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
                            {l.compraLinhaId ? (
                              <>Stock · pagaste <strong className="text-sm text-zinc-900 dark:text-zinc-100">{formatEur(l.custoUnit)}/un.</strong>
                                {l.autoliqUe || l.taxaCompra === 0
                                  ? <span title="Fornecedor da UE: a fatura vem sem IVA (autoliquidação — o IVA declara-se e deduz-se na mesma declaração, não te custa nada). O teu custo é este valor."> · fornecedor UE, sem IVA na fatura</span>
                                  : <> (c/ IVA) · {formatEur(l.custoUnit / (1 + l.taxaCompra))} s/ IVA</>}
                                {(l.portesUnit ?? 0) > 0 && (
                                  <span className="ml-1 font-semibold text-amber-700 dark:text-amber-300" title="Parte dos portes da compra que cabe a esta unidade — conta com isto no preço">
                                    + {formatEur(l.portesUnit!)} portes
                                  </span>
                                )}
                                {l.id && venda && venda.estado !== VENDA_ESTADO.Cancelada && isAdmin && (
                                  <button type="button" className="ml-2 font-semibold text-brand-600 hover:underline dark:text-brand-300"
                                    title="Saiu outra peça (ex.: outro modelo ao mesmo preço)? Escolhe o lote certo — o stock acerta-se e a fatura não muda."
                                    onClick={() => { setTrocarItem(l.id!); setPickerOpen(true); }}>
                                    Trocar peça
                                  </button>
                                )}</>
                            ) : semIva ? 'Serviço · IVA somado ao preço' : 'Serviço · IVA incluído no preço'}
                          </span>
                        )}
                        <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                          {!l.compraLinhaId && l.encomenda == null && (
                            <label className="inline-flex items-center gap-1 text-zinc-600 dark:text-zinc-300"
                              title="Quanto te custou esta linha (material, transporte…) — desconta no lucro">
                              custo
                              <input aria-label="Custo da linha" inputMode="decimal" value={l.custoSemFatura} disabled={fechada} placeholder="0,00"
                                onChange={(e) => setLinha(l.key, { custoSemFatura: e.target.value })}
                                className="min-h-8 w-20 rounded-md border border-zinc-300 bg-white px-2 text-xs dark:border-zinc-700 dark:bg-zinc-950" />
                              €
                            </label>
                          )}
                        </div>
                      </td>
                      <td className="py-1 sm:py-2 sm:pr-2">
                        <span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">Qtd</span>
                        {l.compraLinhaId && l.maxStock != null && l.maxStock <= 1 && Number(l.quantidade) <= 1 ? (
                          // 19/11 (Bruno): só há 1 no stock → a quantidade não se escreve, é fixa.
                          <span className="flex min-h-10 items-center px-1 font-semibold tabular-nums" title="Só há 1 unidade deste lote no stock">1</span>
                        ) : (
                          <input aria-label="Quantidade" inputMode="numeric" value={l.quantidade} onChange={(e) => setLinha(l.key, { quantidade: e.target.value.replace(/\D/g, '') })} className={inputCls} disabled={fechada} />
                        )}
                        {l.maxStock != null && Number(l.quantidade) > l.maxStock && <span className="mt-1 block text-xs text-rose-600">só {l.maxStock}</span>}
                      </td>
                      <td className="py-1 sm:py-2 sm:pr-2">
                        {l.compraLinhaId ? (
                          // 19/11 (Bruno): numa peça do stock o valor por unidade é o que pagaste ao fornecedor — fixo.
                          // O preço de venda escreve-se em «Venda c/ IVA»; o lucro é a diferença.
                          <>
                            <span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">Custo un. (fixo)</span>
                            <span className="flex min-h-10 items-center rounded-lg bg-zinc-100 px-2 font-semibold tabular-nums text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200"
                              title="O que pagaste ao fornecedor por unidade — não muda com o preço de venda">
                              {fmt2(l.custoUnit)}
                            </span>
                            <span className="mt-1 block text-[11px] text-zinc-500">custo fixo</span>
                          </>
                        ) : (
                          <><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">{semIva ? 'Preço s/ IVA' : 'Preço c/ IVA'}</span><input aria-label={semIva ? 'Preço unitário sem IVA' : 'Preço unitário com IVA'} inputMode="decimal" value={l.preco} onChange={(e) => setLinha(l.key, { preco: e.target.value })} className={inputCls} placeholder="0,00" disabled={fechada} /></>
                        )}
                      </td>
                      <td className="py-1 sm:py-2 sm:pr-2">
                        <span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">IVA</span>
                        <select aria-label="Taxa de IVA" value={l.iva} onChange={(e) => setLinha(l.key, { iva: e.target.value })} className={inputCls} disabled={fechada || !!l.compraLinhaId}>
                          {['23', '13', '6', '0'].map((t) => <option key={t} value={t}>{t}%</option>)}
                        </select>
                      </td>
                      <td className="py-1 font-medium tabular-nums sm:py-2 sm:pr-2 sm:text-right"><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">Venda c/ IVA</span>
                        <input aria-label="Total da linha com IVA" inputMode="decimal" disabled={fechada}
                          value={totEdit[l.key] ?? fmt2(c.total)}
                          onChange={(e) => escreverTotal(l, e.target.value)}
                          onBlur={() => setTotEdit((t) => { const n = { ...t }; delete n[l.key]; return n; })}
                          className={`${inputCls} text-right font-semibold`} />
                        {(() => {
                          const q = Math.max(1, Number(l.quantidade) || 1);
                          const unitCom = c.total / q;
                          const unitSem = unitCom / (1 + (parseDecimal(l.iva) ?? IVA_NORMAL) / 100);
                          return <span className="mt-1 block text-[11px] font-normal text-zinc-500">{q > 1 ? `venda un. ${fmt2(unitCom)} · ` : ''}{fmt2(unitSem)} s/ IVA</span>;
                        })()}
                      </td>
                      <td className="py-1 tabular-nums sm:py-2 sm:pr-2 sm:pt-4 sm:text-right"><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">IVA ao Estado</span>{formatEur(c.ivaAPagar)}</td>
                      <td className="py-1 tabular-nums sm:py-2 sm:pr-2 sm:text-right"><span className="mb-1 block text-[11px] text-zinc-500 sm:hidden">Lucro un. (s/ IVA)</span>
                        <input aria-label="Lucro por unidade sem IVA" inputMode="decimal" disabled={fechada}
                          value={lucroEdit[l.key] ?? fmt2(c.lucro / Math.max(1, Number(l.quantidade) || 1))}
                          onChange={(e) => escreverLucroUn(l, e.target.value)}
                          onBlur={() => setLucroEdit((t) => { const n = { ...t }; delete n[l.key]; return n; })}
                          className={`${inputCls} text-right font-semibold ${c.lucro < 0 ? 'text-rose-600' : 'text-emerald-700 dark:text-emerald-400'}`} />
                        <span className={`mt-1 block text-[11px] font-normal ${c.lucro < 0 ? 'text-rose-600' : 'text-zinc-500'}`}>linha {formatEur(c.lucro)}</span>
                      </td>
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
            <Button type="button" size="sm" onClick={aplicarTotal} disabled={!totalAlvo.trim()}>Aplicar</Button>
            <p className="w-full text-[11px] text-brand-800/80 dark:text-brand-200/80">Escreves o total que combinaste com o cliente (com IVA): a mão de obra acerta-se sozinha, as peças do stock mantêm o preço, e o IVA e o lucro recalculam-se.</p>
          </div>
        )}
        <dl className="mt-3 ml-auto grid max-w-xs grid-cols-[1fr_auto] gap-x-6 gap-y-1 text-sm tabular-nums">
          <dt className="font-medium">Total a cobrar (c/ IVA)</dt><dd className="text-right text-lg font-semibold">{formatEur(contas.total)}</dd>
          <dt className="text-zinc-500">Total sem IVA</dt><dd className="text-right">{formatEur(contas.total - contas.ivaVenda)}</dd>
          {/* 22/11 (Bruno): IVA e IVA a entregar sempre os dois; o custo já leva os portes repartidos pelos produtos. */}
          <dt className="text-zinc-500">IVA</dt><dd className="text-right">{formatEur(contas.ivaVenda)}</dd>
          <dt className="text-zinc-500" title="IVA da venda − IVA já pago ao fornecedor (fornecedor UE/sem IVA: igual ao IVA)">IVA a entregar</dt><dd className="text-right">{formatEur(contas.ivaAPagar)}</dd>
          <dt className="text-zinc-500" title="O que pagaste pelos produtos (c/ IVA), já com a parte dos portes da compra">Custo dos produtos c/ IVA</dt><dd className="text-right">{formatEur(contas.custoComIva)}</dd>
          <dt className="font-medium" title="Total − custo das peças − IVA a entregar">Lucro</dt><dd className={`text-right font-semibold ${contas.lucro < 0 ? 'text-rose-600' : 'text-emerald-600'}`}>{formatEur(contas.lucro)}</dd>
          <dt className="text-zinc-500">Margem</dt><dd className="text-right">{contas.total - contas.ivaVenda > 0 ? `${Math.round((contas.lucro / (contas.total - contas.ivaVenda)) * 100)} %` : '—'}</dd>
        </dl>
      </SectionCard>

      {isNew && (
        <SectionCard title="Concluir">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
            <p className="text-sm text-zinc-500">"Guardar orçamento" não mexe no stock. Venda feita na hora? Escolhe o pagamento e regista como entregue.</p>
            <div className="flex flex-col gap-2">
              <PagamentoPicker value={pagamento} onChange={setPagamento} />
              <Button type="button" leftIcon={<CheckCircle2 size={15} />} disabled={!podeGravar || linhas.length === 0 || criarPorReceber.isPending} loading={guardar.isPending && guardar.variables === VENDA_ESTADO.Entregue} onClick={() => guardar.mutate(VENDA_ESTADO.Entregue)}>
                Entregue & paga
              </Button>
              <Button type="button" variant="secondary" disabled={!podeGravar || linhas.length === 0 || guardar.isPending} loading={criarPorReceber.isPending} onClick={() => criarPorReceber.mutate()}
                title="Empresa/Junta: emites a fatura agora e o recibo quando pagarem">
                Entregue, paga depois (fatura agora)
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
          </div>
          <div className="mt-4 space-y-2 border-t border-zinc-200 pt-3 dark:border-zinc-800">
            <span className={labelCls}>Pago com</span>
            <PagamentoPicker value={pagamento} onChange={setPagamento} disabled={busy} />
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
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
          {erroAcao && (
            <div id="venda-erro-acao" role="alert" className="mt-3 flex items-start gap-2 rounded-lg border-2 border-rose-400 bg-rose-50 px-3 py-2 text-sm font-medium text-rose-800 dark:border-rose-700 dark:bg-rose-950/40 dark:text-rose-200">
              <XCircle size={18} className="mt-0.5 shrink-0" />
              <span className="flex-1">{erroAcao.msg}</span>
              <button type="button" className="text-xs underline" onClick={() => setErroAcao(null)}>Fechar</button>
            </div>
          )}
          <p className="mt-2 text-xs text-zinc-500">Ao sair de Orçamento as peças saem do stock. Entregue fecha a venda (as linhas deixam de se poder editar).</p>
          {isAdmin && <div className="mt-3 border-t border-zinc-200 pt-3 dark:border-zinc-800">{cancelarUi}</div>}
        </SectionCard>
      )}

      {tipo !== VENDA_TIPO.Produto && (!fechada || (venda?.adiantamentoCents ?? 0) > 0) && (
        <SectionCard title="Adiantamento (para encomendar peças)" collapsible defaultOpen={(venda?.adiantamentoCents ?? 0) > 0}>
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

      {venda && (
        <div id="documento-fiscal"><DocumentoMoloni venda={venda} isAdmin={isAdmin} onChange={(v) => {
          qc.invalidateQueries({ queryKey: ['vendas'] });
          qc.setQueryData(['venda', v.id], v);
        }} /></div>
      )}

      {venda && venda.estado === VENDA_ESTADO.Entregue && !venda.moloniDocumentoTipo && (
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

      <LotePicker open={pickerOpen} onClose={() => { setPickerOpen(false); setASubstituir(null); setTrocarItem(null); }}
        onPick={(lote) => (trocarItem ? void trocarPara(lote) : addLote(lote))} reservado={reservado} />
    </form>
  );
}
