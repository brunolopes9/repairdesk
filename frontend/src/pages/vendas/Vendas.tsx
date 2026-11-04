import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronRight, Code2, Download, FileUp, Package, Plus, Receipt, Search, SlidersHorizontal, Wrench } from 'lucide-react';
import { useAuth } from '../../lib/auth/AuthContext';
import { apiErrorMessage } from '../../lib/errors';
import { downloadFile } from '../../lib/downloadPdf';
import { toast } from '../../lib/toast';
import { Button, DateRangeFilter, EmptyState, PageHeader, Paginacao, SectionCard, SkeletonRow, SortableTh, ViewTabs, intervaloParaInstantes } from '../../components/ui';
import { useOrdenacaoUrl } from '../../lib/useOrdenacao';
import { useFiltrosUrl } from '../../lib/useFiltrosUrl';
import { inputCls } from '../../components/ui/formClasses';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import { vendasApi } from '../../lib/vendas/api';
import DocumentosMoloniVista from './DocumentosMoloni';
import PecasEncomendaVista from './PecasEncomenda';
import { BotaoImprimir } from '../../components/BotaoImprimir';
import {
  VENDA_ESTADO,
  VENDA_ESTADO_COLOR,
  VENDA_ESTADO_LABEL,
  VENDA_TIPO,
  VENDA_TIPO_LABEL,
  type VendaFiltro,
  type VendaTipo,
} from '../../lib/vendas/types';

/** Doc 94 Fase 4: um só ecrã para Reparações, Serviços (software/websites) e Produtos. */
const VISTAS = [
  { key: 'curso', label: 'Em curso' },
  // Sprint 594: as peças que faltam nas reparações em curso (o que encomendar / o que está a chegar).
  { key: 'pecas', label: 'Peças a encomendar' },
  { key: 'entregues', label: 'Entregues' },
  { key: 'fatura', label: 'Fatura por registar' },
  { key: 'porpagar', label: 'Por pagar' },
  { key: 'todas', label: 'Todas' },
  // Sprint 575: rewards de prop firms (série TRADING) — entram no IVA, não nas vendas da loja.
  { key: 'trading', label: 'Trading' },
  // Sprint 571: a antiga página Faturação — faturas, recibos e orçamentos tal como estão no Moloni ON.
  { key: 'documentos', label: 'Documentos Moloni' },
] as const;
type Vista = (typeof VISTAS)[number]['key'];

const TIPO_ICON = { 0: Package, 1: Wrench, 2: Code2 } as const;
const PAGE_SIZE = 50;

export default function Vendas() {
  const navigate = useNavigate();
  // Sprint 592: vista, filtros, ordem e página no URL — ao voltar de uma venda, a lista está como se deixou.
  const { f, set, page, setPage } = useFiltrosUrl({ vista: 'curso', tipo: '', q: '', de: '', ate: '', page: '1', sort: 'data', dir: 'desc' });
  const vista = (VISTAS.find((v) => v.key === f.vista)?.key ?? 'curso') as Vista;
  const tipo = f.tipo !== '' ? (Number(f.tipo) as VendaTipo) : undefined;
  const q = f.q;
  const datas = { de: f.de, ate: f.ate };
  // Telemóvel: tipo e datas escondidos atrás de "Filtros" (no computador estão sempre à vista).
  const [filtrosAbertos, setFiltrosAbertos] = useState(false);
  const filtrosAtivos = (tipo !== undefined ? 1 : 0) + (datas.de || datas.ate ? 1 : 0);
  const { ordem, alternar: ordenar, params: ordemParams } = useOrdenacaoUrl<'numero' | 'cliente' | 'estado' | 'data' | 'total'>('data', f, set);

  const filtro: VendaFiltro = {
    q: q.trim() || undefined,
    tipo,
    page,
    pageSize: PAGE_SIZE,
    ...intervaloParaInstantes(datas),
    ...ordemParams,
    ...(vista === 'curso' ? { emCurso: true } : {}),
    ...(vista === 'entregues' ? { estado: VENDA_ESTADO.Entregue } : {}),
    ...(vista === 'fatura' ? { faturaPorRegistar: true } : {}),
    ...(vista === 'porpagar' ? { porPagar: true } : {}),
    ...(vista === 'trading' ? { atividade: 'trading' as const } : {}),
  };

  const list = useQuery({ queryKey: ['vendas', filtro], queryFn: () => vendasApi.list(filtro), placeholderData: keepPreviousData, enabled: vista !== 'documentos' && vista !== 'pecas' });
  const pecas = useQuery({ queryKey: ['vendas', 'pecas-encomenda'], queryFn: vendasApi.pecasEncomenda });
  const porFaturar = useQuery({ queryKey: ['vendas', 'fatura-count'], queryFn: () => vendasApi.list({ faturaPorRegistar: true, pageSize: 1 }) });
  const porPagar = useQuery({ queryKey: ['vendas', 'porpagar-count'], queryFn: () => vendasApi.list({ porPagar: true, pageSize: 1 }) });

  // Sprint 565: faturas emitidas no Moloni entram pelo PDF (uma ou várias de cada vez).
  const qc = useQueryClient();
  const isAdmin = useAuth().hasRole('Admin');
  const faturasRef = useRef<HTMLInputElement | null>(null);
  const [aImportar, setAImportar] = useState(false);
  async function importarFaturas(files: FileList | null) {
    if (!files?.length) return;
    setAImportar(true);
    const contagem = { criada: 0, ligada: 0, repetida: 0 };
    const problemas: string[] = [];
    for (const f of Array.from(files)) {
      try {
        const r = await vendasApi.importarFatura(f);
        contagem[r.resultado] += 1;
        if (r.aviso) problemas.push(`${r.invoiceNumber}: ${r.aviso}`);
      } catch (err) {
        problemas.push(`${f.name}: ${apiErrorMessage(err)}`);
      }
    }
    setAImportar(false);
    if (faturasRef.current) faturasRef.current.value = '';
    await qc.invalidateQueries({ queryKey: ['vendas'] });
    const resumo = [`${contagem.criada} venda(s) criada(s)`, contagem.ligada ? `${contagem.ligada} ligada(s) a vendas existentes` : null,
      contagem.repetida ? `${contagem.repetida} já estavam no Mender` : null].filter(Boolean).join(' · ');
    if (problemas.length) toast.error('Faturas importadas com avisos', `${resumo}. ${problemas.join(' | ')}`);
    else toast.success('Faturas importadas', resumo);
  }
  async function exportarFaturas() {
    try {
      await downloadFile(vendasApi.exportFaturasPath(datas.de, datas.ate), `Faturas-venda_${datas.de}_a_${datas.ate}.zip`);
    } catch (err) {
      toast.fromError(err, 'Não foi possível gerar o ZIP.');
    }
  }

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Vendas"
        description="Reparações, serviços e produtos — e as faturas, recibos e orçamentos do Moloni ON. O IVA e o lucro são calculados em cada venda."
        actions={
          <>
            <span className="hidden sm:contents">
            {isAdmin && (
              <>
                <input ref={faturasRef} type="file" accept=".pdf,application/pdf" multiple className="hidden" onChange={(e) => importarFaturas(e.target.files)} />
                <Button variant="secondary" leftIcon={<FileUp size={15} />} loading={aImportar} onClick={() => faturasRef.current?.click()}
                  title="PDFs das faturas emitidas no Moloni — cada uma cria a venda (ou liga-se à que já tem esse nº)">
                  Importar faturas
                </Button>
              </>
            )}
            <Button variant="secondary" leftIcon={<Download size={15} />} disabled={!datas.de || !datas.ate} onClick={exportarFaturas}
              title={datas.de && datas.ate ? 'ZIP com os PDFs das faturas de venda do período + indice.csv' : 'Escolhe as datas (ex.: Trimestre passado) para exportar as faturas'}>
              Faturas (ZIP)
            </Button>
            </span>
            <Button variant="secondary" className="flex-1 sm:flex-none" leftIcon={<Package size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Produto}`)}>Produto</Button>
            <Button variant="secondary" className="flex-1 sm:flex-none" leftIcon={<Code2 size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Servico}`)}>Serviço</Button>
            <Button className="flex-1 sm:flex-none" leftIcon={<Plus size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Reparacao}`)}>Reparação</Button>
          </>
        }
      />

      <ViewTabs
        value={vista}
        onChange={(v) => set({ vista: v })}
        tabs={VISTAS.map((v) => ({
          key: v.key,
          label: v.label,
          meta: v.key === 'pecas' && pecas.data?.length ? pecas.data.length
            : v.key === 'fatura' && porFaturar.data?.total ? porFaturar.data.total
            : v.key === 'porpagar' && porPagar.data?.total ? porPagar.data.total : undefined,
        }))}
      />

      {vista === 'documentos' ? <DocumentosMoloniVista isAdmin={isAdmin} /> : vista === 'pecas' ? <PecasEncomendaVista /> : (
      <SectionCard
        bodyClassName="p-0"
        action={
          // Sprint 593: imprime tudo o que a lista mostra (ex.: "Hoje" → as vendas do dia, num só PDF).
          <BotaoImprimir caminho="/imprimir/vendas" total={list.data?.total} nome={`Vendas${datas.de ? '_' + datas.de : ''}${datas.ate && datas.ate !== datas.de ? '_a_' + datas.ate : ''}.pdf`}
            params={{ from: filtro.from, to: filtro.to, tipo: filtro.tipo, estado: filtro.estado, emCurso: filtro.emCurso, faturaPorRegistar: filtro.faturaPorRegistar,
              porPagar: filtro.porPagar, q: filtro.q, atividade: filtro.atividade }} />
        }
        title={
          <div className="flex flex-1 flex-wrap items-center gap-2">
            <label className="relative min-w-48 flex-1">
              <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
              <input value={q} onChange={(e) => set({ q: e.target.value })} className={`${inputCls} pl-9`} placeholder="Nº, cliente, equipamento, fatura…" aria-label="Procurar vendas" />
            </label>
            <button type="button" onClick={() => setFiltrosAbertos((f) => !f)} aria-expanded={filtrosAbertos}
              className="relative grid h-11 w-11 place-items-center rounded-lg border border-zinc-300 text-zinc-600 sm:hidden dark:border-zinc-700 dark:text-zinc-300" aria-label="Filtros">
              <SlidersHorizontal size={17} />
              {filtrosAtivos > 0 && <span className="absolute -right-1 -top-1 grid h-4 min-w-4 place-items-center rounded-full bg-brand-600 px-1 text-[10px] font-semibold text-white">{filtrosAtivos}</span>}
            </button>
            <div className={`${filtrosAbertos ? 'flex' : 'hidden'} w-full flex-col gap-2 sm:flex sm:w-auto sm:flex-row sm:flex-wrap sm:items-center`}>
              <select value={tipo ?? ''} onChange={(e) => set({ tipo: e.target.value })} className={`${inputCls} sm:w-auto`} aria-label="Tipo">
                <option value="">Todos os tipos</option>
                {Object.entries(VENDA_TIPO_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
              </select>
              <DateRangeFilter value={datas} onChange={(v) => set({ de: v.de, ate: v.ate })} />
            </div>
          </div>
        }
      >
        <Paginacao posicao="cima" page={page} pages={pages} total={total} label="vendas" onPage={setPage} />
        {!list.isLoading && items.length === 0 ? (
          <div className="p-4">
            <EmptyState
              icon={Receipt}
              title={vista === 'fatura' ? 'Todas as vendas têm fatura registada' : 'Sem vendas aqui'}
              description={vista === 'curso' ? 'Cria uma reparação, um serviço ou uma venda de produto.' : undefined}
            />
          </div>
        ) : (
          <>
          {/* Sprint 572: no telemóvel, um cartão por venda (a tabela obrigava a deslizar para o lado). */}
          <ul className="divide-y divide-zinc-100 sm:hidden dark:divide-zinc-800">
            {list.isLoading && Array.from({ length: 4 }).map((_, i) => <li key={i} className="h-20 animate-pulse bg-zinc-50 dark:bg-zinc-900" />)}
            {items.map((v) => {
              const Icon = TIPO_ICON[v.tipo];
              return (
                <li key={v.id}>
                  <Link to={`/vendas/${v.id}`} className="flex items-start gap-3 px-4 py-3 active:bg-zinc-50 dark:active:bg-zinc-800/50">
                    <span className="mt-0.5 grid h-9 w-9 flex-none place-items-center rounded-lg bg-brand-50 text-brand-700 dark:bg-brand-950/40 dark:text-brand-300">
                      <Icon size={16} aria-label={VENDA_TIPO_LABEL[v.tipo]} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex items-baseline justify-between gap-2">
                        <span className="truncate font-medium">#{v.numero} · {v.cliente?.nome ?? 'Consumidor final'}</span>
                        <span className="flex-none font-semibold tabular-nums">{formatEur(v.totalCents / 100)}</span>
                      </span>
                      <span className="block truncate text-xs text-zinc-500">{v.equipamento ?? v.problema ?? v.items.map((i) => i.descricao).join(', ')}</span>
                      <span className="mt-1 flex flex-wrap items-center gap-1 text-xs">
                        <span className={`rounded-full px-2 py-0.5 font-medium ${VENDA_ESTADO_COLOR[v.estado]}`}>{VENDA_ESTADO_LABEL[v.estado]}</span>
                        {v.faturaPorRegistar && <span className="rounded-full bg-amber-100 px-2 py-0.5 font-medium text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">Sem fatura</span>}
                        {v.paga === false && <span className="rounded-full bg-sky-100 px-2 py-0.5 font-medium text-sky-800 dark:bg-sky-950/40 dark:text-sky-300">Por pagar</span>}
                        <span className="ml-auto tabular-nums text-zinc-500">{formatDateOnly(v.data)}</span>
                      </span>
                    </span>
                    <ChevronRight size={16} className="mt-2 flex-none text-zinc-300" aria-hidden />
                  </Link>
                </li>
              );
            })}
          </ul>
          <div className="hidden overflow-x-auto sm:block">
            <table className="w-full min-w-[46rem] text-sm">
              <thead className="border-b border-zinc-100 text-left text-xs text-zinc-500 dark:border-zinc-800">
                <tr>
                  <SortableTh className="px-4 py-2" label="Nº" campo="numero" ordem={ordem} onOrdenar={ordenar} />
                  <SortableTh className="px-4 py-2" label="Cliente / equipamento" campo="cliente" ordem={ordem} onOrdenar={ordenar} />
                  <SortableTh className="px-4 py-2" label="Estado" campo="estado" ordem={ordem} onOrdenar={ordenar} />
                  <SortableTh className="px-4 py-2" label="Data" campo="data" ordem={ordem} onOrdenar={ordenar} />
                  <SortableTh className="px-4 py-2" label="Total" campo="total" ordem={ordem} onOrdenar={ordenar} align="right" />
                  <th className="px-4 py-2 text-right font-medium">Lucro</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {list.isLoading && Array.from({ length: 5 }).map((_, i) => <tr key={i}><td colSpan={6}><SkeletonRow columns={6} /></td></tr>)}
                {items.map((v) => {
                  const Icon = TIPO_ICON[v.tipo];
                  return (
                    <tr key={v.id} className="transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                      <td className="px-4 py-3">
                        <Link to={`/vendas/${v.id}`} className="inline-flex items-center gap-1.5 font-medium text-brand-700 hover:underline dark:text-brand-300">
                          <Icon size={14} aria-label={VENDA_TIPO_LABEL[v.tipo]} /> #{v.numero}
                        </Link>
                      </td>
                      <td className="px-4 py-3">
                        <div>{v.cliente?.nome ?? <span className="text-zinc-400">Consumidor final</span>}</div>
                        <div className="text-xs text-zinc-500">{v.equipamento ?? v.problema ?? v.items.map((i) => i.descricao).join(', ')}</div>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${VENDA_ESTADO_COLOR[v.estado]}`}>{VENDA_ESTADO_LABEL[v.estado]}</span>
                        {v.faturaPorRegistar && <span className="ml-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">Sem fatura</span>}
                        {v.paga === false && <span className="ml-1 rounded-full bg-sky-100 px-2 py-0.5 text-xs font-medium text-sky-800 dark:bg-sky-950/40 dark:text-sky-300">Por pagar</span>}
                      </td>
                      <td className="px-4 py-3 tabular-nums">{formatDateOnly(v.data)}</td>
                      <td className="px-4 py-3 text-right font-medium tabular-nums">{formatEur(v.totalCents / 100)}</td>
                      <td className={`px-4 py-3 text-right tabular-nums ${v.lucro < 0 ? 'text-rose-600' : ''}`}>{formatEur(v.lucro)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          </>
        )}
        <Paginacao page={page} pages={pages} total={total} label="vendas" onPage={setPage} />
      </SectionCard>
      )}
    </div>
  );
}
