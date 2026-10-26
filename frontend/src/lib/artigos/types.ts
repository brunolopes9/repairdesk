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

export interface BalcaoVenda {
  linhas: { artigoId: string; quantidade: number; precoUnitarioCents?: number | null }[];
  paymentMethod: PaymentMethod;
  clienteId?: string | null;
}
