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
}

export interface ClienteForm {
  nome: string;
  telefone: string | null;
  email: string | null;
  nif: string | null;
  notas: string | null;
  notaImportante?: string | null;
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
