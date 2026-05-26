import { useState, useRef, useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Package, ChevronDown } from 'lucide-react';
import { partKitsApi } from '../../lib/partKits/api';
import { toast } from '../../lib/toast';
import { formatCents } from '../../lib/money';

interface Props {
  reparacaoId: string;
  disabled?: boolean;
}

/**
 * Sprint 353 (Doc 83 Pillar 5): botão dropdown que mostra os kits disponíveis
 * e ao escolher, aplica todas as peças do kit à reparação em 1-click.
 */
export default function ApplyKitButton({ reparacaoId, disabled }: Props) {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const kitsQuery = useQuery({
    queryKey: ['part-kits'],
    queryFn: () => partKitsApi.list(),
    staleTime: 60_000,
    enabled: open, // só carrega quando o user abre
  });

  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    if (open) document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [open]);

  const applyMut = useMutation({
    mutationFn: (kitId: string) => partKitsApi.apply(kitId, reparacaoId),
    onSuccess: (res, _kitId) => {
      if (res.failedAt) {
        toast.error(`Aplicado parcial: ${res.applied.length} ok, parou em ${res.failedAt}`);
      } else {
        toast.success(`Kit aplicado: ${res.applied.length} peças adicionadas.`);
      }
      qc.invalidateQueries({ queryKey: ['reparacao', reparacaoId] });
      qc.invalidateQueries({ queryKey: ['part-movimentos', reparacaoId] });
      setOpen(false);
    },
    onError: (err) => toast.fromError(err, 'Erro a aplicar kit.'),
  });

  return (
    <div className="relative" ref={ref}>
      <button
        type="button" disabled={disabled}
        onClick={() => setOpen((v) => !v)}
        className="inline-flex items-center gap-1 rounded-lg border border-zinc-300 px-2 py-1 text-xs hover:bg-zinc-50 disabled:opacity-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
        title="Aplicar kit de peças (todas em 1 click)"
      >
        <Package size={12} /> Kit
        <ChevronDown size={11} className={`transition ${open ? 'rotate-180' : ''}`} />
      </button>
      {open && (
        <div role="menu" className="absolute right-0 z-30 mt-1 w-64 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-lg dark:border-zinc-700 dark:bg-zinc-900">
          <div className="border-b border-zinc-100 px-3 py-2 text-[10px] uppercase tracking-wide text-zinc-500 dark:border-zinc-800">
            Aplicar kit
          </div>
          {kitsQuery.isLoading && <p className="px-3 py-3 text-xs text-zinc-500">A carregar…</p>}
          {kitsQuery.data?.length === 0 && (
            <p className="px-3 py-3 text-xs text-zinc-500">
              Sem kits definidos. Cria em <span className="font-medium">Definições → Kits de peças</span>.
            </p>
          )}
          <ul className="max-h-64 divide-y divide-zinc-100 overflow-y-auto dark:divide-zinc-800">
            {(kitsQuery.data ?? []).map((kit) => (
              <li key={kit.id}>
                <button
                  type="button"
                  disabled={applyMut.isPending}
                  onClick={() => applyMut.mutate(kit.id)}
                  className="block w-full px-3 py-2 text-left text-sm hover:bg-zinc-50 disabled:opacity-50 dark:hover:bg-zinc-800"
                >
                  <div className="font-medium">{kit.nome}</div>
                  <div className="text-[11px] text-zinc-500">
                    {kit.items.length} peças · custo {formatCents(kit.custoTotalCents)}
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
