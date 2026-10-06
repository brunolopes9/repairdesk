export type RegimeFiscal = 0 | 1 | 2;

export const REGIME_FISCAL_LABELS: Record<RegimeFiscal, string> = {
  0: 'Isenção Art. 53',
  1: 'Regime Normal IVA',
  2: 'Regime Simplificado',
};

export interface TenantSettings {
  id: string;
  name: string;
  legalName: string | null;
  nif: string | null;
  address: string | null;
  postalCode: string | null;
  locality: string | null;
  country: string | null;
  phone: string | null;
  email: string | null;
  website: string | null;
  iban: string | null;
  caePrincipal: string | null;
  caeSecundarios: string | null;
  regimeFiscal: RegimeFiscal;
  termosCondicoes: string | null;
  logoUrl: string | null;
  primaryColor: string | null;
  onboardingCompletado: boolean;
  garantiaDiasDefault: number;
  garantiaCoberturaDefault: string | null;
  garantiaExclusoesDefault: string | null;
  garantiaVendaDiasDefault: number;
  // Sprint 127: granularidade DL 84/2021 por condição. Novo herda do default flat acima.
  garantiaVendaOpenBoxDias: number;
  garantiaVendaRecondicionadoDias: number;
  garantiaVendaUsadoDias: number;
  garantiaVendaCoberturaDefault: string | null;
  garantiaVendaExclusoesDefault: string | null;
  googleReviewUrl: string | null;
  // Sprint 175b: retention policy (dias). NULL = nunca apaga.
  retentionRejectedDays: number | null;
  retentionFailedDays: number | null;
  retentionApprovedPdfDays: number | null;
}

export interface OnboardingStatus {
  onboardingCompletado: boolean;
  empresaCompleta: boolean;
  clienteCriado: boolean;
  reparacaoCriada: boolean;
  dashboardVisto: boolean;
  equipaConvidada: boolean;
  currentStep: number;
  totalSteps: number;
}

export type UpdateTenantSettings = Omit<TenantSettings, 'id' | 'onboardingCompletado'>;
