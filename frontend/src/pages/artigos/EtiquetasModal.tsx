import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download, Files, Printer, X } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button } from '../../components/ui';
import { inputCls, labelCls } from '../../components/ui/formClasses';
import { artigosApi } from '../../lib/artigos/api';
import type { EtiquetasPedido } from '../../lib/artigos/types';
import { guardarBlob, imprimirPdf } from '../../lib/downloadPdf';
import { toast } from '../../lib/toast';

/** Tamanho do rolo da última impressão (Marklife P50 por defeito) — conveniência local, não crítico. */
function tamanhoGuardado(): { l: number; a: number } {
  try {
    const v = JSON.parse(localStorage.getItem('mender.etiquetas.tamanho') ?? 'null');
    if (v && typeof v.l === 'number' && typeof v.a === 'number') return v;
  } catch { /* sem storage */ }
  return { l: 40, a: 30 };
}

/** Sprint 596: rolos conhecidos — um toque e o PDF sai do tamanho certo (uma etiqueta por página). */
const PREDEFINIDOS = [
  { nome: 'Marklife P50 · 40×30', l: 40, a: 30 },
  { nome: '50×30', l: 50, a: 30 },
  { nome: 'Epson TM · papel 80 mm', l: 72, a: 35 },
];

interface Props {
  open: boolean;
  onClose: () => void;
  /** artigoId → nº de etiquetas. */
  escolhidos: Record<string, number>;
  onChange: (e: Record<string, number>) => void;
  nomes: Map<string, string>;
}

/**
 * Sprint 572: etiquetas com código de barras. O PDF prepara-se sozinho enquanto a janela está aberta (e a
 * cada alteração), por isso "Imprimir" é imediato: no telemóvel abre a partilha (app da Marklife P50),
 * no computador a janela de impressão. Preparar antes é o que permite à partilha funcionar no telemóvel —
 * só é autorizada dentro do próprio toque.
 */
export default function EtiquetasModal({ open, onClose, escolhidos, onChange, nomes }: Props) {
  const [tamanho, setTamanho] = useState(tamanhoGuardado);
  const [comPreco, setComPreco] = useState(true);

  const itens = Object.entries(escolhidos).filter(([, n]) => n > 0).map(([artigoId, copias]) => ({ artigoId, copias }));
  const total = itens.reduce((s, i) => s + i.copias, 0);
  const pedido: EtiquetasPedido = { itens, larguraMm: tamanho.l, alturaMm: tamanho.a, mostrarPreco: comPreco };

  // Espera um instante depois de cada alteração (ex.: a escrever o nº de cópias) antes de pedir o PDF.
  const [pedidoEstavel, setPedidoEstavel] = useState(pedido);
  const chave = JSON.stringify(pedido);
  useEffect(() => {
    const t = setTimeout(() => setPedidoEstavel(JSON.parse(chave)), 400);
    return () => clearTimeout(t);
  }, [chave]);
  const valido = total > 0 && total <= 500 && tamanho.l >= 20 && tamanho.l <= 120 && tamanho.a >= 10 && tamanho.a <= 120;
  const pdf = useQuery({
    queryKey: ['artigos', 'etiquetas', JSON.stringify(pedidoEstavel)],
    queryFn: () => artigosApi.etiquetasPdf(pedidoEstavel),
    enabled: open && valido && JSON.stringify(pedidoEstavel) === chave,
    staleTime: 60_000,
    gcTime: 60_000,
  });
  const nome = `etiquetas-${total}.pdf`;

  function lembrarTamanho() {
    try { localStorage.setItem('mender.etiquetas.tamanho', JSON.stringify(tamanho)); } catch { /* ignore */ }
  }

  function imprimir() {
    if (!pdf.data) return;
    lembrarTamanho();
    imprimirPdf(pdf.data, nome).catch((err) => toast.fromError(err, 'Não foi possível imprimir.'));
  }

  const pronto = !!pdf.data && !pdf.isFetching && JSON.stringify(pedidoEstavel) === chave;
  const [verTamanho, setVerTamanho] = useState(false);
  const [aSeparar, setASeparar] = useState(false);

  /**
   * Sprint 597: um PDF por artigo (com as cópias dele), de uma vez. No telemóvel abre a partilha com os ficheiros
   * todos (guardar em Ficheiros ou mandar para a app da Marklife); no computador descarrega-os um a um.
   */
  async function guardarSeparados() {
    setASeparar(true);
    try {
      const ficheiros = await Promise.all(itens.map(async (i) => {
        const blob = await artigosApi.etiquetasPdf({ itens: [i], larguraMm: tamanho.l, alturaMm: tamanho.a, mostrarPreco: comPreco });
        const base = (nomes.get(i.artigoId) ?? 'etiqueta').replace(/[\\/:*?"<>|]/g, '-').slice(0, 60);
        return new File([blob], `${base} (${i.copias}).pdf`, { type: 'application/pdf' });
      }));
      lembrarTamanho();
      if (navigator.canShare?.({ files: ficheiros })) {
        try { await navigator.share({ files: ficheiros, title: `${ficheiros.length} etiquetas` }); } catch (err) {
          if ((err as DOMException)?.name !== 'AbortError') throw err;
        }
      } else {
        for (const f of ficheiros) { guardarBlob(f, f.name); await new Promise((r) => setTimeout(r, 300)); }
      }
    } catch (err) {
      toast.fromError(err, 'Não foi possível preparar os PDFs.');
    } finally {
      setASeparar(false);
    }
  }
  // Pré-visualização do PDF (a 1.ª etiqueta), para ver o tamanho e o desenho antes de gastar rolo.
  const previa = useMemo(() => (pdf.data ? URL.createObjectURL(pdf.data) : null), [pdf.data]);
  useEffect(() => () => { if (previa) URL.revokeObjectURL(previa); }, [previa]);
  const epson = tamanho.l > 60;
  return (
    <Modal open={open} onClose={onClose} title={`Imprimir ${total} etiqueta(s)`}
      footer={<>
        <Button type="button" variant="secondary" leftIcon={<Files size={15} />} disabled={!valido} loading={aSeparar} onClick={guardarSeparados}
          title="Um PDF por artigo, todos de uma vez (no telemóvel: partilhar para Ficheiros ou para a app da Marklife)">
          Um PDF por artigo
        </Button>
        <Button type="button" variant="secondary" leftIcon={<Download size={15} />} disabled={!pronto}
          onClick={() => { lembrarTamanho(); guardarBlob(pdf.data!, nome); }} title="Todas as etiquetas num só PDF (uma por página)">
          Guardar PDF
        </Button>
        <Button type="button" leftIcon={<Printer size={15} />} disabled={!pronto} loading={valido && !pronto && !pdf.isError} onClick={imprimir}>
          Imprimir
        </Button>
      </>}>
      <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
        Uma etiqueta por página, do tamanho do rolo. No telemóvel, <strong>Imprimir</strong> abre a partilha: escolhe a app da Marklife.
      </p>
      <p className="mb-2 text-sm">
        Rolo: <strong>{tamanho.l}×{tamanho.a} mm</strong>{tamanho.l === 40 && tamanho.a === 30 ? ' (Marklife P50)' : ''} ·{' '}
        <button type="button" className="text-brand-700 underline dark:text-brand-300" onClick={() => setVerTamanho((v) => !v)}>{verTamanho ? 'fechar' : 'mudar'}</button>
      </p>
      {verTamanho && (<>
      <div className="mb-3 flex flex-wrap gap-2" role="radiogroup" aria-label="Rolo">
        {PREDEFINIDOS.map((p) => (
          <button key={p.nome} type="button" role="radio" aria-checked={tamanho.l === p.l && tamanho.a === p.a} onClick={() => setTamanho({ l: p.l, a: p.a })}
            className={`min-h-9 rounded-lg border px-3 text-xs font-medium ${tamanho.l === p.l && tamanho.a === p.a ? 'border-brand-600 bg-brand-600 text-white' : 'border-zinc-300 dark:border-zinc-700'}`}>
            {p.nome}
          </button>
        ))}
      </div>
      {pdf.isError && <p className="mb-3 text-sm text-rose-600">Não foi possível preparar as etiquetas. Fecha e tenta outra vez.</p>}
      <div className="grid grid-cols-2 gap-3">
        <label><span className={labelCls}>Largura (mm)</span>
          <input type="number" inputMode="numeric" min={20} max={120} value={tamanho.l} onChange={(e) => setTamanho((t) => ({ ...t, l: Number(e.target.value) }))} className={inputCls} /></label>
        <label><span className={labelCls}>Altura (mm)</span>
          <input type="number" inputMode="numeric" min={10} max={120} value={tamanho.a} onChange={(e) => setTamanho((t) => ({ ...t, a: Number(e.target.value) }))} className={inputCls} /></label>
      </div>
      </>)}
      <label className="mt-3 flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" className="h-5 w-5" checked={comPreco} onChange={(e) => setComPreco(e.target.checked)} /> Mostrar o preço na etiqueta</label>
      {previa && (
        <div className="mt-2 flex justify-center rounded-lg bg-zinc-100 p-3 dark:bg-zinc-800">
          <iframe title="Pré-visualização da etiqueta" src={`${previa}#toolbar=0&navpanes=0&view=Fit`}
            style={{ width: `${tamanho.l * 4}px`, height: `${tamanho.a * 4 + 8}px` }} className="max-w-full rounded border border-zinc-300 bg-white" />
        </div>
      )}
      <details className="mt-3 text-xs text-zinc-600 dark:text-zinc-300">
        <summary className="cursor-pointer font-medium">As etiquetas saem tortas ou do tamanho errado?</summary>
        <ul className="mt-1 list-disc space-y-1 pl-4">
          <li>O PDF já tem o tamanho exato da etiqueta ({tamanho.l}×{tamanho.a} mm) — se sai inclinada, é o rolo a entrar torto: encosta as guias do rolo à largura do papel e faz a <strong>calibração do papel</strong> na app da Marklife (ou desliga e volta a ligar a impressora com o rolo novo).</li>
          <li>Imprime em <strong>tamanho real / 100 %</strong> e orientação <strong>retrato</strong> (o PDF já está deitado: 40 de largura × 30 de altura) — «horizontal» no controlador roda a página outra vez.</li>
          <li>Na janela de impressão escolhe a impressora e, em <em>Mais definições</em>: tamanho do papel <strong>{tamanho.l}×{tamanho.a} mm</strong>, margens <strong>Nenhuma</strong>, escala <strong>100 %</strong> (ou «Ajustar à área»).</li>
          <li>Se o tamanho não existir na lista, cria-o nas preferências da impressora do Windows (papel personalizado {tamanho.l}×{tamanho.a} mm) — senão sai uma folha comprida por etiqueta.</li>
          {epson && <li>Epson TM: no controlador escolhe papel <strong>80 × {tamanho.a} mm</strong> (ou «Roll Paper 80 × Receipt») e «Corte por página». O papel de talão normal não é autocolante — para colar nos artigos usa a Marklife.</li>}
        </ul>
      </details>
      <ul className="mt-3 divide-y divide-zinc-100 rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
        {Object.entries(escolhidos).map(([id, n]) => (
          <li key={id} className="flex items-center gap-2 px-3 py-2">
            <span className="min-w-0 flex-1 truncate">{nomes.get(id) ?? 'Artigo'}</span>
            <input type="number" inputMode="numeric" min={0} max={200} value={n} className={`${inputCls} w-20`} aria-label={`Cópias de ${nomes.get(id) ?? 'artigo'}`}
              onChange={(e) => onChange({ ...escolhidos, [id]: Math.max(0, Math.min(200, Number(e.target.value))) })} />
            <button type="button" aria-label={`Tirar ${nomes.get(id) ?? 'artigo'}`} className="grid h-9 w-9 place-items-center rounded text-zinc-400 hover:bg-zinc-100 hover:text-rose-600 dark:hover:bg-zinc-800"
              onClick={() => { const n = { ...escolhidos }; delete n[id]; onChange(n); }}>
              <X size={15} />
            </button>
          </li>
        ))}
      </ul>
      {total > 500 && <p className="mt-2 text-sm text-amber-700">Máximo 500 etiquetas de cada vez.</p>}
    </Modal>
  );
}
