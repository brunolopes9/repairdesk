import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from '../../lib/toast';
import { PenLine, Check } from 'lucide-react';
import { reparacoesApi } from '../../lib/reparacoes/api';

/**
 * Sprint 551 (Doc 80 pilar assinaturas / Doc 93 #5): recolha da assinatura do cliente no
 * balcão — vira-se o telemóvel/tablet para o cliente, ele assina no canvas, e a assinatura
 * fica estampada no PDF de entrada/entrega. Recapturar substitui a anterior.
 */
export function ReparacaoAssinaturas({ reparacaoId }: { reparacaoId: string }) {
  const qc = useQueryClient();
  const [capturing, setCapturing] = useState<'entrada' | 'entrega' | null>(null);

  const assinaturas = useQuery({
    queryKey: ['reparacao-assinaturas', reparacaoId],
    queryFn: () => reparacoesApi.listAssinaturas(reparacaoId),
    staleTime: 60_000,
  });

  const save = useMutation({
    mutationFn: ({ tipo, dataUrl }: { tipo: 'entrada' | 'entrega'; dataUrl: string }) =>
      reparacoesApi.saveAssinatura(reparacaoId, tipo, dataUrl),
    onSuccess: () => {
      toast.success('Assinatura guardada — já aparece no PDF.');
      setCapturing(null);
      void qc.invalidateQueries({ queryKey: ['reparacao-assinaturas', reparacaoId] });
    },
    onError: (e) => toast.fromError(e, 'Não foi possível guardar a assinatura.'),
  });

  const info = (tipo: string) => assinaturas.data?.find((a) => a.tipo === tipo);

  return (
    <div className="border-t border-zinc-200 pt-3 dark:border-zinc-800">
      <p className="text-[11px] font-medium uppercase tracking-wide text-zinc-500">Assinaturas do cliente</p>
      <div className="mt-2 grid grid-cols-2 gap-2">
        {(['entrada', 'entrega'] as const).map((tipo) => {
          const a = info(tipo);
          return (
            <button
              key={tipo}
              type="button"
              onClick={() => setCapturing(tipo)}
              className={
                a
                  ? 'flex flex-col items-center gap-0.5 rounded-md border border-emerald-300 px-2 py-2 text-xs text-emerald-700 hover:bg-emerald-50 dark:border-emerald-800/60 dark:text-emerald-300 dark:hover:bg-emerald-950/30'
                  : 'flex flex-col items-center gap-0.5 rounded-md border border-zinc-300 px-2 py-2 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800'
              }
            >
              <span className="flex items-center gap-1 font-medium capitalize">
                {a ? <Check size={12} /> : <PenLine size={12} />} {tipo}
              </span>
              <span className="text-[10px] text-zinc-500">
                {a ? new Date(a.assinadaEm).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : 'recolher'}
              </span>
            </button>
          );
        })}
      </div>

      {capturing && (
        <SignaturePadModal
          title={capturing === 'entrada' ? 'Assinatura — entrada do equipamento' : 'Assinatura — entrega do equipamento'}
          subtitle={
            capturing === 'entrada'
              ? 'O cliente confirma os dados e termos do comprovativo de entrada.'
              : 'O cliente confirma o levantamento do equipamento.'
          }
          saving={save.isPending}
          onClose={() => setCapturing(null)}
          onSave={(dataUrl) => save.mutate({ tipo: capturing, dataUrl })}
        />
      )}
    </div>
  );
}

function SignaturePadModal({
  title,
  subtitle,
  saving,
  onClose,
  onSave,
}: {
  title: string;
  subtitle: string;
  saving: boolean;
  onClose: () => void;
  onSave: (dataUrl: string) => void;
}) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const drawing = useRef(false);
  const [hasInk, setHasInk] = useState(false);

  // Canvas dimensionado ao container × devicePixelRatio para o traço ficar nítido no telemóvel.
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.round(rect.width * dpr);
    canvas.height = Math.round(rect.height * dpr);
    const ctx = canvas.getContext('2d');
    if (ctx) {
      ctx.scale(dpr, dpr);
      ctx.lineWidth = 2.5;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.strokeStyle = '#1e293b';
    }
  }, []);

  const pos = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  };

  const start = (e: React.PointerEvent<HTMLCanvasElement>) => {
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    drawing.current = true;
    const ctx = e.currentTarget.getContext('2d');
    if (!ctx) return;
    const { x, y } = pos(e);
    ctx.beginPath();
    ctx.moveTo(x, y);
  };

  const move = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return;
    e.preventDefault();
    const ctx = e.currentTarget.getContext('2d');
    if (!ctx) return;
    const { x, y } = pos(e);
    ctx.lineTo(x, y);
    ctx.stroke();
    if (!hasInk) setHasInk(true);
  };

  const end = () => {
    drawing.current = false;
  };

  const clear = () => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    setHasInk(false);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div
        className="w-full max-w-lg rounded-xl bg-white p-4 shadow-xl dark:bg-zinc-900"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="text-base font-semibold text-zinc-900 dark:text-zinc-50">{title}</h3>
        <p className="mt-0.5 text-xs text-zinc-500">{subtitle}</p>

        <canvas
          ref={canvasRef}
          className="mt-3 h-48 w-full touch-none rounded-lg border-2 border-dashed border-zinc-300 bg-zinc-50 dark:border-zinc-700 dark:bg-zinc-950"
          onPointerDown={start}
          onPointerMove={move}
          onPointerUp={end}
          onPointerLeave={end}
        />
        <p className="mt-1 text-center text-[11px] text-zinc-400">Assine dentro da caixa com o dedo ou stylus.</p>

        <div className="mt-3 flex items-center justify-between gap-2">
          <button
            type="button"
            onClick={clear}
            className="rounded-md border border-zinc-300 px-3 py-2 text-sm hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
          >
            Limpar
          </button>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md border border-zinc-300 px-3 py-2 text-sm hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
            >
              Cancelar
            </button>
            <button
              type="button"
              disabled={!hasInk || saving}
              onClick={() => {
                const canvas = canvasRef.current;
                if (!canvas) return;
                onSave(canvas.toDataURL('image/png'));
              }}
              className="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-brand-700 disabled:opacity-50"
            >
              {saving ? 'A guardar…' : 'Guardar assinatura'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
