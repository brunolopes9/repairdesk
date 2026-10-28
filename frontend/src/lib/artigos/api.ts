import { api } from '../api';
import type { Venda } from '../vendas/types';
import type { Artigo, ArtigoDetalhe, ArtigoSugestao, ArtigoWrite, BalcaoVenda, CategoriaArtigo, EtiquetasPedido } from './types';

export const artigosApi = {
  list(q?: string, incluirInativos = false) {
    return api.get<Artigo[]>('/artigos', { params: { q: q || undefined, incluirInativos } }).then((r) => r.data);
  },
  get(id: string) {
    return api.get<ArtigoDetalhe>(`/artigos/${id}`).then((r) => r.data);
  },
  /** O que a pistola leu. Devolve null se não houver artigo com esse código. */
  porCodigo(codigo: string) {
    return api
      .get<Artigo>(`/artigos/codigo/${encodeURIComponent(codigo.trim())}`)
      .then((r) => r.data)
      .catch((err) => {
        if (err?.response?.status === 404) return null;
        throw err;
      });
  },
  create(req: ArtigoWrite) {
    return api.post<ArtigoDetalhe>('/artigos', req).then((r) => r.data);
  },
  update(id: string, req: ArtigoWrite) {
    return api.put<ArtigoDetalhe>(`/artigos/${id}`, req).then((r) => r.data);
  },
  desligarLote(id: string, loteId: string) {
    return api.delete<ArtigoDetalhe>(`/artigos/${id}/lotes/${loteId}`).then((r) => r.data);
  },
  /** PDF das etiquetas (uma por página, do tamanho do rolo) — abre noutro separador. */
  /** PDF das etiquetas (preparado antes do clique em "Imprimir" — ver EtiquetasModal). */
  etiquetasPdf(req: EtiquetasPedido) {
    return api.post<Blob>('/artigos/etiquetas.pdf', req, { responseType: 'blob', timeout: 60_000 }).then((r) => r.data);
  },
  sugestoes() {
    return api.get<ArtigoSugestao[]>('/artigos/sugestoes').then((r) => r.data);
  },
  categorias() {
    return api.get<CategoriaArtigo[]>('/artigos/categorias').then((r) => r.data);
  },
  guardarCategoria(nome: string, precoVendaCents: number | null) {
    return api.put<CategoriaArtigo>('/artigos/categorias', { nome, precoVendaCents }).then((r) => r.data);
  },
  criarEmLote(itens: { nome: string; loteIds: string[]; precoVendaCents?: number | null; categoria?: string | null; agrupado?: boolean }[], categoria: string | null) {
    return api.post<Artigo[]>('/artigos/em-lote', { itens, categoria }, { timeout: 120_000 }).then((r) => r.data);
  },
  /** Sprint 581: junta artigos repetidos neste (as etiquetas antigas continuam a ler). */
  fundir(destinoId: string, origemIds: string[]) {
    return api.post<ArtigoDetalhe>(`/artigos/${destinoId}/fundir`, { origemIds }).then((r) => r.data);
  },
  /** Sprint 581: desfaz uma junção (o artigo junto volta com os seus lotes e etiqueta). */
  desfazerFusao(fusaoId: string) {
    return api.post<ArtigoDetalhe>(`/artigos/fusoes/${fusaoId}/desfazer`, {}).then((r) => r.data);
  },
  /** Sprint 581: lotes que são peças de reparação (saem da lista "por ligar"). */
  marcarPecaReparacao(loteIds: string[], pecaReparacao = true) {
    return api.put<number>('/artigos/lotes/peca-reparacao', { loteIds, pecaReparacao }).then((r) => r.data);
  },
  vender(req: BalcaoVenda) {
    return api.post<Venda>('/artigos/balcao/vender', req).then((r) => r.data);
  },
};
