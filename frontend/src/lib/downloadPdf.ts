import { api } from './api';

function descarregar(url: string, nome: string) {
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
}

/**
 * Faz download autenticado de um endpoint que devolve PDF e abre numa nova janela.
 * A janela abre-se logo (ainda dentro do toque/clique): depois do pedido ao servidor os browsers do
 * telemóvel já não deixam abrir janelas e o PDF "não fazia nada". Sem janela → descarrega.
 * Chamar diretamente no onClick, antes de qualquer outro await.
 */
export async function openPdfInNewTab(path: string): Promise<void> {
  const win = window.open('', '_blank');
  try {
    if (win) {
      win.document.title = 'A preparar o PDF…';
      win.document.body.innerHTML = '<p style="font-family:system-ui,sans-serif;padding:24px;color:#52525b">A preparar o PDF…</p>';
    }
  } catch { /* janela de outra origem — ignora */ }
  try {
    const resp = await api.get<Blob>(path, { responseType: 'blob' });
    const url = URL.createObjectURL(resp.data);
    if (win && !win.closed) win.location.href = url;
    else descarregar(url, '');
    setTimeout(() => URL.revokeObjectURL(url), 120_000);
  } catch (err) {
    win?.close();
    throw err;
  }
}

const ehTatil = () => typeof window !== 'undefined' && window.matchMedia?.('(pointer: coarse)').matches;

/**
 * Imprime um PDF já descarregado (tem de ser chamado diretamente no clique, sem await antes).
 * Telemóvel: abre a partilha do sistema — escolhe-se a app da impressora (ex.: Marklife) ou guarda-se.
 * Computador: abre a janela de impressão do browser.
 */
export async function imprimirPdf(blob: Blob, nome: string): Promise<void> {
  const file = new File([blob], nome, { type: 'application/pdf' });
  if (ehTatil() && navigator.canShare?.({ files: [file] })) {
    try {
      await navigator.share({ files: [file], title: nome });
    } catch (err) {
      if ((err as DOMException)?.name !== 'AbortError') throw err; // cancelar a partilha não é erro
    }
    return;
  }
  const url = URL.createObjectURL(blob);
  if (ehTatil()) {
    // Telemóvel sem partilha de ficheiros (ex.: browser antigo): abre o PDF no visualizador.
    if (!window.open(url, '_blank')) descarregar(url, nome);
    setTimeout(() => URL.revokeObjectURL(url), 120_000);
    return;
  }
  const frame = document.createElement('iframe');
  frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
  frame.src = url;
  frame.onload = () => {
    try {
      frame.contentWindow?.focus();
      frame.contentWindow?.print();
    } catch {
      window.open(url, '_blank');
    }
    setTimeout(() => { frame.remove(); URL.revokeObjectURL(url); }, 120_000);
  };
  document.body.appendChild(frame);
}

/** Guarda um blob já descarregado com o nome dado. */
export function guardarBlob(blob: Blob, nome: string) {
  const url = URL.createObjectURL(blob);
  descarregar(url, nome);
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** Faz download autenticado de um endpoint que devolve um ficheiro (CSV/blob) com o nome sugerido. */
export async function downloadFile(path: string, fallbackName: string): Promise<void> {
  const resp = await api.get<Blob>(path, { responseType: 'blob' });
  // Tenta extrair filename do header Content-Disposition
  let filename = fallbackName;
  const cd = resp.headers?.['content-disposition'] as string | undefined;
  if (cd) {
    const m = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(cd);
    if (m) filename = decodeURIComponent(m[1]);
  }
  const url = URL.createObjectURL(resp.data);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
