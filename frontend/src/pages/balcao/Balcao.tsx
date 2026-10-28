import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Camera, Minus, Plus, Printer, ScanBarcode, ShoppingCart, Trash2 } from 'lucide-react';
import LeitorCamera from '../../components/LeitorCamera';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { vendasApi } from '../../lib/vendas/api';
import { Button, PageHeader, SectionCard } from '../../components/ui';
import { inputCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { Artigo, ArtigoLote } from '../../lib/artigos/types';
import EscolherModelo from './EscolherModelo';
import { formatEur } from '../../lib/compras/format';
import { toast } from '../../lib/toast';
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL, type PaymentMethod } from '../../lib/vendas/types';
import DadosFatura, { escolhaInicial, pedidoFatura, problemaFatura, type EscolhaFatura } from '../vendas/DadosFatura';

interface LinhaCarrinho {
  artigo: Artigo;
  quantidade: number;
  /** Sprint 581: artigo agrupado — o modelo (lote) escolhido; null = sai do lote mais antigo. */
  lote: ArtigoLote | null;
  /** Sprint 583: modelo escrito à mão (o lote não o diz) — vai para a fatura. */
  modelo: string | null;
}

const chave = (l: { artigo: Artigo; lote: ArtigoLote | null; modelo: string | null }) => `${l.artigo.id}:${l.lote?.linhaId ?? ''}:${l.modelo ?? ''}`;
const textoLinha = (l: LinhaCarrinho) => (l.modelo ? `${l.artigo.nome} – ${l.modelo}` : l.lote?.textoFatura ?? l.artigo.nome);

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
  const [camara, setCamara] = useState(false);
  const [pesquisa, setPesquisa] = useState('');
  const [carrinho, setCarrinho] = useState<LinhaCarrinho[]>([]);
  const [pagamento, setPagamento] = useState<PaymentMethod>(PAYMENT_METHOD.Dinheiro);
  // Sprint 571: fatura-recibo sem NIF (consumidor final) por defeito; com NIF pede nome e morada.
  const [fatura, setFatura] = useState<EscolhaFatura>(() => escolhaInicial(null));
  const [ultimaVenda, setUltimaVenda] = useState<{ id: string; numero: number; fatura: string | null } | null>(null);
  // Sprint 581: artigo agrupado à espera da escolha do modelo.
  const [escolher, setEscolher] = useState<Artigo | null>(null);

  const resultados = useQuery({
    queryKey: ['artigos', 'balcao', pesquisa],
    queryFn: () => artigosApi.list(pesquisa),
    enabled: pesquisa.trim().length >= 2,
  });

  // Sprint 574: pesquisa enquanto se escreve (antes só com Enter — escrever 's23' não mostrava nada).
  // Só números compridos não disparam: é a pistola a meio de um código de barras (essa acaba em Enter).
  useEffect(() => {
    const t = codigo.trim();
    // Vazio não limpa: depois de um Enter a caixa esvazia mas os resultados ficam à vista.
    if (t.length < 2 || /^\d{8,}$/.test(t)) return;
    const id = setTimeout(() => setPesquisa(t), 250);
    return () => clearTimeout(id);
  }, [codigo]);

  // No telemóvel não se foca a caixa (abriria o teclado por cima do ecrã); usa-se a câmara.
  useEffect(() => { if (!window.matchMedia?.('(pointer: coarse)').matches) leitor.current?.focus(); }, []);

  /** Artigo agrupado sem modelo escolhido → pergunta o modelo primeiro. */
  function adicionar(a: Artigo, qtd = 1, lote: ArtigoLote | null = null, modelo: string | null = null) {
    if (a.agrupado && !lote) { setEscolher(a); return; }
    setCarrinho((c) => {
      const k = chave({ artigo: a, lote, modelo });
      const i = c.findIndex((l) => chave(l) === k);
      const atual = i >= 0 ? c[i].quantidade : 0;
      // O mesmo lote pode estar em várias linhas (modelos escritos à mão diferentes): conta todas.
      const noLote = lote ? c.filter((l) => l.lote?.linhaId === lote.linhaId && chave(l) !== k).reduce((s, l) => s + l.quantidade, 0) : 0;
      const disponivel = (lote ? lote.quantidadeEmStock : a.stock) - noLote;
      if (atual + qtd > disponivel) {
        toast.error('Sem stock suficiente', `${lote?.descricao ?? a.nome}: há ${disponivel} em stock.`);
        return c;
      }
      if (i < 0) return [...c, { artigo: a, quantidade: qtd, lote, modelo }];
      return c.map((l, j) => (j === i ? { ...l, quantidade: l.quantidade + qtd } : l));
    });
  }

  async function ler(e: FormEvent) {
    e.preventDefault();
    const c = codigo.trim();
    if (!c) return;
    setCodigo('');
    await lerCodigo(c);
  }

  /** Pistola, teclado ou câmara do telemóvel — o mesmo caminho. */
  async function lerCodigo(c: string) {
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
      if (!camara) leitor.current?.focus();
    }
  }

  const total = useMemo(() => carrinho.reduce((s, l) => s + l.quantidade * (l.artigo.precoEfetivoCents ?? 0), 0), [carrinho]);

  const vender = useMutation({
    mutationFn: () => artigosApi.vender({
      linhas: carrinho.map((l) => ({ artigoId: l.artigo.id, quantidade: l.quantidade, loteId: l.lote?.linhaId ?? null, modelo: l.modelo })),
      paymentMethod: pagamento,
      comNif: pedidoFatura(fatura).comNif,
    }),
    onSuccess: (v) => {
      if (v.invoiceNumber) toast.success(`Venda #${v.numero} — ${formatEur(v.totalCents / 100)}`, `Fatura-recibo ${v.invoiceNumber} emitida.`);
      else toast.error(`Venda #${v.numero} registada, mas sem fatura-recibo`, 'Abre a venda e emite-a (o Moloni ON não respondeu ou não está ligado).');
      setUltimaVenda({ id: v.id, numero: v.numero, fatura: v.invoiceNumber });
      setCarrinho([]);
      setPesquisa('');
      setFatura(escolhaInicial(null));
      qc.invalidateQueries({ queryKey: ['artigos'] });
      qc.invalidateQueries({ queryKey: ['vendas'] });
      qc.invalidateQueries({ queryKey: ['compras', 'inventario'] });
      leitor.current?.focus();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível registar a venda.'),
  });

  return (
    <div className="space-y-4">
      <PageHeader title="Balcão" description="Passa a pistola no código de barras (ou escreve o nome) e fecha a venda."
        actions={ultimaVenda && (
          <Button type="button" variant="secondary" leftIcon={<Printer size={15} />}
            onClick={() => openPdfInNewTab(vendasApi.talaoPath(ultimaVenda.id)).catch((e) => toast.fromError(e, 'Não foi possível gerar o talão.'))}>
            Talão da venda #{ultimaVenda.numero}{ultimaVenda.fatura ? ` · ${ultimaVenda.fatura}` : ''}
          </Button>
        )} />

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_380px]">
        <div className="space-y-4">
          <form onSubmit={ler} className="flex gap-2">
            <div className="relative flex-1">
              <ScanBarcode size={20} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-brand-600" />
              <input ref={leitor} value={codigo} onChange={(e) => setCodigo(e.target.value)} className={`${inputCls} h-14 pl-11 text-base sm:text-lg`}
                placeholder="Código de barras ou nome (ex.: película a34)" aria-label="Código de barras ou pesquisa" autoComplete="off" enterKeyHint="search" />
            </div>
            <button type="button" onClick={() => setCamara(true)} aria-label="Ler com a câmara"
              className="flex h-14 flex-none items-center gap-2 rounded-lg bg-brand-600 px-4 font-medium text-white shadow-sm hover:bg-brand-700">
              <Camera size={20} /> <span className="hidden sm:inline">Câmara</span>
            </button>
          </form>
          <LeitorCamera open={camara} onClose={() => setCamara(false)} onLido={(c) => { void lerCodigo(c); }} continuo
            titulo="Ler artigos com a câmara" />
          <EscolherModelo artigo={escolher} filtroInicial={pesquisa} onClose={() => { setEscolher(null); leitor.current?.focus(); }}
            onEscolher={(lote, modelo) => { if (escolher) adicionar(escolher, 1, lote, modelo); setEscolher(null); leitor.current?.focus(); }} />

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
                <li key={chave(l)} className="flex items-center gap-2 py-2 text-sm">
                  <span className="min-w-0 flex-1">
                    {/* Sprint 582: o texto exato da linha da fatura. */}
                    <span className="font-medium">{textoLinha(l)}</span>
                    <span className="block text-xs text-zinc-500">{formatEur((l.artigo.precoEfetivoCents ?? 0) / 100)} / un</span>
                  </span>
                  <button type="button" aria-label="Menos uma" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800"
                    onClick={() => setCarrinho((c) => c.map((x) => (chave(x) === chave(l) ? { ...x, quantidade: Math.max(1, x.quantidade - 1) } : x)))}>
                    <Minus size={14} />
                  </button>
                  <span className="w-6 text-center tabular-nums">{l.quantidade}</span>
                  <button type="button" aria-label="Mais uma" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => adicionar(l.artigo, 1, l.lote, l.modelo)}>
                    <Plus size={14} />
                  </button>
                  <span className="w-16 text-right tabular-nums">{formatEur((l.quantidade * (l.artigo.precoEfetivoCents ?? 0)) / 100)}</span>
                  <button type="button" aria-label="Tirar" className="rounded p-1 text-zinc-400 hover:text-red-600"
                    onClick={() => setCarrinho((c) => c.filter((x) => chave(x) !== chave(l)))}>
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

          <div className="mt-4"><DadosFatura value={fatura} onChange={setFatura} /></div>
          {problemaFatura(fatura) && carrinho.length > 0 && <p className="mt-2 text-xs font-medium text-amber-700 dark:text-amber-400">{problemaFatura(fatura)}</p>}

          <Button type="button" className="mt-4 w-full" leftIcon={<ShoppingCart size={16} />} loading={vender.isPending}
            disabled={carrinho.length === 0 || !!problemaFatura(fatura)} onClick={() => vender.mutate()}>
            Fechar venda · {formatEur(total / 100)}
          </Button>
        </SectionCard>
      </div>
    </div>
  );
}
