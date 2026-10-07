import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Plus, Save, Trash2 } from 'lucide-react';
import { BackButton, Button, PageHeader, SectionCard, SkeletonCard } from '../../components/ui';
import { apiErrorCode, apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import { useAuth } from '../../lib/auth/AuthContext';
import { comprasApi } from '../../lib/compras/api';
import type { CompraDocumento, CompraDocumentoWrite } from '../../lib/compras/types';
import { formatEur, formatPct, lucroPorDefeito, parseDecimal, previewUnidade } from '../../lib/compras/format';
import { fornecedoresApi, REGIME_IVA, REGIME_IVA_LABEL, type Fornecedor } from '../../lib/fornecedores/api';
import { inputCls, labelCls } from './ui';

/** Taxa normal de IVA nas vendas (o servidor usa FiscalDefaults.TaxaIvaNormal). */
const TAXA_VENDA = 0.23;

interface LinhaForm {
  key: string;
  id: string | null;
  descricao: string;
  quantidade: string;
  preco: string;
  /** '' = automática pelo regime do fornecedor. */
  taxa: string;
  /** '' = automático (1 € películas/vidros, 5 € resto). */
  lucro: string;
  localizacao: string;
  movimentos: number;
}

interface DocForm {
  fornecedorId: string;
  data: string;
  numeroFatura: string;
  encomendas: string;
  metodoPagamento: string;
  portes: string;
  portesIva: string;
  total: string;
  notas: string;
}

const novaLinha = (): LinhaForm => ({
  key: crypto.randomUUID(), id: null, descricao: '', quantidade: '1', preco: '', taxa: '', lucro: '', localizacao: '', movimentos: 0,
});

const hoje = () => new Date().toISOString().slice(0, 10);
const num = (v: number | null | undefined) => (v == null ? '' : String(v).replace('.', ','));

function fromDoc(doc: CompraDocumento): { form: DocForm; linhas: LinhaForm[] } {
  return {
    form: {
      fornecedorId: doc.fornecedorId,
      data: doc.data.slice(0, 10),
      numeroFatura: doc.numeroFatura ?? '',
      encomendas: doc.numerosEncomenda.join(', '),
      metodoPagamento: doc.metodoPagamento ?? '',
      portes: doc.portesPagos ? num(doc.portesPagos) : '',
      portesIva: num(doc.portesIva),
      total: num(doc.totalDocumento),
      notas: doc.notas ?? '',
    },
    linhas: doc.linhas.map((l) => ({
      key: l.id,
      id: l.id,
      descricao: l.descricao,
      quantidade: String(l.quantidade),
      preco: num(l.precoUnitarioPago),
      taxa: String(Math.round(l.taxaIvaCompra * 100)),
      lucro: num(l.lucroUnitario),
      localizacao: l.localizacao ?? '',
      movimentos: l.quantidadeVendida + l.quantidadeAbatida,
    })),
  };
}

export default function CompraEditor() {
  const { id } = useParams<{ id: string }>();
  const isNew = !id;
  const navigate = useNavigate();
  const qc = useQueryClient();
  const isAdmin = useAuth().hasRole('Admin');

  const fornecedores = useQuery({ queryKey: ['fornecedores', false], queryFn: () => fornecedoresApi.list(false) });
  const existente = useQuery({ queryKey: ['compra', id], queryFn: () => comprasApi.get(id!), enabled: !isNew });

  const [form, setForm] = useState<DocForm>({
    fornecedorId: '', data: hoje(), numeroFatura: '', encomendas: '', metodoPagamento: '', portes: '', portesIva: '', total: '', notas: '',
  });
  const [linhas, setLinhas] = useState<LinhaForm[]>([novaLinha()]);
  const [duplicado, setDuplicado] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);

  useEffect(() => {
    if (!existente.data) return;
    const s = fromDoc(existente.data);
    setForm(s.form);
    setLinhas(s.linhas.length ? s.linhas : [novaLinha()]);
  }, [existente.data]);

  const fornecedor: Fornecedor | undefined = fornecedores.data?.find((f) => f.id === form.fornecedorId);
  const regime = fornecedor?.regimeIva ?? REGIME_IVA.Nacional;
  const taxaDefeito = regime === REGIME_IVA.Nacional ? TAXA_VENDA : 0;

  const calc = useMemo(() => {
    const porLinha = linhas.map((l) => {
      const preco = parseDecimal(l.preco);
      const qtd = Number(l.quantidade) || 0;
      if (preco == null) return null;
      const taxa = l.taxa === '' ? taxaDefeito : (parseDecimal(l.taxa) ?? 0) / 100;
      const lucro = l.lucro === '' ? lucroPorDefeito(l.descricao) : parseDecimal(l.lucro) ?? 0;
      return { qtd, total: qtd * preco, ...previewUnidade(preco, taxa, lucro, TAXA_VENDA) };
    });
    const totalLinhas = porLinha.reduce((s, c) => s + (c?.total ?? 0), 0);
    const portes = parseDecimal(form.portes) ?? 0;
    const totalDoc = parseDecimal(form.total);
    const diferenca = totalDoc == null ? null : Math.round((totalDoc - totalLinhas - portes) * 100) / 100;
    return { porLinha, totalLinhas, calculado: totalLinhas + portes, diferenca };
  }, [linhas, form.portes, form.total, taxaDefeito]);

  function buildRequest(ignorarDuplicado: boolean): CompraDocumentoWrite {
    return {
      fornecedorId: form.fornecedorId,
      data: form.data,
      numeroFatura: form.numeroFatura.trim() || null,
      numerosEncomenda: form.encomendas.trim() ? [form.encomendas] : null,
      metodoPagamento: form.metodoPagamento.trim() || null,
      portesPagos: parseDecimal(form.portes) ?? 0,
      portesIva: parseDecimal(form.portesIva),
      totalDocumento: parseDecimal(form.total),
      notas: form.notas.trim() || null,
      ignorarDuplicado,
      linhas: linhas
        .filter((l) => l.descricao.trim() || l.preco.trim())
        .map((l) => ({
          id: l.id,
          descricao: l.descricao.trim(),
          quantidade: Number(l.quantidade) || 0,
          precoUnitarioPago: parseDecimal(l.preco) ?? 0,
          taxaIvaCompra: l.taxa === '' ? null : (parseDecimal(l.taxa) ?? 0) / 100,
          lucroUnitario: l.lucro === '' ? null : parseDecimal(l.lucro),
          localizacao: l.localizacao.trim() || null,
        })),
    };
  }

  const save = useMutation({
    mutationFn: (ignorarDuplicado: boolean) => {
      const req = buildRequest(ignorarDuplicado);
      return isNew ? comprasApi.create(req) : comprasApi.update(id!, req);
    },
    onSuccess: (doc) => {
      setDuplicado(null);
      qc.invalidateQueries({ queryKey: ['compras'] });
      qc.setQueryData(['compra', doc.id], doc);
      toast.success(isNew ? 'Compra registada' : 'Compra atualizada', `${doc.unidades} unidade(s) · ${formatEur(doc.totalCalculado)}`);
      if (isNew) navigate(`/compras/${doc.id}`, { replace: true });
    },
    onError: (err) => {
      if (apiErrorCode(err) === 'compra_duplicada') setDuplicado(apiErrorMessage(err));
      else toast.fromError(err);
    },
  });

  const remove = useMutation({
    mutationFn: () => comprasApi.remove(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['compras'] });
      toast.success('Compra apagada');
      navigate('/compras', { replace: true });
    },
    onError: (err) => toast.fromError(err),
  });

  function setLinha(key: string, patch: Partial<LinhaForm>) {
    setLinhas((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    setDuplicado(null);
    save.mutate(false);
  }

  const linhasValidas = linhas.filter((l) => l.descricao.trim() && parseDecimal(l.preco) != null && Number(l.quantidade) >= 1);
  const podeGravar = !!form.fornecedorId && !!form.data && linhasValidas.length > 0 && !save.isPending;

  if (!isNew && existente.isLoading) return <SkeletonCard />;
  if (!isNew && existente.isError) {
    return <p className="text-sm text-rose-600">{apiErrorMessage(existente.error)}</p>;
  }

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      <BackButton to="/compras" label="Compras" />
      <PageHeader
        title={isNew ? 'Nova compra' : `Compra ${existente.data?.numeroFatura ?? existente.data?.numerosEncomenda[0] ?? ''}`}
        description="Uma fatura (ou encomenda) do fornecedor. Cada linha é um lote de stock."
        meta={existente.data?.faturaEmFalta && (
          <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">Fatura em falta</span>
        )}
        actions={
          <>
            {!isNew && isAdmin && (
              <Button type="button" variant="ghost" leftIcon={<Trash2 size={15} />} onClick={() => setConfirmDelete(true)}>Apagar</Button>
            )}
            <Button type="submit" loading={save.isPending} disabled={!podeGravar} leftIcon={<Save size={15} />}>
              {isNew ? 'Registar compra' : 'Guardar'}
            </Button>
          </>
        }
      />

      {duplicado && (
        <div role="alert" className="flex flex-col gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200 sm:flex-row sm:items-center sm:justify-between">
          <span className="flex items-start gap-2"><AlertTriangle size={16} className="mt-0.5 shrink-0" />{duplicado} Confirma que não é a mesma fatura.</span>
          <div className="flex gap-2">
            <Button type="button" size="sm" variant="secondary" onClick={() => setDuplicado(null)}>Rever</Button>
            <Button type="button" size="sm" onClick={() => save.mutate(true)} loading={save.isPending}>Gravar mesmo assim</Button>
          </div>
        </div>
      )}

      {confirmDelete && (
        <div role="alert" className="flex flex-col gap-2 rounded-xl border border-rose-300 bg-rose-50 p-3 text-sm text-rose-900 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-200 sm:flex-row sm:items-center sm:justify-between">
          <span>Apagar este documento e todos os lotes? Só é possível se nada foi vendido.</span>
          <div className="flex gap-2">
            <Button type="button" size="sm" variant="secondary" onClick={() => setConfirmDelete(false)}>Cancelar</Button>
            <Button type="button" size="sm" variant="danger" loading={remove.isPending} onClick={() => remove.mutate()}>Apagar</Button>
          </div>
        </div>
      )}

      <SectionCard title="Documento">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <label className="block sm:col-span-2">
            <span className={labelCls}>Fornecedor *</span>
            <select required value={form.fornecedorId} onChange={(e) => setForm({ ...form, fornecedorId: e.target.value })} className={inputCls}>
              <option value="">Escolhe…</option>
              {fornecedores.data?.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
            {fornecedor && <span className="mt-1 block text-xs text-zinc-500">{REGIME_IVA_LABEL[regime]}</span>}
          </label>
          <label className="block">
            <span className={labelCls}>Data *</span>
            <input type="date" required value={form.data} onChange={(e) => setForm({ ...form, data: e.target.value })} className={inputCls} />
          </label>
          <label className="block">
            <span className={labelCls}>Pagamento</span>
            <input value={form.metodoPagamento} onChange={(e) => setForm({ ...form, metodoPagamento: e.target.value })} className={inputCls} placeholder="MB Way, PayPal…" maxLength={50} />
          </label>
          <label className="block">
            <span className={labelCls}>Nº da fatura</span>
            <input value={form.numeroFatura} onChange={(e) => setForm({ ...form, numeroFatura: e.target.value })} className={inputCls} placeholder="Vazio = fatura em falta" maxLength={100} />
          </label>
          <label className="block">
            <span className={labelCls}>Nº encomenda(s)</span>
            <input value={form.encomendas} onChange={(e) => setForm({ ...form, encomendas: e.target.value })} className={inputCls} placeholder="#165048, 165227" />
          </label>
          <label className="block">
            <span className={labelCls}>Portes pagos (c/ IVA)</span>
            <input inputMode="decimal" value={form.portes} onChange={(e) => setForm({ ...form, portes: e.target.value })} className={inputCls} placeholder="0,00" />
          </label>
          <label className="block">
            <span className={labelCls}>IVA dos portes</span>
            <input inputMode="decimal" value={form.portesIva} onChange={(e) => setForm({ ...form, portesIva: e.target.value })} className={inputCls} placeholder={regime === REGIME_IVA.Nacional ? 'ex.: 0,73' : '0'} />
          </label>
          <label className="block">
            <span className={labelCls}>Total impresso no documento</span>
            <input inputMode="decimal" value={form.total} onChange={(e) => setForm({ ...form, total: e.target.value })} className={inputCls} placeholder="Para conferir" />
          </label>
          <label className="block sm:col-span-2 lg:col-span-3">
            <span className={labelCls}>Notas</span>
            <input value={form.notas} onChange={(e) => setForm({ ...form, notas: e.target.value })} className={inputCls} maxLength={2000} />
          </label>
        </div>
      </SectionCard>

      <SectionCard title="Linhas (lotes de stock)">
        <p className="mb-3 text-xs text-zinc-500">
          IVA na compra por defeito: {formatPct(taxaDefeito)}. Lucro por defeito: 1 € películas/vidros, 5 € resto.
          Preço de venda calculado com IVA {formatPct(TAXA_VENDA)}.
        </p>
        <div className="-mx-4 overflow-x-auto px-4">
          <table className="w-full min-w-[56rem] text-sm">
            <thead className="text-left text-xs text-zinc-500">
              <tr>
                <th className="py-2 pr-2 font-medium">Descrição</th>
                <th className="w-16 py-2 pr-2 font-medium">Qtd</th>
                <th className="w-24 py-2 pr-2 font-medium">Preço un. pago</th>
                <th className="w-20 py-2 pr-2 font-medium">IVA %</th>
                <th className="w-20 py-2 pr-2 font-medium">Lucro un.</th>
                <th className="w-28 py-2 pr-2 font-medium">Localização</th>
                <th className="w-24 py-2 pr-2 text-right font-medium">Venda c/ IVA</th>
                <th className="w-24 py-2 pr-2 text-right font-medium">IVA ao Estado</th>
                <th className="w-10" />
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {linhas.map((l, i) => {
                const c = calc.porLinha[i];
                return (
                  <tr key={l.key} className="align-top">
                    <td className="py-2 pr-2">
                      <input aria-label="Descrição" value={l.descricao} onChange={(e) => setLinha(l.key, { descricao: e.target.value })} className={inputCls} placeholder="Ecrã Samsung A15" maxLength={500} />
                      {l.movimentos > 0 && <span className="mt-1 block text-xs text-zinc-500">{l.movimentos} já saíram do stock</span>}
                    </td>
                    <td className="py-2 pr-2"><input aria-label="Quantidade" inputMode="numeric" value={l.quantidade} onChange={(e) => setLinha(l.key, { quantidade: e.target.value.replace(/\D/g, '') })} className={inputCls} /></td>
                    <td className="py-2 pr-2"><input aria-label="Preço unitário pago" inputMode="decimal" value={l.preco} onChange={(e) => setLinha(l.key, { preco: e.target.value })} className={inputCls} placeholder="0,00" /></td>
                    <td className="py-2 pr-2"><input aria-label="Taxa de IVA" inputMode="decimal" value={l.taxa} onChange={(e) => setLinha(l.key, { taxa: e.target.value })} className={inputCls} placeholder={String(taxaDefeito * 100)} /></td>
                    <td className="py-2 pr-2"><input aria-label="Lucro unitário" inputMode="decimal" value={l.lucro} onChange={(e) => setLinha(l.key, { lucro: e.target.value })} className={inputCls} placeholder={String(lucroPorDefeito(l.descricao))} /></td>
                    <td className="py-2 pr-2"><input aria-label="Localização" value={l.localizacao} onChange={(e) => setLinha(l.key, { localizacao: e.target.value })} className={inputCls} maxLength={100} /></td>
                    <td className="py-2 pr-2 pt-4 text-right tabular-nums">{c ? formatEur(c.precoFinalComIva) : '—'}</td>
                    <td className="py-2 pr-2 pt-4 text-right tabular-nums">{c ? formatEur(c.ivaAPagarEstado) : '—'}</td>
                    <td className="py-2">
                      <Button
                        type="button"
                        variant="icon"
                        aria-label="Remover linha"
                        title={l.movimentos > 0 ? 'Já tem vendas — não pode ser removida' : 'Remover linha'}
                        disabled={l.movimentos > 0 || linhas.length === 1}
                        onClick={() => setLinhas((ls) => ls.filter((x) => x.key !== l.key))}
                      >
                        <Trash2 size={15} />
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <div className="mt-3 flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <Button type="button" variant="secondary" size="sm" leftIcon={<Plus size={15} />} onClick={() => setLinhas((ls) => [...ls, novaLinha()])}>
            Adicionar linha
          </Button>
          <dl className="grid grid-cols-[auto_auto] gap-x-6 gap-y-1 text-sm tabular-nums">
            <dt className="text-zinc-500">Artigos</dt><dd className="text-right">{formatEur(calc.totalLinhas)}</dd>
            <dt className="text-zinc-500">+ Portes</dt><dd className="text-right">{formatEur(parseDecimal(form.portes) ?? 0)}</dd>
            <dt className="font-medium">Total calculado</dt><dd className="text-right font-medium">{formatEur(calc.calculado)}</dd>
            {calc.diferenca != null && (
              <>
                <dt className="text-zinc-500">Diferença p/ documento</dt>
                <dd className={`text-right ${Math.abs(calc.diferenca) > 0.01 ? 'font-medium text-rose-600' : 'text-emerald-600'}`}>
                  {Math.abs(calc.diferenca) > 0.01 ? formatEur(calc.diferenca) : 'Confere ✓'}
                </dd>
              </>
            )}
          </dl>
        </div>
      </SectionCard>
    </form>
  );
}
