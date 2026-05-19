import { test as base, expect } from '@playwright/test';
import { RepairDeskApi } from './api';

export const test = base.extend<{ api: RepairDeskApi }>({
  api: async ({ request }, use) => {
    const api = new RepairDeskApi(request);
    await api.reset();
    await api.login();
    await use(api);
  },
});

export { expect };
