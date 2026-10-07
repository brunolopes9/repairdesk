import { test, expect } from '../support/fixtures';
import { VENDA_ESTADO, VENDA_TIPO } from '../support/api';
import { openAppPage } from '../support/ui';

test('venda de produto entregue tira unidades do lote de stock', async ({ api, page }) => {
  await api.completeOnboarding();

  const cliente = await api.createCliente({ nome: 'Cliente E2E Venda', telefone: '919999999' });
  const lote = await api.createLote('E2E Pelicula iPhone', 3, 1.29);

  await api.createVenda({
    tipo: VENDA_TIPO.Produto,
    clienteId: cliente.id,
    equipamento: null,
    problema: null,
    notas: null,
    linhas: [{ id: null, compraLinhaId: lote, descricao: null, quantidade: 2, precoUnitarioCents: 1000 }],
    estado: VENDA_ESTADO.Entregue,
    paymentMethod: 2,
  });

  expect(await api.stockDoLote(lote)).toBe(1);

  await openAppPage(page, '/vendas?vista=entregues');
  await expect(page.getByText('Cliente E2E Venda')).toBeVisible();
});
