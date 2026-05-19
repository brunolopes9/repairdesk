import { test, expect } from '../support/fixtures';
import { openAppPage } from '../support/ui';

test('bulk emit emite tres reparacoes pagas sem fatura a partir do chip', async ({ api, page }) => {
  await api.completeOnboarding();
  await api.configureBilling();

  const cliente = await api.createCliente({ nome: 'Cliente E2E Bulk' });
  const ids: string[] = [];
  for (let i = 1; i <= 3; i += 1) {
    const reparacao = await api.createReparacao(cliente.id, {
      equipamento: `E2E Bulk Equipamento ${i}`,
      avaria: 'Teste bulk faturacao',
      orcamentoCents: 5000 + i * 1000,
    });
    await api.changeEstado(reparacao.id, 1);
    await api.changeEstado(reparacao.id, 4);
    await api.changeEstado(reparacao.id, 5);
    ids.push(reparacao.id);
  }

  expect(await api.listReparacoesPagasSemFatura()).toHaveLength(3);

  await openAppPage(page, '/reparacoes');
  await page.getByRole('button', { name: /3 pendentes fatura/i }).click();
  await page.getByLabel(/Seleccionar todas/i).check();

  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: /Emitir 3 faturas/i }).click();

  await expect(page.getByText(/Nenhuma repar.*pendente de fatura/i)).toBeVisible();
  expect(await api.listReparacoesPagasSemFatura()).toHaveLength(0);
});
