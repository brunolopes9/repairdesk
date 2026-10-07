import type { RegimeIva } from '../fornecedores/api';

/** Linha de compra = lote de stock. Valores em euros (decimais), calculados pelo motor de IVA do backend. */
export interface CompraLinha {
  id: string;
  descricao: string;
  quantidade: number;
  quantidadeVendida: number;
  quantidadeAbatida: number;
  quantidadeEmStock: number;
  precoUnitarioPago: number;
  taxaIvaCompra: number;
  lucroUnitario: number;
  localizacao: string | null;
  custoSemIva: number;
  ivaPagoNaCompra: number;
  autoliquidacaoUe: number;
  lucroComIva: number;
  precoVendaSemIva: number;
  ivaDaVenda: number;
  precoFinalComIva: number;
  ivaAPagarEstado: number;
  totalPago: number;
  totalSemIva: number;
}

export interface CompraDocumento {
  id: string;
  fornecedorId: string;
  fornecedorNome: string;
  regimeIva: RegimeIva;
  data: string;
  numeroFatura: string | null;
  numerosEncomenda: string[];
  metodoPagamento: string | null;
  portesPagos: number;
  portesIva: number | null;
  totalDocumento: number | null;
  notas: string | null;
  faturaEmFalta: boolean;
  supplierInvoiceImportId: string | null;
  unidades: number;
  unidadesEmStock: number;
  totalLinhasPago: number;
  totalCalculado: number;
  diferenca: number | null;
  temDiferenca: boolean;
  linhas: CompraLinha[];
}

export interface CompraLinhaWrite {
  id: string | null;
  descricao: string;
  quantidade: number;
  precoUnitarioPago: number;
  /** null → taxa do regime do fornecedor (23% nacional, 0% UE). */
  taxaIvaCompra: number | null;
  /** null → 1 € películas/vidros, 5 € resto. */
  lucroUnitario: number | null;
  localizacao: string | null;
}

export interface CompraDocumentoWrite {
  fornecedorId: string;
  data: string;
  numeroFatura: string | null;
  numerosEncomenda: string[] | null;
  metodoPagamento: string | null;
  portesPagos: number;
  portesIva: number | null;
  totalDocumento: number | null;
  notas: string | null;
  linhas: CompraLinhaWrite[];
  ignorarDuplicado?: boolean;
}

export interface CompraFiltro {
  q?: string;
  fornecedorId?: string;
  faturaEmFalta?: boolean;
  de?: string;
  ate?: string;
  page?: number;
  pageSize?: number;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface InventarioLinha {
  linhaId: string;
  documentoId: string;
  fornecedor: string;
  data: string;
  referencia: string;
  faturaEmFalta: boolean;
  descricao: string;
  localizacao: string | null;
  quantidadeEmStock: number;
  precoUnitarioPago: number;
  totalPago: number;
  totalSemIva: number;
  precoFinalComIva: number;
  lucro: number;
  ivaAPagarEstado: number;
}

export interface ResumoColuna {
  unidades: number;
  valorCobradoComIva: number;
  valorPecasPago: number;
  ivaDaVenda: number;
  ivaJaPagoFornecedores: number;
  ivaAPagarEstado: number;
  ivaAPagarNacionais: number;
  ivaAPagarUe: number;
  lucro: number;
}

export interface ResumoFornecedor {
  fornecedorId: string;
  nome: string;
  regimeIva: RegimeIva;
  documentos: number;
  totalPecas: number;
  ivaPecas: number;
  portes: number;
  totalGasto: number;
  faturasEmFalta: number;
}

export interface ResumoCompras {
  taxaIvaVenda: number;
  jaVendido: ResumoColuna;
  emStock: ResumoColuna;
  autoliquidacaoUeDeclarada: number;
  ivaComprasNacionais: number;
  ivaPortesNacionais: number;
  portesPagos: number;
  documentosComFaturaEmFalta: number;
  documentosComDiferenca: number;
  porFornecedor: ResumoFornecedor[];
}

export interface SimuladorRequest {
  precoPago: number;
  regime: RegimeIva;
  lucro: number;
  precoVendaComIva: number | null;
}

export interface SimuladorResponse {
  custoSemIva: number;
  ivaPagoNaCompra: number;
  autoliquidacaoUe: number;
  precoVendaSemIva: number;
  ivaDaVenda: number;
  precoFinalComIva: number;
  ivaAPagarEstado: number;
  lucroQueSobra: number;
  vendaIvaAPagar: number | null;
  vendaLucro: number | null;
}

export interface ImportComprasResultado {
  fornecedoresCriados: number;
  fornecedoresAtualizados: number;
  documentosCriados: number;
  documentosJaExistentes: number;
  linhas: number;
  avisos: string[];
}
