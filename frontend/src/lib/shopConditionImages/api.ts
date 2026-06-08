import { api } from '../api';

// Sprint 531: imagens ilustrativas por estado de condição da loja online (geridas no Mender, SSoT).
export interface ShopConditionImage {
  grade: string;
  url: string;
  url480w: string | null;
  url1024w: string | null;
  url2048w: string | null;
  blurDataUrl: string | null;
  alt: string | null;
  width: number;
  height: number;
}

/** Os 4 estados da montra (slug ↔ rótulo cliente), na ordem Swappie. */
export const SHOP_CONDITION_GRADES: ReadonlyArray<{ slug: string; label: string; hint: string }> = [
  { slug: 'a-plus', label: 'Premium', hint: 'Impecável, indistinguível de novo' },
  { slug: 'a', label: 'Excelente', hint: 'Micro-marcas imperceptíveis' },
  { slug: 'b-plus', label: 'Muito Bom', hint: 'Marcas ligeiras vistas de perto' },
  { slug: 'b', label: 'Bom', hint: 'Marcas visíveis, 100% funcional' },
];

export const shopConditionImagesApi = {
  list() {
    return api.get<ShopConditionImage[]>('/shop-condition-images').then((r) => r.data);
  },
  upload(grade: string, file: File, alt?: string) {
    const fd = new FormData();
    fd.append('image', file);
    if (alt) fd.append('alt', alt);
    return api
      .put<ShopConditionImage>(`/shop-condition-images/${grade}`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
  remove(grade: string) {
    return api.delete(`/shop-condition-images/${grade}`).then((r) => r.data);
  },
};
