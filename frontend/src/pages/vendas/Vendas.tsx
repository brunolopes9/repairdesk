import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CreditCard, FileText, Minus, Plus, Receipt, Search, ShoppingCart, Trash2, UserRound } from 'lucide-react';
import { toast } from 'sonner';
import { clientesApi } from '../../lib/clientes/api';
import type { Cliente } from '../../lib/clientes/types';
import { formatCents } from '../../lib/money';
import { stockApi } from '../../lib/stock/api';
import type { Part } from '../../lib/stock/types';
import { vendasApi } from '../../lib/vendas/api';
import { PAYMENT_METHOD, type PaymentMethod, type Venda } from '../../lib/vendas/types';

type CartLine = {
  part: Part;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents: number;
  ivaRate: number;
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
  const [cart, setCart] = useState<CartLine[]>([]);
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>(PAYMENT_METHOD.MBWay);
  const [lastVenda, setLastVenda] = useState<Venda | null>(null);

  const parts = useQuery({
    queryKey: ['vendas-parts', q],
    queryFn: () => stockApi.list({ q, pageSize: 12 }),
    staleTime: 10_000,
  });

  const clientes = useQuery({
    queryKey: ['vendas-clientes', clienteQ],
    queryFn: () => clientesApi.list(clienteQ, 1, 8),
    enabled: clienteQ.trim().length >= 2,
    staleTime: 10_000,
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
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'Nao foi possivel emitir fatura.'),
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
                onClick={() => emitirFatura.mutate(lastVenda.id)}
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
                  <button type="button" onClick={() => setCart((c) => c.filter((l) => l.part.id !== line.part.id))} className="text-zinc-400 hover:text-red-600">
                    <Trash2 size={16} />
                  </button>
                </div>
                <div className="mt-3 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <button type="button" onClick={() => updateQty(line.part.id, -1)} className="rounded border border-zinc-200 p-1 dark:border-zinc-800"><Minus size={14} /></button>
                    <span className="w-8 text-center text-sm">{line.quantidade}</span>
                    <button type="button" onClick={() => updateQty(line.part.id, 1)} className="rounded border border-zinc-200 p-1 dark:border-zinc-800"><Plus size={14} /></button>
                  </div>
                  <strong className="text-sm">{formatCents(line.quantidade * line.precoUnitarioCents - line.descontoCents)}</strong>
                </div>
              </div>
            ))}
          </div>

          <div className="border-t border-zinc-200 pt-3 text-sm dark:border-zinc-800">
            <label className="mb-2 flex items-center gap-2 text-xs font-medium text-zinc-500"><UserRound size={14} /> Cliente</label>
            <input
              value={clienteQ}
              onChange={(e) => setClienteQ(e.target.value)}
              placeholder={cliente ? cliente.nome : 'Anonimo ou pesquisar cliente'}
              className="h-10 w-full rounded-md border border-zinc-200 bg-white px-3 text-sm dark:border-zinc-800 dark:bg-zinc-950"
            />
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
                  className={`rounded-md border px-2 py-2 text-xs ${paymentMethod === opt.value ? 'border-brand-500 bg-brand-50 text-brand-700 dark:bg-zinc-900' : 'border-zinc-200 dark:border-zinc-800'}`}
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
    </div>
  );
}
