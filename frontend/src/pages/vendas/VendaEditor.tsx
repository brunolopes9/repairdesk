import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Boxes, CheckCircle2, FileText, Save, Trash2, Wrench, XCircle } from 'lucide-react';
import { BackButton, Button, PageHeader, SectionCard, SkeletonCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { useAuth } from '../../lib/auth/AuthContext';
import { apiErrorMessage } from '../../lib/errors';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { toast } from '../../lib/toast';
import { formatEur, parseDecimal, previewVenda } from '../../lib/compras/format';
import type { InventarioLinha } from '../../lib/compras/types';
import { vendasApi } from '../../lib/vendas/api';
import {
  PAYMENT_METHOD,
  PAYMENT_METHOD_LABEL,
  VENDA_ESTADO,
  VENDA_ESTADO_COLOR,
  VENDA_ESTADO_LABEL,
  VENDA_FLUXO,
  VENDA_TIPO,
  VENDA_TIPO_LABEL,
  type PaymentMethod,
  type Venda,
  type VendaEstado,
  type VendaTipo,
  type VendaWrite,
} from '../../lib/vendas/types';
import ClienteSelect, { type ClienteEscolhido } from './ClienteSelect';
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
}

const num = (cents: number) => (cents / 100).toFixed(2).replace('.', ',');
const toCents = (v: string) => Math.round((parseDecimal(v) ?? 0) * 100);

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
    maxStock: null,
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
  const [linhas, setLinhas] = useState<LinhaForm[]>([]);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [pagamento, setPagamento] = useState<PaymentMethod>(PAYMENT_METHOD.MBWay);
  const [fatura, setFatura] = useState('');
  const [faturaData, setFaturaData] = useState(new Date().toISOString().slice(0, 10));
  const [confirmCancel, setConfirmCancel] = useState(false);

  useEffect(() => {
    if (!venda) return;
    setTipo(venda.tipo);
    setCliente(venda.cliente ? { id: venda.cliente.id, nome: venda.cliente.nome } : null);
    setEquipamento(venda.equipamento ?? '');
    setProblema(venda.problema ?? '');
    setNotas(venda.notas ?? '');
    setPrevisto(venda.previstoPara ? venda.previstoPara.slice(0, 10) : '');
    setLinhas(linhasFrom(venda));
    setFatura(venda.invoiceNumber ?? '');
    if (venda.invoiceEmittedAt) setFaturaData(venda.invoiceEmittedAt.slice(0, 10));
  }, [venda]);

  const fechada = venda ? venda.estado === VENDA_ESTADO.Entregue || venda.estado === VENDA_ESTADO.Cancelada : false;

  const contas = useMemo(() => {
    const porLinha = linhas.map((l) => {
      const qtd = Number(l.quantidade) || 0;
      const total = qtd * (parseDecimal(l.preco) ?? 0);
      return { total, ...previewVenda(qtd, total, l.custoUnit, l.taxaCompra, (parseDecimal(l.iva) ?? IVA_NORMAL) / 100) };
    });
    const soma = (k: 'total' | 'ivaVenda' | 'ivaAPagar' | 'lucro') => porLinha.reduce((s, c) => s + c[k], 0);
    return { porLinha, total: soma('total'), ivaVenda: soma('ivaVenda'), ivaAPagar: soma('ivaAPagar'), lucro: soma('lucro') };
  }, [linhas]);

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
      linhas: linhas.map((l) => ({
        id: l.id,
        compraLinhaId: l.compraLinhaId,
        descricao: l.descricao.trim() || null,
        quantidade: Number(l.quantidade) || 0,
        precoUnitarioCents: toCents(l.preco),
        ivaRate: parseDecimal(l.iva) ?? IVA_NORMAL,
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

  const guardar = useMutation({
    mutationFn: (estado?: VendaEstado) => (isNew ? vendasApi.create(buildRequest(estado)) : vendasApi.update(id!, buildRequest())),
    onSuccess: (v) => onSaved(v, isNew ? 'Venda criada' : 'Venda guardada'),
    onError: (err) => toast.fromError(err),
  });

  const mudarEstado = useMutation({
    mutationFn: async (estado: VendaEstado) => {
      // Grava alterações pendentes antes de mudar de estado (exceto ao cancelar).
      if (!fechada && estado !== VENDA_ESTADO.Cancelada) await vendasApi.update(id!, buildRequest());
      return vendasApi.mudarEstado(id!, estado, estado === VENDA_ESTADO.Entregue ? pagamento : undefined);
    },
    onSuccess: (v) => { setConfirmCancel(false); onSaved(v, VENDA_ESTADO_LABEL[v.estado]); },
    onError: (err) => toast.fromError(err),
  });

  const registarFatura = useMutation({
    mutationFn: () => vendasApi.registarFatura(id!, fatura.trim() || null, faturaData),
    onSuccess: (v) => onSaved(v, v.invoiceNumber ? 'Fatura registada' : 'Fatura removida'),
    onError: (err) => toast.fromError(err),
  });

  function setLinha(key: string, patch: Partial<LinhaForm>) {
    setLinhas((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  }

  function addLote(lote: InventarioLinha) {
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(),
      id: null,
      compraLinhaId: lote.linhaId,
      descricao: lote.descricao,
      quantidade: '1',
      preco: (Math.round(lote.precoFinalComIva * 100) / 100).toFixed(2).replace('.', ','),
      iva: String(IVA_NORMAL),
      custoUnit: lote.precoUnitarioPago,
      taxaCompra: lote.taxaIvaCompra,
      maxStock: lote.quantidadeEmStock,
    }]);
    setPickerOpen(false);
  }

  function addServico() {
    setLinhas((ls) => [...ls, {
      key: crypto.randomUUID(), id: null, compraLinhaId: null,
      descricao: tipo === VENDA_TIPO.Reparacao ? 'Mão de obra' : '',
      quantidade: '1', preco: '', iva: String(IVA_NORMAL), custoUnit: 0, taxaCompra: 0, maxStock: null,
    }]);
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    guardar.mutate(isNew ? VENDA_ESTADO.Orcamento : undefined);
  }

  const precisaCliente = tipo === VENDA_TIPO.Reparacao && !cliente;
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
        title={isNew ? `Nova ${VENDA_TIPO_LABEL[tipo].toLowerCase()}` : `${VENDA_TIPO_LABEL[venda!.tipo]} #${venda!.numero}`}
        meta={venda && <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${VENDA_ESTADO_COLOR[venda.estado]}`}>{VENDA_ESTADO_LABEL[venda.estado]}</span>}
        actions={(
          <>
          {venda && (
            <Button type="button" variant="secondary" leftIcon={<FileText size={15} />} onClick={() => openPdfInNewTab(vendasApi.pdfPath(venda.id, 'orcamento'))}>
              Orçamento PDF
            </Button>
          )}
          {!fechada && (
          <Button type="submit" variant={isNew ? 'secondary' : 'primary'} loading={guardar.isPending && guardar.variables !== VENDA_ESTADO.Entregue} disabled={!podeGravar} leftIcon={<Save size={15} />}>
            {isNew ? 'Guardar orçamento' : 'Guardar'}
          </Button>
          )}
          </>
        )}
      />

      {venda?.faturaPorRegistar && (
        <div role="alert" className="flex items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          <AlertTriangle size={16} className="mt-0.5 shrink-0" />
          Falta registar o nº da fatura desta venda. Não existe venda sem fatura.
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
                  onClick={() => setTipo(Number(v) as VendaTipo)}
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
          </div>
        )}
      >
        {linhas.length === 0 ? (
          <p className="py-4 text-center text-sm text-zinc-500">
            Junta peças/artigos do stock ou {tipo === VENDA_TIPO.Reparacao ? 'mão de obra' : 'um serviço'}.
          </p>
        ) : (
          <div className="-mx-4 overflow-x-auto px-4">
            <table className="w-full min-w-[46rem] text-sm">
              <thead className="text-left text-xs text-zinc-500">
                <tr>
                  <th className="py-2 pr-2 font-medium">Descrição</th>
                  <th className="w-16 py-2 pr-2 font-medium">Qtd</th>
                  <th className="w-28 py-2 pr-2 font-medium">Preço un. c/ IVA</th>
                  <th className="w-20 py-2 pr-2 font-medium">IVA %</th>
                  <th className="w-24 py-2 pr-2 text-right font-medium">Total</th>
                  <th className="w-28 py-2 pr-2 text-right font-medium">IVA ao Estado</th>
                  <th className="w-24 py-2 pr-2 text-right font-medium">Lucro</th>
                  <th className="w-10" />
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {linhas.map((l, i) => {
                  const c = contas.porLinha[i];
                  return (
                    <tr key={l.key} className="align-top">
                      <td className="py-2 pr-2">
                        <input aria-label="Descrição" value={l.descricao} onChange={(e) => setLinha(l.key, { descricao: e.target.value })} className={inputCls} disabled={fechada} maxLength={300} />
                        <span className="mt-1 block text-xs text-zinc-500">
                          {l.compraLinhaId ? `Stock · pago ${formatEur(l.custoUnit)}/un.` : 'Serviço · IVA sobre o valor todo'}
                        </span>
                      </td>
                      <td className="py-2 pr-2">
                        <input aria-label="Quantidade" inputMode="numeric" value={l.quantidade} onChange={(e) => setLinha(l.key, { quantidade: e.target.value.replace(/\D/g, '') })} className={inputCls} disabled={fechada} />
                        {l.maxStock != null && Number(l.quantidade) > l.maxStock && <span className="mt-1 block text-xs text-rose-600">só {l.maxStock}</span>}
                      </td>
                      <td className="py-2 pr-2"><input aria-label="Preço unitário com IVA" inputMode="decimal" value={l.preco} onChange={(e) => setLinha(l.key, { preco: e.target.value })} className={inputCls} placeholder="0,00" disabled={fechada} /></td>
                      <td className="py-2 pr-2">
                        <select aria-label="Taxa de IVA" value={l.iva} onChange={(e) => setLinha(l.key, { iva: e.target.value })} className={inputCls} disabled={fechada || !!l.compraLinhaId}>
                          {['23', '13', '6', '0'].map((t) => <option key={t} value={t}>{t}%</option>)}
                        </select>
                      </td>
                      <td className="py-2 pr-2 pt-4 text-right font-medium tabular-nums">{formatEur(c.total)}</td>
                      <td className="py-2 pr-2 pt-4 text-right tabular-nums">{formatEur(c.ivaAPagar)}</td>
                      <td className={`py-2 pr-2 pt-4 text-right tabular-nums ${c.lucro < 0 ? 'text-rose-600' : ''}`}>{formatEur(c.lucro)}</td>
                      <td className="py-2">
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
        <dl className="mt-3 ml-auto grid max-w-xs grid-cols-[1fr_auto] gap-x-6 gap-y-1 text-sm tabular-nums">
          <dt className="font-medium">Total a cobrar</dt><dd className="text-right text-lg font-semibold">{formatEur(contas.total)}</dd>
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
              <Button key={e} type="button" variant="secondary" disabled={busy || !podeGravar} onClick={() => mudarEstado.mutate(e)}>
                {VENDA_ESTADO_LABEL[e]}
              </Button>
            ))}
            <span className="mx-1 hidden h-6 w-px bg-zinc-200 sm:block dark:bg-zinc-700" />
            <select aria-label="Método de pagamento" value={pagamento} onChange={(e) => setPagamento(Number(e.target.value) as PaymentMethod)} className={`${inputCls} w-auto`}>
              {Object.entries(PAYMENT_METHOD_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
            </select>
            <Button type="button" leftIcon={<CheckCircle2 size={15} />} disabled={busy || !podeGravar || linhas.length === 0} loading={mudarEstado.isPending && mudarEstado.variables === VENDA_ESTADO.Entregue} onClick={() => mudarEstado.mutate(VENDA_ESTADO.Entregue)}>
              Entregue & paga
            </Button>
          </div>
          <p className="mt-2 text-xs text-zinc-500">Ao sair de Orçamento as peças saem do stock. Entregue fecha a venda (as linhas deixam de se poder editar).</p>
        </SectionCard>
      )}

      {venda && venda.estado === VENDA_ESTADO.Entregue && (
        <SectionCard title="Fatura">
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

      {venda && venda.estado !== VENDA_ESTADO.Cancelada && isAdmin && (
        <div className="flex justify-end">
          {!confirmCancel ? (
            <Button type="button" variant="ghost" leftIcon={<XCircle size={15} />} onClick={() => setConfirmCancel(true)}>Cancelar venda</Button>
          ) : (
            <div role="alert" className="flex flex-col gap-2 rounded-xl border border-rose-300 bg-rose-50 p-3 text-sm text-rose-900 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-200 sm:flex-row sm:items-center">
              <span>
                Cancelar devolve as peças ao stock.
                {venda.estado === VENDA_ESTADO.Entregue && ' Se a fatura já foi emitida, emite a nota de crédito no Moloni.'}
              </span>
              <div className="flex gap-2">
                <Button type="button" size="sm" variant="secondary" onClick={() => setConfirmCancel(false)}>Não</Button>
                <Button type="button" size="sm" variant="danger" loading={mudarEstado.isPending} onClick={() => mudarEstado.mutate(VENDA_ESTADO.Cancelada)}>Cancelar venda</Button>
              </div>
            </div>
          )}
        </div>
      )}

      {venda && venda.tipo === VENDA_TIPO.Reparacao && <ReparacaoExtras venda={venda} />}

      <LotePicker open={pickerOpen} onClose={() => setPickerOpen(false)} onPick={addLote} reservado={reservado} />
    </form>
  );
}
