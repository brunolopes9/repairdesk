import { request, type APIRequestContext } from '@playwright/test';
import { e2eEnv } from './env';

async function waitForOk(api: APIRequestContext, url: string, timeoutMs: number): Promise<void> {
  const started = Date.now();
  let lastError = '';

  while (Date.now() - started < timeoutMs) {
    try {
      const response = await api.get(url, { timeout: 5_000 });
      if (response.ok()) return;
      lastError = `${response.status()} ${await response.text()}`;
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
    }

    await new Promise((resolve) => setTimeout(resolve, 2_000));
  }

  throw new Error(`Timed out waiting for ${url}. Last error: ${lastError}`);
}

export default async function globalSetup(): Promise<void> {
  const api = await request.newContext();
  try {
    await waitForOk(api, `${e2eEnv.apiURL}/health/live`, 120_000);
    await waitForOk(api, `${e2eEnv.apiURL}/health/ready`, 180_000);
    await waitForOk(api, e2eEnv.baseURL, 120_000);
  } finally {
    await api.dispose();
  }
}
