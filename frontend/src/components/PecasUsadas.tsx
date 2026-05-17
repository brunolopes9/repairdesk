import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { PackagePlus, RotateCcw } from 'lucide-react';
import { Button } from './ui/Button';
import { StatusBadge } from './ui/StatusBadge';
import { stockApi } from '../lib/stock/api';
import {
  PART_CATEGORIA_LABEL,
  PART_MOVIMENTO_LABEL,
  PART_MOVIMENTO_MOTIVO,
  type Part,
  type PartMovimento,
} from '../lib/stock/types';
import { formatDate } from '../lib/money';
import { toast } from '../lib/toast';

export default function PecasUsadas({ reparacaoId, readOnly }: { reparacaoId: string; readOnly?: boolean }) {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Part | null>(null);
  const [qty, setQty] = useState('1');

  const parts = useQuery({
    queryKey: ['stock-lookup', search],
    queryFn: () => stockApi.list({ q: search, page: 1, pageSize: 8 }),
    enabled: !readOnly && search.trim().length >= 2,
    placeholderData: keepPreviousData,
  });

  const movimentos = useQuery({
    queryKey: ['stock-movimentos-reparacao', reparacaoId],
    queryFn: () => stockApi.movimentos({ reparacaoId }),
  });

  function invalidate() {
    qc.invalidateQueries({ queryKey: ['stock'] });
    qc.invalidateQueries({ queryKey: ['stock-lookup'] });
    qc.invalidateQueries({ queryKey: ['stock-movimentos-reparacao', reparacaoId] });
    qc.invalidateQueries({ queryKey: ['reparacao', reparacaoId] });
    qc.invalidateQueries({ queryKey: ['reparacoes'] });
    qc.invalidateQueries({ queryKey: ['dashboard'] });
  }

  const add = useMutation({
    mutationFn: () => {
      if (!selected) throw new Error('Escolhe uma peça.');
      return stockApi.addMovimento(selected.id, {
        quantidade: -Math.abs(Number(qty || 1)),
        motivo: PART_MOVIMENTO_MOTIVO.UsoEmReparacao,
        reparacaoId,
        notas: null,
      });
    },
    onSuccess: () => {
      toast.success('Peça adicionada à reparação');
      setSelected(null);
      setSearch('');
      setQty('1');
      invalidate();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível adicionar a peça.'),
  });

  const devolucao = useMutation({
    mutationFn: (mov: PartMovimento) => stockApi.addMovimento(mov.partId, {
      quantidade: Math.abs(mov.quantidade),
      motivo: PART_MOVIMENTO_MOTIVO.Devolucao,
      reparacaoId,
      notas: `Devolução do movimento ${mov.id}`,
    }),
    onSuccess: () => {
      toast.success('Peça devolvida ao stock');
      invalidate();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível devolver a peça.'),
  });

  const lookupItems = (parts.data?.items ?? []).filter((p) => p.activo);
  const rows = movimentos.data ?? [];
  const saldo = useMemo(() => rows.reduce((sum, m) => sum + m.quantidade, 0), [rows]);

  return (
    <section className="space-y-3 rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold">Peças usadas</h2>
          <p className="text-xs text-zinc-500">
            {rows.length} movimento(s) · saldo {saldo}
          </p>
        </div>
        {rows.some((m) => m.quantidade < 0) && <StatusBadge tone="blue">Custo recalculado</StatusBadge>}
      </div>

      {!readOnly && (
        <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-800 dark:bg-zinc-950">
          <div className="grid gap-2 sm:grid-cols-[1fr_90px_auto]">
            <div className="relative">
              <input
                value={selected ? `${selected.sku ? `${selected.sku} · ` : ''}${selected.nome}` : search}
                onChange={(e) => { setSelected(null); setSearch(e.target.value); }}
                placeholder="Pesquisar por SKU ou nome da peça..."
                className={inputCls}
              />
              {!selected && lookupItems.length > 0 && (
                <ul className="absolute z-20 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-800 dark:bg-zinc-900">
                  {lookupItems.map((p) => (
                    <li key={p.id}>
                      <button
                        type="button"
                        onClick={() => setSelected(p)}
                        className="block w-full px-3 py-2 text-left text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800"
                      >
                        <div className="flex items-center justify-between gap-2">
                          <span className="font-medium">{p.nome}</span>
                          <span className="text-xs text-zinc-500">stock {p.qtdStock}</span>
                        </div>
                        <div className="text-xs text-zinc-500">
                          {p.sku ?? 'sem SKU'} · {PART_CATEGORIA_LABEL[p.categoria]} · {[p.marca, p.modelo].filter(Boolean).join(' ')}
                        </div>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <input
              inputMode="numeric"
              value={qty}
              onChange={(e) => setQty(e.target.value)}
              className={inputCls}
              aria-label="Quantidade"
            />
            <Button
              type="button"
              loading={add.isPending}
              disabled={!selected || !qty || Number(qty) <= 0}
              onClick={() => add.mutate()}
              leftIcon={<PackagePlus size={15} />}
            >
              Adicionar
            </Button>
          </div>
        </div>
      )}

      {movimentos.isLoading ? (
        <p className="text-sm text-zinc-500">A carregar peças...</p>
      ) : rows.length === 0 ? (
        <p className="rounded-lg border border-dashed border-zinc-300 p-4 text-center text-sm text-zinc-500 dark:border-zinc-700">
          Ainda não há peças ligadas a esta reparação.
        </p>
      ) : (
        <ul className="space-y-2">
          {rows.map((m) => (
            <li key={m.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-zinc-200 px-3 py-2 text-sm dark:border-zinc-800">
              <div>
                <div className="font-medium">
                  {m.partSku && <span className="mr-1 font-mono text-xs text-zinc-500">{m.partSku}</span>}
                  {m.partNome}
                </div>
                <div className="text-xs text-zinc-500">
                  {PART_MOVIMENTO_LABEL[m.motivo]} · {m.quantidade > 0 ? '+' : ''}{m.quantidade} · {formatDate(m.createdAt)}
                </div>
              </div>
              {!readOnly && m.quantidade < 0 && (
                <Button
                  type="button"
                  variant="secondary"
                  size="sm"
                  loading={devolucao.isPending}
                  onClick={() => devolucao.mutate(m)}
                  leftIcon={<RotateCcw size={14} />}
                >
                  Devolver
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

const inputCls = 'block w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';
