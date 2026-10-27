export interface ClienteTag {
  id: string;
  nome: string;
  corHex: string;
}

export interface Cliente {
  id: string;
  nome: string;
  telefone: string | null;
  email: string | null;
  nif: string | null;
  notas: string | null;
  createdAt: string;
  updatedAt: string | null;
  /** Sprint 355: alerta curto destacado. */
  notaImportante?: string | null;
  contactoPreferido?: 'Telefone' | 'WhatsApp' | 'Email' | 'Sms' | null;
  aceitaMarketing?: boolean;
  naoContactar?: boolean;
  /** Sprint 510: morada fiscal — usada na fatura. */
  morada?: string | null;
  codigoPostal?: string | null;
  localidade?: string | null;
  /** Sprint 571: nome que vai na fatura quando é diferente do nome interno. */
  nomeFaturacao?: string | null;
  tags?: ClienteTag[];
}

export interface ClienteCampanhaSegmento {
  tagIds: string[];
  totalSegmento: number;
  totalElegiveis: number;
  clientes: Cliente[];
}

export interface ClienteForm {
  nome: string;
  telefone: string | null;
  email: string | null;
  nif: string | null;
  notas: string | null;
  notaImportante?: string | null;
  contactoPreferido?: 'Telefone' | 'WhatsApp' | 'Email' | 'Sms' | null;
  aceitaMarketing?: boolean;
  naoContactar?: boolean;
  morada?: string | null;
  codigoPostal?: string | null;
  localidade?: string | null;
  nomeFaturacao?: string | null;
}

export interface ClienteEquipamento {
  nome: string;
  imei: string | null;
  primeiroRegistoEm: string;
  ultimoRegistoEm: string;
  reparacoesCount: number;
  vendasCount: number;
  ultimaReparacaoId: string | null;
  ultimaReparacaoNumero: number | null;
  ultimaVendaId: string | null;
  ultimaVendaNumero: number | null;
}

export interface AtNifLookup {
  nif: string;
  nome: string;
  morada: string | null;
  status: string;
  checkedAtUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
