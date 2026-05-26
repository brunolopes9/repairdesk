import { useEffect, useRef, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eraser } from 'lucide-react';
import Modal from '../Modal';
import { signaturesApi } from '../../lib/signatures/api';
import { SIGNATURE_TYPE, SIGNATURE_TYPE_LABEL, type SignatureType } from '../../lib/signatures/types';
import { toast } from '../../lib/toast';

interface Props {
  open: boolean;
  reparacaoId: string;
  tipoSugerido: SignatureType;
  /** Pré-preenche o nome (cliente da reparação) — Bruno pode editar. */
  defaultName?: string;
  defaultContacto?: string;
  onClose: () => void;
  onSaved?: () => void;
}

/**
 * Sprint 344 (Doc 83 Pillar 3): canvas para captura de assinatura digital.
 * Suporta touch (tablet/phone) e rato. Exporta PNG transparente como data URL.
 * Stack-pure (sem deps externas — usa HTML5 canvas nativo + pointer events).
 */
export default function SignaturePadModal({
  open,
  reparacaoId,
  tipoSugerido,
  defaultName = '',
  defaultContacto = '',
  onClose,
  onSaved,
}: Props) {
  const qc = useQueryClient();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [tipo, setTipo] = useState<SignatureType>(tipoSugerido);
  const [nome, setNome] = useState(defaultName);
  const [contacto, setContacto] = useState(defaultContacto);
  const [hasInk, setHasInk] = useState(false);
  const drawingRef = useRef(false);
  const lastPointRef = useRef<{ x: number; y: number } | null>(null);

  useEffect(() => {
    if (!open) return;
    setTipo(tipoSugerido);
    setNome(defaultName);
    setContacto(defaultContacto);
    setHasInk(false);
    // Setup canvas (high-DPI safe)
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ratio = window.devicePixelRatio || 1;
    const cssWidth = canvas.clientWidth;
    const cssHeight = canvas.clientHeight;
    canvas.width = cssWidth * ratio;
    canvas.height = cssHeight * ratio;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(ratio, ratio);
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.lineWidth = 2.5;
    ctx.strokeStyle = '#111';
    ctx.clearRect(0, 0, cssWidth, cssHeight);
  }, [open, tipoSugerido, defaultName, defaultContacto]);

  function getPoint(e: React.PointerEvent<HTMLCanvasElement>) {
    const rect = e.currentTarget.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  function startDraw(e: React.PointerEvent<HTMLCanvasElement>) {
    e.currentTarget.setPointerCapture(e.pointerId);
    drawingRef.current = true;
    lastPointRef.current = getPoint(e);
  }
  function moveDraw(e: React.PointerEvent<HTMLCanvasElement>) {
    if (!drawingRef.current) return;
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) return;
    const pt = getPoint(e);
    const last = lastPointRef.current;
    if (last) {
      ctx.beginPath();
      ctx.moveTo(last.x, last.y);
      ctx.lineTo(pt.x, pt.y);
      ctx.stroke();
    }
    lastPointRef.current = pt;
    if (!hasInk) setHasInk(true);
  }
  function endDraw() {
    drawingRef.current = false;
    lastPointRef.current = null;
  }
  function clearCanvas() {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) return;
    const ratio = window.devicePixelRatio || 1;
    ctx.clearRect(0, 0, canvas.width / ratio, canvas.height / ratio);
    setHasInk(false);
  }

  const save = useMutation({
    mutationFn: async () => {
      const canvas = canvasRef.current!;
      const imagemDataUrl = canvas.toDataURL('image/png');
      return signaturesApi.capture(reparacaoId, {
        tipo,
        imagemDataUrl,
        assinanteNome: nome.trim(),
        assinanteContacto: contacto.trim() || undefined,
      });
    },
    onSuccess: () => {
      toast.success('Assinatura guardada.');
      qc.invalidateQueries({ queryKey: ['signatures', reparacaoId] });
      onSaved?.();
      onClose();
    },
    onError: (err) => {
      const e = err as { response?: { data?: { message?: string; code?: string } } };
      toast.error(e.response?.data?.message ?? e.response?.data?.code ?? 'Erro ao guardar assinatura.');
    },
  });

  const canSave = hasInk && nome.trim().length >= 2 && !save.isPending;

  return (
    <Modal
      open={open}
      title="Recolher assinatura digital"
      onClose={onClose}
      footer={<>
        <button
          type="button"
          onClick={onClose}
          className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800"
        >
          Cancelar
        </button>
        <button
          type="button"
          disabled={!canSave}
          onClick={() => save.mutate()}
          className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
        >
          {save.isPending ? 'A guardar…' : 'Guardar assinatura'}
        </button>
      </>}
    >
      <div className="space-y-3">
        <label className="block text-sm">
          <span className="font-medium">Contexto</span>
          <select
            value={tipo}
            onChange={(e) => setTipo(Number(e.target.value) as SignatureType)}
            className="mt-1 w-full rounded-md border border-zinc-300 px-3 py-2 dark:border-zinc-700 dark:bg-zinc-800"
          >
            <option value={SIGNATURE_TYPE.EntradaAutorizacao}>{SIGNATURE_TYPE_LABEL[SIGNATURE_TYPE.EntradaAutorizacao]}</option>
            <option value={SIGNATURE_TYPE.EntregaLevantamento}>{SIGNATURE_TYPE_LABEL[SIGNATURE_TYPE.EntregaLevantamento]}</option>
          </select>
        </label>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label className="block text-sm">
            <span className="font-medium">Nome do assinante</span>
            <input
              type="text"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
              placeholder="Nome completo"
              className="mt-1 w-full rounded-md border border-zinc-300 px-3 py-2 dark:border-zinc-700 dark:bg-zinc-800"
            />
          </label>
          <label className="block text-sm">
            <span className="font-medium">Contacto (opcional)</span>
            <input
              type="text"
              value={contacto}
              onChange={(e) => setContacto(e.target.value)}
              placeholder="Telefone ou email"
              className="mt-1 w-full rounded-md border border-zinc-300 px-3 py-2 dark:border-zinc-700 dark:bg-zinc-800"
            />
          </label>
        </div>

        <div className="space-y-1">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium">Assinatura</span>
            <button
              type="button"
              onClick={clearCanvas}
              className="inline-flex items-center gap-1 rounded border border-zinc-300 px-2 py-0.5 text-xs hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
            >
              <Eraser size={12} /> Limpar
            </button>
          </div>
          <canvas
            ref={canvasRef}
            onPointerDown={startDraw}
            onPointerMove={moveDraw}
            onPointerUp={endDraw}
            onPointerCancel={endDraw}
            onPointerLeave={endDraw}
            className="block h-48 w-full touch-none rounded-md border border-dashed border-zinc-400 bg-white dark:border-zinc-600 dark:bg-zinc-100"
            style={{ touchAction: 'none' }}
          />
          <p className="text-xs text-zinc-500">
            Cliente assina aqui. Suporta toque (tablet/phone) e rato.
          </p>
        </div>
      </div>
    </Modal>
  );
}
