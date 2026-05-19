import { test, expect } from '../support/fixtures';
import { loginViaUi } from '../support/ui';

test('onboarding cria a primeira ficha sem chamada com o Bruno', async ({ api, page }) => {
  await loginViaUi(page);
  await page.goto('/bemvindo');

  await page.getByLabel(/Nome da loja/i).fill('Oficina E2E Beta');
  await page.getByLabel(/^NIF$/i).fill('263758141');
  await page.getByRole('button', { name: /Guardar e continuar/i }).click();

  await page.getByLabel(/Nome \*/i).fill('Cliente E2E Onboarding');
  await page.getByLabel(/Telefone/i).fill('912345678');
  await page.getByRole('button', { name: /Criar cliente e continuar/i }).click();

  await page.getByRole('button', { name: /Usar reparacao demo|Usar reparação demo|Saltar por agora/i }).click();
  await page.getByRole('button', { name: /^Continuar$/i }).click();
  await page.getByRole('button', { name: /Terminar arranque/i }).click();

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole('heading', { name: /Dashboard/i })).toBeVisible();
});
