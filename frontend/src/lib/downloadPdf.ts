import { api } from './api';

/** Faz download autenticado de um endpoint que devolve PDF e abre na nova janela. */
export async function openPdfInNewTab(path: string): Promise<void> {
  const resp = await api.get<Blob>(path, { responseType: 'blob' });
  const url = URL.createObjectURL(resp.data);
  const win = window.open(url, '_blank', 'noopener,noreferrer');
  if (!win) {
    // Browser bloqueou popup — download direto
    const a = document.createElement('a');
    a.href = url;
    a.download = '';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  }
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
