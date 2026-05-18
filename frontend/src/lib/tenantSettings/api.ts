import { api } from '../api';
import type {
  BillingConnectionTest,
  BillingSerie,
  OnboardingStatus,
  TenantBillingSettings,
  TenantSettings,
  UpdateTenantBillingSettings,
  UpdateTenantSettings,
} from './types';

export const tenantSettingsApi = {
  getMine() {
    return api.get<TenantSettings>('/tenant-settings/me').then((r) => r.data);
  },
  updateMine(payload: UpdateTenantSettings) {
    return api.put<TenantSettings>('/tenant-settings/me', payload).then((r) => r.data);
  },
  onboardingStatus() {
    return api.get<OnboardingStatus>('/tenant-settings/me/onboarding/status').then((r) => r.data);
  },
  completeOnboarding() {
    return api.post<OnboardingStatus>('/tenant-settings/me/onboarding/complete').then((r) => r.data);
  },
  getBilling() {
    return api.get<TenantBillingSettings>('/tenant-settings/me/billing').then((r) => r.data);
  },
  updateBilling(payload: UpdateTenantBillingSettings) {
    return api.put<TenantBillingSettings>('/tenant-settings/me/billing', payload).then((r) => r.data);
  },
  testBillingConnection() {
    return api.post<BillingConnectionTest>('/tenant-settings/me/billing/test-connection').then((r) => r.data);
  },
  syncBillingSeries() {
    return api.post<BillingSerie[]>('/tenant-settings/me/billing/sync-series').then((r) => r.data);
  },
};
