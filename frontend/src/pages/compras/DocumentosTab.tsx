import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { FileSpreadsheet, Plus, ReceiptText, Search } from 'lucide-react';
import { Button, EmptyState, SectionCard, SkeletonRow } from '../../components/ui';
import { useAuth } from '../../lib/auth/AuthContext';
import { comprasApi } from '../../lib/compras/api';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import { fornecedoresApi, REGIME_IVA } from '../../lib/fornecedores/api';
import ImportarExcelModal from './ImportarExcelModal';
import { inputCls } from './ui';

const PAGE_SIZE = 50;

export default function DocumentosTab() {
  const navigate = useNavigate();
  const isAdmin = useAuth().hasRole('Admin');
  const [q, setQ] = useState('');
  const [fornecedorId, setFornecedorId] = useState('');
  const [faturaEmFalta, setFaturaEmFalta] = useState(false);
  const [page, setPage] = useState(1);
  const [importOpen, setImportOpen] = useState(false);

  const fornecedores = useQuery({ queryKey: ['fornecedores', false], queryFn: () => fornecedoresApi.list(false) });
  const list = useQuery({
    queryKey: ['compras', 'docs', q, fornecedorId, faturaEmFalta, page],
    queryFn: () => comprasApi.search({ q: q || undefined, fornecedorId: fornecedorId || undefined, faturaEmFalta, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const semFiltros = !q && !fornecedorId && !faturaEmFalta;

  return (
    <SectionCard
      bodyClassName="p-0"
      title={
        <div className="flex flex-1 flex-wrap items-center gap-2">
          <label className="relative min-w-48 flex-1">
            <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
            <input
              value={q}
              onChange={(e) => { setQ(e.target.value); setPage(1); }}
              className={`${inputCls} pl-9`}
              placeholder="Procurar nº fatura, encomenda ou artigo…"
              aria-label="Procurar"
            />
          </label>
          <select value={fornecedorId} onChange={(e) => { setFornecedorId(e.target.value); setPage(1); }} className={`${inputCls} w-auto`} aria-label="Fornecedor">
            <option value="">Todos os fornecedores</option>
            {fornecedores.data?.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
          </select>
          <label className="flex min-h-11 items-center gap-2 text-sm">
            <input type="checkbox" checked={faturaEmFalta} onChange={(e) => { setFaturaEmFalta(e.target.checked); setPage(1); }} />
            Fatura em falta
          </label>
        </div>
      }
      action={
        <div className="flex gap-2">
          {isAdmin && (
            <Button variant="secondary" leftIcon={<FileSpreadsheet size={15} />} onClick={() => setImportOpen(true)}>Importar Excel</Button>
          )}
          <Button leftIcon={<Plus size={15} />} onClick={() => navigate('/compras/nova')}>Nova compra</Button>
        </div>
      }
    >
      {!list.isLoading && items.length === 0 ? (
        <div className="p-4">
          <EmptyState
            icon={ReceiptText}
            title={semFiltros ? 'Ainda não há compras registadas' : 'Nenhuma compra encontrada'}
            description={semFiltros ? 'Regista a primeira fatura de fornecedor — cada linha passa a ser um lote de stock.' : 'Experimenta limpar os filtros.'}
          />
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[48rem] text-sm">
            <thead className="border-b border-zinc-100 text-left text-xs text-zinc-500 dark:border-zinc-800">
              <tr>
                <th className="px-4 py-2 font-medium">Data</th>
                <th className="px-4 py-2 font-medium">Fornecedor</th>
                <th className="px-4 py-2 font-medium">Documento</th>
                <th className="px-4 py-2 text-right font-medium">Unid.</th>
                <th className="px-4 py-2 text-right font-medium">Em stock</th>
                <th className="px-4 py-2 text-right font-medium">Total</th>
                <th className="px-4 py-2 font-medium">Estado</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {list.isLoading && Array.from({ length: 4 }).map((_, i) => <tr key={i}><td colSpan={7}><SkeletonRow columns={7} /></td></tr>)}
              {items.map((d) => (
                <tr key={d.id} className="transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                  <td className="px-4 py-3 tabular-nums">{formatDateOnly(d.data)}</td>
                  <td className="px-4 py-3">
                    {d.fornecedorNome}
                    {d.regimeIva === REGIME_IVA.UeAutoliquidacao && (
                      <span className="ml-1.5 rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-950/40 dark:text-amber-400">UE</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <Link to={`/compras/${d.id}`} className="font-medium text-brand-700 hover:underline dark:text-brand-300">
                      {d.numeroFatura ?? (d.numerosEncomenda.length ? `Enc. ${d.numerosEncomenda.join(', ')}` : 'Sem referência')}
                    </Link>
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums">{d.unidades}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{d.unidadesEmStock}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{formatEur(d.totalCalculado)}</td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {d.faturaEmFalta && <Badge tone="amber">Fatura em falta</Badge>}
                      {d.temDiferenca && <Badge tone="rose">Dif. {formatEur(d.diferenca)}</Badge>}
                      {!d.faturaEmFalta && !d.temDiferenca && <Badge tone="emerald">OK</Badge>}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {pages > 1 && (
        <div className="flex items-center justify-between border-t border-zinc-100 px-4 py-2 text-sm dark:border-zinc-800">
          <span className="text-zinc-500">{total} documentos</span>
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
            <Button size="sm" variant="secondary" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
          </div>
        </div>
      )}
      <ImportarExcelModal open={importOpen} onClose={() => setImportOpen(false)} />
    </SectionCard>
  );
}

const BADGE = {
  amber: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
  rose: 'bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300',
  emerald: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300',
};

function Badge({ tone, children }: { tone: keyof typeof BADGE; children: React.ReactNode }) {
  return <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${BADGE[tone]}`}>{children}</span>;
}
