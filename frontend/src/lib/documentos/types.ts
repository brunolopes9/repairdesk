// Sprint 513: lista única de documentos de venda (faturas) emitidos no Mender.
export interface DocumentoDto {
  id: string;
  origem: string; // "Venda" | "Reparacao" | "Trabalho"
  numeroInterno: number;
  tipo: string; // "Fatura" | "Fatura Simplificada" | "Nota de Crédito" | …
  tipoCodigo: string; // "FT" | "FS" | "NC" | …
  numero: string | null; // ex: "FT M/2"
  externalId: string | null;
  pdfUrl: string | null;
  provider: string;
  data: string;
  clienteId: string | null;
  clienteNome: string | null;
  clienteNif: string | null;
  totalCents: number;
  ivaCents: number;
  baseCents: number;
  estado: string; // "Ativo"
  // Sprint 528: se preenchido, a fatura está liquidada por este recibo → esconde "Emitir recibo".
  reciboNumero: string | null;
  reciboEmitidoEm: string | null;
}

export interface DocumentosListDto {
  items: DocumentoDto[];
  totalDocumentos: number;
  totalCents: number;
  totalIvaCents: number;
  totalBaseCents: number;
}
