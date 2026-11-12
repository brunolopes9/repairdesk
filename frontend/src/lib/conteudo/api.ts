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

  /** Sprint 629: já saiu mas foi apagada nas redes. */
  removida: (id: string) => api.post<ConteudoPublicacao>(`/conteudo/${id}/removida`).then((r) => r.data),
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

    if (ticket.metodo === 'MULTIPART' && ticket.tamanhoParte && ticket.partes) {
      const [meta, partes] = await Promise.all([
        lerDimensoes(file),
        enviarPorPartes(ticket.ficheiroId, file, ticket.tamanhoParte, ticket.partes, onProgress, signal),
      ]);
      return comRetentativas(() =>
        api.post<ConteudoFicheiro>(`/conteudo/ficheiros/${ticket.ficheiroId}/confirmar`, { ...meta, partes }).then((r) => r.data),
        signal,
      );
    }

    const [meta] = await Promise.all([lerDimensoes(file), putComProgresso(ticket.uploadUrl, file, contentType, onProgress, signal)]);

    return api
      .post<ConteudoFicheiro>(`/conteudo/ficheiros/${ticket.ficheiroId}/confirmar`, meta)
      .then((r) => r.data);
  },
};

// ---- Sprint 638: envio por partes ------------------------------------------------------------

const PARTES_EM_PARALELO = 3;
const URLS_POR_PEDIDO = 50;
const TENTATIVAS = 8;

/**
 * Sobe o ficheiro em partes direto para o R2. Se a rede cair (dados móveis, elevador…) só se
 * repete a parte que falhou, com espera crescente; URLs expirados pedem-se outra vez.
 */
async function enviarPorPartes(
  ficheiroId: string,
  file: File,
  tamanhoParte: number,
  total: number,
  onProgress: (fracao: number) => void,
  signal?: AbortSignal,
): Promise<{ numero: number; eTag: string }[]> {
  const urls = new Map<number, string>();
  const pedirUrls = async (de: number) => {
    const ate = Math.min(total, de + URLS_POR_PEDIDO - 1);
    const lista = await comRetentativas(
      () => api.post<{ numero: number; url: string }[]>(`/conteudo/ficheiros/${ficheiroId}/partes`, { de, ate }).then((r) => r.data),
      signal,
    );
    lista.forEach((u) => urls.set(u.numero, u.url));
  };

  const enviadoPorParte = new Map<number, number>();
  const reportar = () => {
    let soma = 0;
    enviadoPorParte.forEach((v) => (soma += v));
    onProgress(Math.min(1, soma / file.size));
  };

  const feitas: { numero: number; eTag: string }[] = [];
  let proxima = 1;
  const trabalhar = async () => {
    while (proxima <= total) {
      const numero = proxima++;
      const inicio = (numero - 1) * tamanhoParte;
      const pedaco = file.slice(inicio, Math.min(file.size, inicio + tamanhoParte));
      for (let tentativa = 1; ; tentativa++) {
        if (signal?.aborted) throw new DOMException('Envio cancelado.', 'AbortError');
        try {
          if (!urls.has(numero)) await pedirUrls(numero);
          const eTag = await putParte(urls.get(numero)!, pedaco, (enviado) => {
            enviadoPorParte.set(numero, enviado);
            reportar();
          }, signal);
          feitas.push({ numero, eTag });
          break;
        } catch (err) {
          enviadoPorParte.set(numero, 0);
          reportar();
          if (err instanceof DOMException && err.name === 'AbortError') throw err;
          if (tentativa >= TENTATIVAS)
            throw new Error(`A parte ${numero} de ${total} falhou várias vezes — verifica a internet e tenta outra vez.`, { cause: err });
          if (err instanceof ErroParte && err.status === 403) urls.delete(numero); // URL expirado: pedir outro
          await esperar(Math.min(30_000, 1000 * 2 ** (tentativa - 1)), signal);
        }
      }
    }
  };
  await Promise.all(Array.from({ length: Math.min(PARTES_EM_PARALELO, total) }, trabalhar));
  return feitas.sort((a, b) => a.numero - b.numero);
}

class ErroParte extends Error {
  readonly status: number;
  constructor(status: number) {
    super(`Parte recusada (${status}).`);
    this.status = status;
  }
}

function putParte(url: string, pedaco: Blob, onEnviado: (bytes: number) => void, signal?: AbortSignal): Promise<string> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('PUT', url);
    xhr.upload.onprogress = (e) => onEnviado(e.loaded);
    xhr.onload = () => {
      const eTag = xhr.getResponseHeader('ETag');
      if (xhr.status >= 200 && xhr.status < 300 && eTag) resolve(eTag);
      else reject(new ErroParte(xhr.status));
    };
    xhr.onerror = () => reject(new ErroParte(0));
    xhr.ontimeout = () => reject(new ErroParte(0));
    xhr.onabort = () => reject(new DOMException('Envio cancelado.', 'AbortError'));
    signal?.addEventListener('abort', () => xhr.abort(), { once: true });
    xhr.send(pedaco);
  });
}

async function comRetentativas<T>(fn: () => Promise<T>, signal?: AbortSignal, vezes = 5): Promise<T> {
  for (let i = 1; ; i++) {
    try {
      return await fn();
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status;
      // Erros de validação (4xx) não melhoram a repetir.
      if (i >= vezes || (status && status >= 400 && status < 500)) throw err;
      await esperar(1000 * 2 ** (i - 1), signal);
    }
  }
}

function esperar(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const t = window.setTimeout(resolve, ms);
    signal?.addEventListener('abort', () => {
      window.clearTimeout(t);
      reject(new DOMException('Envio cancelado.', 'AbortError'));
    }, { once: true });
  });
}

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

export function putComProgresso(
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
