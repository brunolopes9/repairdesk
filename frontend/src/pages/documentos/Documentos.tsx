import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Receipt, Banknote, Percent, FileText, Download, ExternalLink, Search, FilePlus, ChevronRight, ChevronDown } from 'lucide-react';
import { KpiCard } from '../../components/ui';
import { formatCents, formatDateOnly } from '../../lib/money';
import { downloadFile } from '../../lib/downloadPdf';
import { toast } from '../../lib/toast';
import { documentosApi } from '../../lib/documentos/api';
import type { DocumentoDto } from '../../lib/documentos/types';
import NovaFaturaModal from '../../components/documentos/NovaFaturaModal';

/**
 * Sprint 513: separador "Vendas" de Compras e Operação — a lista única de TODAS as faturas
 * emitidas (de reparações, vendas de balcão ou trabalhos), o Mender como sítio único onde se
 * vêem todos os documentos. Filtros por período/tipo/pesquisa, totais e export para contabilista.
 */

const TIPO_BADGE: Record<string, string> = {
  FT: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  FS: 'bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300',
  FR: 'bg-violet-100 text-violet-700 dark:bg-violet-900/40 dark:text-violet-300',
  NC: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
  // Sprint 529: recibos (liquidação) e outros tipos que agora aparecem na lista.
  RG: 'bg-teal-100 text-teal-700 dark:bg-teal-900/40 dark:text-teal-300',
  VD: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  ND: 'bg-orange-100 text-orange-700 dark:bg-orange-900/40 dark:text-orange-300',
  // Sprint 529d: orçamentos (pré-venda, não fiscal) — neutro/índigo.
  ORC: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-900/40 dark:text-indigo-300',
};

const ORIGEM_LABEL: Record<string, string> = {
  Venda: 'Balcão',
  Reparacao: 'Reparação',
  Trabalho: 'Trabalho',
};

const ORIGEM_LINK: Record<string, (id: string) => string> = {
  Venda: (id) => `/vendas/${id}`,
  Reparacao: (id) => `/reparacoes/${id}`,
  Trabalho: (id) => `/trabalhos/${id}`,
};

type TipoFiltro = '' | 'FT' | 'FS' | 'FR' | 'RG' | 'NC' | 'ORC';

// Sprint 529: secções por tipo, como um ERP a sério. Os Recibos (RG) vêm do receipts/getAll do
// Moloni (família separada); Faturas-Recibo e Notas de Crédito já vinham do documents/getAll.
// Sprint 529d: Orçamentos (ORC) vêm dos campos Estimate* da reparação/trabalho — fora dos totais.
const TIPO_TABS: ReadonlyArray<readonly [TipoFiltro, string]> = [
  ['', 'Todos'],
  ['FT', 'Faturas'],
  ['FS', 'Simplificadas'],
  ['FR', 'Faturas-Recibo'],
  ['RG', 'Recibos'],
  ['NC', 'Notas de crédito'],
  ['ORC', 'Orçamentos'],
];

export default function Documentos() {
  const hoje = new Date().toISOString().slice(0, 10);
  const inicioAno = `${new Date().getFullYear()}-01-01`;
  const [from, setFrom] = useState(inicioAno);
  const [to, setTo] = useState(hoje);
  const [q, setQ] = useState('');
  const [tipo, setTipo] = useState<TipoFiltro>('');
  // Sprint 544: "Montante em dívida" (consulta de pendentes, como no Moloni) — só FT ativas sem recibo.
  const [soDivida, setSoDivida] = useState(false);
  const [novaFaturaOpen, setNovaFaturaOpen] = useState(false);

  // Sprint 529: o tipo é filtrado NO CLIENTE (tabs com contagem por secção). O backend devolve
  // todos os tipos do período de uma vez → trocar de secção é instantâneo e dá para contar cada uma.
  const params = useMemo(() => ({
    from: new Date(`${from}T00:00:00`).toISOString(),
    to: new Date(`${to}T23:59:59`).toISOString(),
    q: q.trim() || undefined,
  }), [from, to, q]);

  const { data, isLoading } = useQuery({
    queryKey: ['documentos-vendas', params],
    queryFn: () => documentosApi.listVendas(params),
    staleTime: 30_000,
  });

  const items = data?.items ?? [];

  // Sprint 529: contagem por tipo (para os badges dos tabs) + lista visível da secção activa.
  const countByTipo = useMemo(() => {
    const m: Record<string, number> = {};
    for (const d of items) m[d.tipoCodigo] = (m[d.tipoCodigo] ?? 0) + 1;
    return m;
  }, [items]);
  // Sprint 544: em dívida = Fatura a crédito (FT) ativa que ainda não foi liquidada por Recibo.
  // FS/FR/VD são pagamento imediato; NC/ORC/RG não são dívida. (ND fica de fora — Bruno não usa.)
  const emDivida = useMemo(
    () => items.filter((d) => d.tipoCodigo === 'FT' && d.estado === 'Ativo' && !d.reciboNumero),
    [items],
  );
  const emDividaTotal = useMemo(() => emDivida.reduce((s, d) => s + d.totalCents, 0), [emDivida]);
  const visibleItems = useMemo(
    () => (soDivida ? emDivida : tipo ? items.filter((d) => d.tipoCodigo === tipo) : items),
    [items, tipo, soDivida, emDivida],
  );

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Vendas · Faturas</h1>
          <p className="text-sm text-zinc-500">
            Todos os documentos — faturas, recibos, orçamentos — de reparações, balcão e trabalhos.
          </p>
        </div>
        <div className="flex items-end gap-2">
          <label className="flex flex-col text-[11px] font-medium text-zinc-500">
            De
            <input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)}
              className="mt-0.5 rounded-lg border border-zinc-200 bg-white px-2.5 py-1.5 text-sm dark:border-zinc-700 dark:bg-zinc-900" />
          </label>
          <label className="flex flex-col text-[11px] font-medium text-zinc-500">
            Até
            <input type="date" value={to} min={from} max={hoje} onChange={(e) => setTo(e.target.value)}
              className="mt-0.5 rounded-lg border border-zinc-200 bg-white px-2.5 py-1.5 text-sm dark:border-zinc-700 dark:bg-zinc-900" />
          </label>
          <button
            type="button"
            onClick={() => downloadFile(documentosApi.exportVendasPath(params.from, params.to), 'faturas-vendas.csv')}
            disabled={items.length === 0}
            className="flex h-9 items-center gap-1.5 rounded-lg border border-zinc-200 px-3 text-sm font-medium transition hover:bg-zinc-100 disabled:opacity-50 dark:border-zinc-800 dark:hover:bg-zinc-800"
          >
            <Download size={15} /> Exportar CSV
          </button>
          <button
            type="button"
            onClick={() => setNovaFaturaOpen(true)}
            className="flex h-9 items-center gap-1.5 rounded-lg bg-emerald-600 px-3 text-sm font-medium text-white shadow-sm transition hover:bg-emerald-700"
          >
            <FilePlus size={15} /> Nova fatura
          </button>
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <KpiCard icon={Receipt} tone="emerald" label="Total faturado" value={formatCents(data?.totalCents ?? 0)} sub="com IVA" />
        <KpiCard icon={Banknote} tone="brand" label="Base (sem IVA)" value={formatCents(data?.totalBaseCents ?? 0)} />
        <KpiCard icon={Percent} tone="amber" label="IVA" value={formatCents(data?.totalIvaCents ?? 0)} />
        <KpiCard icon={FileText} tone="zinc" label="Documentos" value={String(data?.totalDocumentos ?? 0)} />
        {/* Sprint 544: clicar filtra a lista para as faturas por receber. */}
        <button
          type="button"
          onClick={() => { setSoDivida((v) => !v); setTipo(''); }}
          className={`rounded-xl text-left transition focus:outline-none focus-visible:ring-2 focus-visible:ring-red-400 ${soDivida ? 'ring-2 ring-red-400' : ''}`}
          title="Faturas a crédito (FT) ativas ainda sem recibo de liquidação — clica para listar"
        >
          <KpiCard icon={Banknote} tone="red" label="Em dívida" value={formatCents(emDividaTotal)}
            sub={`${emDivida.length} ${emDivida.length === 1 ? 'fatura' : 'faturas'} por receber`} />
        </button>
      </div>

      {/* Filtros */}
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-wrap gap-1">
          {TIPO_TABS.map(([k, label]) => {
            const n = k === '' ? items.length : (countByTipo[k] ?? 0);
            const active = !soDivida && tipo === k;
            return (
              <button
                key={k || 'all'}
                type="button"
                onClick={() => { setTipo(k); setSoDivida(false); }}
                className={`rounded-lg px-3 py-1.5 text-sm font-medium transition ${active ? 'bg-brand-600 text-white' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}
              >
                {label}
                <span className={`ml-1.5 text-xs ${active ? 'text-white/80' : 'text-zinc-400'}`}>{n}</span>
              </button>
            );
          })}
          {/* Sprint 544: listagem de pendentes — chip dedicado, como a consulta do Moloni. */}
          <button
            type="button"
            onClick={() => { setSoDivida((v) => !v); setTipo(''); }}
            className={`rounded-lg px-3 py-1.5 text-sm font-medium transition ${soDivida ? 'bg-red-600 text-white' : 'text-red-700 hover:bg-red-50 dark:text-red-300 dark:hover:bg-red-950/40'}`}
          >
            Em dívida
            <span className={`ml-1.5 text-xs ${soDivida ? 'text-white/80' : 'text-red-400'}`}>{emDivida.length}</span>
          </button>
        </div>
        <div className="relative sm:w-72">
          <Search size={15} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Procurar nº, cliente ou NIF…"
            className="w-full rounded-lg border border-zinc-200 bg-white py-1.5 pl-8 pr-3 text-sm dark:border-zinc-700 dark:bg-zinc-900"
          />
        </div>
      </div>

      {/* Tabela */}
      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        {isLoading ? (
          <div className="p-4 text-sm text-zinc-500">A carregar…</div>
        ) : visibleItems.length === 0 ? (
          <div className="flex flex-col items-center gap-2 p-10 text-center text-sm text-zinc-500">
            <FileText className="text-zinc-300" size={28} />
            {items.length === 0
              ? 'Sem documentos no período. Emite uma fatura a partir de uma reparação ou venda.'
              : soDivida
                ? 'Sem faturas em dívida no período — está tudo recebido. 🎉'
                : 'Sem documentos deste tipo no período.'}
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-zinc-200 text-left text-[11px] font-semibold uppercase tracking-wide text-zinc-500 dark:border-zinc-800">
                  <th className="px-4 py-2.5">Data</th>
                  <th className="px-4 py-2.5">Tipo</th>
                  <th className="px-4 py-2.5">Número</th>
                  <th className="px-4 py-2.5">Cliente</th>
                  <th className="px-4 py-2.5">Origem</th>
                  <th className="px-4 py-2.5 text-right">Total</th>
                  <th className="px-4 py-2.5 text-right">PDF</th>
                </tr>
              </thead>
              <tbody>
                {visibleItems.map((d: DocumentoDto) => (
                  // Sprint 529 fix: docs só-Moloni têm Id=Guid.Empty (todos iguais!) → key duplicada
                  // fazia o React reaproveitar linhas e a lista "colava" no filtro anterior. O
                  // externalId (document_id Moloni) é único; cai no Guid para docs locais.
                  <DocumentoRow key={d.externalId ?? d.id} d={d} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <p className="text-[11px] text-zinc-400">
        Inclui o histórico do Moloni — faturas antigas, notas de crédito e documentos feitos directamente no painel aparecem aqui (com valores e estado reais, sincronizados a cada poucos minutos).
        Para os documentos ainda só locais, o IVA é estimado a 23%. O SAF-T do Moloni continua a ser a fonte legal.
      </p>

      <NovaFaturaModal open={novaFaturaOpen} onClose={() => setNovaFaturaOpen(false)} />
    </div>
  );
}

/**
 * Sprint 523: linha de fatura EXPANSÍVEL — clicar abre a reparação/trabalho/venda de origem,
 * o cliente (com link) e os valores discriminados. Centraliza tudo como ERP (pedido do Bruno:
 * "ao clicar numa fatura mostrar a reparação/trabalho e cliente sobre o qual foi emitida").
 */
function DocumentoRow({ d }: { d: DocumentoDto }) {
  const [open, setOpen] = useState(false);
  const qc = useQueryClient();
  const origemHref = ORIGEM_LINK[d.origem]?.(d.id);
  const origemLabel = `${ORIGEM_LABEL[d.origem] ?? d.origem}${d.numeroInterno > 0 ? ` #${d.numeroInterno}` : ''}`;

  // Sprint 527/528: só a Fatura pura (FT) activa AINDA SEM recibo pode emitir recibo de liquidação.
  // Com recibo já emitido (d.reciboNumero), o botão desaparece → evita documentos duplicados.
  const podeEmitirRecibo = d.tipoCodigo === 'FT' && d.estado === 'Ativo' && !!d.externalId && !d.reciboNumero;
  // Sprint 544: idade da dívida — FT ativa sem recibo está por receber desde a emissão.
  // >30 dias = fora do prazo habitual PT → vermelho; até 30 = âmbar.
  const emDivida = d.tipoCodigo === 'FT' && d.estado === 'Ativo' && !d.reciboNumero;
  const diasDivida = emDivida ? Math.max(0, Math.floor((Date.now() - new Date(d.data).getTime()) / 86_400_000)) : 0;
  const emitirRecibo = useMutation({
    mutationFn: () => documentosApi.emitirRecibo(Number(d.externalId)),
    onSuccess: (r) => {
      toast.success(`Recibo emitido — fatura ${d.numero ?? ''} liquidada (${formatCents(r.valorCents)}).`);
      qc.invalidateQueries({ queryKey: ['documentos-vendas'] });
    },
    onError: (err: unknown) => {
      const detail = (err as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
      toast.error(detail?.detail ?? detail?.title ?? 'Não foi possível emitir o recibo.');
    },
  });
  return (
    <>
      <tr
        onClick={() => setOpen((o) => !o)}
        className="cursor-pointer border-b border-zinc-100 transition last:border-0 hover:bg-zinc-50 dark:border-zinc-800/60 dark:hover:bg-zinc-800/50"
      >
        <td className="whitespace-nowrap px-4 py-3 text-zinc-600 dark:text-zinc-300">
          <span className="mr-1.5 inline-flex align-middle text-zinc-400">{open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}</span>
          {formatDateOnly(d.data)}
        </td>
        <td className="px-4 py-3">
          <span className={`inline-flex rounded-md px-2 py-0.5 text-[11px] font-semibold ${TIPO_BADGE[d.tipoCodigo] ?? 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>{d.tipo}</span>
        </td>
        <td className="whitespace-nowrap px-4 py-3 font-medium">
          <span className={d.estado === 'Anulado' ? 'text-zinc-400 line-through' : ''}>{d.numero ?? '—'}</span>
          {(d.estado === 'Anulado' || d.estado === 'Rascunho') && (
            <span className={`ml-1.5 inline-flex rounded px-1.5 py-0.5 text-[10px] font-semibold ${d.estado === 'Anulado' ? 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300' : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>{d.estado}</span>
          )}
          {emDivida && (
            <span
              className={`ml-1.5 inline-flex rounded px-1.5 py-0.5 text-[10px] font-semibold ${diasDivida > 30 ? 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300' : 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300'}`}
              title={`Fatura a crédito por receber há ${diasDivida} dia${diasDivida === 1 ? '' : 's'} (sem recibo de liquidação)`}
            >
              dívida · {diasDivida}d
            </span>
          )}
        </td>
        <td className="px-4 py-3">
          <div className="font-medium">{d.clienteNome ?? 'Consumidor final'}</div>
          {d.clienteNif && <div className="text-xs text-zinc-500">NIF {d.clienteNif}</div>}
        </td>
        <td className="px-4 py-3 text-zinc-500">{origemLabel}</td>
        <td className="whitespace-nowrap px-4 py-3 text-right font-semibold">{formatCents(d.totalCents)}</td>
        <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
          {d.pdfUrl ? (
            <a href={d.pdfUrl} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 text-brand-600 hover:underline dark:text-brand-400" title="Abrir PDF (Moloni)">
              <ExternalLink size={14} /> PDF
            </a>
          ) : (
            <span className="text-xs text-zinc-400">—</span>
          )}
        </td>
      </tr>
      {open && (
        <tr className="border-b border-zinc-100 bg-zinc-50/60 dark:border-zinc-800/60 dark:bg-zinc-900/40">
          <td colSpan={7} className="px-4 py-3">
            <div className="grid gap-4 sm:grid-cols-3">
              <div>
                <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">Emitida a partir de</div>
                {origemHref ? (
                  <Link to={origemHref} className="inline-flex items-center gap-1 font-medium text-brand-600 hover:underline dark:text-brand-400">
                    {origemLabel} <ExternalLink size={12} />
                  </Link>
                ) : (
                  <div className="text-zinc-600 dark:text-zinc-300">Documento criado diretamente no Moloni</div>
                )}
              </div>
              <div>
                <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">Cliente</div>
                {d.clienteId ? (
                  <Link to={`/clientes/${d.clienteId}`} className="inline-flex items-center gap-1 font-medium text-brand-600 hover:underline dark:text-brand-400">
                    {d.clienteNome ?? 'Consumidor final'} <ExternalLink size={12} />
                  </Link>
                ) : (
                  <div className="font-medium">{d.clienteNome ?? 'Consumidor final'}</div>
                )}
                {d.clienteNif && <div className="text-xs text-zinc-500">NIF {d.clienteNif}</div>}
              </div>
              <div>
                <div className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-zinc-500">Valores</div>
                <div className="text-zinc-600 dark:text-zinc-300">Base {formatCents(d.baseCents)} · IVA {formatCents(d.ivaCents)}</div>
                <div className="font-semibold">Total {formatCents(d.totalCents)}</div>
                <div className="mt-0.5 text-xs text-zinc-500">Estado: {d.estado}</div>
                {d.reciboNumero && (
                  <div className="mt-1.5 inline-flex items-center gap-1 rounded-lg bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-400">
                    <Receipt size={13} /> Liquidada · {d.reciboNumero}
                  </div>
                )}
                {podeEmitirRecibo && (
                  <button
                    type="button"
                    onClick={() => {
                      if (confirm(`Emitir um Recibo que liquida a fatura ${d.numero ?? ''} (${formatCents(d.totalCents)})?\n\nSó deves fazer isto se a fatura ainda está em dívida.`))
                        emitirRecibo.mutate();
                    }}
                    disabled={emitirRecibo.isPending}
                    className="mt-2 inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white transition hover:bg-emerald-700 disabled:opacity-50"
                    title="Liquida a fatura a crédito emitindo um Recibo no Moloni"
                  >
                    <Receipt size={13} /> {emitirRecibo.isPending ? 'A emitir…' : 'Emitir recibo'}
                  </button>
                )}
              </div>
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
