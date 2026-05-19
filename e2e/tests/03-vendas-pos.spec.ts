import { test, expect } from '../support/fixtures';
import { openAppPage } from '../support/ui';

test('POS vende dois artigos com MBWay e decrementa stock', async ({ api, page }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente E2E POS', telefone: '919999999' });
  const cabo = await api.createPart({ nome: 'E2E Cabo USB-C', sku: 'E2E-CABO-USBC', qtdStock: 5, custoUnitarioCents: 990 });
  const pelicula = await api.createPart({ nome: 'E2E Pelicula iPhone', sku: 'E2E-PELICULA', qtdStock: 3, custoUnitarioCents: 1290 });

  await openAppPage(page, '/vendas');
  await page.getByRole('button', { name: /E2E Cabo USB-C/i }).click();
  await page.getByRole('button', { name: /E2E Pelicula iPhone/i }).click();
  await page.getByPlaceholder(/Anonimo|pesquisar cliente/i).fill(cliente.nome);
  await page.getByRole('button', { name: /Cliente E2E POS/i }).click();
  await page.getByRole('button', { name: /Cobrar/i }).click();

  await expect(page.getByText(/Venda #\d+ paga/i)).toBeVisible();

  const caboDepois = await api.getPart(cabo.id);
  const peliculaDepois = await api.getPart(pelicula.id);
  expect(caboDepois.qtdStock).toBe(4);
  expect(peliculaDepois.qtdStock).toBe(2);

  await page.reload();
  await expect(page.getByText('Cliente E2E POS')).toBeVisible();
  await expect(page.getByText(/Paga/i).first()).toBeVisible();
});
