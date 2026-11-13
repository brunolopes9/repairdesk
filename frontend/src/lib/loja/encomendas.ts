import { api } from '../api';

// Sprint 656: encomendas da loja online (lopestech.pt/loja) — pagas pelo Stripe, preparadas e enviadas pelo dono.

export const EncomendaEstado = {
  AguardaPagamento: 0, Paga: 1, EmPreparacao: 2, Enviada: 3, Entregue: 4, Cancelada: 5, Expirada: 6,
} as const;
export type EncomendaEstado = (typeof EncomendaEstado)[keyof typeof EncomendaEstado];

export const ENCOMENDA_ESTADO_LABEL: Record<EncomendaEstado, string> = {
  0: 'A aguardar pagamento', 1: 'Paga — preparar', 2: 'Em preparação', 3: 'Enviada', 4: 'Entregue', 5: 'Cancelada', 6: 'Não pagou',
};

export interface EncomendaLinha {
  produtoLojaId: string;
  artigoId: string | null;
  slug: string;
  titulo: string;
  quantidade: number;
  precoUnitario: number;
}

export interface Encomenda {
  id: string;
  numero: number;
  referencia: string;
  estado: EncomendaEstado;
  criadaEm: string;
  nome: string;
  email: string;
  telefone: string;
  nif: string | null;
  morada: string;
  codigoPostal: string;
  localidade: string;
  notas: string | null;
  transportadora: string;
  subtotal: number;
  portes: number;
  total: number;
  pagaEm: string | null;
  enviadaEm: string | null;
  entregueEm: string | null;
  canceladaEm: string | null;
  numeroSeguimento: string | null;
  vendaId: string | null;
  problema: string | null;
  linhas: EncomendaLinha[];
}

export const encomendasApi = {
  list: () => api.get<Encomenda[]>('/loja/encomendas').then((r) => r.data),
  mudarEstado: (id: string, estado: EncomendaEstado, numeroSeguimento?: string | null) =>
    api.put<Encomenda>(`/loja/encomendas/${id}/estado`, { estado, numeroSeguimento: numeroSeguimento ?? null }).then((r) => r.data),
};
