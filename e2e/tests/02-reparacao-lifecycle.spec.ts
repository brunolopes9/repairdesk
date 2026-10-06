import { test, expect } from '../support/fixtures';
import { openAppPage } from '../support/ui';

test('reparacao passa o ciclo completo ate entregue e paga', async ({ api, page }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente E2E Reparacao', nif: '263758141' });
  const reparacao = await api.createReparacao(cliente.id, {
    equipamento: 'iPhone 15 Pro E2E',
    avaria: 'Display partido',
    orcamentoCents: 15900,
  });

  for (const estado of [1, 2, 3, 4, 5]) {
    await api.changeEstado(reparacao.id, estado, `E2E estado ${estado}`);
  }

  const detalhe = await api.getRepair(reparacao.id);
  expect(detalhe.reparacao.estado).toBe(5);
  expect(detalhe.reparacao.estadoPagamento).toBe(2);

  await openAppPage(page, `/reparacoes/${reparacao.id}`);
  await expect(page.getByRole('heading', { name: 'iPhone 15 Pro E2E' })).toBeVisible();
});
