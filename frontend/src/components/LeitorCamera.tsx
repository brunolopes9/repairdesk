import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Camera } from 'lucide-react';
import Modal from './Modal';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Código lido. Em modo contínuo o leitor fica aberto (balcão: vários artigos seguidos). */
  onLido: (codigo: string) => void;
  continuo?: boolean;
  titulo?: string;
}

type Detector = { detect: (v: HTMLVideoElement) => Promise<{ rawValue: string }[]> };
const FORMATOS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'code_39', 'qr_code'];

/**
 * Sprint 572: ler códigos de barras com a câmara do telemóvel (a "pistola" no telemóvel).
 * Usa o leitor nativo do browser (BarcodeDetector: Chrome Android) e, onde não existe (iPhone/Safari),
 * o ZXing carregado só quando é preciso. A câmara desliga-se ao fechar. Precisa de HTTPS.
 */
export default function LeitorCamera({ open, onClose, onLido, continuo = false, titulo = 'Ler código de barras' }: Props) {
  const video = useRef<HTMLVideoElement | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [ultimo, setUltimo] = useState<string | null>(null);
  const onLidoRef = useRef(onLido);
  const onCloseRef = useRef(onClose);
  // Callbacks sempre atuais sem reiniciar a câmara a cada render do pai.
  useLayoutEffect(() => { onLidoRef.current = onLido; onCloseRef.current = onClose; });

  useEffect(() => {
    if (!open) return;
    const el = video.current;
    setErro(null);
    setUltimo(null);
    let parar = false;
    let stream: MediaStream | null = null;
    let pararZxing: (() => void) | null = null;
    let anterior = { codigo: '', em: 0 };

    const lido = (codigo: string) => {
      const agora = Date.now();
      // A câmara lê o mesmo código várias vezes por segundo — só conta de novo passado 1,5 s.
      if (codigo === anterior.codigo && agora - anterior.em < 1500) return;
      anterior = { codigo, em: agora };
      navigator.vibrate?.(80);
      setUltimo(codigo);
      onLidoRef.current(codigo);
      if (!continuo) onCloseRef.current();
    };

    (async () => {
      if (!navigator.mediaDevices?.getUserMedia) {
        setErro('Este browser não deixa usar a câmara (abre o Mender pelo endereço https://).');
        return;
      }
      const Nativo = (window as unknown as { BarcodeDetector?: { new (o: { formats: string[] }): Detector; getSupportedFormats?: () => Promise<string[]> } }).BarcodeDetector;
      try {
        if (Nativo) {
          const suportados = (await Nativo.getSupportedFormats?.()) ?? FORMATOS;
          const formatos = FORMATOS.filter((f) => suportados.includes(f));
          if (formatos.length) {
            stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' } }, audio: false });
            if (parar || !el) return;
            el.srcObject = stream;
            await el.play();
            const detector = new Nativo({ formats: formatos });
            const ciclo = async () => {
              if (parar || !el) return;
              try {
                const r = await detector.detect(el);
                if (r[0]?.rawValue) lido(r[0].rawValue);
              } catch { /* frame sem imagem ainda */ }
              setTimeout(ciclo, 150);
            };
            ciclo();
            return;
          }
        }
        const { BrowserMultiFormatReader } = await import('@zxing/browser');
        if (parar || !el) return;
        const leitor = new BrowserMultiFormatReader();
        const controlo = await leitor.decodeFromConstraints({ video: { facingMode: { ideal: 'environment' } }, audio: false }, el,
          (res) => { if (res) lido(res.getText()); });
        pararZxing = () => controlo.stop();
        if (parar) pararZxing();
      } catch (e) {
        const nome = (e as DOMException)?.name;
        setErro(nome === 'NotAllowedError' ? 'Sem autorização para a câmara — permite o acesso nas definições do browser.'
          : nome === 'NotFoundError' ? 'Não encontrei nenhuma câmara.' : 'Não foi possível abrir a câmara.');
      }
    })();

    return () => {
      parar = true;
      pararZxing?.();
      stream?.getTracks().forEach((t) => t.stop());
      if (el) el.srcObject = null;
    };
  }, [open, continuo]);

  return (
    <Modal open={open} onClose={onClose} title={titulo}>
      {erro ? (
        <p className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700 dark:bg-rose-950/40 dark:text-rose-300">{erro}</p>
      ) : (
        <div className="relative overflow-hidden rounded-xl bg-black">
          <video ref={video} className="aspect-[4/3] w-full object-cover" playsInline muted />
          <div className="pointer-events-none absolute inset-x-8 top-1/2 h-24 -translate-y-1/2 rounded-lg border-2 border-white/80 shadow-[0_0_0_9999px_rgba(0,0,0,0.35)]" />
        </div>
      )}
      <p className="mt-3 flex items-center gap-2 text-sm text-zinc-600 dark:text-zinc-300">
        <Camera size={15} />
        {ultimo ? <>Lido: <span className="font-mono">{ultimo}</span>{continuo && ' — podes ler o próximo.'}</> : 'Aponta para o código de barras, dentro do retângulo.'}
      </p>
    </Modal>
  );
}
