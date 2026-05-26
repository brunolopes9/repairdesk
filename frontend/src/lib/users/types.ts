export const APP_ROLES = ['Admin', 'Tech', 'Cashier', 'ReadOnly'] as const;
export type AppRole = (typeof APP_ROLES)[number];

export const ROLE_LABEL: Record<AppRole, string> = {
  Admin: 'Admin',
  Tech: 'Técnico',
  Cashier: 'Vendas/POS',
  ReadOnly: 'Só leitura',
};

export const ROLE_DESCRIPTION: Record<AppRole, string> = {
  Admin: 'Acesso total — fiscal, fornecedores, peças, RGPD.',
  Tech: 'Reparações, diagnóstico, peças (sem fatura).',
  Cashier: 'Vendas POS, caixa, fatura, clientes.',
  ReadOnly: 'Leitura apenas (dashboard, histórico).',
};

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
}
