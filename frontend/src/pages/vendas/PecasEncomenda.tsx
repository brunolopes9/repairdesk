import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PackageCheck, PackageSearch } from 'lucide-react';
import { Button, EmptyState, SectionCard, SkeletonRow } from '../../components/ui';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import { toast } from '../../lib/toast';
import { vendasApi } from '../../lib/vendas/api';
import { PECA_ENCOMENDA, VENDA_ESTADO_COLOR, VENDA_ESTADO_LABEL, type PecaEncomenda } from '../../lib/vendas/types';

const IVA = 1.23;

/** Lucro estimado de uma peça: (preço − custo) sem IVA, × quantidade. */
const lucroEstimado = (p: PecaEncomenda) => p.quantidade * ((p.precoUnitarioCents / 100) / IVA - p.custoEstimado / IVA);

/**
 * Sprint 594: as peças que faltam nas reparações em curso — o que ainda é preciso encomendar e o que já está a
 * caminho. Marca-se "Encomendei" daqui; quando a peça chega, abre-se a reparação e carrega-se em "Chegou".
 */
export default function PecasEncomendaVista() {
  const qc = useQueryClient();
  const lista = useQuery({ queryKey: ['vendas', 'pecas-encomenda'], queryFn: vendasApi.pecasEncomenda });
  const marcar = useMutation({
    mutationFn: (p: { peca: PecaEncomenda; encomenda: 0 | 1 }) => vendasApi.marcarEncomenda(p.peca.vendaId, p.peca.itemId, p.encomenda),
    onSuccess: (_, p) => {
      qc.invalidateQueries({ queryKey: ['vendas'] });
      qc.invalidateQueries({ queryKey: ['venda', p.peca.vendaId] });
      toast.success(p.encomenda === PECA_ENCOMENDA.Encomendada ? 'Marcada como encomendada' : 'Volta a "por encomendar"', p.peca.descricao);
    },
    onError: (err) => toast.fromError(err),
  });

  const pecas = lista.data ?? [];
  const porEncomendar = pecas.filter((p) => p.encomenda === PECA_ENCOMENDA.PorEncomendar);
  const aGastar = porEncomendar.reduce((s, p) => s + p.custoEstimado * p.quantidade, 0);

  return (
    <SectionCard
      bodyClassName="p-0"
      title={<span className="flex items-center gap-2"><PackageSearch size={16} /> Peças a encomendar</span>}
      action={pecas.length > 0 && (
        <span className="text-sm text-zinc-500 tabular-nums">
          {porEncomendar.length} por encomendar (~{formatEur(aGastar)}) · {pecas.length - porEncomendar.length} a caminho
        </span>
      )}
    >
      {!lista.isLoading && pecas.length === 0 ? (
        <div className="p-4">
          <EmptyState icon={PackageCheck} title="Não falta nenhuma peça"
            description="Numa reparação, usa «Peça a encomendar» para anotar a peça que falta (quanto custa e por quanto a vendes) — aparece aqui até chegar." />
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[52rem] text-sm">
            <thead className="border-b border-zinc-100 text-left text-xs text-zinc-500 dark:border-zinc-800">
              <tr>
                <th className="px-4 py-2 font-medium">Reparação</th>
                <th className="px-4 py-2 font-medium">Peça</th>
                <th className="px-4 py-2 text-right font-medium">Custa</th>
                <th className="px-4 py-2 text-right font-medium">Vendes a</th>
                <th className="px-4 py-2 text-right font-medium">Lucro est.</th>
                <th className="px-4 py-2 font-medium">Encomenda</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {lista.isLoading && Array.from({ length: 3 }).map((_, i) => <tr key={i}><td colSpan={6}><SkeletonRow columns={6} /></td></tr>)}
              {pecas.map((p) => (
                <tr key={p.itemId} className="align-top">
                  <td className="px-4 py-3">
                    <Link to={`/vendas/${p.vendaId}`} className="font-medium text-brand-700 hover:underline dark:text-brand-300">#{p.vendaNumero} · {p.cliente ?? 'Consumidor final'}</Link>
                    <div className="text-xs text-zinc-500">{p.equipamento ?? '—'}</div>
                    <span className={`mt-1 inline-block rounded-full px-2 py-0.5 text-[11px] font-medium ${VENDA_ESTADO_COLOR[p.estado]}`}>{VENDA_ESTADO_LABEL[p.estado]}</span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="font-medium">{p.quantidade > 1 ? `${p.quantidade} × ` : ''}{p.descricao}</div>
                    {p.nota && <div className="text-xs text-zinc-500">{p.nota}</div>}
                    <div className="text-[11px] text-zinc-400">desde {formatDateOnly(p.desde)}</div>
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums">{formatEur(p.custoEstimado)}</td>
                  <td className="px-4 py-3 text-right tabular-nums">{formatEur(p.precoUnitarioCents / 100)}</td>
                  <td className="px-4 py-3 text-right tabular-nums text-emerald-700 dark:text-emerald-400">{formatEur(lucroEstimado(p))}</td>
                  <td className="px-4 py-3">
                    {p.encomenda === PECA_ENCOMENDA.PorEncomendar ? (
                      <Button size="sm" variant="secondary" loading={marcar.isPending && marcar.variables?.peca.itemId === p.itemId}
                        onClick={() => marcar.mutate({ peca: p, encomenda: PECA_ENCOMENDA.Encomendada })}>
                        Já encomendei
                      </Button>
                    ) : (
                      <div className="space-y-1">
                        <span className="inline-block rounded-full bg-sky-100 px-2 py-0.5 text-xs font-medium text-sky-800 dark:bg-sky-950/40 dark:text-sky-300">A caminho</span>
                        <button type="button" className="block text-[11px] text-zinc-500 underline hover:text-zinc-800 dark:hover:text-zinc-200"
                          onClick={() => marcar.mutate({ peca: p, encomenda: PECA_ENCOMENDA.PorEncomendar })}>
                          afinal ainda não
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="border-t border-zinc-100 px-4 py-2 text-xs text-zinc-500 dark:border-zinc-800">
            Quando a peça chegar, regista a compra e, na reparação, carrega em <strong>Chegou</strong> na linha da peça — passa a usar a peça do stock.
          </p>
        </div>
      )}
    </SectionCard>
  );
}
