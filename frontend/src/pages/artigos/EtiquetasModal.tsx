import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Download, Printer } from 'lucide-react';
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
  return { l: 50, a: 30 };
}

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
  return (
    <Modal open={open} onClose={onClose} title={`Imprimir ${total} etiqueta(s)`}
      footer={<>
        <Button type="button" variant="secondary" leftIcon={<Download size={15} />} disabled={!pronto}
          onClick={() => { lembrarTamanho(); guardarBlob(pdf.data!, nome); }}>
          Guardar PDF
        </Button>
        <Button type="button" leftIcon={<Printer size={15} />} disabled={!pronto} loading={valido && !pronto && !pdf.isError} onClick={imprimir}>
          Imprimir
        </Button>
      </>}>
      <p className="mb-3 text-sm text-zinc-600 dark:text-zinc-300">
        Uma etiqueta por página, do tamanho do rolo. No telemóvel, <strong>Imprimir</strong> abre a partilha: escolhe a app da Marklife.
      </p>
      {pdf.isError && <p className="mb-3 text-sm text-rose-600">Não foi possível preparar as etiquetas. Fecha e tenta outra vez.</p>}
      <div className="grid grid-cols-2 gap-3">
        <label><span className={labelCls}>Largura (mm)</span>
          <input type="number" inputMode="numeric" min={20} max={120} value={tamanho.l} onChange={(e) => setTamanho((t) => ({ ...t, l: Number(e.target.value) }))} className={inputCls} /></label>
        <label><span className={labelCls}>Altura (mm)</span>
          <input type="number" inputMode="numeric" min={10} max={120} value={tamanho.a} onChange={(e) => setTamanho((t) => ({ ...t, a: Number(e.target.value) }))} className={inputCls} /></label>
      </div>
      <label className="mt-3 flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" className="h-5 w-5" checked={comPreco} onChange={(e) => setComPreco(e.target.checked)} /> Mostrar o preço na etiqueta</label>
      <ul className="mt-3 divide-y divide-zinc-100 rounded-lg border border-zinc-200 text-sm dark:divide-zinc-800 dark:border-zinc-800">
        {Object.entries(escolhidos).map(([id, n]) => (
          <li key={id} className="flex items-center gap-2 px-3 py-2">
            <span className="min-w-0 flex-1 truncate">{nomes.get(id) ?? id}</span>
            <input type="number" inputMode="numeric" min={0} max={200} value={n} className={`${inputCls} w-20`} aria-label={`Cópias de ${nomes.get(id) ?? 'artigo'}`}
              onChange={(e) => onChange({ ...escolhidos, [id]: Math.max(0, Math.min(200, Number(e.target.value))) })} />
          </li>
        ))}
      </ul>
      {total > 500 && <p className="mt-2 text-sm text-amber-700">Máximo 500 etiquetas de cada vez.</p>}
    </Modal>
  );
}
