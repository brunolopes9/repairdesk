import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, Plus, Repeat } from 'lucide-react';
import { Button, EmptyState, SkeletonCard } from '../../components/ui';
import { DespesaFormModal } from '../../components/DespesasImputadas';
import { despesasApi } from '../../lib/despesas/api';
import { DESPESA_LABEL, type Despesa } from '../../lib/despesas/types';
import { formatCents, formatDateOnly } from '../../lib/money';

function periodicidadeLabel(meses: number | null) {
  if (meses === 1) return 'Mensal';
  if (meses === 3) return 'Trimestral';
  if (meses === 12) return 'Anual';
  return 'Sem periodicidade';
}

function proximaData(data: string, meses: number | null) {
  if (!meses) return null;
  const d = new Date(data);
  d.setMonth(d.getMonth() + meses);
  return d;
}

export default function RecorrentesTab() {
  const qc = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<Despesa | null>(null);
  const list = useQuery({
    queryKey: ['despesas', 'recorrentes'],
    queryFn: () => despesasApi.list({ isRecorrente: true, pageSize: 100 }),
  });

  const items = list.data?.items ?? [];

  function invalidate() {
    qc.invalidateQueries({ queryKey: ['despesas'] });
    qc.invalidateQueries({ queryKey: ['dashboard'] });
  }

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>
          Nova recorrente
        </Button>
      </div>

      <ul className="space-y-2">
        {list.isLoading && Array.from({ length: 3 }).map((_, index) => <SkeletonCard key={index} />)}
        {items.map((d) => {
          const next = proximaData(d.data, d.periodicidadeMeses);
          return (
            <li key={d.id} className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
              <button type="button" onClick={() => setEditing(d)} className="w-full text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[10px] font-medium text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
                    {DESPESA_LABEL[d.categoria]}
                  </span>
                  <span className="inline-flex items-center gap-1 rounded bg-emerald-100 px-1.5 py-0.5 text-[10px] font-medium text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300">
                    <Repeat size={12} /> {periodicidadeLabel(d.periodicidadeMeses)}
                  </span>
                  <span className="text-xs text-zinc-500">{formatDateOnly(d.data)}</span>
                </div>
                <div className="mt-2 flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
                  <div className="min-w-0">
                    <div className="truncate font-medium">{d.descricao}</div>
                    <div className="text-xs text-zinc-500">
                      {d.fornecedor ?? 'Sem fornecedor'}
                      {next && ` · próxima: ${formatDateOnly(next.toISOString())}`}
                    </div>
                  </div>
                  <span className="font-semibold text-red-600 dark:text-red-400">−{formatCents(d.valorCents)}</span>
                </div>
              </button>
            </li>
          );
        })}
        {items.length === 0 && !list.isLoading && (
          <li>
            <EmptyState
              icon={CalendarClock}
              title="Sem despesas recorrentes"
              description="Marca renda, energia, telecomunicações ou software como recorrentes para veres a próxima ocorrência prevista."
              action={<Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>Criar recorrente</Button>}
            />
          </li>
        )}
      </ul>

      <DespesaFormModal
        open={createOpen}
        initialRecorrente
        onClose={() => setCreateOpen(false)}
        onSaved={() => { invalidate(); setCreateOpen(false); }}
      />

      <DespesaFormModal
        open={!!editing}
        editing={editing}
        onClose={() => setEditing(null)}
        onSaved={() => { invalidate(); setEditing(null); }}
      />
    </div>
  );
}
