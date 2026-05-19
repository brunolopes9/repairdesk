function trimRightSlash(value: string): string {
  return value.replace(/\/+$/, '');
}

export const e2eEnv = {
  baseURL: trimRightSlash(process.env.E2E_BASE_URL ?? 'http://localhost'),
  apiURL: trimRightSlash(process.env.E2E_API_URL ?? 'http://localhost:5080/api'),
  adminEmail: process.env.E2E_ADMIN_EMAIL ?? 'bruno.miguel.martins.lopes@gmail.com',
  adminPassword: process.env.E2E_ADMIN_PASSWORD ?? 'ChangeMe!2026',
  resetKey: process.env.E2E_API_KEY ?? '',
};
