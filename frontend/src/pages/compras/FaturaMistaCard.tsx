import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Receipt, Split } from 'lucide-react';
import { Button, SectionCard } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { api } from '../../lib/api';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import { toast } from '../../lib/toast';
import type { CompraDocumento } from '../../lib/compras/types';

/** Sprint 688: despesa que saiu da fatura desta compra (fatura mista). */
interface DespesaLigada {
  id: string;
  numero: number;
  descricao: string;
  valor: number;
  iva: number | null;
  natureza: 0 | 1;
  data: string;
}

interface Resultado { despesa: DespesaLigada; valorLinhas: number; portesDaDespesa: number; totalQueFicaNaCompra: number | null }

const CATEGORIAS: { v: number; label: string }[] = [
  { v: 1, label: 'Material / consumíveis' },
  { v: 2, label: 'Ferramenta / equipamento' },
  { v: 6, label: 'Marketing' },
  { v: 99, label: 'Outro' },
];

const round2 = (n: number) => Math.round(n * 100) / 100;

/**
 * Sprint 688 (fatura mista): o fornecedor não separa faturas — separa-se aqui. As linhas escolhidas saem da compra
 * (e do stock) e passam a uma despesa ligada, com a parte proporcional dos portes. O PDF continua a ser um só.
 */
export function FaturaMistaCard({ compra, isAdmin }: { compra: CompraDocumento; isAdmin: boolean }) {
  const qc = useQueryClient();
  const ligadas = useQuery({
    queryKey: ['compra', compra.id, 'despesas'],
    queryFn: () => api.get<DespesaLigada[]>(`/compras/${compra.id}/despesas`).then((r) => r.data),
  });
  const [aberto, setAberto] = useState(false);
  const [escolhidas, setEscolhidas] = useState<Set<string>>(new Set());
  const [descricao, setDescricao] = useState('');
  const [categoria, setCategoria] = useState(1);
  const [natureza, setNatureza] = useState<0 | 1>(0);

  const passar = useMutation({
    mutationFn: () => api.post<Resultado>(`/compras/${compra.id}/passar-a-despesa`, {
      linhaIds: [...escolhidas], descricao: descricao.trim() || null, categoria, natureza,
    }).then((r) => r.data),
    onSuccess: (r) => {
      toast.success(`Despesa D#${r.despesa.numero} criada (${formatEur(r.despesa.valor)})`, 'As linhas saíram da compra e do stock.');
      setAberto(false); setEscolhidas(new Set()); setDescricao('');
      qc.invalidateQueries({ queryKey: ['compra', compra.id] });
      qc.invalidateQueries({ queryKey: ['compras'] });
      qc.invalidateQueries({ queryKey: ['despesas'] });
    },
    onError: (e) => toast.fromError(e),
  });

  const lista = ligadas.data ?? [];
  const totalLinhas = compra.linhas.reduce((s, l) => s + l.quantidade * l.precoUnitarioPago, 0);
  const sel = compra.linhas.filter((l) => escolhidas.has(l.id));
  const valorSel = sel.reduce((s, l) => s + l.quantidade * l.precoUnitarioPago, 0);
  const portesSel = totalLinhas > 0 ? round2(compra.portesPagos * valorSel / totalLinhas) : 0;
  const todas = sel.length > 0 && sel.length === compra.linhas.length;

  if (lista.length === 0 && !isAdmin) return null;

  return (
    <SectionCard title={<span className="flex items-center gap-2"><Split size={16} /> Fatura mista (compra + despesa)</span>}
      action={isAdmin && !aberto && compra.linhas.length > 1 && (
        <Button type="button" size="sm" variant="secondary" onClick={() => setAberto(true)}>Passar linhas para despesa</Button>
      )}>
      {lista.length > 0 ? (
        <div className="space-y-2">
          <p className="text-sm text-zinc-600 dark:text-zinc-400">
            Parte desta fatura é despesa. O PDF é um só — o desta compra — e vai uma vez para a contabilidade, marcado «Mista».
          </p>
          <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 dark:divide-zinc-800 dark:border-zinc-800">
            {lista.map((d) => (
              <li key={d.id} className="flex flex-wrap items-center gap-2 px-3 py-2 text-sm">
                <Receipt size={14} className="text-rose-600" />
                <Link to={`/despesas/${d.id}`} className="font-medium text-brand-700 hover:underline dark:text-brand-300">Despesa D#{d.numero}</Link>
                <span className="flex-1 text-zinc-700 dark:text-zinc-300">{d.descricao}</span>
                <span className="text-xs text-zinc-500">{d.natureza === 1 ? 'Investimento' : 'Gasto'} · {formatDateOnly(d.data)}</span>
                <strong className="tabular-nums">{formatEur(d.valor)}</strong>
              </li>
            ))}
          </ul>
          <p className="text-xs text-zinc-500 tabular-nums">
            Total da fatura = compra {formatEur(compra.totalCalculado)} + despesa{lista.length > 1 ? 's' : ''} {formatEur(lista.reduce((s, d) => s + d.valor, 0))}
            {' '}= <strong>{formatEur(compra.totalCalculado + lista.reduce((s, d) => s + d.valor, 0))}</strong>
          </p>
        </div>
      ) : !aberto && (
        <p className="text-sm text-zinc-500">
          A fatura traz coisas que não são para vender (colas, ferramentas, expositores…)? Passa essas linhas para uma despesa — não é preciso pedir faturas separadas ao fornecedor.
        </p>
      )}

      {aberto && (
        <div className="mt-3 space-y-3 rounded-lg border border-rose-200 bg-rose-50/50 p-3 dark:border-rose-900/50 dark:bg-rose-950/20">
          <p className="text-sm font-medium">Que linhas são despesa?</p>
          <ul className="space-y-1">
            {compra.linhas.map((l) => {
              const vendida = l.quantidadeVendida > 0;
              return (
                <li key={l.id}>
                  <label className={`flex items-center gap-2 text-sm ${vendida ? 'opacity-50' : 'cursor-pointer'}`}>
                    <input type="checkbox" disabled={vendida} checked={escolhidas.has(l.id)}
                      onChange={(e) => setEscolhidas((s) => { const n = new Set(s); if (e.target.checked) n.add(l.id); else n.delete(l.id); return n; })} />
                    <span className="text-xs text-zinc-500">Lote {l.numero}</span>
                    <span className="flex-1">{l.quantidade > 1 ? `${l.quantidade} × ` : ''}{l.descricao}</span>
                    <span className="tabular-nums">{formatEur(l.quantidade * l.precoUnitarioPago)}</span>
                    {vendida && <span className="text-xs text-rose-600">já vendida</span>}
                  </label>
                </li>
              );
            })}
          </ul>
          <div className="grid gap-2 sm:grid-cols-3">
            <label className="sm:col-span-3">
              <span className={labelCls}>Descrição da despesa (opcional — por defeito, as linhas)</span>
              <input className={inputCls} value={descricao} onChange={(e) => setDescricao(e.target.value)} maxLength={300} />
            </label>
            <label>
              <span className={labelCls}>Categoria</span>
              <select className={inputCls} value={categoria} onChange={(e) => setCategoria(Number(e.target.value))}>
                {CATEGORIAS.map((c) => <option key={c.v} value={c.v}>{c.label}</option>)}
              </select>
            </label>
            <label>
              <span className={labelCls}>Tipo</span>
              <select className={inputCls} value={natureza} onChange={(e) => setNatureza(Number(e.target.value) as 0 | 1)}>
                <option value={0}>Gasto (consome-se)</option>
                <option value={1}>Investimento (equipamento)</option>
              </select>
            </label>
          </div>
          {sel.length > 0 && (
            <p className={`text-sm tabular-nums ${todas ? 'text-rose-700 dark:text-rose-300' : ''}`} role={todas ? 'alert' : undefined}>
              {todas
                ? 'Escolheste todas as linhas — então a fatura é toda despesa: apaga a compra e lança a fatura como despesa.'
                : <>Despesa: {formatEur(valorSel)} + {formatEur(portesSel)} de portes (proporcional) = <strong>{formatEur(valorSel + portesSel)}</strong>. Estas linhas saem do stock.</>}
            </p>
          )}
          <div className="flex gap-2">
            <Button type="button" variant="danger" loading={passar.isPending} disabled={sel.length === 0 || todas} onClick={() => passar.mutate()}>
              Passar para despesa
            </Button>
            <Button type="button" variant="ghost" onClick={() => { setAberto(false); setEscolhidas(new Set()); }}>Cancelar</Button>
          </div>
        </div>
      )}
    </SectionCard>
  );
}
