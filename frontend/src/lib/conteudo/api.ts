import { api } from '../api';
import type { ConteudoEstado, HoraPublicacao, ConteudoFicheiro, ConteudoPublicacao, ConteudoResumo, GuardarOpcoesTikTok, UploadTicket } from './types';

export const conteudoApi = {
  list: (filtros: { estados?: ConteudoEstado[]; de?: string; ate?: string } = {}) =>
    api
      .get<ConteudoPublicacao[]>('/conteudo', {
        params: { estados: filtros.estados, de: filtros.de, ate: filtros.ate },
        // ASP.NET espera estados=1&estados=2 (não estados[]=1).
        paramsSerializer: { indexes: null },
      })
      .then((r) => r.data),

  obter: (id: string) => api.get<ConteudoPublicacao>(`/conteudo/${id}`).then((r) => r.data),

  resumo: () => api.get<ConteudoResumo>('/conteudo/resumo').then((r) => r.data),

  /** Sem `hora` fica a hora marcada; com `hora` aprova para essa hora (ou «agora»). */
  aprovar: (id: string, hora?: HoraPublicacao) =>
    api.post<ConteudoPublicacao>(`/conteudo/${id}/aprovar`, hora).then((r) => r.data),

  /** Muda a hora (ou publica já) sem devolver a publicação à fila «Por aprovar». */
  mudarHora: (id: string, hora: HoraPublicacao) =>
    api.patch<ConteudoPublicacao>(`/conteudo/${id}/hora`, hora).then((r) => r.data),
  guardarTikTok: (id: string, o: GuardarOpcoesTikTok) =>
    api.put<ConteudoPublicacao>(`/conteudo/${id}/tiktok`, o).then((r) => r.data),

  rejeitar: (id: string, motivo: string | null) =>
    api.post<ConteudoPublicacao>(`/conteudo/${id}/rejeitar`, { motivo }).then((r) => r.data),

  cancelar: (id: string) => api.post<ConteudoPublicacao>(`/conteudo/${id}/cancelar`).then((r) => r.data),

  biblioteca: (soPorProcessar = false) =>
    api.get<ConteudoFicheiro[]>('/conteudo/biblioteca', { params: { soPorProcessar } }).then((r) => r.data),

  apagarFicheiro: (id: string) => api.delete(`/conteudo/ficheiros/${id}`),

  /**
   * Envia um ficheiro em 3 passos: pede o URL (pré-assinado no R2), faz PUT direto do browser
   * com progresso — sem passar pela API, por isso aguenta vídeos de vários GB — e confirma.
   */
  async enviar(
    file: File,
    notas: string | null,
    onProgress: (fracao: number) => void,
    signal?: AbortSignal,
  ): Promise<ConteudoFicheiro> {
    const contentType = normalizarTipo(file);
    const ticket = await api
      .post<UploadTicket>('/conteudo/ficheiros/upload', {
        nomeOriginal: file.name,
        contentType,
        bytes: file.size,
        notas,
      })
      .then((r) => r.data);

    const [meta] = await Promise.all([lerDimensoes(file), putComProgresso(ticket.uploadUrl, file, contentType, onProgress, signal)]);

    return api
      .post<ConteudoFicheiro>(`/conteudo/ficheiros/${ticket.ficheiroId}/confirmar`, meta)
      .then((r) => r.data);
  },
};

/** Alguns telemóveis não preenchem file.type (ex: .mov no Android) — deduz pela extensão. */
function normalizarTipo(file: File): string {
  if (file.type) return file.type;
  const ext = file.name.split('.').pop()?.toLowerCase();
  const porExtensao: Record<string, string> = {
    mov: 'video/quicktime', mp4: 'video/mp4', m4v: 'video/x-m4v',
    png: 'image/png', webp: 'image/webp', heic: 'image/heic', heif: 'image/heif',
  };
  return (ext && porExtensao[ext]) || 'image/jpeg';
}

function putComProgresso(
  url: string,
  file: File,
  contentType: string,
  onProgress: (fracao: number) => void,
  signal?: AbortSignal,
): Promise<void> {
  // XHR em vez de fetch: é a única forma de ter progresso de upload no browser.
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('PUT', url);
    xhr.setRequestHeader('Content-Type', contentType);
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) onProgress(e.loaded / e.total);
    };
    xhr.onload = () =>
      xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error(`O envio falhou (${xhr.status}).`));
    xhr.onerror = () => reject(new Error('Sem ligação ao armazenamento — verifica a internet e tenta outra vez.'));
    xhr.onabort = () => reject(new DOMException('Envio cancelado.', 'AbortError'));
    signal?.addEventListener('abort', () => xhr.abort());
    xhr.send(file);
  });
}

/** Largura/altura (e duração nos vídeos) lidas no próprio browser, sem custo no servidor. */
type Dimensoes = { largura: number | null; altura: number | null; duracaoSeg: number | null };

function lerDimensoes(file: File): Promise<Dimensoes> {
  const vazio: Dimensoes = { largura: null, altura: null, duracaoSeg: null };
  const url = URL.createObjectURL(file);
  const limpar = () => URL.revokeObjectURL(url);
  return new Promise((resolve) => {
    const timeout = window.setTimeout(() => {
      limpar();
      resolve(vazio);
    }, 15_000);
    const fim = (v: Dimensoes) => {
      window.clearTimeout(timeout);
      limpar();
      resolve(v);
    };
    if (normalizarTipo(file).startsWith('video/')) {
      const v = document.createElement('video');
      v.preload = 'metadata';
      v.onloadedmetadata = () =>
        fim({ largura: v.videoWidth || null, altura: v.videoHeight || null, duracaoSeg: Number.isFinite(v.duration) ? v.duration : null });
      v.onerror = () => fim(vazio);
      v.src = url;
    } else {
      const img = new Image();
      img.onload = () => fim({ largura: img.naturalWidth || null, altura: img.naturalHeight || null, duracaoSeg: null });
      img.onerror = () => fim(vazio);
      img.src = url;
    }
  });
}
