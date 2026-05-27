import { api } from '../api';

// Sprint 385/386 (Doc 87): tipos espelham os DTOs do CatalogService (backend).

export type CatalogTab = 'todos' | 'fisico' | 'virtual' | 'loja' | 'sem-conteudo' | 'critico';

export interface CatalogVariant {
  kind: 'product' | 'part';
  id: string;
  sku: string | null;
  cor: string | null;
  armazenamento: string | null;
  grade: string | null;
  fornecedor: string | null;
  tipoStock: 'fisico' | 'virtual';
  qtd: number;
  precoVendaCents: number | null;
  custoUnitarioCents: number;
  lojaOnline: boolean;
  stockCritico: boolean;
  estado: string;
}

export interface CatalogParent {
  kind: 'model' | 'product-group' | 'part-group';
  key: string;
  modelId: string | null;
  nome: string;
  subtitle: string | null;
  skuPai: string | null;
  categoria: string;
  marca: string | null;
  variantCount: number;
  stockFisicoUnidades: number;
  stockVirtualUnidades: number;
  valorStockCents: number;
  lojaOnline: string; // "Publicado" | "Oculto" | "Parcial" | "—"
  conteudo: string;   // "Completo" | "Incompleto" | "—"
  margemMediaPct: number | null;
  imageUrl: string | null;
  variants: CatalogVariant[];
}

export interface CatalogKpis {
  stockFisicoUnidades: number;
  stockFisicoCustoCents: number;
  stockVirtualUnidades: number;
  publicadosLoja: number;
  totalPublicavel: number;
  stockCritico: number;
  semConteudo: number;
}

export interface CatalogList {
  kpis: CatalogKpis;
  parents: CatalogParent[];
}

export interface CatalogFilters {
  q?: string;
  categoria?: string;
  marca?: string;
  fornecedor?: string;
  estado?: string;
  tab?: CatalogTab;
}

export const catalogApi = {
  list(filters: CatalogFilters = {}) {
    return api
      .get<CatalogList>('/catalog', {
        params: {
          q: filters.q || undefined,
          categoria: filters.categoria || undefined,
          marca: filters.marca || undefined,
          fornecedor: filters.fornecedor || undefined,
          estado: filters.estado || undefined,
          tab: filters.tab || 'todos',
        },
      })
      .then((r) => r.data);
  },
  setLojaOnline(kind: 'product' | 'part', id: string, value: boolean) {
    return api
      .post<{ lojaOnline: boolean }>(`/catalog/variant/${kind}/${id}/loja-online`, null, { params: { value } })
      .then((r) => r.data.lojaOnline);
  },
  updateProductFields(id: string, fields: { priceCents?: number; stockQuantity?: number }) {
    return api.post<{ ok: boolean }>(`/catalog/variant/product/${id}/fields`, fields).then((r) => r.data);
  },
};
