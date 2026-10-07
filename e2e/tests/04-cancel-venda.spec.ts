import { test, expect } from '../support/fixtures';
import { VENDA_ESTADO, VENDA_TIPO } from '../support/api';
import { openAppPage } from '../support/ui';

test('cancelar venda entregue repoe stock e marca Cancelada', async ({ api, page }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente E2E Cancelar Venda' });
  const lote = await api.createLote('E2E Bateria Cancelamento', 4, 24.9);
  const venda = await api.createVenda({
    tipo: VENDA_TIPO.Produto,
    clienteId: cliente.id,
    equipamento: null,
    problema: null,
    notas: 'E2E venda a cancelar',
    linhas: [{ id: null, compraLinhaId: lote, descricao: null, quantidade: 1, precoUnitarioCents: 3990 }],
    estado: VENDA_ESTADO.Entregue,
    paymentMethod: 2,
  });
  expect(await api.stockDoLote(lote)).toBe(3);

  const cancelada = await api.mudarEstado(venda.id, VENDA_ESTADO.Cancelada);
  expect(cancelada.estado).toBe(VENDA_ESTADO.Cancelada);
  expect(await api.stockDoLote(lote)).toBe(4);

  await openAppPage(page, '/vendas?vista=todas');
  await expect(page.getByText('Cliente E2E Cancelar Venda')).toBeVisible();
  await expect(page.getByText(/Cancelada/i).first()).toBeVisible();
});
