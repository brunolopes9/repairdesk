export interface Cliente {
  id: string;
  nome: string;
  telefone: string | null;
  email: string | null;
  nif: string | null;
  notas: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ClienteForm {
  nome: string;
  telefone: string | null;
  email: string | null;
  nif: string | null;
  notas: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
