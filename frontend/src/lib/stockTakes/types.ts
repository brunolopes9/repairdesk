// Sprint 421: tipos do inventário físico (StockTake).

export enum StockTakeStatus {
  Aberto = 0,
  Concluido = 1,
  Cancelado = 2,
}

export interface StockTakeItem {
  id: string;
  partId: string;
  partNome: string;
  partSku: string | null;
  partMarca: string | null;
  partModelo: string | null;
  localArmazenamento: string | null;
  qtdSistema: number;
  qtdContada: number | null;
  diferenca: number;
  contadoEm: string | null;
}

export interface StockTake {
  id: string;
  openedAt: string;
  openedByUserId: string;
  closedAt: string | null;
  closedByUserId: string | null;
  status: StockTakeStatus;
  notas: string | null;
  totalItems: number;
  contadosCount: number;
  diferencasCount: number;
  items: StockTakeItem[] | null;
}
