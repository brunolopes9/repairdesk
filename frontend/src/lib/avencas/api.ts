import { api } from '../api';

/** Sprint 546 (Doc 93 #1): avenças — faturação recorrente a clientes. */
export interface Avenca {
  id: string;
  clienteId: string;
  clienteNome: string | null;
  descricao: string;
  valorCents: number;
  ivaRate: number;
  categoria: number; // JobCategory
  periodicidadeMeses: number;
  proximaEmissao: string;
  ativa: boolean;
  notas: string | null;
  ultimaEmissaoEm: string | null;
  ultimoTrabalhoId: string | null;
  /** true quando a próxima emissão já está devida e a avença está ativa. */
  devida: boolean;
}

export interface SaveAvencaForm {
  clienteId: string;
  descricao: string;
  valorCents: number;
  ivaRate: number;
  categoria: number;
  periodicidadeMeses: number;
  proximaEmissao: string; // yyyy-MM-dd
  ativa: boolean;
  notas?: string | null;
}

export interface AvencaEmissaoResult {
  avenca: Avenca;
  trabalhoId: string;
  invoiceNumber: string | null;
}

export const avencasApi = {
  list(clienteId?: string) {
    return api
      .get<Avenca[]>('/avencas', { params: { clienteId: clienteId || undefined } })
      .then((r) => r.data);
  },
  create(form: SaveAvencaForm) {
    return api.post<Avenca>('/avencas', form).then((r) => r.data);
  },
  update(id: string, form: SaveAvencaForm) {
    return api.put<Avenca>(`/avencas/${id}`, form).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/avencas/${id}`).then(() => undefined);
  },
  /** Cria o Trabalho do período + emite a Fatura (FT) Moloni — o "1 clique". */
  emitir(id: string) {
    return api.post<AvencaEmissaoResult>(`/avencas/${id}/emitir`).then((r) => r.data);
  },
};
