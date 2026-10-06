import { test, expect } from '../support/fixtures';
import { openAppPage } from '../support/ui';

test('cancelar venda paga repoe stock e marca Cancelada', async ({ api, page }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente E2E Cancelar Venda' });
  const part = await api.createPart({ nome: 'E2E Bateria Cancelamento', sku: 'E2E-BAT-CANCEL', qtdStock: 4, custoUnitarioCents: 2490 });
  const venda = await api.createVenda({
    clienteId: cliente.id,
    notas: 'E2E venda a cancelar',
    items: [
      {
        partId: part.id,
        descricao: part.nome,
        quantidade: 1,
        precoUnitarioCents: 2490,
        descontoCents: 0,
        ivaRate: 23,
      },
    ],
  });

  const paga = await api.payVenda(venda.id, 2);
  expect(paga.status).toBe(1);

  const cancelada = await api.cancelVenda(venda.id);
  expect(cancelada.status).toBe(2);

  const partDepois = await api.getPart(part.id);
  expect(partDepois.qtdStock).toBe(4);

  await openAppPage(page, '/vendas');
  await expect(page.getByText('Cliente E2E Cancelar Venda')).toBeVisible();
  await expect(page.getByText(/Cancelada/i)).toBeVisible();
});
