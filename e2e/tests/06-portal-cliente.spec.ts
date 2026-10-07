import { test, expect } from '../support/fixtures';
import { VENDA_ESTADO } from '../support/api';
import { e2eEnv } from '../support/env';

test('portal publico mostra estado, fotos e garantia da reparacao', async ({ api, browser }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente Portal E2E', telefone: '918888888' });
  const reparacao = await api.createReparacao(cliente.id, 'Samsung S24 E2E', 'Vidro traseiro partido', 11900);
  expect(reparacao.publicSlug).toBeTruthy();

  await api.uploadRepairPhoto(reparacao.id, 0, 'Antes E2E');
  await api.uploadRepairPhoto(reparacao.id, 2, 'Depois E2E');
  await api.mudarEstado(reparacao.id, VENDA_ESTADO.EmCurso);
  await api.mudarEstado(reparacao.id, VENDA_ESTADO.Pronta);
  await api.mudarEstado(reparacao.id, VENDA_ESTADO.Entregue, 2);

  const anonymous = await browser.newContext({ locale: 'pt-PT', timezoneId: 'Europe/Lisbon' });
  const portal = await anonymous.newPage();
  await portal.goto(`${e2eEnv.baseURL}/r/${reparacao.publicSlug}`);

  await expect(portal.getByText(/Estado actual/i)).toBeVisible();
  await expect(portal.getByText(/Entregue/i).first()).toBeVisible();
  await expect(portal.getByText('Samsung S24 E2E')).toBeVisible();
  await expect(portal.getByText(/Fotos da repar/i)).toBeVisible();
  await expect(portal.getByRole('heading', { name: /Garantia digital/i })).toBeVisible();

  await anonymous.close();
});
