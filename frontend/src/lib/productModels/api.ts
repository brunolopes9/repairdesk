import { api } from '../api';

export interface ModelImageDto {
  url: string;
  alt: string | null;
  ordem: number;
}

export interface ProductModelDto {
  id: string;
  brand: string;
  model: string;
  descriptionMarkdown: string | null;
  specsJson: string | null;
  batteryUpgradePriceCents: number | null;
  category: number;
  series: string | null;
  active: boolean;
  unitsCount: number;
  images: ModelImageDto[];
}

export interface CreateOrUpdateModelPayload {
  brand: string;
  model: string;
  descriptionMarkdown?: string | null;
  specsJson?: string | null;
  batteryUpgradePriceCents?: number | null;
  category?: number | null;
  series?: string | null;
  active?: boolean | null;
}

export const productModelsApi = {
  list() {
    return api.get<ProductModelDto[]>('/product-models').then((r) => r.data);
  },
  get(id: string) {
    return api.get<ProductModelDto>(`/product-models/${id}`).then((r) => r.data);
  },
  create(payload: CreateOrUpdateModelPayload) {
    return api.post<ProductModelDto>('/product-models', payload).then((r) => r.data);
  },
  update(id: string, payload: CreateOrUpdateModelPayload) {
    return api.put<ProductModelDto>(`/product-models/${id}`, payload).then((r) => r.data);
  },
  delete(id: string) {
    return api.delete<void>(`/product-models/${id}`).then((r) => r.data);
  },
};
