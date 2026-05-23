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

// Sprint 197: 2D classification — substitui Grading no UI novo.
export const PRODUCT_ORIGIN = {
  New: 0,
  Used: 1,
  Refurbished: 2,
} as const;
export type ProductOrigin = (typeof PRODUCT_ORIGIN)[keyof typeof PRODUCT_ORIGIN];
export const PRODUCT_ORIGIN_LABEL: Record<ProductOrigin, string> = {
  0: 'Novo (selado)',
  1: 'Usado original',
  2: 'Recondicionado',
};

export const PRODUCT_GRADE = {
  Sealed: 0,
  APlusPlus: 1,
  APlus: 2,
  A: 3,
  BPlus: 4,
  B: 5,
  CPlus: 6,
  C: 7,
} as const;
export type ProductGrade = (typeof PRODUCT_GRADE)[keyof typeof PRODUCT_GRADE];
export const PRODUCT_GRADE_LABEL: Record<ProductGrade, string> = {
  0: 'Selado',
  1: 'A++ · Como novo (open-box 100% bateria)',
  2: 'A+ · Como novo (vestígio quase impercetível)',
  3: 'A · Excelente (ligeira descoloração possível)',
  4: 'B+ · Muito bom (max 3 vestígios menores)',
  5: 'B · Bom (max 5 vestígios menores)',
  6: 'C+ · Razoável (riscos profundos ou amolgadelas)',
  7: 'C · Aceitável (desgaste significativo)',
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
  /** Sprint 197: deprecated, server recalcula de Origin+Grade. UI nova usa Origin+Grade. */
  grading: ProductGrading;
  origin: ProductOrigin;
  grade: ProductGrade;
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
  /** ID existente quando editing produto; null/undefined em novas imagens. */
  id?: string;
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
  /** Sprint 197: deprecated. Server recalcula de Origin+Grade. */
  grading: ProductGrading;
  origin: ProductOrigin;
  grade: ProductGrade;
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

export interface ImportProductsResponse {
  created: number;
  updated: number;
  skipped: number;
  errors: { line: number; field: string; message: string; sku: string | null }[];
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
  // Sprint 153: importer CSV Molano. Upsert idempotente — re-importar mesmo CSV não duplica.
  importMolano(fornecedorId: string, csv: string) {
    return api.post<ImportProductsResponse>('/products/import-molano', { fornecedorId, csv }).then((r) => r.data);
  },
  // Sprint 155b: migração one-off de produtos shop-only (vinham só da loja antes do
  // single-source-of-truth). Aceita o JSON exportado pelo outro Claude via npm run db:export-shop-only.
  migrateShop(products: MigrateShopProductRequest[]) {
    return api.post<ImportProductsResponse>('/products/migrate-shop', { products }).then((r) => r.data);
  },
};

export interface MigrateShopProductRequest {
  sku: string;
  brand: string;
  model: string;
  title: string;
  category: string;
  priceCents: number;
  compareAtPriceCents: number | null;
  stockQuantity: number;
  storage: string | null;
  color: string | null;
  grading: string | null;
  description: string | null;
  seoTitle: string | null;
  seoDescription: string | null;
  images: string[] | null;
  isOpenBox: boolean;
  openBoxReason: string | null;
  isActive: boolean;
}
