import { api } from '../api';

export const FOTO_TIPO = {
  Antes: 0,
  Durante: 1,
  Depois: 2,
} as const;

export type FotoTipo = (typeof FOTO_TIPO)[keyof typeof FOTO_TIPO];

export const FOTO_TIPO_LABEL: Record<FotoTipo, string> = {
  0: 'Antes',
  1: 'Durante',
  2: 'Depois',
};

export const FOTO_TIPO_COLOR: Record<FotoTipo, string> = {
  0: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
  1: 'bg-blue-100 text-blue-800 dark:bg-blue-950/40 dark:text-blue-300',
  2: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300',
};

export interface Foto {
  id: string;
  reparacaoId: string;
  fileName: string;
  contentType: string;
  size: number;
  tipo: FotoTipo;
  ordem: number;
  legenda: string | null;
  visivelNoPortal: boolean;
  criadaEm: string;
}

export const fotosApi = {
  list(reparacaoId: string) {
    return api.get<Foto[]>(`/reparacoes/${reparacaoId}/fotos`).then((r) => r.data);
  },
  async upload(reparacaoId: string, file: File, tipo: FotoTipo, legenda?: string | null) {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('tipo', String(tipo));
    if (legenda) fd.append('legenda', legenda);
    const r = await api.post<Foto>(`/reparacoes/${reparacaoId}/fotos`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return r.data;
  },
  update(fotoId: string, payload: { tipo: FotoTipo; ordem: number; legenda: string | null; visivelNoPortal: boolean }) {
    return api.put<Foto>(`/reparacoes/fotos/${fotoId}`, payload).then((r) => r.data);
  },
  remove(fotoId: string) {
    return api.delete(`/reparacoes/fotos/${fotoId}`).then(() => undefined);
  },
  contentUrl(fotoId: string) {
    // O backend valida auth (cookie ou header). Como axios envia Authorization
    // header, vamos buscar como blob.
    return api.get(`/reparacoes/fotos/${fotoId}/content`, { responseType: 'blob' })
      .then((r) => URL.createObjectURL(r.data as Blob));
  },
};
