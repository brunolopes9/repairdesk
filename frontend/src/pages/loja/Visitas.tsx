import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BarChart3, Clock, Eye, MousePointerClick, ShoppingCart, Users } from 'lucide-react';
import { EmptyState, KpiCard, PageHeader, SectionCard, SkeletonTable } from '../../components/ui';
import { apiErrorMessage } from '../../lib/errors';
import { visitasApi, type Contagem, type EventoVisita, type SessaoVisita } from '../../lib/loja/visitas';

/**
 * 23/11 (Bruno: «quero saber tudo o que as pessoas fazem na loja»): quem entrou no lopestech.pt, por onde andou,
 * o que pesquisou (e não encontrou), quanto tempo ficou, onde saiu, carrinhos, checkouts, pagamentos e compras.
 * Medição própria (js/visitas.js) — os números são do Mender, não do Google Analytics.
 */

const PERIODOS = [
  { id: 'hoje', label: 'Hoje', dias: 0 },
  { id: '7', label: '7 dias', dias: 6 },
  { id: '30', label: '30 dias', dias: 29 },
  { id: '90', label: '90 dias', dias: 89 },
] as const;

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const tempo = (s: number) => (s < 60 ? `${s} s` : s < 3600 ? `${Math.floor(s / 60)} min ${s % 60 ? `${s % 60} s` : ''}`.trim() : `${Math.floor(s / 3600)} h ${Math.floor((s % 3600) / 60)} min`);
const eur = (v: number) => `${v.toFixed(2).replace('.', ',')} €`;
const pct = (a: number, b: number) => (b > 0 ? `${Math.round((a / b) * 100)} %` : '—');
const hora = (s: string) => new Date(s).toLocaleString('pt-PT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });

const TIPO: Record<string, { label: string; cor: string }> = {
  pagina: { label: 'Página', cor: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300' },
  saida: { label: 'Saiu da página', cor: 'bg-zinc-50 text-zinc-500 dark:bg-zinc-900 dark:text-zinc-400' },
  pesquisa: { label: 'Pesquisou', cor: 'bg-sky-100 text-sky-800 dark:bg-sky-950/40 dark:text-sky-300' },
  produto: { label: 'Viu produto', cor: 'bg-violet-100 text-violet-800 dark:bg-violet-950/40 dark:text-violet-300' },
  carrinho: { label: 'Pôs no carrinho', cor: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300' },
  remover: { label: 'Tirou do carrinho', cor: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400' },
  checkout: { label: 'Abriu o checkout', cor: 'bg-orange-100 text-orange-800 dark:bg-orange-950/40 dark:text-orange-300' },
  pagamento: { label: 'Iniciou pagamento', cor: 'bg-orange-200 text-orange-900 dark:bg-orange-900/40 dark:text-orange-200' },
  compra: { label: 'Comprou', cor: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300' },
  contacto: { label: 'Contactou', cor: 'bg-teal-100 text-teal-800 dark:bg-teal-950/40 dark:text-teal-300' },
};

function Lista({ titulo, itens, vazio, extra, media, destaque }: {
  titulo: string; itens: Contagem[]; vazio: string; extra?: string; media?: (v: number) => string; destaque?: boolean;
}) {
  const max = Math.max(1, ...itens.map((i) => i.total));
  return (
    <SectionCard title={titulo} className={destaque ? 'ring-1 ring-amber-300 dark:ring-amber-700' : ''}>
      {itens.length === 0 ? <p className="text-sm text-zinc-500">{vazio}</p> : (
        <ul className="max-h-80 space-y-1.5 overflow-y-auto pr-1">
          {itens.map((i) => (
            <li key={i.nome} className="relative rounded-md px-2 py-1 text-sm">
              <span className="absolute inset-y-0 left-0 rounded-md bg-brand-50 dark:bg-brand-950/30" style={{ width: `${(i.total / max) * 100}%` }} />
              <span className="relative flex items-center justify-between gap-3">
                <span className="truncate" title={i.nome}>{i.nome}</span>
                <span className="flex-none tabular-nums text-zinc-600 dark:text-zinc-300">
                  <b>{i.total}</b>
                  {extra && i.extra != null ? <span className="ml-2 text-xs text-zinc-500">{i.extra} {extra}</span> : null}
                  {media && i.media != null ? <span className="ml-2 text-xs text-zinc-500">{media(i.media)}</span> : null}
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}

function Percurso({ sessao }: { sessao: string }) {
  const q = useQuery({ queryKey: ['loja', 'visitas', 'sessao', sessao], queryFn: () => visitasApi.sessao(sessao) });
  if (q.isLoading) return <p className="px-4 py-3 text-sm text-zinc-500">A carregar o percurso…</p>;
  if (q.isError) return <p className="px-4 py-3 text-sm text-rose-600">{apiErrorMessage(q.error)}</p>;
  const ev = (q.data ?? []).filter((e) => !(e.tipo === 'saida' && !e.duracao));
  return (
    <ol className="space-y-1 border-l-2 border-zinc-200 py-2 pl-4 dark:border-zinc-700">
      {ev.map((e: EventoVisita, i) => {
        const t = TIPO[e.tipo] ?? { label: e.tipo, cor: 'bg-zinc-100' };
        return (
          <li key={i} className="flex flex-wrap items-baseline gap-2 text-sm">
            <span className="w-12 flex-none tabular-nums text-xs text-zinc-500">{new Date(e.em).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}</span>
            <span className={`rounded px-1.5 py-0.5 text-xs font-medium ${t.cor}`}>{t.label}</span>
            {e.tipo === 'pagina' && <span className="text-zinc-700 dark:text-zinc-200">{e.titulo ?? e.caminho} <span className="text-xs text-zinc-500">{e.caminho}</span>{e.origem ? <span className="ml-1 text-xs text-zinc-500">· veio de {e.origem}</span> : null}</span>}
            {e.tipo === 'saida' && <span className="text-xs text-zinc-500">{e.caminho} · ficou {tempo(e.duracao ?? 0)}{e.scroll != null ? ` · viu ${e.scroll}% da página` : ''}</span>}
            {e.tipo === 'pesquisa' && <span>«{e.termo}» {e.resultados === 0 ? <b className="text-rose-600">sem resultados</b> : e.resultados != null ? <span className="text-xs text-zinc-500">{e.resultados} resultado(s)</span> : null}</span>}
            {['produto', 'carrinho', 'remover'].includes(e.tipo) && <span>{e.produto}{e.valor != null ? ` · ${eur(e.valor)}` : ''}</span>}
            {['checkout', 'pagamento', 'compra'].includes(e.tipo) && <span>{e.termo ? `${e.termo} · ` : ''}{e.valor != null ? eur(e.valor) : ''}{e.encomenda ? ` · ${e.encomenda}` : ''}</span>}
            {e.tipo === 'contacto' && <span>{e.termo}</span>}
          </li>
        );
      })}
    </ol>
  );
}

/** 26/11: as visitas do Bruno não contam — os aparelhos com o Mender já não entram; isto limpa as que ficaram para trás. */
function FuiEu({ sessao }: { sessao: string }) {
  const qc = useQueryClient();
  const [confirmar, setConfirmar] = useState(false);
  const m = useMutation({
    mutationFn: () => visitasApi.naoContar(sessao),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['loja', 'visitas'] }),
  });
  return (
    <div className="mt-2 flex flex-wrap items-center gap-2 text-xs">
      {!confirmar ? (
        <button type="button" onClick={() => setConfirmar(true)} className="rounded border border-zinc-300 px-2 py-1 text-zinc-600 hover:bg-zinc-100 dark:border-zinc-700 dark:text-zinc-300 dark:hover:bg-zinc-800">
          Fui eu — não contar
        </button>
      ) : (
        <>
          <span className="text-zinc-600 dark:text-zinc-300">Apagar esta visita e as outras deste visitante?</span>
          <button type="button" disabled={m.isPending} onClick={() => m.mutate()} className="rounded bg-rose-600 px-2 py-1 font-medium text-white hover:bg-rose-700 disabled:opacity-60">
            {m.isPending ? 'A apagar…' : 'Apagar'}
          </button>
          <button type="button" onClick={() => setConfirmar(false)} className="rounded px-2 py-1 text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800">Cancelar</button>
        </>
      )}
      {m.isError && <span className="text-rose-600">{apiErrorMessage(m.error)}</span>}
    </div>
  );
}

function LinhaSessao({ s }: { s: SessaoVisita }) {
  const [aberta, setAberta] = useState(false);
  const etiquetas = [
    s.regressou && { t: 'voltou', c: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-950/40 dark:text-indigo-300' },
    s.pesquisas.length > 0 && { t: `pesquisou ${s.pesquisas.length}`, c: TIPO.pesquisa.cor },
    s.carrinho && { t: 'carrinho', c: TIPO.carrinho.cor },
    s.checkout && { t: 'checkout', c: TIPO.checkout.cor },
    s.pagamento && { t: 'pagamento', c: TIPO.pagamento.cor },
    s.comprou && { t: `comprou${s.valor != null ? ` ${eur(s.valor)}` : ''}`, c: TIPO.compra.cor },
  ].filter(Boolean) as { t: string; c: string }[];
  return (
    <li className="border-b border-zinc-100 last:border-0 dark:border-zinc-800">
      <button type="button" onClick={() => setAberta((v) => !v)} className="flex w-full flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5 text-left hover:bg-zinc-50 dark:hover:bg-zinc-800/40" aria-expanded={aberta}>
        <span className="w-24 flex-none tabular-nums text-sm text-zinc-600 dark:text-zinc-300">{hora(s.inicio)}</span>
        <span className="min-w-0 flex-1 truncate text-sm font-medium">
          {s.cliente ?? `Visitante ${s.visitante.slice(0, 6)}`}
          <span className="ml-2 font-normal text-zinc-500">{s.origem ?? 'direto'}{s.campanha ? ` · ${s.campanha}` : ''} · {s.dispositivo ?? '?'} {s.sistema ?? ''}</span>
        </span>
        <span className="text-xs tabular-nums text-zinc-500">{s.paginas} pág. · {tempo(s.duracaoSeg)}</span>
        <span className="flex flex-wrap gap-1">{etiquetas.map((e) => <span key={e.t} className={`rounded px-1.5 py-0.5 text-xs font-medium ${e.c}`}>{e.t}</span>)}</span>
        <span className="w-full truncate text-xs text-zinc-500 sm:w-auto">entrou em {s.entrada ?? '—'} · saiu em {s.saida ?? '—'}</span>
      </button>
      {aberta && <div className="px-4 pb-3"><Percurso sessao={s.sessao} /><FuiEu sessao={s.sessao} /></div>}
    </li>
  );
}

export default function Visitas() {
  const [periodo, setPeriodo] = useState<(typeof PERIODOS)[number]['id']>('7');
  const { de, ate } = useMemo(() => {
    const hoje = new Date();
    const dias = PERIODOS.find((p) => p.id === periodo)!.dias;
    const ini = new Date(hoje);
    ini.setDate(hoje.getDate() - dias);
    return { de: iso(ini), ate: iso(hoje) };
  }, [periodo]);

  const resumo = useQuery({ queryKey: ['loja', 'visitas', 'resumo', de, ate], queryFn: () => visitasApi.resumo(de, ate), refetchInterval: 60_000 });
  const sessoes = useQuery({ queryKey: ['loja', 'visitas', 'sessoes', de, ate], queryFn: () => visitasApi.sessoes(de, ate), refetchInterval: 60_000 });
  const r = resumo.data;
  const maxDia = Math.max(1, ...(r?.porDia ?? []).map((d) => d.visitas));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Visitas do site"
        description="Quem entra no lopestech.pt, o que procura, por onde anda, onde desiste e o que compra. Atualiza sozinho a cada minuto."
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <div className="inline-flex rounded-lg border border-zinc-200 p-0.5 dark:border-zinc-700" role="group" aria-label="Período">
              {PERIODOS.map((p) => (
                <button key={p.id} type="button" onClick={() => setPeriodo(p.id)} aria-pressed={periodo === p.id}
                  className={`rounded-md px-3 py-1.5 text-sm font-medium ${periodo === p.id ? 'bg-brand-600 text-white' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}>
                  {p.label}
                </button>
              ))}
            </div>
            <Link to="/loja" className="text-sm font-medium text-brand-600 hover:underline">Produtos</Link>
            <Link to="/loja/encomendas" className="text-sm font-medium text-brand-600 hover:underline">Encomendas</Link>
          </div>
        }
      />

      {resumo.isLoading ? <SkeletonTable columns={4} rows={6} /> : resumo.isError ? (
        <p className="text-sm text-rose-600">{apiErrorMessage(resumo.error)}</p>
      ) : !r || r.visitas === 0 ? (
        <EmptyState icon={BarChart3} title="Ainda sem visitas neste período"
          description="As visitas começam a contar a partir de agora (o site passou a enviar cada página, pesquisa, carrinho e compra para o Mender)." />
      ) : (
        <>
          {r.truncado && <p className="rounded-md bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:bg-amber-950/30 dark:text-amber-300">Período com demasiados eventos — os números contam só os primeiros 250 mil. Escolhe um período mais curto.</p>}
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
            <KpiCard icon={Users} label="Visitantes" value={String(r.visitantes)} sub={`${r.regressaram} visita(s) de quem voltou`} />
            <KpiCard icon={Eye} label="Visitas" value={String(r.visitas)} sub={`${pct(r.visitasDeUmaPagina, r.visitas)} viram só 1 página`} tone="zinc" />
            <KpiCard icon={MousePointerClick} label="Páginas vistas" value={String(r.paginas)} sub={`${(r.paginas / Math.max(1, r.visitas)).toFixed(1).replace('.', ',')} por visita`} tone="zinc" />
            <KpiCard icon={Clock} label="Tempo médio" value={tempo(r.tempoMedioSeg)} sub="por visita" tone="amber" />
            <KpiCard icon={ShoppingCart} label="Carrinhos" value={String(r.funil.carrinho)} sub={`${r.funil.checkout} checkout · ${r.funil.pagamento} pagamento`} tone="amber" />
            <KpiCard icon={ShoppingCart} label="Compras" value={String(r.funil.compras)} sub={`${eur(r.funil.valorComprado)} · conversão ${pct(r.funil.compras, r.visitas)}`} tone="emerald" />
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <SectionCard title="Funil da loja (por visita)">
              <ul className="space-y-2">
                {[
                  ['Entraram no site', r.funil.visitas],
                  ['Viram um produto', r.funil.viramProduto],
                  ['Puseram no carrinho', r.funil.carrinho],
                  ['Abriram o checkout', r.funil.checkout],
                  ['Iniciaram o pagamento', r.funil.pagamento],
                  ['Compraram', r.funil.compras],
                ].map(([nome, n], i, arr) => (
                  <li key={nome as string} className="text-sm">
                    <div className="mb-0.5 flex justify-between"><span>{nome}</span><span className="tabular-nums"><b>{n}</b>{i > 0 ? <span className="ml-2 text-xs text-zinc-500">{pct(n as number, arr[i - 1][1] as number)} do passo anterior</span> : null}</span></div>
                    <div className="h-2 rounded bg-zinc-100 dark:bg-zinc-800"><div className="h-2 rounded bg-brand-500" style={{ width: `${((n as number) / Math.max(1, r.funil.visitas)) * 100}%` }} /></div>
                  </li>
                ))}
              </ul>
            </SectionCard>
            <SectionCard title="Visitas por dia">
              <div className="flex h-48 items-end gap-1" role="img" aria-label="Visitas por dia">
                {r.porDia.map((d) => (
                  <div key={d.dia} className="group flex flex-1 flex-col items-center justify-end" title={`${new Date(d.dia).toLocaleDateString('pt-PT')}: ${d.visitas} visitas, ${d.visitantes} visitantes, ${d.paginas} páginas, ${d.compras} compras`}>
                    <span className="text-[10px] tabular-nums text-zinc-500 opacity-0 group-hover:opacity-100">{d.visitas}</span>
                    <div className={`w-full rounded-t ${d.compras > 0 ? 'bg-emerald-500' : 'bg-brand-500'}`} style={{ height: `${(d.visitas / maxDia) * 100}%`, minHeight: 2 }} />
                    <span className="mt-1 text-[10px] text-zinc-500">{new Date(d.dia).getDate()}</span>
                  </div>
                ))}
              </div>
            </SectionCard>
          </div>

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <Lista titulo="O que pesquisaram e NÃO encontraram" itens={r.pesquisasSemResultados} vazio="Nenhuma pesquisa ficou sem resultados. 👍" destaque />
            <Lista titulo="O que pesquisaram" itens={r.pesquisas} vazio="Ninguém pesquisou neste período." />
            <Lista titulo="Produtos mais vistos" itens={r.produtos} vazio="Ainda ninguém abriu um produto." />
            <Lista titulo="Postos no carrinho" itens={r.carrinhos} vazio="Nenhum carrinho neste período." />
            <Lista titulo="Páginas mais vistas" itens={r.topPaginas} vazio="—" extra="visitas" media={(s) => `~${tempo(Math.round(s))}`} />
            <Lista titulo="De onde vieram" itens={r.origens} vazio="—" />
            <Lista titulo="Por onde entraram" itens={r.entradas} vazio="—" />
            <Lista titulo="Onde saíram (última página)" itens={r.saidas} vazio="—" />
            <Lista titulo="Dispositivos" itens={r.dispositivos} vazio="—" />
            {r.campanhas.length > 0 && <Lista titulo="Campanhas (utm)" itens={r.campanhas} vazio="—" />}
            <Lista titulo="Contactos (cliques)" itens={r.contactos} vazio="Ninguém clicou no WhatsApp, telefone ou email." />
          </div>
        </>
      )}

      <SectionCard title={`Visitas (${sessoes.data?.length ?? 0}) — clica para ver o percurso completo`} bodyClassName="p-0">
        {sessoes.isLoading ? <SkeletonTable columns={4} rows={5} /> : sessoes.isError ? (
          <p className="p-4 text-sm text-rose-600">{apiErrorMessage(sessoes.error)}</p>
        ) : (sessoes.data ?? []).length === 0 ? <p className="p-4 text-sm text-zinc-500">Sem visitas neste período.</p> : (
          <ul>{sessoes.data!.map((s) => <LinhaSessao key={s.sessao} s={s} />)}</ul>
        )}
      </SectionCard>
    </div>
  );
}
