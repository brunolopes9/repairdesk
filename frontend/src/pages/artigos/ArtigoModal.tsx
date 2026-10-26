import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link2Off, Save } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { ArtigoDetalhe } from '../../lib/artigos/types';
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
}

/** Sprint 566: criar/editar um artigo da loja e ligar-lhe os lotes de compra (o stock é a soma deles). */
export default function ArtigoModal({ artigoId, open, onClose, sugestao }: Props) {
  const qc = useQueryClient();
  const detalhe = useQuery({ queryKey: ['artigos', artigoId], queryFn: () => artigosApi.get(artigoId!), enabled: open && !!artigoId });
  const inventario = useQuery({ queryKey: ['compras', 'inventario'], queryFn: comprasApi.inventario, enabled: open });

  const [nome, setNome] = useState('');
  const [categoria, setCategoria] = useState('');
  const [preco, setPreco] = useState('');
  const [fabricante, setFabricante] = useState('');
  const [barras, setBarras] = useState('');
  const [localizacao, setLocalizacao] = useState('');
  const [ativo, setAtivo] = useState(true);
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
      };
      return artigoId ? artigosApi.update(artigoId, req) : artigosApi.create(req);
    },
    onSuccess: (d: ArtigoDetalhe) => {
      qc.invalidateQueries({ queryKey: ['artigos'] });
      qc.invalidateQueries({ queryKey: ['compras', 'inventario'] });
      toast.success(artigoId ? 'Artigo guardado' : `Artigo ${d.artigo.sku} criado`, `Stock: ${d.artigo.stock}`);
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

  const a = detalhe.data?.artigo;
  return (
    <Modal
      open={open}
      onClose={onClose}
      wide
      title={a ? `${a.sku} · ${a.nome}` : 'Novo artigo'}
      footer={
        <Button type="button" leftIcon={<Save size={15} />} loading={guardar.isPending} disabled={!nome.trim()} onClick={() => guardar.mutate()}>
          Guardar
        </Button>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="sm:col-span-2">
          <span className={labelCls}>Nome (o que aparece na etiqueta)</span>
          <input value={nome} onChange={(e) => setNome(e.target.value)} className={inputCls} placeholder="Película vidro temperado Samsung A34" autoFocus />
        </label>
        <label>
          <span className={labelCls}>Preço de venda c/ IVA (€)</span>
          <input value={preco} onChange={(e) => setPreco(e.target.value)} className={inputCls} inputMode="decimal"
            placeholder={a?.precoEfetivoCents != null ? `Calculado: ${formatEur(a.precoEfetivoCents / 100)}` : 'Vazio = calculado do lote'} />
        </label>
        <label>
          <span className={labelCls}>Categoria</span>
          <input value={categoria} onChange={(e) => setCategoria(e.target.value)} className={inputCls} placeholder="Películas, Cabos, Capas…" />
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
        {a && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={ativo} onChange={(e) => setAtivo(e.target.checked)} /> Ativo (aparece no balcão e na lista)
          </label>
        )}
      </div>

      {detalhe.data && detalhe.data.lotes.length > 0 && (
        <div className="mt-4">
          <h3 className="mb-1 text-xs font-semibold uppercase text-zinc-500">Lotes deste artigo (stock {a?.stock})</h3>
          <ul className="divide-y divide-zinc-100 rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
            {detalhe.data.lotes.map((l) => (
              <li key={l.linhaId} className="flex items-center gap-2 px-3 py-2">
                <span className="w-14 font-mono text-xs text-zinc-500">#{l.numero}</span>
                <span className="flex-1 truncate">{l.descricao} <span className="text-zinc-500">· {l.fornecedor}</span></span>
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
