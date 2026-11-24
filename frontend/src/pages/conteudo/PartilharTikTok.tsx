import { useState } from 'react';
import { Loader2, Music2 } from 'lucide-react';
import { ConteudoFicheiroTipo, RedeSocial, type ConteudoPublicacao } from '../../lib/conteudo/types';
import { toast } from '../../lib/toast';

/**
 * 24/11: TikTok sem custos. O TikTok recusou a app («uso interno não elegível») e o Bruno não quer pagar
 * um intermediário — por isso o vídeo vai pelo menu de partilha do telemóvel: o botão descarrega o reel,
 * copia a legenda e abre a folha de partilha (escolhe-se TikTok → cola a legenda → Publicar).
 * Sem Web Share com ficheiros (computador), descarrega o vídeo e copia a legenda.
 */
export function PartilharTikTok({ p }: { p: ConteudoPublicacao }) {
  const [aCarregar, setACarregar] = useState(false);
  const video = p.ficheiros.find((f) => f.tipo === ConteudoFicheiroTipo.Video && f.url);
  if (!video?.url) return null;

  const legenda = p.destinos.find((d) => d.rede === RedeSocial.TikTok)?.legendaPropria ?? p.legenda ?? '';
  const nome = `lopestech-${p.numero}.mp4`;

  const copiarLegenda = async () => {
    if (!legenda) return false;
    try {
      await navigator.clipboard.writeText(legenda);
      return true;
    } catch {
      return false;
    }
  };

  const descarregar = (blob: Blob) => {
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = nome;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 60_000);
  };

  const partilhar = async () => {
    setACarregar(true);
    // A legenda copia-se primeiro: o clipboard só funciona enquanto o clique ainda é «recente».
    const copiada = await copiarLegenda();
    try {
      const resp = await fetch(video.url!);
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const blob = await resp.blob();
      const ficheiro = new File([blob], nome, { type: blob.type || 'video/mp4' });
      if (navigator.canShare?.({ files: [ficheiro] })) {
        try {
          await navigator.share({ files: [ficheiro], title: p.titulo });
          toast.success(copiada ? 'No TikTok, cola a legenda (já está copiada) e publica.' : 'Vídeo enviado para a partilha.');
        } catch (e) {
          if ((e as DOMException)?.name !== 'AbortError') throw e;
        }
      } else {
        descarregar(blob);
        toast.success(copiada
          ? 'Vídeo descarregado e legenda copiada — carrega-o no TikTok e cola a legenda.'
          : 'Vídeo descarregado — carrega-o no TikTok.');
      }
    } catch {
      // Sem acesso direto ao ficheiro (rede/CORS): abre o vídeo numa aba para guardar à mão.
      window.open(video.url!, '_blank', 'noopener');
      toast.error(copiada
        ? 'Não consegui preparar o vídeo — abri-o numa aba: guarda-o e publica no TikTok (legenda copiada).'
        : 'Não consegui preparar o vídeo — abri-o numa aba para o guardares.');
    } finally {
      setACarregar(false);
    }
  };

  return (
    <button
      type="button"
      onClick={partilhar}
      disabled={aCarregar}
      title="Abre a partilha do telemóvel com o vídeo e copia a legenda"
      className="inline-flex min-h-9 items-center gap-1.5 rounded-md bg-zinc-950 px-3 text-sm font-medium text-white hover:bg-zinc-800 disabled:opacity-60 dark:bg-white dark:text-zinc-950 dark:hover:bg-zinc-200"
    >
      {aCarregar ? <Loader2 size={15} className="animate-spin" /> : <Music2 size={15} />}
      {aCarregar ? 'A preparar…' : 'Partilhar no TikTok'}
    </button>
  );
}
