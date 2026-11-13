import { api } from '../api';
import { putComProgresso } from '../conteudo/api';

// Sprint 614 (Doc 97): loja do lopestech.pt — produtos geridos no Mender, publicados no site.

export const LojaCategoria = { Smartphones: 0, Computadores: 1, Tablets: 2, Consolas: 3, Acessorios: 4, Outros: 5, Pecas: 6 } as const;
export type LojaCategoria = (typeof LojaCategoria)[keyof typeof LojaCategoria];
export const LojaEstado = { Novo: 0, NovoCaixaAberta: 1, Recondicionado: 2, Usado: 3 } as const;
export type LojaEstado = (typeof LojaEstado)[keyof typeof LojaEstado];
export const LojaGrau = { Premium: 0, Excelente: 1, MuitoBom: 2, Bom: 3 } as const;
export type LojaGrau = (typeof LojaGrau)[keyof typeof LojaGrau];
export const LojaOrigem = { Proprio: 0, Fornecedor: 1 } as const;
export type LojaOrigem = (typeof LojaOrigem)[keyof typeof LojaOrigem];
export const LojaDisponibilidade = { EmStock: 0, PorEncomenda: 1, Esgotado: 2 } as const;
export type LojaDisponibilidade = (typeof LojaDisponibilidade)[keyof typeof LojaDisponibilidade];
export const LojaRegimeIva = { Normal: 0, Margem: 1 } as const;
export type LojaRegimeIva = (typeof LojaRegimeIva)[keyof typeof LojaRegimeIva];

export const CATEGORIA_LABEL: Record<LojaCategoria, string> = {
  0: 'Smartphones', 1: 'Computadores', 2: 'Tablets', 3: 'Consolas', 4: 'Acessórios', 5: 'Outros', 6: 'Peças',
};
export const ESTADO_LABEL: Record<LojaEstado, string> = { 0: 'Novo', 1: 'Novo · caixa aberta', 2: 'Recondicionado', 3: 'Usado' };
export const GRAU_LABEL: Record<LojaGrau, string> = { 0: 'Premium', 1: 'Excelente', 2: 'Muito bom', 3: 'Bom' };
export const DISPONIBILIDADE_LABEL: Record<LojaDisponibilidade, string> = { 0: 'Em stock', 1: 'Por encomenda', 2: 'Esgotado' };

export interface SpecLoja {
  nome: string;
  valor: string;
}

export interface ProdutoLojaFoto {
  id: string;
  ordem: number;
  alt: string | null;
  url: string | null;
  enviada: boolean;
}

export interface ProdutoLoja {
  id: string;
  numero: number;
  slug: string;
  titulo: string;
  marca: string;
  modelo: string | null;
  categoria: LojaCategoria;
  estado: LojaEstado;
  grau: LojaGrau | null;
  garantiaMeses: number;
  regimeIva: LojaRegimeIva;
  origem: LojaOrigem;
  fornecedorId: string | null;
  disponibilidade: LojaDisponibilidade;
  preco: number;
  custo: number | null;
  /** Sprint 654: preço riscado (promoção) e unidades em stock. */
  precoAnterior?: number | null;
  unidades?: number | null;
  ivaEstimado: number;
  lucroEstimado: number | null;
  cor: string | null;
  ramGb: number | null;
  armazenamentoGb: number | null;
  descricao: string | null;
  specs: SpecLoja[];
  seoTitulo: string | null;
  seoDescricao: string | null;
  publicado: boolean;
  publicadoEm: string | null;
  urlSite: string;
  fotos: ProdutoLojaFoto[];
  createdAt: string;
  updatedAt: string | null;
  /** Sprint 655: artigo do stock ligado — preço, custo e unidades vêm dele. */
  artigoId?: string | null;
  artigoNumero?: number | null;
  artigoNome?: string | null;
}

export interface GuardarProdutoLoja {
  titulo: string;
  marca: string;
  modelo: string | null;
  categoria: LojaCategoria;
  estado: LojaEstado;
  grau: LojaGrau | null;
  garantiaMeses: number;
  regimeIva: LojaRegimeIva;
  origem: LojaOrigem;
  fornecedorId: string | null;
  disponibilidade: LojaDisponibilidade | null;
  preco: number;
  custo: number | null;
  /** Sprint 654: preço riscado (promoção) e unidades em stock. */
  precoAnterior?: number | null;
  unidades?: number | null;
  cor: string | null;
  ramGb: number | null;
  armazenamentoGb: number | null;
  descricao: string | null;
  specs: SpecLoja[];
  slug: string | null;
  seoTitulo: string | null;
  seoDescricao: string | null;
  /** Sprint 655: artigo do stock (o servidor ignora preço/unidades/custo enviados e usa os do artigo). */
  artigoId?: string | null;
}

interface UploadFotoTicket {
  fotoId: string;
  uploadUrl: string;
  contentType: string;
  expiraEm: string;
}

const base = '/loja/produtos';

export const lojaApi = {
  list: () => api.get<ProdutoLoja[]>(base).then((r) => r.data),
  get: (id: string) => api.get<ProdutoLoja>(`${base}/${id}`).then((r) => r.data),
  create: (b: GuardarProdutoLoja) => api.post<ProdutoLoja>(base, b).then((r) => r.data),
  update: (id: string, b: GuardarProdutoLoja) => api.put<ProdutoLoja>(`${base}/${id}`, b).then((r) => r.data),
  publicar: (id: string, sim: boolean) =>
    api.post<ProdutoLoja>(`${base}/${id}/${sim ? 'publicar' : 'despublicar'}`).then((r) => r.data),
  apagar: (id: string) => api.delete(`${base}/${id}`),
  ordenarFotos: (id: string, ordem: string[]) => api.put<ProdutoLoja>(`${base}/${id}/fotos/ordem`, ordem).then((r) => r.data),
  apagarFoto: (id: string, fotoId: string) => api.delete<ProdutoLoja>(`${base}/${id}/fotos/${fotoId}`).then((r) => r.data),

  /** Envia a foto direto para o armazenamento (sem passar pelo servidor) e confirma. */
  async enviarFoto(id: string, file: File, alt: string | null, onProgress: (f: number) => void): Promise<ProdutoLoja> {
    const contentType = file.type || 'image/jpeg';
    const t = await api
      .post<UploadFotoTicket>(`${base}/${id}/fotos/upload`, { nomeOriginal: file.name, contentType, bytes: file.size, alt })
      .then((r) => r.data);
    await putComProgresso(t.uploadUrl, file, contentType, onProgress);
    return api.post<ProdutoLoja>(`${base}/${id}/fotos/${t.fotoId}/confirmar`).then((r) => r.data);
  },
};

export const euro = (v: number | null | undefined) =>
  v == null ? '—' : new Intl.NumberFormat('pt-PT', { style: 'currency', currency: 'EUR' }).format(v);
