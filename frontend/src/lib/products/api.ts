import { api } from '../api';
import type { PagedResult } from '../clientes/types';

export const PRODUCT_GRADING = {
  Novo: 0,
  GradeA: 1,
  GradeB: 2,
  GradeC: 3,
  OpenBox: 4,
  Premium: 5,
} as const;

export type ProductGrading = (typeof PRODUCT_GRADING)[keyof typeof PRODUCT_GRADING];

export const PRODUCT_GRADING_LABEL: Record<ProductGrading, string> = {
  0: 'Novo',
  1: 'Grade A',
  2: 'Grade B',
  3: 'Grade C',
  4: 'Open Box',
  5: 'Premium',
};

export const PRODUCT_SUPPLY_TYPE = {
  Stock: 0,
  Dropship: 1,
} as const;

export type ProductSupplyType = (typeof PRODUCT_SUPPLY_TYPE)[keyof typeof PRODUCT_SUPPLY_TYPE];

export const PRODUCT_SUPPLY_TYPE_LABEL: Record<ProductSupplyType, string> = {
  0: 'Stock próprio',
  1: 'Dropshipping',
};

// Sprint 151: categoria de produto na loja online. Acessórios separados de telemóveis
// para filtros mais limpos.
export const PRODUCT_CATEGORY = {
  Phone: 0,
  Accessory: 1,
  Other: 2,
} as const;

export type ProductCategory = (typeof PRODUCT_CATEGORY)[keyof typeof PRODUCT_CATEGORY];

export const PRODUCT_CATEGORY_LABEL: Record<ProductCategory, string> = {
  0: 'Telemóvel',
  1: 'Acessório',
  2: 'Outro',
};

export interface ProductImage {
  id: string;
  url: string;
  alt: string | null;
  ordem: number;
  // Sprint 151: true = imagem editada/curada por Bruno; false = raw do importer.
  isCurated: boolean;
}

export interface Product {
  id: string;
  sku: string;
  slug: string;
  brand: string;
  model: string;
  storage: string | null;
  color: string | null;
  grading: ProductGrading;
  supplyType: ProductSupplyType;
  category: ProductCategory;
  dropshipSupplierSku: string | null;
  priceCents: number;
  compareAtPriceCents: number | null;
  stockQuantity: number;
  stockMinima: number;
  custoUnitarioCents: number;
  descriptionMarkdown: string | null;
  attributesJson: string | null;
  seoTitle: string | null;
  seoDescription: string | null;
  openBoxReason: string | null;
  active: boolean;
  mostrarLojaOnline: boolean;
  fornecedorId: string | null;
  fornecedorNome: string | null;
  fornecedorCode: string | null;
  images: ProductImage[];
  createdAt: string;
  updatedAt: string | null;
}

export interface ProductImageWriteRequest {
  url: string;
  alt: string | null;
  ordem: number;
  isCurated: boolean;
}

export interface ProductWriteRequest {
  sku: string | null;
  slug: string | null;
  brand: string;
  model: string;
  storage: string | null;
  color: string | null;
  grading: ProductGrading;
  supplyType: ProductSupplyType;
  category: ProductCategory;
  dropshipSupplierSku: string | null;
  priceCents: number;
  compareAtPriceCents: number | null;
  stockQuantity: number;
  stockMinima: number;
  custoUnitarioCents: number;
  descriptionMarkdown: string | null;
  attributesJson: string | null;
  seoTitle: string | null;
  seoDescription: string | null;
  openBoxReason: string | null;
  active: boolean;
  mostrarLojaOnline: boolean;
  fornecedorId: string | null;
  images: ProductImageWriteRequest[];
}

export const productsApi = {
  list(params: { search?: string; brand?: string; lojaOnline?: boolean; includeInactive?: boolean; page?: number; pageSize?: number } = {}) {
    return api.get<PagedResult<Product>>('/products', { params }).then((r) => r.data);
  },
  get(id: string) {
    return api.get<Product>(`/products/${id}`).then((r) => r.data);
  },
  create(req: ProductWriteRequest) {
    return api.post<Product>('/products', req).then((r) => r.data);
  },
  update(id: string, req: ProductWriteRequest) {
    return api.put<Product>(`/products/${id}`, req).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/products/${id}`).then(() => undefined);
  },
};
