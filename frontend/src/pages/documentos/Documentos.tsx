import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Receipt, Banknote, Percent, FileText, Download, ExternalLink, Search } from 'lucide-react';
import { KpiCard } from '../../components/ui';
import { formatCents, formatDateOnly } from '../../lib/money';
import { downloadFile } from '../../lib/downloadPdf';
import { documentosApi } from '../../lib/documentos/api';
import type { DocumentoDto } from '../../lib/documentos/types';

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

type TipoFiltro = '' | 'FT' | 'FS';

export default function Documentos() {
  const hoje = new Date().toISOString().slice(0, 10);
  const inicioAno = `${new Date().getFullYear()}-01-01`;
  const [from, setFrom] = useState(inicioAno);
  const [to, setTo] = useState(hoje);
  const [q, setQ] = useState('');
  const [tipo, setTipo] = useState<TipoFiltro>('');

  const params = useMemo(() => ({
    from: new Date(`${from}T00:00:00`).toISOString(),
    to: new Date(`${to}T23:59:59`).toISOString(),
    q: q.trim() || undefined,
    tipo: tipo || undefined,
  }), [from, to, q, tipo]);

  const { data, isLoading } = useQuery({
    queryKey: ['documentos-vendas', params],
    queryFn: () => documentosApi.listVendas(params),
    staleTime: 30_000,
  });

  const items = data?.items ?? [];

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Vendas · Faturas</h1>
          <p className="text-sm text-zinc-500">
            Todas as faturas emitidas — reparações, balcão e trabalhos — num só sítio.
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
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiCard icon={Receipt} tone="emerald" label="Total faturado" value={formatCents(data?.totalCents ?? 0)} sub="com IVA" />
        <KpiCard icon={Banknote} tone="brand" label="Base (sem IVA)" value={formatCents(data?.totalBaseCents ?? 0)} />
        <KpiCard icon={Percent} tone="amber" label="IVA" value={formatCents(data?.totalIvaCents ?? 0)} />
        <KpiCard icon={FileText} tone="zinc" label="Documentos" value={String(data?.totalDocumentos ?? 0)} />
      </div>

      {/* Filtros */}
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex gap-1">
          {([['', 'Todos'], ['FT', 'Faturas'], ['FS', 'Simplificadas']] as const).map(([k, label]) => (
            <button
              key={k}
              type="button"
              onClick={() => setTipo(k)}
              className={`rounded-lg px-3 py-1.5 text-sm font-medium transition ${tipo === k ? 'bg-brand-600 text-white' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}
            >
              {label}
            </button>
          ))}
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
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center gap-2 p-10 text-center text-sm text-zinc-500">
            <FileText className="text-zinc-300" size={28} />
            Sem faturas no período. Emite uma a partir de uma reparação ou venda.
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
                {items.map((d: DocumentoDto) => (
                  <tr key={d.id} className="border-b border-zinc-100 transition last:border-0 hover:bg-zinc-50 dark:border-zinc-800/60 dark:hover:bg-zinc-800/50">
                    <td className="whitespace-nowrap px-4 py-3 text-zinc-600 dark:text-zinc-300">{formatDateOnly(d.data)}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex rounded-md px-2 py-0.5 text-[11px] font-semibold ${TIPO_BADGE[d.tipoCodigo] ?? 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>
                        {d.tipo}
                      </span>
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 font-medium">{d.numero ?? '—'}</td>
                    <td className="px-4 py-3">
                      <div className="font-medium">{d.clienteNome ?? 'Consumidor final'}</div>
                      {d.clienteNif && <div className="text-xs text-zinc-500">NIF {d.clienteNif}</div>}
                    </td>
                    <td className="px-4 py-3">
                      {ORIGEM_LINK[d.origem] ? (
                        <Link to={ORIGEM_LINK[d.origem](d.id)} className="text-brand-600 hover:underline dark:text-brand-400">
                          {ORIGEM_LABEL[d.origem] ?? d.origem} #{d.numeroInterno}
                        </Link>
                      ) : (
                        <span className="text-zinc-500">{ORIGEM_LABEL[d.origem] ?? d.origem} #{d.numeroInterno}</span>
                      )}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right font-semibold">{formatCents(d.totalCents)}</td>
                    <td className="px-4 py-3 text-right">
                      {d.pdfUrl ? (
                        <a href={d.pdfUrl} target="_blank" rel="noopener noreferrer"
                          className="inline-flex items-center gap-1 text-brand-600 hover:underline dark:text-brand-400" title="Abrir PDF (Moloni)">
                          <ExternalLink size={14} /> PDF
                        </a>
                      ) : (
                        <span className="text-xs text-zinc-400">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <p className="text-[11px] text-zinc-400">
        IVA estimado a 23% sobre o total (LopesTech opera maioritariamente a esta taxa). O SAF-T do Moloni continua a ser a fonte legal.
        Faturas antigas feitas directamente no Moloni vão aparecer aqui assim que ligar o histórico.
      </p>
    </div>
  );
}
