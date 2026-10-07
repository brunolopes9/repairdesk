import { test, expect } from '../support/fixtures';
import { VENDA_ESTADO } from '../support/api';
import { openAppPage } from '../support/ui';

test('reparacao passa o ciclo completo ate entregue e paga', async ({ api, page }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente E2E Reparacao', nif: '263758141' });
  const reparacao = await api.createReparacao(cliente.id, 'iPhone 15 Pro E2E', 'Display partido', 15900);

  for (const estado of [VENDA_ESTADO.EmCurso, VENDA_ESTADO.Pronta]) {
    await api.mudarEstado(reparacao.id, estado);
  }
  const entregue = await api.mudarEstado(reparacao.id, VENDA_ESTADO.Entregue, 2);
  expect(entregue.estado).toBe(VENDA_ESTADO.Entregue);
  expect(entregue.garantiaSlug).toBeTruthy();

  await openAppPage(page, `/vendas/${reparacao.id}`);
  await expect(page.getByRole('heading', { name: new RegExp(`Reparação #${reparacao.numero}`) })).toBeVisible();
  await expect(page.getByText(/Falta registar o nº da fatura/i)).toBeVisible();
});
