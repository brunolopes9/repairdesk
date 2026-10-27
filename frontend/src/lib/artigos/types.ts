import type { MoloniDadosFiscais } from '../moloni/api';
import type { PaymentMethod } from '../vendas/types';

/** Sprint 566: artigo da loja (SKU + código de barras). Stock = soma dos lotes ligados. */
export interface Artigo {
  id: string;
  numero: number;
  sku: string;
  codigoBarras: string;
  codigoFabricante: string | null;
  nome: string;
  categoria: string | null;
  /** Preço definido (c/ IVA, cêntimos) ou null. */
  precoVendaCents: number | null;
  /** O que o balcão cobra: o definido ou o calculado do lote mais antigo com stock. */
  precoEfetivoCents: number | null;
  /** Sprint 570: "artigo" (preço próprio), "categoria" (ex.: Películas 15 €) ou "calculado" (do lote). */
  origemPreco: 'artigo' | 'categoria' | 'calculado' | null;
  stock: number;
  lotes: number;
  localizacao: string | null;
  ativo: boolean;
  notas: string | null;
}

export interface ArtigoLote {
  linhaId: string;
  numero: number;
  descricao: string;
  fornecedor: string;
  data: string;
  quantidade: number;
  quantidadeEmStock: number;
  custoUnitarioPago: number;
  precoCalculadoComIva: number;
}

export interface ArtigoDetalhe {
  artigo: Artigo;
  lotes: ArtigoLote[];
}

export interface ArtigoWrite {
  nome: string;
  categoria: string | null;
  precoVendaCents: number | null;
  codigoBarras: string | null;
  codigoFabricante: string | null;
  localizacao: string | null;
  notas: string | null;
  ativo: boolean;
  loteIds?: string[];
}

export interface EtiquetasPedido {
  itens: { artigoId: string; copias: number }[];
  larguraMm: number;
  alturaMm: number;
  mostrarPreco: boolean;
}

export interface ArtigoSugestao {
  nome: string;
  loteIds: string[];
  lotes: number[];
  stock: number;
  precoCalculadoCents: number | null;
  localizacao: string | null;
  /** Sprint 570: categoria sugerida (películas → "Películas"). */
  categoria: string | null;
}

/** Sprint 570: categoria com preço por defeito (Guid vazio = só escrita nos artigos, ainda sem registo). */
export interface CategoriaArtigo {
  id: string;
  nome: string;
  precoVendaCents: number | null;
  artigos: number;
}

export interface BalcaoVenda {
  linhas: { artigoId: string; quantidade: number; precoUnitarioCents?: number | null }[];
  paymentMethod: PaymentMethod;
  clienteId?: string | null;
  /** Sprint 571: vazio = fatura-recibo sem NIF (consumidor final). */
  comNif?: MoloniDadosFiscais | null;
}
