import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Minus, Plus, ScanBarcode, ShoppingCart, Trash2 } from 'lucide-react';
import { Button, PageHeader, SectionCard } from '../../components/ui';
import { inputCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { Artigo } from '../../lib/artigos/types';
import { formatEur } from '../../lib/compras/format';
import { toast } from '../../lib/toast';
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL, type PaymentMethod } from '../../lib/vendas/types';

interface LinhaCarrinho {
  artigo: Artigo;
  quantidade: number;
}

const PAGAMENTOS: PaymentMethod[] = [PAYMENT_METHOD.Dinheiro, PAYMENT_METHOD.MBWay, PAYMENT_METHOD.Cartao, PAYMENT_METHOD.Multibanco, PAYMENT_METHOD.TransferenciaBancaria];

/**
 * Sprint 566: balcão POS. A pistola de códigos de barras "escreve" o código e carrega Enter — por isso a
 * caixa de leitura está sempre com foco. Se o código não for de nenhum artigo, o texto serve de pesquisa.
 * Fechar a venda tira do lote mais antigo e regista a venda entregue (a fatura-recibo sai no Moloni).
 */
export default function Balcao() {
  const qc = useQueryClient();
  const leitor = useRef<HTMLInputElement | null>(null);
  const [codigo, setCodigo] = useState('');
  const [pesquisa, setPesquisa] = useState('');
  const [carrinho, setCarrinho] = useState<LinhaCarrinho[]>([]);
  const [pagamento, setPagamento] = useState<PaymentMethod>(PAYMENT_METHOD.Dinheiro);

  const resultados = useQuery({
    queryKey: ['artigos', 'balcao', pesquisa],
    queryFn: () => artigosApi.list(pesquisa),
    enabled: pesquisa.trim().length >= 2,
  });

  useEffect(() => { leitor.current?.focus(); }, []);

  function adicionar(a: Artigo, qtd = 1) {
    setCarrinho((c) => {
      const i = c.findIndex((l) => l.artigo.id === a.id);
      const atual = i >= 0 ? c[i].quantidade : 0;
      if (atual + qtd > a.stock) {
        toast.error('Sem stock suficiente', `${a.nome}: há ${a.stock} em stock.`);
        return c;
      }
      if (i < 0) return [...c, { artigo: a, quantidade: qtd }];
      return c.map((l, j) => (j === i ? { ...l, quantidade: l.quantidade + qtd } : l));
    });
  }

  async function ler(e: FormEvent) {
    e.preventDefault();
    const c = codigo.trim();
    if (!c) return;
    setCodigo('');
    try {
      const a = await artigosApi.porCodigo(c);
      if (a) {
        if (a.precoEfetivoCents == null) toast.error('Artigo sem preço', `Define o preço de venda de "${a.nome}" em Artigos.`);
        else adicionar(a);
        setPesquisa('');
      } else {
        setPesquisa(c); // não é um código conhecido: mostra a pesquisa por texto
      }
    } catch (err) {
      toast.fromError(err, 'Não foi possível ler o código.');
    } finally {
      leitor.current?.focus();
    }
  }

  const total = useMemo(() => carrinho.reduce((s, l) => s + l.quantidade * (l.artigo.precoEfetivoCents ?? 0), 0), [carrinho]);

  const vender = useMutation({
    mutationFn: () => artigosApi.vender({
      linhas: carrinho.map((l) => ({ artigoId: l.artigo.id, quantidade: l.quantidade })),
      paymentMethod: pagamento,
    }),
    onSuccess: (v) => {
      toast.success(`Venda #${v.numero} registada — ${formatEur(v.totalCents / 100)}`,
        'Falta emitir a fatura-recibo (fica em "Fatura por registar" até a ligação ao Moloni estar ativa).');
      setCarrinho([]);
      setPesquisa('');
      qc.invalidateQueries({ queryKey: ['artigos'] });
      qc.invalidateQueries({ queryKey: ['vendas'] });
      qc.invalidateQueries({ queryKey: ['compras', 'inventario'] });
      leitor.current?.focus();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível registar a venda.'),
  });

  return (
    <div className="space-y-4">
      <PageHeader title="Balcão" description="Passa a pistola no código de barras (ou escreve o nome) e fecha a venda." />

      <div className="grid gap-4 lg:grid-cols-[1fr_380px]">
        <div className="space-y-4">
          <form onSubmit={ler} className="relative">
            <ScanBarcode size={20} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-brand-600" />
            <input ref={leitor} value={codigo} onChange={(e) => setCodigo(e.target.value)} className={`${inputCls} h-14 pl-11 text-lg`}
              placeholder="Passa o código de barras… ou escreve (ex.: película a34) e Enter" aria-label="Código de barras ou pesquisa" autoComplete="off" />
          </form>

          {pesquisa && (
            <SectionCard title={`Resultados para "${pesquisa}"`} bodyClassName="p-0">
              {resultados.isLoading ? <p className="p-4 text-sm text-zinc-500">A procurar…</p>
                : (resultados.data ?? []).length === 0 ? (
                  <p className="p-4 text-sm text-zinc-500">
                    Nenhum artigo. Se é um produto novo, <Link to="/artigos" className="text-brand-600 underline">cria-o em Artigos</Link> — se não há stock, é para encomendar.
                  </p>
                ) : (
                  <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                    {resultados.data!.map((a) => (
                      <li key={a.id}>
                        <button type="button" disabled={a.stock === 0 || a.precoEfetivoCents == null}
                          onClick={() => { adicionar(a); leitor.current?.focus(); }}
                          className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-zinc-50 disabled:cursor-not-allowed disabled:opacity-60 dark:hover:bg-zinc-800/40">
                          <span className="flex-1">
                            <span className="font-medium">{a.nome}</span>
                            <span className="block text-xs text-zinc-500">{a.sku}{a.localizacao ? ` · ${a.localizacao}` : ''}</span>
                          </span>
                          {a.stock > 0
                            ? <span className="text-sm tabular-nums text-emerald-700 dark:text-emerald-400">{a.stock} em stock</span>
                            : <span className="rounded-full bg-red-50 px-2 py-0.5 text-xs font-medium text-red-700 dark:bg-red-950/40 dark:text-red-300">Sem stock — encomendar</span>}
                          <span className="w-20 text-right font-semibold tabular-nums">{a.precoEfetivoCents != null ? formatEur(a.precoEfetivoCents / 100) : '—'}</span>
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
            </SectionCard>
          )}
        </div>

        <SectionCard title={<span className="flex items-center gap-2"><ShoppingCart size={16} /> Venda</span>}>
          {carrinho.length === 0 ? (
            <p className="text-sm text-zinc-500">Ainda vazio — passa a pistola num artigo.</p>
          ) : (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {carrinho.map((l) => (
                <li key={l.artigo.id} className="flex items-center gap-2 py-2 text-sm">
                  <span className="flex-1">
                    <span className="font-medium">{l.artigo.nome}</span>
                    <span className="block text-xs text-zinc-500">{formatEur((l.artigo.precoEfetivoCents ?? 0) / 100)} / un</span>
                  </span>
                  <button type="button" aria-label="Menos uma" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800"
                    onClick={() => setCarrinho((c) => c.map((x) => (x.artigo.id === l.artigo.id ? { ...x, quantidade: Math.max(1, x.quantidade - 1) } : x)))}>
                    <Minus size={14} />
                  </button>
                  <span className="w-6 text-center tabular-nums">{l.quantidade}</span>
                  <button type="button" aria-label="Mais uma" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => adicionar(l.artigo)}>
                    <Plus size={14} />
                  </button>
                  <span className="w-16 text-right tabular-nums">{formatEur((l.quantidade * (l.artigo.precoEfetivoCents ?? 0)) / 100)}</span>
                  <button type="button" aria-label="Tirar" className="rounded p-1 text-zinc-400 hover:text-red-600"
                    onClick={() => setCarrinho((c) => c.filter((x) => x.artigo.id !== l.artigo.id))}>
                    <Trash2 size={14} />
                  </button>
                </li>
              ))}
            </ul>
          )}

          <div className="mt-4 flex items-baseline justify-between border-t border-zinc-200 pt-3 dark:border-zinc-800">
            <span className="text-sm text-zinc-500">Total c/ IVA</span>
            <span className="text-2xl font-bold tabular-nums">{formatEur(total / 100)}</span>
          </div>

          <div className="mt-3 grid grid-cols-3 gap-2">
            {PAGAMENTOS.map((p) => (
              <button key={p} type="button" onClick={() => setPagamento(p)}
                className={`rounded-lg border px-2 py-2 text-xs font-medium ${pagamento === p ? 'border-brand-500 bg-brand-50 text-brand-700 dark:bg-brand-950/40 dark:text-brand-300' : 'border-zinc-200 dark:border-zinc-700'}`}>
                {PAYMENT_METHOD_LABEL[p]}
              </button>
            ))}
          </div>

          <Button type="button" className="mt-4 w-full" leftIcon={<ShoppingCart size={16} />} loading={vender.isPending}
            disabled={carrinho.length === 0} onClick={() => vender.mutate()}>
            Fechar venda · {formatEur(total / 100)}
          </Button>
        </SectionCard>
      </div>
    </div>
  );
}
