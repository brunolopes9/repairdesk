import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CreditCard, Download, FileText, Minus, Plus, Receipt, Search, ShoppingCart, Trash2, UserRound, XCircle, CheckCircle2, History as HistoryIcon } from 'lucide-react';
import { downloadFile } from '../../lib/downloadPdf';
import { toast } from '../../lib/toast';
import { clientesApi } from '../../lib/clientes/api';
import type { Cliente } from '../../lib/clientes/types';
import { formatCents } from '../../lib/money';
import { stockApi } from '../../lib/stock/api';
import { tenantSettingsApi } from '../../lib/tenantSettings/api';
import { PART_CATEGORIA_REQUER_IMEI, type Part } from '../../lib/stock/types';
import { isValidImei, normalizeImei } from '../../lib/imei';
import { Link } from 'react-router-dom';
import { vendasApi, type VendaImeiLookup, type VendaReparacaoRelacionada } from '../../lib/vendas/api';
import { STATUS_LABEL, STATUS_COLOR } from '../../lib/reparacoes/types';
import { CONDICAO_ARTIGO, CONDICAO_ARTIGO_LABEL, PAYMENT_METHOD, VENDA_ORIGEM, VENDA_ORIGEM_LABEL, VENDA_STATUS, type CondicaoArtigo, type PaymentMethod, type Venda } from '../../lib/vendas/types';
import GarantiaCard from '../../components/GarantiaCard';
import NovoClienteModal from '../../components/NovoClienteModal';
import { SkeletonTable } from '../../components/ui';

type CartLine = {
  part: Part;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents: number;
  ivaRate: number;
  imei?: string;
  imei2?: string;
  fornecedorNome?: string;
  condicao?: CondicaoArtigo;
  garantiaFornecedorAteAo?: string;
};

const paymentOptions: Array<{ value: PaymentMethod; label: string }> = [
  { value: PAYMENT_METHOD.Dinheiro, label: 'Numerario' },
  { value: PAYMENT_METHOD.MBWay, label: 'MBWay' },
  { value: PAYMENT_METHOD.Multibanco, label: 'Multibanco' },
  { value: PAYMENT_METHOD.TransferenciaBancaria, label: 'Transferencia' },
  { value: PAYMENT_METHOD.Cartao, label: 'Cartao' },
];

export default function Vendas() {
  const qc = useQueryClient();
  const [q, setQ] = useState('');
  const [clienteQ, setClienteQ] = useState('');
  const [cliente, setCliente] = useState<Cliente | null>(null);
  const [novoClienteOpen, setNovoClienteOpen] = useState(false);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>(PAYMENT_METHOD.MBWay);
  const [lastVenda, setLastVenda] = useState<Venda | null>(null);

  const parts = useQuery({
    queryKey: ['vendas-parts', q],
    queryFn: () => stockApi.list({ q, pageSize: 12 }),
    staleTime: 10_000,
  });

  const billing = useQuery({
    queryKey: ['tenant-billing-settings'],
    queryFn: () => tenantSettingsApi.getBilling(),
    staleTime: 5 * 60_000,
  });

  const clientes = useQuery({
    queryKey: ['vendas-clientes', clienteQ],
    queryFn: () => clientesApi.list(clienteQ, 1, 8),
    enabled: clienteQ.trim().length >= 2,
    staleTime: 10_000,
  });

  const fornecedoresQuery = useQuery({
    queryKey: ['vendas-fornecedores'],
    queryFn: () => vendasApi.fornecedores(),
    staleTime: 5 * 60_000,
  });

  const totalCents = useMemo(
    () => cart.reduce((sum, l) => sum + Math.max(0, l.quantidade * l.precoUnitarioCents - l.descontoCents), 0),
    [cart],
  );
  const ivaCents = useMemo(
    () =>
      cart.reduce((sum, l) => {
        if (l.ivaRate <= 0) return sum;
        const gross = Math.max(0, l.quantidade * l.precoUnitarioCents - l.descontoCents);
        return sum + Math.round(gross - gross / (1 + l.ivaRate / 100));
      }, 0),
    [cart],
  );

  const cobrar = useMutation({
    mutationFn: async () => {
      if (cart.length === 0) throw new Error('Carrinho vazio.');

      // Validação client-side IMEI antes de submeter — evita 422 do backend.
      for (const line of cart) {
        if (PART_CATEGORIA_REQUER_IMEI.has(line.part.categoria)) {
          if (!line.imei || !isValidImei(line.imei)) {
            throw new Error(`IMEI inválido ou em falta para ${line.part.nome}.`);
          }
          if (line.imei2 && !isValidImei(line.imei2)) {
            throw new Error(`IMEI secundário inválido para ${line.part.nome}.`);
          }
        }
      }

      const venda = await vendasApi.create({
        clienteId: cliente?.id ?? null,
        notas: null,
        items: cart.map((line) => ({
          partId: line.part.id,
          descricao: line.part.nome,
          quantidade: line.quantidade,
          precoUnitarioCents: line.precoUnitarioCents,
          descontoCents: line.descontoCents,
          ivaRate: line.ivaRate,
          imei: line.imei ? normalizeImei(line.imei) : null,
          imei2: line.imei2 ? normalizeImei(line.imei2) : null,
          fornecedorNome: line.fornecedorNome?.trim() || null,
          condicao: line.condicao ?? null,
          garantiaFornecedorAteAo: line.garantiaFornecedorAteAo || null,
        })),
      });
      return vendasApi.marcarPaga(venda.id, paymentMethod, false);
    },
    onSuccess: (res) => {
      setLastVenda(res.venda);
      setCart([]);
      qc.invalidateQueries({ queryKey: ['vendas-parts'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      toast.success(`Venda #${String(res.venda.numero).padStart(5, '0')} paga`);
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'Nao foi possivel cobrar.'),
  });

  const emitirFatura = useMutation({
    mutationFn: (id: string) => vendasApi.emitirFatura(id),
    onSuccess: (invoice) => {
      toast.success(`Fatura ${invoice.number} emitida`);
      if (invoice.pdfUrl) window.open(invoice.pdfUrl, '_blank', 'noopener,noreferrer');
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['vendas-historico'] });
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'Nao foi possivel emitir fatura.'),
  });

  // Histórico de vendas — últimos 30 dias por defeito
  const [historicoFrom, setHistoricoFrom] = useState<string>(() => {
    const d = new Date();
    d.setDate(d.getDate() - 30);
    return d.toISOString().slice(0, 10);
  });
  const [historicoTo, setHistoricoTo] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [vendaDetalhe, setVendaDetalhe] = useState<Venda | null>(null);

  const historico = useQuery({
    queryKey: ['vendas-historico', historicoFrom, historicoTo],
    queryFn: () => vendasApi.list({
      from: `${historicoFrom}T00:00:00Z`,
      to: `${historicoTo}T23:59:59Z`,
      page: 1,
      pageSize: 50,
    }),
    staleTime: 15_000,
  });

  const cancelar = useMutation({
    mutationFn: (id: string) => vendasApi.cancelar(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['vendas-historico'] });
      qc.invalidateQueries({ queryKey: ['vendas-parts'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setVendaDetalhe(null);
      toast.success('Venda cancelada', 'O stock foi reposto.');
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'Nao foi possivel cancelar.'),
  });

  const anularFatura = useMutation({
    mutationFn: (id: string) => vendasApi.anularFatura(id),
    onSuccess: (venda) => {
      qc.invalidateQueries({ queryKey: ['vendas-historico'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setVendaDetalhe(venda);
      toast.success('Fatura anulada no Moloni', 'documentCancel ou NC emitida. O documento já não aparece no Relatório IVA.');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível anular fatura.'),
  });

  const limparFaturaLocal = useMutation({
    mutationFn: (id: string) => vendasApi.limparFaturaLocal(id),
    onSuccess: (venda) => {
      qc.invalidateQueries({ queryKey: ['vendas-historico'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setVendaDetalhe(venda);
      toast.success('Referência limpa', 'Venda removida do Relatório IVA do RepairDesk. (Moloni não foi chamado.)');
    },
    onError: (err) => toast.fromError(err, 'Não foi possível limpar referência.'),
  });

  function addPart(part: Part) {
    if (part.qtdStock <= 0) {
      toast.warning('Esta peca esta sem stock.');
      return;
    }

    setCart((current) => {
      const existing = current.find((l) => l.part.id === part.id);
      if (existing) {
        if (existing.quantidade >= part.qtdStock) {
          toast.warning(`So existem ${part.qtdStock} em stock.`);
          return current;
        }
        return current.map((l) => (l.part.id === part.id ? { ...l, quantidade: l.quantidade + 1 } : l));
      }

      return [
        ...current,
        {
          part,
          quantidade: 1,
          precoUnitarioCents: Math.max(part.custoUnitarioCents, 0),
          descontoCents: 0,
          ivaRate: 23,
        },
      ];
    });
  }

  function updateQty(partId: string, delta: number) {
    setCart((current) =>
      current
        .map((l) => {
          if (l.part.id !== partId) return l;
          const next = Math.max(1, Math.min(l.part.qtdStock, l.quantidade + delta));
          return { ...l, quantidade: next };
        })
        .filter((l) => l.quantidade > 0),
    );
  }

  function updateImei(partId: string, value: string, slot: 'imei' | 'imei2') {
    setCart((c) => c.map((l) => l.part.id === partId ? { ...l, [slot]: value } : l));
  }

  // Debounced IMEI duplicate lookup (warning, não bloqueia).
  const [imeiWarnings, setImeiWarnings] = useState<Record<string, VendaImeiLookup | null>>({});
  useEffect(() => {
    const handles: number[] = [];
    cart.forEach((line) => {
      const imei = line.imei?.trim();
      if (!imei || !isValidImei(imei)) {
        setImeiWarnings((w) => {
          if (!(line.part.id in w)) return w;
          const next = { ...w };
          delete next[line.part.id];
          return next;
        });
        return;
      }
      const h = window.setTimeout(async () => {
        try {
          const hit = await vendasApi.imeiLookup(normalizeImei(imei));
          setImeiWarnings((w) => ({ ...w, [line.part.id]: hit }));
        } catch {
          /* silencio — não bloqueia operação */
        }
      }, 400);
      handles.push(h);
    });
    return () => handles.forEach((h) => window.clearTimeout(h));
  }, [cart]);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Vendas</h1>
          <p className="text-sm text-zinc-500">POS rapido para acessorios, pecas e equipamentos.</p>
        </div>
        {lastVenda && (
          <div className="flex gap-2">
            <a
              href={vendasApi.reciboUrl(lastVenda.id)}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-2 rounded-md border border-zinc-200 px-3 py-2 text-sm hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-900"
            >
              <Receipt size={16} /> Recibo
            </a>
            {!lastVenda.invoiceExternalId && (
              <button
                type="button"
                onClick={() => {
                  const isSandbox = billing.data?.sandboxMode === true;
                  const ok = confirm(
                    isSandbox
                      ? 'MODO SANDBOX — fatura de teste\n\n' +
                        'Não é comunicada à AT real. Útil para validar o fluxo.\n\n' +
                        `Total: ${formatCents(lastVenda.totalCents)}\nContinuar?`
                      : 'ATENÇÃO: MODO PRODUÇÃO — fatura real à AT\n\n' +
                        'Vai ser comunicada em tempo real. Entra na tua declaração IVA.\n\n' +
                        `Total: ${formatCents(lastVenda.totalCents)}\n` +
                        `Cliente: ${lastVenda.cliente?.nome ?? 'Consumidor final'}\n\nTem a certeza?`
                  );
                  if (ok) emitirFatura.mutate(lastVenda.id);
                }}
                className="inline-flex items-center gap-2 rounded-md bg-brand-600 px-3 py-2 text-sm font-medium text-white hover:bg-brand-700"
              >
                <FileText size={16} /> Emitir fatura Moloni
              </button>
            )}
          </div>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-[1fr_360px]">
        <section className="space-y-3">
          <label className="relative block">
            <Search className="pointer-events-none absolute left-3 top-2.5 text-zinc-400" size={18} />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Pesquisar por SKU, nome, marca ou modelo"
              className="h-11 w-full rounded-lg border border-zinc-200 bg-white pl-10 pr-3 text-sm outline-none focus:ring-2 focus:ring-brand-400 dark:border-zinc-800 dark:bg-zinc-950"
            />
          </label>

          <div className="grid gap-2 sm:grid-cols-2">
            {(parts.data?.items ?? []).map((part) => (
              <button
                key={part.id}
                type="button"
                onClick={() => addPart(part)}
                disabled={!part.activo || part.qtdStock <= 0}
                className="flex min-h-24 items-start justify-between gap-3 rounded-lg border border-zinc-200 bg-white p-3 text-left transition hover:border-brand-300 hover:bg-brand-50/40 disabled:cursor-not-allowed disabled:opacity-50 dark:border-zinc-800 dark:bg-zinc-950 dark:hover:bg-zinc-900"
              >
                <span>
                  <span className="block text-sm font-medium">{part.nome}</span>
                  <span className="mt-1 block text-xs text-zinc-500">{part.sku ?? 'Sem SKU'} · {part.marca ?? 'Sem marca'}</span>
                  <span className="mt-2 block text-xs text-zinc-500">Stock: {part.qtdStock}</span>
                </span>
                <span className="text-sm font-semibold">{formatCents(part.custoUnitarioCents)}</span>
              </button>
            ))}
          </div>
        </section>

        <aside className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-950">
          <div className="flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-sm font-semibold"><ShoppingCart size={18} /> Carrinho</h2>
            <button type="button" onClick={() => setCart([])} className="text-xs text-zinc-500 hover:text-red-600">Limpar</button>
          </div>

          <div className="space-y-2">
            {cart.length === 0 && <p className="rounded-md bg-zinc-50 p-3 text-sm text-zinc-500 dark:bg-zinc-900">Sem artigos.</p>}
            {cart.map((line) => (
              <div key={line.part.id} className="rounded-md border border-zinc-200 p-3 dark:border-zinc-800">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <div className="text-sm font-medium">{line.part.nome}</div>
                    <div className="text-xs text-zinc-500">{formatCents(line.precoUnitarioCents)} · IVA {line.ivaRate}%</div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setCart((c) => c.filter((l) => l.part.id !== line.part.id))}
                    className="inline-flex h-10 w-10 items-center justify-center rounded-md text-zinc-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-950/40"
                    aria-label="Remover artigo do carrinho"
                  >
                    <Trash2 size={18} />
                  </button>
                </div>
                <div className="mt-3 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => updateQty(line.part.id, -1)}
                      className="inline-flex h-10 w-10 items-center justify-center rounded border border-zinc-200 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-900"
                      aria-label="Diminuir quantidade"
                    >
                      <Minus size={16} />
                    </button>
                    <span className="w-10 text-center text-base font-medium tabular-nums">{line.quantidade}</span>
                    <button
                      type="button"
                      onClick={() => updateQty(line.part.id, 1)}
                      className="inline-flex h-10 w-10 items-center justify-center rounded border border-zinc-200 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-900"
                      aria-label="Aumentar quantidade"
                    >
                      <Plus size={16} />
                    </button>
                  </div>
                  <strong className="text-sm">{formatCents(line.quantidade * line.precoUnitarioCents - line.descontoCents)}</strong>
                </div>
                {PART_CATEGORIA_REQUER_IMEI.has(line.part.categoria) && (
                  <div className="mt-3 space-y-2 rounded-md bg-amber-50/60 p-2 text-xs dark:bg-amber-950/30">
                    <div className="font-medium text-amber-900 dark:text-amber-200">IMEI (obrigatório · DL 84/2021)</div>
                    <input
                      type="text"
                      inputMode="numeric"
                      placeholder="IMEI principal (15 dígitos)"
                      value={line.imei ?? ''}
                      onChange={(e) => updateImei(line.part.id, e.target.value, 'imei')}
                      className={`h-10 w-full rounded border bg-white px-2 font-mono text-sm dark:bg-zinc-950 ${
                        line.imei && !isValidImei(line.imei)
                          ? 'border-red-400'
                          : 'border-zinc-200 dark:border-zinc-800'
                      }`}
                    />
                    {line.imei && !isValidImei(line.imei) && (
                      <div className="text-[10px] text-red-600">IMEI inválido (Luhn falhou).</div>
                    )}
                    {imeiWarnings[line.part.id] && (
                      <div className="rounded border border-orange-300 bg-orange-50 px-2 py-1.5 text-[10px] text-orange-900 dark:border-orange-700 dark:bg-orange-950/40 dark:text-orange-200">
                        ⚠ Este IMEI já foi vendido em{' '}
                        <strong>{new Date(imeiWarnings[line.part.id]!.data).toLocaleDateString('pt-PT')}</strong>
                        {' · '}Venda #{String(imeiWarnings[line.part.id]!.numero).padStart(5, '0')}
                        {imeiWarnings[line.part.id]!.clienteNome && (
                          <> a {imeiWarnings[line.part.id]!.clienteNome}</>
                        )}.
                        {' '}Devolução / re-venda?
                      </div>
                    )}
                    <input
                      type="text"
                      inputMode="numeric"
                      placeholder="IMEI 2 (opcional, dual-SIM)"
                      value={line.imei2 ?? ''}
                      onChange={(e) => updateImei(line.part.id, e.target.value, 'imei2')}
                      className="h-10 w-full rounded border border-zinc-200 bg-white px-2 font-mono text-sm dark:border-zinc-800 dark:bg-zinc-950"
                    />
                    <div className="mt-2 grid grid-cols-1 gap-2 border-t border-amber-200/60 pt-2 dark:border-amber-800/60 sm:grid-cols-3">
                      <input
                        type="text"
                        list="fornecedores-list"
                        placeholder="Fornecedor (ex: Molano)"
                        value={line.fornecedorNome ?? ''}
                        onChange={(e) => setCart((cur) => cur.map((l) => l.part.id === line.part.id ? { ...l, fornecedorNome: e.target.value } : l))}
                        className="h-10 w-full rounded border border-zinc-200 bg-white px-2 text-xs dark:border-zinc-800 dark:bg-zinc-950"
                      />
                      <select
                        value={line.condicao ?? CONDICAO_ARTIGO.NaoAplicavel}
                        onChange={(e) => setCart((cur) => cur.map((l) => l.part.id === line.part.id ? { ...l, condicao: Number(e.target.value) as CondicaoArtigo } : l))}
                        className="h-10 w-full rounded border border-zinc-200 bg-white px-2 text-xs dark:border-zinc-800 dark:bg-zinc-950"
                      >
                        {Object.entries(CONDICAO_ARTIGO_LABEL).map(([v, label]) => (
                          <option key={v} value={v}>{label === '—' ? 'Condição: —' : label}</option>
                        ))}
                      </select>
                      <input
                        type="date"
                        title="Até quando o fornecedor cobre garantia B2B (ex: Molano open-box +60d)"
                        value={line.garantiaFornecedorAteAo ?? ''}
                        onChange={(e) => setCart((cur) => cur.map((l) => l.part.id === line.part.id ? { ...l, garantiaFornecedorAteAo: e.target.value } : l))}
                        className="h-10 w-full rounded border border-zinc-200 bg-white px-2 text-xs dark:border-zinc-800 dark:bg-zinc-950"
                      />
                    </div>
                    <div className="text-[10px] text-zinc-500">
                      Garantia upstream — opcional. Para saber rapidamente se uma reparação futura pode ir ao fornecedor.
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
          {/* Datalist partilhada para autocomplete de fornecedores */}
          <datalist id="fornecedores-list">
            {(fornecedoresQuery.data ?? []).map((f) => <option key={f} value={f} />)}
          </datalist>

          <div className="border-t border-zinc-200 pt-3 text-sm dark:border-zinc-800">
            <label className="mb-2 flex items-center gap-2 text-xs font-medium text-zinc-500"><UserRound size={14} /> Cliente</label>
            <div className="flex gap-2">
              <input
                value={clienteQ}
                onChange={(e) => setClienteQ(e.target.value)}
                placeholder={cliente ? cliente.nome : 'Anonimo ou pesquisar cliente'}
                className="h-10 flex-1 rounded-md border border-zinc-200 bg-white px-3 text-sm dark:border-zinc-800 dark:bg-zinc-950"
              />
              {/* Sprint 150b: criar cliente inline a partir da POS — sem ter de sair para /clientes. */}
              <button
                type="button"
                onClick={() => setNovoClienteOpen(true)}
                className="h-10 whitespace-nowrap rounded-md border border-brand-300 bg-brand-50 px-3 text-xs font-medium text-brand-700 hover:bg-brand-100 dark:border-brand-700 dark:bg-brand-950/40 dark:text-brand-300"
                title="Criar cliente novo agora — útil quando começa cliente na loja"
              >
                + Novo
              </button>
            </div>
            {cliente && <button type="button" onClick={() => setCliente(null)} className="mt-1 text-xs text-zinc-500">Usar cliente anonimo</button>}
            {!cliente && (clientes.data?.items?.length ?? 0) > 0 && (
              <div className="mt-2 max-h-32 overflow-auto rounded-md border border-zinc-200 dark:border-zinc-800">
                {clientes.data!.items.map((c) => (
                  <button key={c.id} type="button" onClick={() => { setCliente(c); setClienteQ(''); }} className="block w-full px-3 py-2 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-900">
                    {c.nome}
                  </button>
                ))}
              </div>
            )}
          </div>

          <div className="space-y-2 border-t border-zinc-200 pt-3 text-sm dark:border-zinc-800">
            <div className="flex justify-between"><span>IVA incluido</span><span>{formatCents(ivaCents)}</span></div>
            <div className="flex justify-between text-lg font-semibold"><span>Total</span><span>{formatCents(totalCents)}</span></div>
            <div className="grid grid-cols-2 gap-2">
              {paymentOptions.map((opt) => (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setPaymentMethod(opt.value)}
                  className={`min-h-11 rounded-md border px-2 text-sm font-medium ${paymentMethod === opt.value ? 'border-brand-500 bg-brand-50 text-brand-700 dark:bg-zinc-900' : 'border-zinc-200 dark:border-zinc-800'}`}
                >
                  {opt.label}
                </button>
              ))}
            </div>
            <button
              type="button"
              onClick={() => cobrar.mutate()}
              disabled={cart.length === 0 || cobrar.isPending}
              className="mt-2 flex h-11 w-full items-center justify-center gap-2 rounded-md bg-brand-600 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
            >
              <CreditCard size={17} /> Cobrar {formatCents(totalCents)}
            </button>
          </div>
        </aside>
      </div>

      {/* Histórico de vendas */}
      <section className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-950">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold">
            <HistoryIcon size={16} /> Histórico de vendas
          </h2>
          <div className="flex flex-wrap items-center gap-2 text-xs">
            <span className="text-zinc-500">De</span>
            <input
              type="date"
              value={historicoFrom}
              onChange={(e) => setHistoricoFrom(e.target.value)}
              className="min-h-11 rounded-md border border-zinc-200 px-3 py-2 dark:border-zinc-800 dark:bg-zinc-950"
            />
            <span className="text-zinc-500">até</span>
            <input
              type="date"
              value={historicoTo}
              onChange={(e) => setHistoricoTo(e.target.value)}
              className="min-h-11 rounded-md border border-zinc-200 px-3 py-2 dark:border-zinc-800 dark:bg-zinc-950"
            />
            <button
              type="button"
              onClick={() => downloadFile(
                `/vendas/export.csv?from=${historicoFrom}T00:00:00Z&to=${historicoTo}T23:59:59Z`,
                `vendas_${historicoFrom}_${historicoTo}.csv`,
              )}
              className="inline-flex min-h-11 items-center gap-1 rounded-md border border-zinc-200 px-3 py-2 hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-900"
              title="Exportar CSV para análise interna (Excel). NÃO substitui o SAFT-PT mensal do Moloni — esse é o documento oficial para o contabilista."
            >
              <Download size={13} /> CSV
            </button>
          </div>
        </div>

        {historico.isLoading ? (
          <SkeletonTable columns={7} rows={5} minWidth="min-w-[820px]" />
        ) : (historico.data?.items.length ?? 0) === 0 ? (
          <p className="py-4 text-center text-sm text-zinc-500">Sem vendas no período seleccionado.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-[820px] text-sm">
              <thead className="border-b border-zinc-200 text-xs text-zinc-500 dark:border-zinc-800">
                <tr>
                  <th className="px-2 py-2 text-left font-medium">Nº</th>
                  <th className="px-2 py-2 text-left font-medium">Data</th>
                  <th className="px-2 py-2 text-left font-medium">Cliente</th>
                  <th className="px-2 py-2 text-right font-medium">Total</th>
                  <th className="px-2 py-2 text-center font-medium">Estado</th>
                  <th className="px-2 py-2 text-center font-medium">Fatura</th>
                  <th className="px-2 py-2 text-right font-medium">Acções</th>
                </tr>
              </thead>
              <tbody>
                {historico.data!.items.map((v) => (
                  <tr key={v.id} className="border-b border-zinc-100 hover:bg-zinc-50 dark:border-zinc-900 dark:hover:bg-zinc-900/40">
                    <td className="px-2 py-2 font-mono text-xs">
                      #{String(v.numero).padStart(5, '0')}
                      {v.origem !== VENDA_ORIGEM.Balcao && (
                        <span
                          className={`ml-1.5 inline-flex items-center rounded-full px-1.5 py-0.5 text-[9px] font-medium ${
                            v.origem === VENDA_ORIGEM.Online
                              ? 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300'
                              : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400'
                          }`}
                          title={`Canal de origem: ${VENDA_ORIGEM_LABEL[v.origem]}`}
                        >
                          {VENDA_ORIGEM_LABEL[v.origem]}
                        </span>
                      )}
                    </td>
                    <td className="px-2 py-2 text-xs text-zinc-600 dark:text-zinc-400">
                      {new Date(v.data).toLocaleString('pt-PT', { dateStyle: 'short', timeStyle: 'short' })}
                    </td>
                    <td className="px-2 py-2">{v.cliente?.nome ?? <span className="italic text-zinc-500">anónimo</span>}</td>
                    <td className="px-2 py-2 text-right font-medium">{formatCents(v.totalCents)}</td>
                    <td className="px-2 py-2 text-center">
                      {v.status === VENDA_STATUS.Paga ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-medium text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
                          <CheckCircle2 size={11} /> Paga
                        </span>
                      ) : v.status === VENDA_STATUS.Cancelada ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-0.5 text-[10px] font-medium text-red-700 dark:bg-red-950/40 dark:text-red-300">
                          <XCircle size={11} /> Cancelada
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-medium text-amber-700 dark:bg-amber-950/40 dark:text-amber-300">
                          Pendente
                        </span>
                      )}
                    </td>
                    <td className="px-2 py-2 text-center text-xs">
                      {v.invoiceNumber ? (
                        <span className="font-mono text-emerald-700 dark:text-emerald-400">{v.invoiceNumber}</span>
                      ) : (
                        <span className="text-zinc-400">—</span>
                      )}
                    </td>
                    <td className="px-2 py-2">
                      <div className="flex justify-end gap-1">
                        <button
                          type="button"
                          onClick={() => setVendaDetalhe(v)}
                          className="min-h-10 rounded-md border border-zinc-200 px-3 py-2 text-xs hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800"
                        >
                          Ver
                        </button>
                        <a
                          href={vendasApi.reciboUrl(v.id)}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-grid h-10 w-10 place-items-center rounded-md border border-zinc-200 text-xs hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800"
                          title="Recibo PDF"
                        >
                          <Receipt size={13} />
                        </a>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Modal detalhe de venda */}
      {vendaDetalhe && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          onClick={() => setVendaDetalhe(null)}
        >
          <div
            onClick={(e) => e.stopPropagation()}
            className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-2xl border border-zinc-200 bg-white p-5 shadow-2xl dark:border-zinc-700 dark:bg-zinc-900"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-base font-semibold">Venda #{String(vendaDetalhe.numero).padStart(5, '0')}</h2>
                <p className="text-xs text-zinc-500">
                  {new Date(vendaDetalhe.data).toLocaleString('pt-PT')} · {vendaDetalhe.cliente?.nome ?? 'Cliente anónimo'}
                </p>
              </div>
              <button type="button" onClick={() => setVendaDetalhe(null)} className="grid h-10 w-10 place-items-center rounded-md text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800">✕</button>
            </div>

            <div className="mt-4 space-y-1.5 rounded-lg border border-zinc-100 p-3 text-sm dark:border-zinc-800">
              {vendaDetalhe.items.map((i) => (
                <div key={i.id} className="flex justify-between gap-3">
                  <div>
                    <div className="font-medium">{i.descricao}</div>
                    <div className="text-xs text-zinc-500">
                      {i.quantidade} × {formatCents(i.precoUnitarioCents)}
                      {i.descontoCents > 0 ? ` − ${formatCents(i.descontoCents)} desc.` : ''}
                      {' '}· IVA {i.ivaRate}%
                    </div>
                  </div>
                  <div className="text-right font-mono text-xs">{formatCents(i.totalCents)}</div>
                </div>
              ))}
              <div className="mt-2 flex justify-between border-t border-zinc-200 pt-2 text-sm font-semibold dark:border-zinc-800">
                <span>Total ({formatCents(vendaDetalhe.ivaCents)} IVA incl.)</span>
                <span>{formatCents(vendaDetalhe.totalCents)}</span>
              </div>
            </div>

            {/* Garantia digital (DL 84/2021) — só visível quando venda paga */}
            {vendaDetalhe.status === VENDA_STATUS.Paga && (
              <div className="mt-3">
                <GarantiaCard kind="venda" vendaId={vendaDetalhe.id} />
              </div>
            )}

            {/* Sprint 91: reparações deste equipamento (cross-link via IMEI) */}
            <VendaReparacoesRelacionadas vendaId={vendaDetalhe.id} />


            {vendaDetalhe.invoiceNumber && (
              <div className="mt-3 rounded-lg border border-emerald-200 bg-emerald-50/40 p-3 text-xs dark:border-emerald-900/40 dark:bg-emerald-950/30">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <strong>Fatura emitida:</strong> {vendaDetalhe.invoiceNumber}
                    {vendaDetalhe.invoicePdfUrl && (
                      <a href={vendaDetalhe.invoicePdfUrl} target="_blank" rel="noreferrer" className="ml-2 underline">
                        ver PDF
                      </a>
                    )}
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    <button
                      type="button"
                      onClick={() => {
                        const ok = confirm(
                          `Anular fatura ${vendaDetalhe.invoiceNumber} via Moloni\n\n` +
                          'O RepairDesk vai chamar a Moloni para cancelar este documento ' +
                          '(documentCancel ou Nota de Crédito).\n\n' +
                          `Saldo na AT após: 0,00 € (nada a pagar)\n\nContinuar?`
                        );
                        if (ok) anularFatura.mutate(vendaDetalhe.id);
                      }}
                      disabled={anularFatura.isPending || limparFaturaLocal.isPending}
                      className="min-h-11 rounded-md border border-red-200 px-3 py-2 text-[11px] text-red-700 hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:hover:bg-red-950/40"
                      title="Chama Moloni para anular (documentCancel ou NC). Saldo IVA fica zero."
                    >
                      {anularFatura.isPending ? 'A anular…' : 'Anular via Moloni'}
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        const ok = confirm(
                          'Já anulei manualmente no painel Moloni\n\n' +
                          'Esta acção APENAS remove a referência da fatura no RepairDesk.\n' +
                          'NÃO chama a Moloni. Usa só se já cancelaste a fatura no painel moloni.pt.\n\n' +
                          'A venda fica sem fatura associada e sai do Relatório IVA do RepairDesk.\n\nContinuar?'
                        );
                        if (ok) limparFaturaLocal.mutate(vendaDetalhe.id);
                      }}
                      disabled={anularFatura.isPending || limparFaturaLocal.isPending}
                      className="min-h-11 rounded-md border border-zinc-200 px-3 py-2 text-[11px] text-zinc-700 hover:bg-zinc-50 disabled:opacity-60 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800"
                      title="Já anulaste no Moloni — só limpa a referência aqui (sem chamar API Moloni)"
                    >
                      {limparFaturaLocal.isPending ? 'A limpar…' : 'Já anulei no Moloni'}
                    </button>
                  </div>
                </div>
              </div>
            )}

            <div className="mt-4 flex flex-wrap justify-end gap-2">
              <a
                href={vendasApi.reciboUrl(vendaDetalhe.id)}
                target="_blank"
                rel="noreferrer"
                className="inline-flex min-h-11 items-center gap-1 rounded-md border border-zinc-200 px-3 py-2 text-xs hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800"
              >
                <Receipt size={13} /> Recibo PDF
              </a>
              {vendaDetalhe.status === VENDA_STATUS.Paga && !vendaDetalhe.invoiceExternalId && (
                <button
                  type="button"
                  onClick={() => {
                    const isSandbox = billing.data?.sandboxMode === true;
                    const ok = confirm(
                      isSandbox
                        ? 'MODO SANDBOX — fatura de teste\n\nNão é comunicada à AT real.\n\n' +
                          `Venda #${vendaDetalhe.numero} · Total: ${formatCents(vendaDetalhe.totalCents)}\nContinuar?`
                        : 'ATENÇÃO: MODO PRODUÇÃO — fatura real à AT\n\n' +
                          'Vai ser comunicada em tempo real. Entra na declaração IVA.\n\n' +
                          `Venda #${vendaDetalhe.numero} · Total: ${formatCents(vendaDetalhe.totalCents)}\n\nTem a certeza?`
                    );
                    if (ok) emitirFatura.mutate(vendaDetalhe.id);
                  }}
                  disabled={emitirFatura.isPending}
                  className="inline-flex min-h-11 items-center gap-1 rounded-md bg-brand-600 px-3 py-2 text-xs font-medium text-white hover:bg-brand-700 disabled:opacity-60"
                >
                  <FileText size={13} /> Emitir fatura Moloni
                </button>
              )}
              {vendaDetalhe.status !== VENDA_STATUS.Cancelada && (
                <button
                  type="button"
                  onClick={() => {
                    const temFatura = !!vendaDetalhe.invoiceExternalId;
                    const msg = temFatura
                      ? `Cancelar venda #${vendaDetalhe.numero}?\n\n` +
                        `Vai fazer 2 coisas:\n` +
                        `  1. Anular fatura ${vendaDetalhe.invoiceNumber} no Moloni (cancel ou NC)\n` +
                        `  2. Reverter stock dos artigos\n\nContinuar?`
                      : `Cancelar venda #${vendaDetalhe.numero}? O stock será reposto.`;
                    if (confirm(msg)) cancelar.mutate(vendaDetalhe.id);
                  }}
                  disabled={cancelar.isPending}
                  className="inline-flex min-h-11 items-center gap-1 rounded-md border border-red-200 px-3 py-2 text-xs text-red-700 hover:bg-red-50 disabled:opacity-60 dark:border-red-900/40 dark:hover:bg-red-950/40"
                  title={vendaDetalhe.invoiceExternalId ? 'Anula fatura no Moloni + reverte stock (1 clique)' : 'Cancela venda + reverte stock'}
                >
                  <XCircle size={13} /> Cancelar venda
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Sprint 150b: criar cliente inline a partir da POS — auto-seleciona após criar. */}
      <NovoClienteModal
        open={novoClienteOpen}
        onClose={() => setNovoClienteOpen(false)}
        onCreated={async (c) => {
          setNovoClienteOpen(false);
          // Fetch full cliente — NovoClienteModal só devolve {id, nome}, precisamos do shape completo.
          try {
            const full = await clientesApi.get(c.id);
            setCliente(full);
          } catch {
            // Fallback minimal — se o fetch falhou pelo menos o utilizador vê o nome no input.
            setClienteQ(c.nome);
          }
          setClienteQ('');
        }}
      />
    </div>
  );
}

/** Sprint 91: secção que mostra reparações posteriores a esta venda com IMEIs batidos. */
function VendaReparacoesRelacionadas({ vendaId }: { vendaId: string }) {
  const q = useQuery({
    queryKey: ['venda-reparacoes-relacionadas', vendaId],
    queryFn: () => vendasApi.reparacoesRelacionadas(vendaId),
    staleTime: 60_000,
  });

  if (q.isLoading || !q.data || q.data.length === 0) return null;

  return (
    <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50/60 p-3 dark:border-amber-900/60 dark:bg-amber-950/30">
      <div className="text-xs font-semibold text-amber-900 dark:text-amber-100">
        Reparações deste equipamento ({q.data.length})
      </div>
      <ul className="mt-2 space-y-1.5 text-xs">
        {q.data.map((r: VendaReparacaoRelacionada) => (
          <li key={r.reparacaoId} className="flex items-center justify-between gap-2">
            <div className="min-w-0 flex-1">
              <Link
                to={`/reparacoes/${r.reparacaoId}`}
                className="font-mono text-brand-600 hover:underline"
              >
                #{String(r.reparacaoNumero).padStart(5, '0')}
              </Link>
              <span className="ml-2 text-zinc-600 dark:text-zinc-400">
                {r.equipamento}
              </span>
              <span className="ml-2 rounded-full bg-zinc-100 px-1.5 py-0.5 text-[9px] font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400">
                {r.diasDesdeAVenda}d após venda
              </span>
            </div>
            <div className="flex items-center gap-2">
              {r.orcamentoCents !== null && (
                <span className="text-zinc-500">{formatCents(r.orcamentoCents)}</span>
              )}
              <span className={`rounded-full px-1.5 py-0.5 text-[9px] font-medium ${STATUS_COLOR[r.estado as keyof typeof STATUS_COLOR] ?? ''}`}>
                {STATUS_LABEL[r.estado as keyof typeof STATUS_LABEL] ?? `Estado ${r.estado}`}
              </span>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
