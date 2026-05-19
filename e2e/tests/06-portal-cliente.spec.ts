import { test, expect } from '../support/fixtures';
import { e2eEnv } from '../support/env';

test('portal publico mostra estado, fotos e garantia da reparacao', async ({ api, browser }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente Portal E2E', telefone: '918888888' });
  const reparacao = await api.createReparacao(cliente.id, {
    equipamento: 'Samsung S24 E2E',
    avaria: 'Vidro traseiro partido',
    orcamentoCents: 11900,
  });

  await api.uploadRepairPhoto(reparacao.id, 0, 'Antes E2E');
  await api.uploadRepairPhoto(reparacao.id, 2, 'Depois E2E');
  await api.changeEstado(reparacao.id, 1);
  await api.changeEstado(reparacao.id, 4);
  await api.changeEstado(reparacao.id, 5);

  const detalhe = await api.getRepair(reparacao.id);
  expect(detalhe.reparacao.publicSlug).toBeTruthy();

  const anonymous = await browser.newContext({ locale: 'pt-PT', timezoneId: 'Europe/Lisbon' });
  const portal = await anonymous.newPage();
  await portal.goto(`${e2eEnv.baseURL}/r/${detalhe.reparacao.publicSlug}`);

  await expect(portal.getByText(/Estado actual/i)).toBeVisible();
  await expect(portal.getByText(/Entregue/i).first()).toBeVisible();
  await expect(portal.getByText('Samsung S24 E2E')).toBeVisible();
  await expect(portal.getByText(/Fotos da repar/i)).toBeVisible();
  await expect(portal.getByRole('heading', { name: /Garantia digital/i })).toBeVisible();

  await anonymous.close();
});
