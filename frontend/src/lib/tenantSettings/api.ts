import { api } from '../api';
import type { OnboardingStatus, TenantSettings, UpdateTenantSettings } from './types';

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
};
