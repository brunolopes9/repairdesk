import { useState } from 'react';
import { Link } from 'react-router-dom';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Ban, FileText, Pencil, Plus, Receipt, Search, Trash2, Users } from 'lucide-react';
import Modal from '../../components/Modal';
import { useConfirm } from '../../components/ConfirmDialog';
import { Button, EmptyState, PageHeader, SectionCard, ViewTabs } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { useAuth } from '../../lib/auth/AuthContext';
import { formatEur, parseDecimal } from '../../lib/compras/format';
import { openPdfInNewTab } from '../../lib/downloadPdf';
import { moloniApi, MOTIVOS_ISENCAO, type MoloniDocumentoLista, type MoloniProduto, type MoloniTipoDocumento } from '../../lib/moloni/api';
import { toast } from '../../lib/toast';
import { PAYMENT_METHOD, PAYMENT_METHOD_LABEL, type PaymentMethod } from '../../lib/vendas/types';

const TABS = [
  { key: 'Fatura', label: 'Faturas' },
  { key: 'FaturaRecibo', label: 'Faturas-recibo' },
  { key: 'Recibo', label: 'Recibos' },
  { key: 'Orcamento', label: 'Orçamentos' },
  { key: 'artigos', label: 'Artigos e serviços' },
  { key: 'clientes', label: 'Clientes' },
] as const;
type Tab = (typeof TABS)[number]['key'];

/**
 * Sprint 568: tudo o que se fazia no site do Moloni ON, no Mender — documentos (incluindo os emitidos antes),
 * catálogo de artigos/serviços e clientes. O Moloni continua a ser a fonte legal; aqui só se lê e se age.
 */
export default function Faturacao() {
  const [tab, setTab] = useState<Tab>('Fatura');
  const estado = useQuery({ queryKey: ['moloni', 'estado'], queryFn: moloniApi.estado });
  const isAdmin = useAuth().hasRole('Admin');

  return (
    <div className="space-y-4">
      <PageHeader title="Faturação" description="Documentos, artigos e clientes do Moloni ON — sem teres de abrir o Moloni." />
      {estado.data && !estado.data.configurado ? (
        <EmptyState icon={FileText} title="Moloni ON por ligar"
          description="Liga-o em Definições → Moloni ON para veres e gerires aqui as faturas, recibos, orçamentos e artigos." />
      ) : (
        <>
          <ViewTabs value={tab} onChange={(v) => setTab(v as Tab)} tabs={TABS.map((t) => ({ key: t.key, label: t.label }))} />
          {tab === 'artigos' ? <ArtigosMoloni isAdmin={isAdmin} />
            : tab === 'clientes' ? <ClientesMoloni isAdmin={isAdmin} />
            : <DocumentosMoloni key={tab} tipo={tab} isAdmin={isAdmin} />}
        </>
      )}
    </div>
  );
}

function DocumentosMoloni({ tipo, isAdmin }: { tipo: MoloniTipoDocumento; isAdmin: boolean }) {
  const qc = useQueryClient();
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [pagar, setPagar] = useState<MoloniDocumentoLista | null>(null);
  const [metodo, setMetodo] = useState<PaymentMethod>(PAYMENT_METHOD.TransferenciaBancaria);
  const [anular, setAnular] = useState<MoloniDocumentoLista | null>(null);
  const [motivo, setMotivo] = useState('');
  const lista = useQuery({ queryKey: ['moloni', 'documentos', tipo, q, page], queryFn: () => moloniApi.documentos(tipo, q, page), placeholderData: keepPreviousData });

  const refrescar = () => { qc.invalidateQueries({ queryKey: ['moloni', 'documentos'] }); qc.invalidateQueries({ queryKey: ['vendas'] }); };
  const recibo = useMutation({
    mutationFn: () => moloniApi.reciboDeFatura(pagar!.documentId, metodo),
    onSuccess: () => { toast.success('Recibo emitido', pagar?.numero); setPagar(null); refrescar(); },
    onError: (err) => toast.fromError(err, 'Não foi possível emitir o recibo.'),
  });
  const anulacao = useMutation({
    mutationFn: () => moloniApi.anularDocumento(anular!.tipo, anular!.documentId, motivo.trim()),
    onSuccess: () => { toast.success('Documento anulado', anular?.numero); setAnular(null); setMotivo(''); refrescar(); },
    onError: (err) => toast.fromError(err, 'Não foi possível anular.'),
  });

  const docs = lista.data?.items ?? [];
  const paginas = Math.max(1, Math.ceil((lista.data?.total ?? 0) / 25));
  return (
    <SectionCard bodyClassName="p-0" title={
      <label className="relative block flex-1">
        <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
        <input value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} className={`${inputCls} pl-9`} placeholder="Cliente, NIF, nº…" aria-label="Procurar documentos" />
      </label>
    }>
      {lista.isLoading ? <p className="p-4 text-sm text-zinc-500">A ler do Moloni ON…</p>
        : docs.length === 0 ? <p className="p-4 text-sm text-zinc-500">Sem documentos.</p> : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[44rem] text-sm">
              <thead className="bg-zinc-50 text-left text-xs uppercase text-zinc-500 dark:bg-zinc-900">
                <tr><th className="px-3 py-2">Nº</th><th className="px-3 py-2">Data</th><th className="px-3 py-2">Cliente</th><th className="px-3 py-2 text-right">Total</th><th className="px-3 py-2">Estado</th><th className="px-3 py-2" /></tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {docs.map((d) => (
                  <tr key={d.documentId} className={d.anulado ? 'opacity-50' : ''}>
                    <td className="px-3 py-2 font-mono text-xs">
                      {d.numero}
                      {d.vendaId && <Link to={`/vendas/${d.vendaId}`} className="ml-2 font-sans text-brand-600 hover:underline">venda #{d.vendaNumero}</Link>}
                    </td>
                    <td className="px-3 py-2 tabular-nums">{d.data ? new Date(d.data).toLocaleDateString('pt-PT') : '—'}</td>
                    <td className="px-3 py-2">{d.cliente ?? '—'}{d.nif && <span className="block text-xs text-zinc-500">{d.nif}</span>}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{formatEur(d.total)}</td>
                    <td className="px-3 py-2">
                      {d.anulado ? <span className="text-xs text-rose-600">Anulado</span>
                        : d.rascunho ? <span className="text-xs text-zinc-500">Rascunho</span>
                        : d.pagoPercentagem == null ? <span className="text-xs text-emerald-700 dark:text-emerald-400">Finalizado</span>
                        : d.pagoPercentagem >= 100 ? <span className="text-xs text-emerald-700 dark:text-emerald-400">Paga</span>
                        : <span className="text-xs font-medium text-amber-700 dark:text-amber-400">Por pagar{d.pagoPercentagem > 0 ? ` (${d.pagoPercentagem}%)` : ''}</span>}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2 text-right">
                      <button type="button" title="PDF" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800"
                        onClick={() => openPdfInNewTab(moloniApi.documentoPdfPath(d.tipo, d.documentId)).catch((e) => toast.fromError(e, 'PDF ainda não disponível.'))}>
                        <FileText size={15} />
                      </button>
                      {d.tipo === 'Fatura' && !d.anulado && !d.rascunho && (d.pagoPercentagem ?? 0) < 100 && (
                        <button type="button" title="Registar pagamento (recibo)" className="rounded p-1 text-emerald-700 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => setPagar(d)}>
                          <Receipt size={15} />
                        </button>
                      )}
                      {isAdmin && !d.anulado && !d.rascunho && (
                        <button type="button" title="Anular" className="rounded p-1 text-rose-600 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => { setAnular(d); setMotivo(''); }}>
                          <Ban size={15} />
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      {paginas > 1 && (
        <div className="flex items-center justify-end gap-2 border-t border-zinc-100 p-3 text-sm dark:border-zinc-800">
          <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
          <span className="tabular-nums text-zinc-500">{page} / {paginas}</span>
          <Button type="button" variant="secondary" disabled={page >= paginas} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
        </div>
      )}

      <Modal open={pagar !== null} onClose={() => setPagar(null)} title={`Registar pagamento — ${pagar?.numero ?? ''}`}
        footer={<Button type="button" leftIcon={<Receipt size={15} />} loading={recibo.isPending} onClick={() => recibo.mutate()}>Emitir recibo</Button>}>
        <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">Emite no Moloni ON o recibo do valor em falta ({pagar ? formatEur(pagar.total) : ''}) — a fatura fica paga.</p>
        <label className="block"><span className={labelCls}>Pago por</span>
          <select value={metodo} onChange={(e) => setMetodo(Number(e.target.value) as PaymentMethod)} className={inputCls}>
            {Object.entries(PAYMENT_METHOD_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
        </label>
      </Modal>

      <Modal open={anular !== null} onClose={() => setAnular(null)} title={`Anular ${anular?.numero ?? ''}`}
        footer={<Button type="button" leftIcon={<Ban size={15} />} loading={anulacao.isPending} disabled={motivo.trim().length < 5} onClick={() => anulacao.mutate()}>Anular no Moloni ON</Button>}>
        <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
          A anulação é comunicada à AT e não se desfaz. Uma fatura já paga só se anula depois de anular o recibo
          {anular?.vendaId ? ' (sendo de uma venda do Mender, o recibo é anulado primeiro automaticamente)' : ''}.
        </p>
        <input value={motivo} onChange={(e) => setMotivo(e.target.value)} className={inputCls} placeholder="Motivo (ex.: emitida com o NIF errado)" autoFocus />
      </Modal>
    </SectionCard>
  );
}

const VAZIO = { nome: '', referencia: '', tipo: 2, preco: '', taxa: 23, motivo: '', categoriaId: '' as number | '', unidadeId: '' as number | '' };

function ArtigosMoloni({ isAdmin }: { isAdmin: boolean }) {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [editar, setEditar] = useState<MoloniProduto | null | undefined>(undefined);
  const [f, setF] = useState(VAZIO);
  const lista = useQuery({ queryKey: ['moloni', 'artigos', q, page], queryFn: () => moloniApi.artigos(q, page), placeholderData: keepPreviousData });
  const refs = useQuery({ queryKey: ['moloni', 'referencias'], queryFn: moloniApi.referencias, enabled: editar !== undefined, staleTime: 10 * 60_000 });

  function abrir(p: MoloniProduto | null) {
    setEditar(p);
    setF(p ? {
      nome: p.nome, referencia: p.referencia, tipo: p.tipo, preco: p.precoComIva.toFixed(2).replace('.', ','), taxa: p.taxa,
      motivo: p.motivoIsencao ?? '', categoriaId: p.categoriaId ?? '', unidadeId: p.unidadeId ?? '',
    } : VAZIO);
  }

  const guardar = useMutation({
    mutationFn: () => {
      const preco = parseDecimal(f.preco);
      if (preco == null || preco < 0) throw new Error('Preço inválido.');
      const req = {
        nome: f.nome.trim(), referencia: f.referencia.trim(), tipo: f.tipo, precoComIva: preco, taxa: f.taxa,
        motivoIsencao: f.taxa === 0 ? f.motivo || null : null,
        categoriaId: f.categoriaId === '' ? null : f.categoriaId, unidadeId: f.unidadeId === '' ? null : f.unidadeId,
      };
      return editar ? moloniApi.atualizarArtigo(editar.productId, req) : moloniApi.criarArtigo(req);
    },
    onSuccess: (p) => { toast.success(editar ? 'Artigo atualizado no Moloni ON' : 'Artigo criado no Moloni ON', `${p.referencia} · ${formatEur(p.precoComIva)}`); setEditar(undefined); qc.invalidateQueries({ queryKey: ['moloni', 'artigos'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar o artigo.'),
  });
  const apagar = useMutation({
    mutationFn: (id: number) => moloniApi.apagarArtigo(id),
    onSuccess: () => { toast.success('Artigo apagado do Moloni ON'); qc.invalidateQueries({ queryKey: ['moloni', 'artigos'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível apagar.'),
  });

  const items = lista.data?.items ?? [];
  const paginas = Math.max(1, Math.ceil((lista.data?.total ?? 0) / 25));
  return (
    <SectionCard bodyClassName="p-0" title={
      <div className="flex flex-1 items-center gap-2">
        <label className="relative block flex-1">
          <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} className={`${inputCls} pl-9`} placeholder="Nome ou referência…" aria-label="Procurar artigos" />
        </label>
        {isAdmin && <Button type="button" leftIcon={<Plus size={15} />} onClick={() => abrir(null)}>Novo</Button>}
      </div>
    }>
      {lista.isLoading ? <p className="p-4 text-sm text-zinc-500">A ler do Moloni ON…</p> : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[40rem] text-sm">
            <thead className="bg-zinc-50 text-left text-xs uppercase text-zinc-500 dark:bg-zinc-900">
              <tr><th className="px-3 py-2">Referência</th><th className="px-3 py-2">Nome</th><th className="px-3 py-2 text-right">Preço c/ IVA</th><th className="px-3 py-2">IVA</th><th className="px-3 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {items.map((p) => (
                <tr key={p.productId}>
                  <td className="px-3 py-2 font-mono text-xs">{p.referencia}</td>
                  <td className="px-3 py-2">{p.nome}<span className="block text-xs text-zinc-500">{p.tipo === 2 ? 'Serviço' : p.tipo === 1 ? 'Produto' : 'Outro'}{p.categoria ? ` · ${p.categoria}` : ''}</span></td>
                  <td className="px-3 py-2 text-right tabular-nums">{formatEur(p.precoComIva)}</td>
                  <td className="px-3 py-2 text-xs">{p.taxa > 0 ? `${p.taxa}%` : p.motivoIsencao ?? '0%'}</td>
                  <td className="whitespace-nowrap px-3 py-2 text-right">
                    {isAdmin && <button type="button" title="Editar" className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => abrir(p)}><Pencil size={15} /></button>}
                    {isAdmin && p.podeApagar && (
                      <button type="button" title="Apagar" className="rounded p-1 text-rose-600 hover:bg-zinc-100 dark:hover:bg-zinc-800"
                        onClick={async () => { if (await confirm({ title: `Apagar ${p.referencia}?`, description: 'Apaga o artigo do Moloni ON.', confirmLabel: 'Apagar', destructive: true })) apagar.mutate(p.productId); }}>
                        <Trash2 size={15} />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {paginas > 1 && (
        <div className="flex items-center justify-end gap-2 border-t border-zinc-100 p-3 text-sm dark:border-zinc-800">
          <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
          <span className="tabular-nums text-zinc-500">{page} / {paginas}</span>
          <Button type="button" variant="secondary" disabled={page >= paginas} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
        </div>
      )}

      <Modal open={editar !== undefined} onClose={() => setEditar(undefined)} title={editar ? `Editar ${editar.referencia}` : 'Novo artigo no Moloni ON'}
        footer={<Button type="button" loading={guardar.isPending} disabled={f.nome.trim().length < 2 || !f.referencia.trim() || (f.taxa === 0 && !f.motivo)} onClick={() => guardar.mutate()}>Guardar no Moloni</Button>}>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="sm:col-span-2"><span className={labelCls}>Nome</span><input value={f.nome} onChange={(e) => setF({ ...f, nome: e.target.value })} className={inputCls} /></label>
          <label><span className={labelCls}>Referência (única)</span><input value={f.referencia} onChange={(e) => setF({ ...f, referencia: e.target.value })} className={inputCls} /></label>
          <label><span className={labelCls}>Tipo</span>
            <select value={f.tipo} onChange={(e) => setF({ ...f, tipo: Number(e.target.value) })} className={inputCls}>
              <option value={2}>Serviço</option><option value={1}>Produto</option><option value={3}>Outro</option>
            </select>
          </label>
          <label><span className={labelCls}>Preço com IVA (€)</span><input value={f.preco} onChange={(e) => setF({ ...f, preco: e.target.value })} className={inputCls} inputMode="decimal" /></label>
          <label><span className={labelCls}>IVA</span>
            <select value={f.taxa} onChange={(e) => setF({ ...f, taxa: Number(e.target.value) })} className={inputCls}>
              {(refs.data?.taxas ?? [23, 13, 6, 0]).map((t) => <option key={t} value={t}>{t === 0 ? 'Isento (0%)' : `${t}%`}</option>)}
            </select>
          </label>
          {f.taxa === 0 && (
            <label className="sm:col-span-2"><span className={labelCls}>Motivo de isenção</span>
              <select value={f.motivo} onChange={(e) => setF({ ...f, motivo: e.target.value })} className={inputCls}>
                <option value="">Escolher…</option>
                {MOTIVOS_ISENCAO.map((m) => <option key={m.codigo} value={m.codigo}>{m.label}</option>)}
              </select>
            </label>
          )}
          <label><span className={labelCls}>Categoria</span>
            <select value={f.categoriaId} onChange={(e) => setF({ ...f, categoriaId: e.target.value ? Number(e.target.value) : '' })} className={inputCls}>
              <option value="">{editar ? 'Manter' : 'Predefinida'}</option>
              {refs.data?.categorias.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </label>
          <label><span className={labelCls}>Unidade</span>
            <select value={f.unidadeId} onChange={(e) => setF({ ...f, unidadeId: e.target.value ? Number(e.target.value) : '' })} className={inputCls}>
              <option value="">{editar ? 'Manter' : 'Unidade (un)'}</option>
              {refs.data?.unidades.map((u) => <option key={u.id} value={u.id}>{u.nome}</option>)}
            </select>
          </label>
        </div>
      </Modal>
    </SectionCard>
  );
}

function ClientesMoloni({ isAdmin }: { isAdmin: boolean }) {
  const qc = useQueryClient();
  const importar = useMutation({
    mutationFn: moloniApi.importarClientes,
    onSuccess: (r) => {
      toast.success('Clientes do Moloni ON importados', `${r.ligados} ligados pelo NIF · ${r.criados} criados · ${r.jaLigados} já estavam ligados`);
      qc.invalidateQueries({ queryKey: ['clientes'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível importar.'),
  });
  return (
    <SectionCard title="Clientes">
      <div className="space-y-3 text-sm text-zinc-600 dark:text-zinc-300">
        <p>Os clientes geres em <Link to="/clientes" className="text-brand-600 underline">Clientes</Link>: ao criar ou alterar um cliente no Mender, ele é criado/atualizado no Moloni ON (pelo NIF). O consumidor final é sempre o cliente predefinido do Moloni.</p>
        <p>Para trazer para o Mender os clientes que já tinhas no Moloni (os que ainda não estão cá são criados; os que já estão ficam ligados pelo NIF):</p>
        {isAdmin && <Button type="button" leftIcon={<Users size={15} />} loading={importar.isPending} onClick={() => importar.mutate()}>Importar clientes do Moloni ON</Button>}
      </div>
    </SectionCard>
  );
}
