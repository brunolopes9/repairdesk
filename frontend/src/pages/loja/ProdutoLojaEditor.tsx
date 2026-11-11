import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, ExternalLink, Globe, ImagePlus, Plus, Save, Trash2, X } from 'lucide-react';
import { Button, SectionCard, SkeletonCard } from '../../components/ui';
import { useConfirm } from '../../components/ConfirmDialog';
import { apiErrorMessage } from '../../lib/errors';
import { toast } from '../../lib/toast';
import {
  CATEGORIA_LABEL,
  DISPONIBILIDADE_LABEL,
  ESTADO_LABEL,
  GRAU_LABEL,
  LojaCategoria,
  LojaEstado,
  LojaOrigem,
  LojaRegimeIva,
  euro,
  lojaApi,
  type GuardarProdutoLoja,
  type LojaDisponibilidade,
  type LojaGrau,
  type ProdutoLoja,
} from '../../lib/loja/api';

const VAZIO: GuardarProdutoLoja = {
  titulo: '', marca: '', modelo: null, categoria: LojaCategoria.Smartphones, estado: LojaEstado.Recondicionado, grau: null,
  garantiaMeses: 36, regimeIva: LojaRegimeIva.Normal, origem: LojaOrigem.Proprio, fornecedorId: null, disponibilidade: null,
  preco: 0, custo: null, cor: null, ramGb: null, armazenamentoGb: null, descricao: null, specs: [], slug: null, seoTitulo: null, seoDescricao: null,
};

function paraForm(p: ProdutoLoja): GuardarProdutoLoja {
  return {
    titulo: p.titulo, marca: p.marca, modelo: p.modelo, categoria: p.categoria, estado: p.estado, grau: p.grau,
    garantiaMeses: p.garantiaMeses, regimeIva: p.regimeIva, origem: p.origem, fornecedorId: p.fornecedorId,
    disponibilidade: p.disponibilidade, preco: p.preco, custo: p.custo, cor: p.cor, ramGb: p.ramGb,
    armazenamentoGb: p.armazenamentoGb, descricao: p.descricao, specs: p.specs, slug: p.slug, seoTitulo: p.seoTitulo, seoDescricao: p.seoDescricao,
  };
}

/** Igual ao servidor: normal = IVA incluído (23/123); margem = IVA só sobre (venda − compra). */
function estimar(f: GuardarProdutoLoja): { iva: number; lucro: number | null } {
  const r = (v: number) => Math.round(v * 100) / 100;
  if (f.regimeIva === LojaRegimeIva.Margem) {
    if (f.custo == null) return { iva: 0, lucro: null };
    const margem = f.preco - f.custo;
    const iva = margem > 0 ? r((margem * 0.23) / 1.23) : 0;
    return { iva, lucro: r(margem - iva) };
  }
  const iva = r((f.preco * 0.23) / 1.23);
  return { iva, lucro: f.custo == null ? null : r(f.preco - iva - f.custo) };
}

const segundaMao = (e: LojaEstado) => e === LojaEstado.Recondicionado || e === LojaEstado.Usado;
const input =
  'w-full min-h-11 rounded-lg border border-zinc-200 bg-white px-3 text-base outline-none focus:border-brand-400 focus:ring-2 focus:ring-brand-100 dark:border-zinc-700 dark:bg-zinc-950 sm:text-sm';

function Campo({ label, hint, children }: { label: string; hint?: ReactNode; children: ReactNode }) {
  return (
    <label className="block text-sm">
      <span className="font-medium text-zinc-700 dark:text-zinc-300">{label}</span>
      <span className="mt-1 block">{children}</span>
      {hint && <span className="mt-1 block text-xs text-zinc-500">{hint}</span>}
    </label>
  );
}

const numOuNull = (v: string) => (v.trim() === '' ? null : Number(v.replace(',', '.')));

/**
 * Sprint 614 (Doc 97): criar/editar um produto da loja. As regras legais (garantia mínima, regime da
 * margem) estão no servidor; aqui só se ajuda a escolher bem e se mostra o lucro real.
 */
export default function ProdutoLojaEditor() {
  const { id } = useParams();
  const novo = !id || id === 'novo';
  const navigate = useNavigate();
  const qc = useQueryClient();
  const confirm = useConfirm();
  const produto = useQuery({ queryKey: ['loja', 'produto', id], queryFn: () => lojaApi.get(id!), enabled: !novo });
  const [f, setF] = useState<GuardarProdutoLoja>(VAZIO);
  const [carregado, setCarregado] = useState(novo);
  const [seo, setSeo] = useState(false);
  const [envio, setEnvio] = useState<number | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (produto.data && !carregado) {
      setF(paraForm(produto.data));
      setCarregado(true);
    }
  }, [produto.data, carregado]);

  const set = <K extends keyof GuardarProdutoLoja>(k: K, v: GuardarProdutoLoja[K]) => setF((s) => ({ ...s, [k]: v }));
  const refrescar = (p: ProdutoLoja) => {
    qc.setQueryData(['loja', 'produto', p.id], p);
    qc.invalidateQueries({ queryKey: ['loja', 'produtos'] });
  };

  const guardar = useMutation({
    mutationFn: () => (novo ? lojaApi.create(f) : lojaApi.update(id!, f)),
    onSuccess: (p) => {
      refrescar(p);
      toast.success(novo ? `Produto #${p.numero} criado — agora põe as fotos.` : 'Guardado.');
      if (novo) navigate(`/loja/${p.id}`, { replace: true });
      else setF(paraForm(p));
    },
    onError: (e) => toast.error(apiErrorMessage(e) || 'Não foi possível guardar.'),
  });

  const publicar = useMutation({
    mutationFn: (sim: boolean) => lojaApi.publicar(id!, sim),
    onSuccess: (p) => {
      refrescar(p);
      toast.success(p.publicado ? 'Publicado — aparece no site em até 15 minutos.' : 'Retirado do site.');
    },
    onError: (e) => toast.error(apiErrorMessage(e)),
  });

  const fotoMut = useMutation({
    mutationFn: async (files: File[]) => {
      let ultimo: ProdutoLoja | null = null;
      for (const [i, file] of files.entries()) {
        ultimo = await lojaApi.enviarFoto(id!, file, null, (x) => setEnvio((i + x) / files.length));
      }
      return ultimo;
    },
    onSuccess: (p) => p && refrescar(p),
    onError: (e) => toast.error(e instanceof Error ? e.message : apiErrorMessage(e)),
    onSettled: () => setEnvio(null),
  });

  const ordenar = useMutation({
    mutationFn: (ordem: string[]) => lojaApi.ordenarFotos(id!, ordem),
    onSuccess: refrescar,
    onError: (e) => toast.error(apiErrorMessage(e)),
  });
  const apagarFoto = useMutation({
    mutationFn: (fotoId: string) => lojaApi.apagarFoto(id!, fotoId),
    onSuccess: refrescar,
    onError: (e) => toast.error(apiErrorMessage(e)),
  });
  const apagar = useMutation({
    mutationFn: () => lojaApi.apagar(id!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['loja', 'produtos'] });
      toast.success('Produto apagado.');
      navigate('/loja');
    },
    onError: (e) => toast.error(apiErrorMessage(e)),
  });

  if (!novo && (produto.isLoading || !carregado)) return <SkeletonCard />;
  if (!novo && produto.isError) return <p className="text-sm text-rose-600">{apiErrorMessage(produto.error)}</p>;

  const p = produto.data;
  const { iva, lucro } = estimar(f);
  const minimoGarantia = f.estado === LojaEstado.Usado ? 18 : 36;
  const fotos = p?.fotos ?? [];

  const mudarEstado = (estado: LojaEstado) =>
    setF((s) => ({
      ...s,
      estado,
      grau: segundaMao(estado) ? s.grau : null,
      regimeIva: segundaMao(estado) ? s.regimeIva : LojaRegimeIva.Normal,
      garantiaMeses: Math.max(s.garantiaMeses, estado === LojaEstado.Usado ? 18 : 36),
    }));

  const mover = (fotoId: string, delta: number) => {
    const ids = fotos.map((x) => x.id);
    const i = ids.indexOf(fotoId);
    const j = i + delta;
    if (j < 0 || j >= ids.length) return;
    [ids[i], ids[j]] = [ids[j], ids[i]];
    ordenar.mutate(ids);
  };

  return (
    <div className="mx-auto max-w-4xl space-y-4 pb-24">
      <Link to="/loja" className="inline-flex min-h-10 items-center gap-1 text-sm text-zinc-600 dark:text-zinc-300">
        <ArrowLeft size={16} /> Loja online
      </Link>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{novo ? 'Novo produto' : `#${p?.numero} · ${p?.titulo}`}</h1>
          {p && (
            <p className="mt-1 text-sm text-zinc-500">
              {p.publicado ? 'No site' : 'Rascunho — ainda não aparece no site'}
              {p.publicado && (
                <a href={p.urlSite} target="_blank" rel="noreferrer" className="ml-2 inline-flex items-center gap-1 text-brand-700 hover:underline dark:text-brand-300">
                  ver página <ExternalLink size={13} />
                </a>
              )}
            </p>
          )}
        </div>
        {p && (
          <Button
            variant={p.publicado ? 'secondary' : 'primary'}
            leftIcon={<Globe size={16} />}
            loading={publicar.isPending}
            onClick={() => publicar.mutate(!p.publicado)}
          >
            {p.publicado ? 'Tirar do site' : 'Publicar no site'}
          </Button>
        )}
      </div>

      <SectionCard title="Produto">
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Campo label="Título (como aparece no site e no Google)">
              <input className={input} value={f.titulo} onChange={(e) => set('titulo', e.target.value)} placeholder="Ex.: iPhone 12 Pro Max 256GB Azul Pacífico" />
            </Campo>
          </div>
          <Campo label="Marca"><input className={input} value={f.marca} onChange={(e) => set('marca', e.target.value)} placeholder="Apple, Lenovo, Samsung…" /></Campo>
          <Campo label="Modelo"><input className={input} value={f.modelo ?? ''} onChange={(e) => set('modelo', e.target.value || null)} placeholder="Ex.: IdeaPad Flex 5 14ARE05" /></Campo>
          <Campo label="Categoria">
            <select className={input} value={f.categoria} onChange={(e) => set('categoria', Number(e.target.value) as LojaCategoria)}>
              {Object.entries(CATEGORIA_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          </Campo>
          <Campo label="Cor"><input className={input} value={f.cor ?? ''} onChange={(e) => set('cor', e.target.value || null)} /></Campo>
          <Campo label="Memória RAM (GB)"><input className={input} inputMode="numeric" value={f.ramGb ?? ''} onChange={(e) => set('ramGb', numOuNull(e.target.value))} /></Campo>
          <Campo label="Armazenamento (GB)"><input className={input} inputMode="numeric" value={f.armazenamentoGb ?? ''} onChange={(e) => set('armazenamentoGb', numOuNull(e.target.value))} /></Campo>
        </div>
      </SectionCard>

      <SectionCard title="Estado e garantia">
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo label="Estado">
            <select className={input} value={f.estado} onChange={(e) => mudarEstado(Number(e.target.value) as LojaEstado)}>
              {Object.entries(ESTADO_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          </Campo>
          {segundaMao(f.estado) && (
            <Campo label="Grau estético">
              <select className={input} value={f.grau ?? ''} onChange={(e) => set('grau', e.target.value === '' ? null : (Number(e.target.value) as LojaGrau))}>
                <option value="">—</option>
                {Object.entries(GRAU_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </Campo>
          )}
          <Campo label="Garantia ao cliente (meses)" hint={`Mínimo legal: ${minimoGarantia} meses${f.estado === LojaEstado.Usado ? ' (com acordo expresso)' : ' — novos (mesmo de caixa aberta) e recondicionados'}.`}>
            <input className={input} inputMode="numeric" value={f.garantiaMeses} onChange={(e) => set('garantiaMeses', Number(e.target.value) || 0)} />
          </Campo>
          <Campo label="Regime de IVA" hint="Margem: só bens em segunda mão comprados sem IVA (ex.: a particulares). O cliente vê sempre o mesmo preço.">
            <select className={input} value={f.regimeIva} disabled={!segundaMao(f.estado)} onChange={(e) => set('regimeIva', Number(e.target.value) as LojaRegimeIva)}>
              <option value={LojaRegimeIva.Normal}>Normal (23 % sobre o preço)</option>
              <option value={LojaRegimeIva.Margem}>Margem (IVA só sobre o lucro)</option>
            </select>
          </Campo>
        </div>
      </SectionCard>

      <SectionCard title="Origem, preço e lucro">
        <div className="grid gap-3 sm:grid-cols-2">
          <Campo label="De onde vem">
            <select className={input} value={f.origem} onChange={(e) => {
              const origem = Number(e.target.value) as LojaOrigem;
              setF((s) => ({ ...s, origem, disponibilidade: null }));
            }}>
              <option value={LojaOrigem.Proprio}>Meu (está na loja)</option>
              <option value={LojaOrigem.Fornecedor}>Do fornecedor (por encomenda)</option>
            </select>
          </Campo>
          <Campo label="Disponibilidade">
            <select className={input} value={f.disponibilidade ?? ''} onChange={(e) => set('disponibilidade', e.target.value === '' ? null : (Number(e.target.value) as LojaDisponibilidade))}>
              <option value="">Automática ({f.origem === LojaOrigem.Fornecedor ? 'por encomenda' : 'em stock'})</option>
              {Object.entries(DISPONIBILIDADE_LABEL).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          </Campo>
          <Campo label="Preço de venda (€, final ao cliente)">
            <input className={input} inputMode="decimal" value={f.preco || ''} onChange={(e) => set('preco', numOuNull(e.target.value) ?? 0)} />
          </Campo>
          <Campo label="Custo (€) — só tu vês" hint={f.regimeIva === LojaRegimeIva.Margem ? 'Preço a que compraste o equipamento (sem as peças da reparação).' : undefined}>
            <input className={input} inputMode="decimal" value={f.custo ?? ''} onChange={(e) => set('custo', numOuNull(e.target.value))} />
          </Campo>
        </div>
        <dl className="mt-3 grid grid-cols-3 gap-2 rounded-lg bg-zinc-50 p-3 text-sm dark:bg-zinc-950">
          <div><dt className="text-xs text-zinc-500">Cliente paga</dt><dd className="font-semibold tabular-nums">{euro(f.preco)}</dd></div>
          <div><dt className="text-xs text-zinc-500">IVA ao Estado</dt><dd className="font-semibold tabular-nums">{euro(iva)}</dd></div>
          <div><dt className="text-xs text-zinc-500">Lucro real</dt><dd className={`font-semibold tabular-nums ${lucro != null && lucro < 0 ? 'text-rose-600' : 'text-emerald-700 dark:text-emerald-300'}`}>{lucro == null ? 'põe o custo' : euro(lucro)}</dd></div>
        </dl>
      </SectionCard>

      <SectionCard title="Descrição e especificações">
        <Campo label="Descrição">
          <textarea className={`${input} min-h-28 py-2`} value={f.descricao ?? ''} onChange={(e) => set('descricao', e.target.value || null)}
            placeholder="O que é, para quem é, estado real (riscos, bateria…), o que inclui." />
        </Campo>
        <p className="mb-2 mt-4 text-sm font-medium text-zinc-700 dark:text-zinc-300">Tabela de especificações</p>
        <div className="space-y-2">
          {f.specs.map((s, i) => (
            <div key={i} className="flex gap-2">
              <input className={input} value={s.nome} placeholder="Ex.: Processador" onChange={(e) => set('specs', f.specs.map((x, j) => (j === i ? { ...x, nome: e.target.value } : x)))} />
              <input className={input} value={s.valor} placeholder="Ex.: AMD Ryzen 3 4300U" onChange={(e) => set('specs', f.specs.map((x, j) => (j === i ? { ...x, valor: e.target.value } : x)))} />
              <button type="button" aria-label="Remover linha" className="grid min-h-11 w-11 shrink-0 place-items-center rounded-lg text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800" onClick={() => set('specs', f.specs.filter((_, j) => j !== i))}>
                <X size={16} />
              </button>
            </div>
          ))}
          <Button variant="ghost" size="sm" leftIcon={<Plus size={14} />} onClick={() => set('specs', [...f.specs, { nome: '', valor: '' }])}>Adicionar linha</Button>
        </div>
      </SectionCard>

      <SectionCard title="Google (opcional)">
        {!seo ? (
          <button type="button" className="text-sm font-medium text-brand-700 dark:text-brand-300" onClick={() => setSeo(true)}>
            Personalizar endereço, título e descrição para o Google
          </button>
        ) : (
          <div className="grid gap-3">
            <Campo label="Endereço" hint={p?.publicado ? 'Depois de publicado o endereço não muda (o Google já o conhece).' : 'Vazio = gerado a partir do título.'}>
              <input className={input} value={f.slug ?? ''} disabled={p?.publicado} onChange={(e) => set('slug', e.target.value || null)} />
            </Campo>
            <Campo label={`Título no Google (${(f.seoTitulo ?? '').length}/70)`}><input className={input} maxLength={70} value={f.seoTitulo ?? ''} onChange={(e) => set('seoTitulo', e.target.value || null)} /></Campo>
            <Campo label={`Descrição no Google (${(f.seoDescricao ?? '').length}/170)`}><textarea className={`${input} min-h-20 py-2`} maxLength={170} value={f.seoDescricao ?? ''} onChange={(e) => set('seoDescricao', e.target.value || null)} /></Campo>
          </div>
        )}
      </SectionCard>

      {p && (
        <SectionCard title={`Fotos (${fotos.length})`}>
          <p className="mb-3 text-sm text-zinc-500">A 1.ª é a principal. Nos teus recondicionados usa fotos reais (frente, trás, lados e riscos).</p>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {fotos.map((x, i) => (
              <figure key={x.id} className="overflow-hidden rounded-lg border border-zinc-200 dark:border-zinc-800">
                <div className="grid aspect-square place-items-center bg-zinc-50 dark:bg-zinc-950">
                  {x.url ? <img src={x.url} alt={x.alt ?? ''} className="h-full w-full object-contain" /> : <span className="text-xs text-zinc-400">a enviar…</span>}
                </div>
                <figcaption className="flex items-center justify-between gap-1 p-1">
                  <button type="button" disabled={i === 0} aria-label="Para a esquerda" className="grid min-h-9 w-9 place-items-center rounded-md hover:bg-zinc-100 disabled:opacity-30 dark:hover:bg-zinc-800" onClick={() => mover(x.id, -1)}>
                    <ArrowLeft size={14} />
                  </button>
                  <span className="text-xs text-zinc-500">{i === 0 ? 'Principal' : i + 1}</span>
                  <button type="button" aria-label="Apagar foto" className="grid min-h-9 w-9 place-items-center rounded-md text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40" onClick={() => apagarFoto.mutate(x.id)}>
                    <Trash2 size={14} />
                  </button>
                </figcaption>
              </figure>
            ))}
            <button type="button" onClick={() => fileRef.current?.click()} disabled={fotoMut.isPending}
              className="grid aspect-square place-items-center rounded-lg border-2 border-dashed border-zinc-300 text-sm text-zinc-500 hover:border-brand-400 hover:text-brand-700 dark:border-zinc-700">
              <span className="flex flex-col items-center gap-1">
                <ImagePlus size={22} />
                {envio != null ? `${Math.round(envio * 100)} %` : 'Adicionar fotos'}
              </span>
            </button>
          </div>
          <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" multiple hidden
            onChange={(e) => { const fs = Array.from(e.target.files ?? []); e.target.value = ''; if (fs.length) fotoMut.mutate(fs); }} />
        </SectionCard>
      )}

      <div className="fixed inset-x-0 bottom-0 z-30 border-t border-zinc-200 bg-white/95 p-3 backdrop-blur dark:border-zinc-800 dark:bg-zinc-900/95 lg:pl-64">
        <div className="mx-auto flex max-w-4xl items-center justify-between gap-2">
          {p && !p.publicado ? (
            <Button variant="ghost" leftIcon={<Trash2 size={15} />} onClick={async () => {
              if (await confirm({ title: `Apagar #${p.numero}?`, description: 'Apaga o produto e as fotos. Não volta atrás.', confirmLabel: 'Apagar', destructive: true })) apagar.mutate();
            }}>Apagar</Button>
          ) : <span />}
          <Button size="lg" leftIcon={<Save size={16} />} loading={guardar.isPending} onClick={() => guardar.mutate()}>
            {novo ? 'Criar produto' : 'Guardar'}
          </Button>
        </div>
      </div>
    </div>
  );
}
