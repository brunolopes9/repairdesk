import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, ClipboardList, Download, Loader2, Play, Search, X } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { useConfirm } from '../../components/ConfirmDialog';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { downloadFile } from '../../lib/downloadPdf';
import { stockTakesApi } from '../../lib/stockTakes/api';
import type { StockTake, StockTakeItem } from '../../lib/stockTakes/types';

/**
 * Sprint 421 (Doc 90 Tier 1 #3): inventário físico.
 *
 * Fluxo end-to-end:
 *   1. Click "Iniciar inventário" → snapshot de todas as Parts activas.
 *   2. Operador percorre a prateleira, search por nome/SKU/marca, edita "Contado".
 *   3. Conforme conta, vê diferenças destacadas. Quando termina, "Fechar" gera
 *      PartMovimentos de ajuste para cada item com diferença != 0.
 *
 * Apenas 1 inventário aberto por tenant (regra de servidor) — UI mostra ou o
 * aberto, ou um placeholder com botão para iniciar.
 */
export default function Inventario() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const current = useQuery({ queryKey: ['stocktake', 'current'], queryFn: stockTakesApi.current });

  const open = useMutation({
    mutationFn: () => stockTakesApi.open(),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['stocktake'] });
      toast.success('Inventário iniciado.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao iniciar inventário.'),
  });

  if (current.isLoading) {
    return <div className="flex items-center gap-2 p-6 text-sm text-zinc-500"><Loader2 className="animate-spin" size={16} /> A carregar…</div>;
  }

  if (!current.data) {
    return (
      <div className="space-y-5">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
            <ClipboardList size={24} /> Inventário físico
          </h1>
          <p className="text-sm text-zinc-500">Contagem da prateleira para reconciliar com o stock do sistema.</p>
        </div>

        <div className="rounded-xl border border-dashed border-zinc-300 p-10 text-center dark:border-zinc-700">
          <ClipboardList className="mx-auto mb-3 text-zinc-400" size={36} />
          <h2 className="text-base font-semibold">Nenhum inventário aberto</h2>
          <p className="mt-1 max-w-md mx-auto text-sm text-zinc-500">
            Iniciar cria uma sessão com todas as peças activas. Vais contar cada uma e,
            no fim, fechar para gerar os ajustes de stock automaticamente.
          </p>
          <Button
            className="mt-4"
            leftIcon={<Play size={16} />}
            onClick={() => open.mutate()}
            loading={open.isPending}
          >
            Iniciar inventário
          </Button>
        </div>
      </div>
    );
  }

  return <StockTakeBoard stockTake={current.data} onReload={() => qc.invalidateQueries({ queryKey: ['stocktake'] })} confirm={confirm} />;
}

function StockTakeBoard({
  stockTake,
  onReload,
  confirm,
}: {
  stockTake: StockTake;
  onReload: () => void;
  confirm: ReturnType<typeof useConfirm>;
}) {
  const qc = useQueryClient();
  const [q, setQ] = useState('');
  const [onlyPending, setOnlyPending] = useState(false);
  const [onlyDiffs, setOnlyDiffs] = useState(false);
  const [closeNotas, setCloseNotas] = useState('');
  const [closeOpen, setCloseOpen] = useState(false);

  const items = stockTake.items ?? [];

  const filtered = useMemo(() => {
    const term = q.trim().toLowerCase();
    return items.filter((i) => {
      if (onlyPending && i.qtdContada !== null) return false;
      if (onlyDiffs && (i.qtdContada === null || i.diferenca === 0)) return false;
      if (!term) return true;
      return (
        i.partNome.toLowerCase().includes(term) ||
        (i.partSku?.toLowerCase().includes(term) ?? false) ||
        (i.partMarca?.toLowerCase().includes(term) ?? false) ||
        (i.partModelo?.toLowerCase().includes(term) ?? false) ||
        (i.localArmazenamento?.toLowerCase().includes(term) ?? false)
      );
    });
  }, [items, q, onlyPending, onlyDiffs]);

  const close = useMutation({
    mutationFn: () => stockTakesApi.close(stockTake.id, closeNotas.trim() || null),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['stocktake'] });
      qc.invalidateQueries({ queryKey: ['parts'] });
      toast.success('Inventário fechado e ajustes aplicados.');
      setCloseOpen(false);
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao fechar inventário.'),
  });

  const cancel = useMutation({
    mutationFn: () => stockTakesApi.cancel(stockTake.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['stocktake'] });
      toast.success('Inventário cancelado.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao cancelar.'),
  });

  const pct = stockTake.totalItems > 0 ? Math.round((stockTake.contadosCount / stockTake.totalItems) * 100) : 0;

  return (
    <div className="space-y-5">
      {/* Header */}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-semibold tracking-tight">
            <ClipboardList size={24} /> Inventário em curso
          </h1>
          <p className="text-sm text-zinc-500">
            Iniciado em {new Date(stockTake.openedAt).toLocaleString('pt-PT')} · {stockTake.totalItems} peças
          </p>
        </div>
        <div className="flex gap-2">
          {/* Sprint 434: export CSV — para contabilista / arquivo. */}
          <Button
            variant="secondary"
            leftIcon={<Download size={15} />}
            onClick={() =>
              downloadFile(stockTakesApi.exportCsvPath(stockTake.id), `inventario_${stockTake.id.slice(0, 8)}.csv`)
                .then(() => toast.success('CSV exportado.'))
                .catch((e) => toast.error(e instanceof Error ? e.message : 'Erro ao exportar.'))
            }
          >
            Exportar CSV
          </Button>
          <Button
            variant="secondary"
            onClick={async () => {
              if (await confirm({ title: 'Cancelar inventário?', description: 'Vais perder todas as contagens registadas. Esta acção não pode ser desfeita.', confirmLabel: 'Cancelar inventário', destructive: true })) {
                cancel.mutate();
              }
            }}
            loading={cancel.isPending}
          >
            Cancelar
          </Button>
          <Button
            disabled={stockTake.contadosCount === 0}
            onClick={() => setCloseOpen(true)}
            leftIcon={<CheckCircle2 size={16} />}
          >
            Fechar e aplicar ajustes
          </Button>
        </div>
      </div>

      {/* Progresso */}
      <div className="grid gap-3 sm:grid-cols-3">
        <Stat label="Total de peças" value={stockTake.totalItems} />
        <Stat label="Contadas" value={`${stockTake.contadosCount} (${pct}%)`} accent="emerald" />
        <Stat label="Diferenças" value={stockTake.diferencasCount} accent={stockTake.diferencasCount > 0 ? 'amber' : 'zinc'} />
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-zinc-200 dark:bg-zinc-800">
        <div className="h-full bg-emerald-500 transition-all" style={{ width: `${pct}%` }} />
      </div>

      {/* Filtros */}
      <div className="flex flex-wrap items-center gap-2 rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="relative flex-1 min-w-[200px]">
          <Search size={14} className="pointer-events-none absolute left-2.5 top-2.5 text-zinc-400" />
          <input
            className="w-full rounded-lg border border-zinc-200 bg-white pl-8 pr-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
            placeholder="Procurar por nome, SKU, marca, modelo, localização…"
            value={q}
            onChange={(e) => setQ(e.target.value)}
          />
        </div>
        <label className="inline-flex items-center gap-1.5 text-xs">
          <input type="checkbox" checked={onlyPending} onChange={(e) => { setOnlyPending(e.target.checked); if (e.target.checked) setOnlyDiffs(false); }} />
          Por contar
        </label>
        <label className="inline-flex items-center gap-1.5 text-xs">
          <input type="checkbox" checked={onlyDiffs} onChange={(e) => { setOnlyDiffs(e.target.checked); if (e.target.checked) setOnlyPending(false); }} />
          Só diferenças
        </label>
      </div>

      {/* Tabela */}
      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="grid grid-cols-[1fr_90px_100px_100px_80px] gap-2 border-b border-zinc-200 bg-zinc-50 px-3 py-2 text-[11px] font-semibold uppercase tracking-wider text-zinc-500 dark:border-zinc-800 dark:bg-zinc-950">
          <div>Peça</div>
          <div className="text-right">Sistema</div>
          <div className="text-right">Contado</div>
          <div className="text-right">Diferença</div>
          <div />
        </div>
        {filtered.length === 0 && (
          <div className="px-3 py-6 text-center text-sm text-zinc-500">Sem resultados para os filtros atuais.</div>
        )}
        {filtered.map((item) => (
          <Row key={item.id} item={item} stockTakeId={stockTake.id} onReload={onReload} />
        ))}
      </div>

      {closeOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={() => setCloseOpen(false)}>
          <div className="w-full max-w-md rounded-2xl border border-zinc-200 bg-white p-5 shadow-xl dark:border-zinc-800 dark:bg-zinc-900" onClick={(e) => e.stopPropagation()}>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-lg font-semibold">Fechar inventário?</h2>
              <button type="button" onClick={() => setCloseOpen(false)} className="rounded-md p-1 text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800"><X size={18} /></button>
            </div>
            <p className="text-sm text-zinc-600 dark:text-zinc-300">
              Vão ser aplicados <strong>{stockTake.diferencasCount}</strong> ajustes de stock, gerados a partir das contagens.
              {stockTake.totalItems - stockTake.contadosCount > 0 && (
                <span className="mt-2 block rounded-md bg-amber-50 px-2.5 py-2 text-xs text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">
                  <AlertTriangle size={12} className="mr-1 inline" />
                  {stockTake.totalItems - stockTake.contadosCount} peças ainda não foram contadas — vão manter o stock actual.
                </span>
              )}
            </p>
            <textarea
              className="mt-3 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
              rows={3}
              placeholder="Notas (opcional)…"
              value={closeNotas}
              onChange={(e) => setCloseNotas(e.target.value)}
            />
            <div className="mt-4 flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setCloseOpen(false)}>Voltar</Button>
              <Button onClick={() => close.mutate()} loading={close.isPending}>Fechar e aplicar</Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function Row({ item, stockTakeId, onReload }: { item: StockTakeItem; stockTakeId: string; onReload: () => void }) {
  const qc = useQueryClient();
  const [value, setValue] = useState<string>(item.qtdContada?.toString() ?? '');

  const mut = useMutation({
    mutationFn: (n: number) => stockTakesApi.count(stockTakeId, item.partId, n),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['stocktake'] });
      onReload();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao guardar contagem.'),
  });

  const commit = () => {
    const n = Number(value);
    if (Number.isNaN(n) || n < 0) return;
    if (n === item.qtdContada) return;
    mut.mutate(n);
  };

  const diffClass = item.qtdContada === null
    ? 'text-zinc-400'
    : item.diferenca === 0
      ? 'text-emerald-700 dark:text-emerald-400'
      : item.diferenca > 0
        ? 'text-sky-700 dark:text-sky-400'
        : 'text-red-700 dark:text-red-400';

  return (
    <div className="grid grid-cols-[1fr_90px_100px_100px_80px] items-center gap-2 border-b border-zinc-100 px-3 py-2 text-sm last:border-b-0 dark:border-zinc-800/50">
      <div className="min-w-0">
        <div className="truncate font-medium">{item.partNome}</div>
        <div className="truncate text-[11px] text-zinc-500">
          {[item.partSku, item.partMarca, item.partModelo, item.localArmazenamento].filter(Boolean).join(' · ') || '—'}
        </div>
      </div>
      <div className="text-right tabular-nums">{item.qtdSistema}</div>
      <div>
        <input
          type="number"
          inputMode="numeric"
          min={0}
          className="w-full rounded-md border border-zinc-200 bg-white px-2 py-1 text-right text-sm tabular-nums outline-none focus:border-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onBlur={commit}
          onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
          placeholder="—"
        />
      </div>
      <div className={`text-right tabular-nums font-medium ${diffClass}`}>
        {item.qtdContada === null ? '—' : (item.diferenca > 0 ? `+${item.diferenca}` : item.diferenca)}
      </div>
      <div className="text-right">
        {mut.isPending && <Loader2 size={14} className="ml-auto animate-spin text-zinc-400" />}
        {!mut.isPending && item.qtdContada !== null && <CheckCircle2 size={14} className="ml-auto text-emerald-500" />}
      </div>
    </div>
  );
}

function Stat({ label, value, accent = 'zinc' }: { label: string; value: number | string; accent?: 'zinc' | 'emerald' | 'amber' }) {
  const accentCls = accent === 'emerald' ? 'text-emerald-700 dark:text-emerald-400'
    : accent === 'amber' ? 'text-amber-700 dark:text-amber-400'
    : 'text-zinc-700 dark:text-zinc-200';
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="text-[11px] uppercase tracking-wider text-zinc-500">{label}</div>
      <div className={`mt-1 text-xl font-semibold tabular-nums ${accentCls}`}>{value}</div>
    </div>
  );
}
