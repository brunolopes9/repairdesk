import { expect, type Page } from '@playwright/test';
import { e2eEnv } from './env';

export async function loginViaUi(page: Page): Promise<void> {
  await page.addInitScript(() => {
    window.localStorage.setItem('rd.cookie.ack', 'e2e');
  });
  await page.goto('/login');
  await dismissCookieBanner(page);
  await page.getByLabel('Email').fill(e2eEnv.adminEmail);
  await page.getByRole('textbox', { name: /^Password$/ }).fill(e2eEnv.adminPassword);
  await page.getByRole('button', { name: /^Entrar$/ }).click();
  await expect(page).not.toHaveURL(/\/login$/);
  await dismissCookieBanner(page);
}

export async function openAppPage(page: Page, path: string): Promise<void> {
  await loginViaUi(page);
  await page.goto(path);
  await dismissCookieBanner(page);
}

export async function dismissCookieBanner(page: Page): Promise<void> {
  const accept = page.getByRole('button', { name: /^Percebi$/ });
  if (await accept.isVisible().catch(() => false)) {
    await accept.click();
  }
}
