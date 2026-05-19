export type RegimeFiscal = 0 | 1 | 2;
export type BillingProvider = 0 | 1 | 2;
export type BillingDocumentType = 0 | 1;

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
  garantiaVendaCoberturaDefault: string | null;
  garantiaVendaExclusoesDefault: string | null;
  googleReviewUrl: string | null;
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

export interface TenantBillingSettings {
  provider: BillingProvider;
  hasApiKey: boolean;
  apiKeyMasked: string | null;
  clientId: string | null;
  hasClientSecret: boolean;
  hasRefreshToken: boolean;
  companyId: number | null;
  defaultDocumentType: BillingDocumentType;
  defaultSerieId: number | null;
  sandboxMode: boolean;
  defaultProductId: number | null;
  defaultTaxId: number | null;
  defaultPaymentMethodId: number | null;
  defaultMaturityDateId: number | null;
  fallbackCustomerId: number | null;
  exemptionReason: string | null;
}

export type UpdateTenantBillingSettings = TenantBillingSettings & {
  apiKey: string | null;
  clientSecret: string | null;
  refreshToken: string | null;
};

export interface BillingConnectionTest {
  success: boolean;
  message: string;
}

export interface MoloniOAuthStart {
  authorizationUrl: string;
  expiresAt: string;
}

export interface MoloniAutoDiscoverStep {
  key: string;
  label: string;
  success: boolean;
  created: boolean;
  id: number | null;
  name: string | null;
  message: string | null;
}

export interface MoloniAutoDiscoverResult {
  productsFound: number;
  taxesFound: number;
  paymentMethodsFound: number;
  maturityDatesFound: number;
  customersFound: number;
  steps: MoloniAutoDiscoverStep[];
  settings: TenantBillingSettings;
}

export interface BillingSerie {
  id: number;
  name: string;
  code: string | null;
  isActive: boolean;
}
