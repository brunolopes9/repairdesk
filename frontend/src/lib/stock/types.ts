import type { PagedResult } from '../clientes/types';

export const PART_CATEGORIA = {
  Ecra: 0,
  Bateria: 1,
  Conector: 2,
  Camara: 3,
  VidroTraseiro: 4,
  CaboFlex: 5,
  Tampa: 6,
  Adesivo: 7,
  Consumivel: 8,
  Smartphone: 9,
  Tablet: 10,
  Acessorio: 11,
  Outro: 99,
} as const;

export type PartCategoria = (typeof PART_CATEGORIA)[keyof typeof PART_CATEGORIA];

export const PART_CATEGORIA_LABEL: Record<PartCategoria, string> = {
  0: 'Ecrã',
  1: 'Bateria',
  2: 'Conector',
  3: 'Câmara',
  4: 'Vidro traseiro',
  5: 'Cabo flex',
  6: 'Tampa',
  7: 'Adesivo',
  8: 'Consumível',
  9: 'Smartphone',
  10: 'Tablet',
  11: 'Acessório',
  99: 'Outro',
};

/// Categorias onde IMEI é obrigatório quando vendido (Sprint 59).
export const PART_CATEGORIA_REQUER_IMEI: ReadonlySet<PartCategoria> = new Set([
  PART_CATEGORIA.Smartphone,
  PART_CATEGORIA.Tablet,
]);

export const PART_MOVIMENTO_MOTIVO = {
  Entrada: 0,
  Saida: 1,
  AjusteManual: 2,
  UsoEmReparacao: 3,
  Devolucao: 4,
  VendaCliente: 5,
} as const;

export type PartMovimentoMotivo = (typeof PART_MOVIMENTO_MOTIVO)[keyof typeof PART_MOVIMENTO_MOTIVO];

export const PART_MOVIMENTO_LABEL: Record<number, string> = {
  0: 'Entrada',
  1: 'Saída',
  2: 'Ajuste manual',
  3: 'Uso em reparação',
  4: 'Devolução',
};

export interface Part {
  id: string;
  sku: string | null;
  nome: string;
  categoria: PartCategoria;
  marca: string | null;
  modelo: string | null;
  priceTableEntryId: string | null;
  qtdStock: number;
  qtdMinima: number;
  custoUnitarioCents: number;
  valorTotalStockCents: number;
  fornecedor: string | null;
  localArmazenamento: string | null;
  notas: string | null;
  activo: boolean;
  stockBaixo: boolean;
  createdAt: string;
  updatedAt: string | null;
  /** Sprint 121: quando true, esta peça aparece no catálogo /api/external/parts?lojaOnline=true. */
  mostrarLojaOnline: boolean;
}

export interface PartForm {
  sku: string | null;
  nome: string;
  categoria: PartCategoria;
  marca: string | null;
  modelo: string | null;
  priceTableEntryId: string | null;
  qtdStock: number;
  qtdMinima: number;
  custoUnitarioCents: number;
  fornecedor: string | null;
  localArmazenamento: string | null;
  notas: string | null;
  mostrarLojaOnline: boolean;
}

export interface PartUpdateForm extends PartForm {
  activo: boolean;
}

export interface PartMovimento {
  id: string;
  partId: string;
  partNome: string;
  partSku: string | null;
  quantidade: number;
  stockAntes: number;
  stockDepois: number;
  motivo: PartMovimentoMotivo;
  reparacaoId: string | null;
  notas: string | null;
  createdAt: string;
  // Sprint 177: custo unitário snapshot da peça para UI mostrar preço.
  custoUnitarioCents: number;
}

export interface CreatePartMovimentoForm {
  quantidade: number;
  motivo: PartMovimentoMotivo;
  reparacaoId: string | null;
  notas: string | null;
}

export interface ImportPartsResponse {
  totalLinhas: number;
  criadas: number;
  ignoradas: number;
  comErro: number;
  pecasCriadas: Part[];
  erros: Array<{ linha: number; campo: string; mensagem: string; valorOriginal: string | null }>;
}

export type PartsPage = PagedResult<Part>;

/** Sprint 186: previsão reabastecer — Part em risco de ruptura no período. */
export interface ReabastecerSugestao {
  partId: string;
  sku: string;
  nome: string;
  qtdStockActual: number;
  consumoDias: number;
  diasRestantesEstimados: number;
  custoUnitarioCents: number;
}
