import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Combine, Link2Off, Printer, Save } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { Artigo, ArtigoDetalhe } from '../../lib/artigos/types';
import { comprasApi } from '../../lib/compras/api';
import { formatEur, parseDecimal } from '../../lib/compras/format';
import { correspondeTermos } from '../../lib/pesquisa';
import { toast } from '../../lib/toast';

interface Props {
  /** null = novo artigo. */
  artigoId: string | null;
  open: boolean;
  onClose: () => void;
  /** Lotes a ligar logo (ex.: "Criar artigo" a partir do Stock) e nome sugerido. */
  sugestao?: { loteIds: string[]; nome: string } | null;
  /** Abre a impressão de etiquetas deste artigo. */
  onEtiquetas?: (a: Artigo) => void;
}

/** Sprint 566: criar/editar um artigo da loja e ligar-lhe os lotes de compra (o stock é a soma deles). */
export default function ArtigoModal({ artigoId, open, onClose, sugestao, onEtiquetas }: Props) {
  const qc = useQueryClient();
  const detalhe = useQuery({ queryKey: ['artigos', artigoId], queryFn: () => artigosApi.get(artigoId!), enabled: open && !!artigoId });
  const inventario = useQuery({ queryKey: ['compras', 'inventario'], queryFn: comprasApi.inventario, enabled: open });
  const categorias = useQuery({ queryKey: ['artigos', 'categorias'], queryFn: artigosApi.categorias, enabled: open });

  const [nome, setNome] = useState('');
  const [categoria, setCategoria] = useState('');
  const [preco, setPreco] = useState('');
  const [fabricante, setFabricante] = useState('');
  const [barras, setBarras] = useState('');
  const [localizacao, setLocalizacao] = useState('');
  const [ativo, setAtivo] = useState(true);
  const [agrupado, setAgrupado] = useState(false);
  const [juntar, setJuntar] = useState('');
  const [novosLotes, setNovosLotes] = useState<string[]>([]);
  const [qLote, setQLote] = useState('');

  useEffect(() => {
    if (!open) return;
    const a = detalhe.data?.artigo;
    setNome(a?.nome ?? sugestao?.nome ?? '');
    setCategoria(a?.categoria ?? '');
    setPreco(a?.precoVendaCents != null ? (a.precoVendaCents / 100).toFixed(2).replace('.', ',') : '');
    setFabricante(a?.codigoFabricante ?? '');
    // O código interno ("20…") gera-se sozinho: só mostra o campo preenchido quando é um EAN de fabricante.
    setBarras(a && !a.codigoBarras.startsWith('20') ? a.codigoBarras : '');
    setLocalizacao(a?.localizacao ?? '');
    setAtivo(a?.ativo ?? true);
    setAgrupado(a?.agrupado ?? false);
    setJuntar('');
    setNovosLotes(sugestao?.loteIds ?? []);
    setQLote('');
  }, [open, detalhe.data, sugestao]);

  // Lotes com stock que ainda não pertencem a nenhum artigo.
  const lotesLivres = useMemo(() => {
    const t = qLote.trim().toLowerCase();
    return (inventario.data ?? [])
      .filter((l) => !l.artigoId)
      .filter((l) => !t || correspondeTermos(`${l.numero} ${l.descricao} ${l.fornecedor}`, t))
      .slice(0, 50);
  }, [inventario.data, qLote]);

  const guardar = useMutation({
    mutationFn: () => {
      const cents = preco.trim() ? Math.round((parseDecimal(preco) ?? NaN) * 100) : null;
      if (cents != null && (Number.isNaN(cents) || cents <= 0)) throw new Error('Preço inválido.');
      const req = {
        nome: nome.trim(),
        categoria: categoria.trim() || null,
        precoVendaCents: cents,
        codigoBarras: barras.trim() || null,
        codigoFabricante: fabricante.trim() || null,
        localizacao: localizacao.trim() || null,
        notas: detalhe.data?.artigo.notas ?? null,
        ativo,
        loteIds: novosLotes,
        agrupado,
      };
      return artigoId ? artigosApi.update(artigoId, req) : artigosApi.create(req);
    },
    onSuccess: (d: ArtigoDetalhe) => {
      qc.invalidateQueries({ queryKey: ['artigos'] });
      qc.invalidateQueries({ queryKey: ['compras', 'inventario'] });
      toast.success(artigoId ? 'Artigo guardado' : `Artigo ${d.artigo.sku} criado`, `Stock: ${d.artigo.stock}`);
      for (const aviso of d.avisos ?? []) toast.error('Atenção', aviso);
      onClose();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar o artigo.'),
  });

  const desligar = useMutation({
    mutationFn: (loteId: string) => artigosApi.desligarLote(artigoId!, loteId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['artigos'] });
      qc.invalidateQueries({ queryKey: ['compras', 'inventario'] });
    },
    onError: (err) => toast.fromError(err, 'Não foi possível desligar o lote.'),
  });

  // Sprint 581: juntar um artigo repetido (ex.: os antigos "Vidro temperado Hoco A12") neste.
  const todos = useQuery({ queryKey: ['artigos', 'todos'], queryFn: () => artigosApi.list(), enabled: open && !!artigoId });
  const fundir = useMutation({
    mutationFn: () => artigosApi.fundir(artigoId!, [juntar]),
    onSuccess: (d) => {
      qc.invalidateQueries({ queryKey: ['artigos'] });
      setJuntar('');
      toast.success('Artigos juntos', `Stock: ${d.artigo.stock}. As etiquetas antigas continuam a ler.`);
    },
    onError: (err) => toast.fromError(err, 'Não foi possível juntar.'),
  });

  const desfazer = useMutation({
    mutationFn: (fusaoId: string) => artigosApi.desfazerFusao(fusaoId),
    onSuccess: (d) => { qc.invalidateQueries({ queryKey: ['artigos'] }); toast.success(`«${d.artigo.nome}» voltou`, `Stock: ${d.artigo.stock}`); },
    onError: (err) => toast.fromError(err, 'Não foi possível desfazer.'),
  });

  const a = detalhe.data?.artigo;
  const precoCategoria = categorias.data?.find((c) => c.nome.toLowerCase() === categoria.trim().toLowerCase())?.precoVendaCents ?? null;
  return (
    <Modal
      open={open}
      onClose={onClose}
      wide
      title={a ? `${a.sku} · ${a.nome}` : 'Novo artigo'}
      footer={<>
        {a && onEtiquetas && (
          <Button type="button" variant="secondary" leftIcon={<Printer size={15} />} onClick={() => onEtiquetas(a)}>Imprimir etiquetas</Button>
        )}
        <Button type="button" leftIcon={<Save size={15} />} loading={guardar.isPending} disabled={!nome.trim()} onClick={() => guardar.mutate()}>
          Guardar
        </Button>
      </>}
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="sm:col-span-2">
          <span className={labelCls}>Nome (o que aparece na etiqueta)</span>
          <input value={nome} onChange={(e) => setNome(e.target.value)} className={inputCls} placeholder="Película vidro temperado Samsung A34" autoFocus />
        </label>
        <label>
          <span className={labelCls}>Preço de venda c/ IVA (€)</span>
          <input value={preco} onChange={(e) => setPreco(e.target.value)} className={inputCls} inputMode="decimal"
            placeholder={precoCategoria != null ? `Vazio = preço da categoria (${formatEur(precoCategoria / 100)})`
              : a?.precoEfetivoCents != null ? `Vazio = calculado (${formatEur(a.precoEfetivoCents / 100)})` : 'Vazio = calculado do lote'} />
        </label>
        <label>
          <span className={labelCls}>Categoria</span>
          <input value={categoria} onChange={(e) => setCategoria(e.target.value)} className={inputCls} placeholder="Películas, Cabos, Capas…" list="categorias-artigo" />
          <datalist id="categorias-artigo">{categorias.data?.map((c) => <option key={c.nome} value={c.nome} />)}</datalist>
        </label>
        <label>
          <span className={labelCls}>Código da embalagem (opcional)</span>
          <input value={fabricante} onChange={(e) => setFabricante(e.target.value)} className={inputCls} placeholder="Passa a pistola no código do produto" />
        </label>
        <label>
          <span className={labelCls}>Localização</span>
          <input value={localizacao} onChange={(e) => setLocalizacao(e.target.value)} className={inputCls} placeholder="Gaveta P1" />
        </label>
        <label className="sm:col-span-2">
          <span className={labelCls}>Código de barras da etiqueta — vazio = código da loja ({a?.codigoBarras ?? 'gerado ao guardar'})</span>
          <input value={barras} onChange={(e) => setBarras(e.target.value)} className={inputCls} inputMode="numeric" placeholder="Só se quiseres imprimir o EAN-13 do fabricante" />
        </label>
        <label className="flex items-start gap-2 text-sm sm:col-span-2">
          <input type="checkbox" className="mt-1" checked={agrupado} onChange={(e) => setAgrupado(e.target.checked)} />
          <span>Agrupado — vários modelos com o mesmo código e preço (ex.: «Película vidro»). <span className="text-zinc-500">No balcão pergunta o modelo.</span></span>
        </label>
        {a && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} /> Ativo (aparece no balcão e na lista)
          </label>
        )}
      </div>

      {detalhe.data?.codigos && detalhe.data.codigos.length > 0 && (
        <div className="mt-4">
          <h3 className="mb-1 text-xs font-semibold uppercase text-zinc-500">Outros códigos que levam a este artigo</h3>
          <div className="flex flex-wrap gap-1.5">
            {detalhe.data.codigos.map((c) => (
              <span key={c.id} className="rounded-full bg-zinc-100 px-2 py-0.5 font-mono text-xs text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
                title={c.fornecedorId ? 'SKU do fornecedor — as próximas compras com este código ligam-se sozinhas' : 'Etiqueta antiga — a pistola continua a ler'}>
                {c.fornecedor ? `${c.fornecedor}: ` : 'etiqueta '}{c.codigo}
              </span>
            ))}
          </div>
        </div>
      )}

      {detalhe.data?.fusoes && detalhe.data.fusoes.length > 0 && (
        <div className="mt-4">
          <h3 className="mb-1 text-xs font-semibold uppercase text-zinc-500">Artigos juntos neste</h3>
          <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
            {detalhe.data.fusoes.map((f) => (
              <li key={f.id} className="flex items-center gap-2 px-3 py-2">
                <span className="flex-1 truncate"><span className="font-mono text-xs text-zinc-500">{f.origemSku}</span> {f.origemNome} <span className="text-zinc-500">· {f.lotes} lote(s)</span></span>
                <button type="button" className="text-xs text-brand-600 underline disabled:opacity-50" disabled={desfazer.isPending}
                  onClick={() => { if (confirm(`Desfazer: «${f.origemNome}» volta a ser um artigo à parte?`)) desfazer.mutate(f.id); }}>
                  Desfazer
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {a && (
        <div className="mt-4 rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
          <p className="text-xs font-medium text-zinc-700 dark:text-zinc-200">Juntar outro artigo neste</p>
          <p className="mt-0.5 text-[11px] text-zinc-500">Os lotes passam para «{a.nome}» e a etiqueta do outro continua a funcionar na pistola. Dá para desfazer.</p>
          <div className="mt-2 flex flex-col gap-2 sm:flex-row">
            <select value={juntar} onChange={(e) => setJuntar(e.target.value)} className={inputCls} aria-label="Artigo a juntar">
              <option value="">Escolhe o artigo…</option>
              {(todos.data ?? []).filter((x) => x.id !== a.id).map((x) => <option key={x.id} value={x.id}>{x.sku} · {x.nome} ({x.stock})</option>)}
            </select>
            <Button type="button" variant="secondary" leftIcon={<Combine size={15} />} disabled={!juntar} loading={fundir.isPending}
              onClick={() => { const o = todos.data?.find((x) => x.id === juntar); if (o && confirm(`Juntar «${o.nome}» em «${a.nome}»?`)) fundir.mutate(); }}>
              Juntar
            </Button>
          </div>
        </div>
      )}

      {detalhe.data && detalhe.data.lotes.length > 0 && (
        <div className="mt-4">
          <h3 className="mb-1 text-xs font-semibold uppercase text-zinc-500">Lotes deste artigo (stock {a?.stock})</h3>
          <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
            {detalhe.data.lotes.map((l) => (
              <li key={l.linhaId} className="flex items-center gap-2 px-3 py-2">
                <span className="w-14 font-mono text-xs text-zinc-500">#{l.numero}</span>
                <span className="flex-1 truncate">{l.descricao} <span className="text-zinc-500">· {l.fornecedor}{l.skuFornecedor ? ` · ${l.skuFornecedor}` : ''}</span></span>
                <span className="tabular-nums">{l.quantidadeEmStock}/{l.quantidade}</span>
                <span className="w-20 text-right tabular-nums text-zinc-500">{formatEur(l.precoCalculadoComIva)}</span>
                <button type="button" title="Desligar este lote do artigo" className="text-zinc-400 hover:text-red-600" onClick={() => desligar.mutate(l.linhaId)}>
                  <Link2Off size={15} />
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mt-4">
        <h3 className="mb-1 text-xs font-semibold uppercase text-zinc-500">Ligar lotes em stock ({novosLotes.length} escolhidos)</h3>
        <input value={qLote} onChange={(e) => setQLote(e.target.value)} className={inputCls} placeholder="Procurar lote: a34, película…" />
        <ul className="mt-2 max-h-56 divide-y divide-zinc-100 overflow-auto rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
          {lotesLivres.length === 0 && <li className="px-3 py-2 text-zinc-500">Sem lotes livres com esse texto.</li>}
          {lotesLivres.map((l) => (
            <li key={l.linhaId}>
              <label className="flex cursor-pointer items-center gap-2 px-3 py-2 hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                <input type="checkbox" checked={novosLotes.includes(l.linhaId)}
                  onChange={(e) => setNovosLotes((s) => (e.target.checked ? [...s, l.linhaId] : s.filter((x) => x !== l.linhaId)))} />
                <span className="w-14 font-mono text-xs text-zinc-500">#{l.numero}</span>
                <span className="flex-1 truncate">{l.descricao} <span className="text-zinc-500">· {l.fornecedor}</span></span>
                <span className="tabular-nums">{l.quantidadeEmStock} un</span>
              </label>
            </li>
          ))}
        </ul>
      </div>
    </Modal>
  );
}
