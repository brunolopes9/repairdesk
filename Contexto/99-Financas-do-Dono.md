# 99 — Finanças do dono: o Excel «Gestão Financeira» no Mender (12/11/2026)

Pedido do Bruno a 12/11: «quero deixar de usar o Excel; tudo digital no Mender». A base é a especificação `Contexto/specs/SPEC_gestao_financeira.md` (folhas Painel, Informática, Trading, Camisolas, Despesas, Fixas, Resumo Mensal). Os ficheiros reais estão em `Documents/Gestão Financeira/{Setembro,Outubro,Novembro}`.

## Decisões (com o Claude fiscal, 12/11)
- **Âmbito da despesa:**
  - Negócio → entra no IVA e no IRS conforme a afetação: LopesTech = Informática, Trading, Mista = «Geral» (Claude, Captions, MEO).
  - Pessoal → fora de tudo o que é fiscal. Só o Admin vê.
  - Obrigação → Segurança Social, conta no saldo. Pagamentos de IVA/IRS são tesouraria: mostram-se, mas ficam fora do saldo, para não contar o IVA duas vezes.
- **Natureza:** Gasto ou Investimento. Conta igual no resultado, mas o Painel mostra-as em separado.
- **Informática** = vendas entregues da loja, com ou sem fatura (inclui o «registo interno»). Lucro pelo motor de IVA.
- **Trading** = payouts (vendas com Atividade Trading).
- **Camisolas: EM ESPERA.** O Claude fiscal fez ao Bruno perguntas de enquadramento: CAE de comércio, fatura e IVA a 23%; se forem réplicas, não há enquadramento. Não se modela «sem fatura» para camisolas.
- **Objetivo mensal:** 1 500 € por omissão, editável (`Tenant.ObjetivoMensal`).

## Feito — Sprint 633
- **Despesa:** `Ambito` e `Natureza`, e as categorias do Excel: Ferramentas IA, Subscrições, VPS/Copier, Prop firms, Alimentação, Café/Restaurante, Casa, Ginásio, Suplementação, Lazer, Saúde, Vestuário, Segurança Social, IVA pago, IRS pago.
- **Fiscal:** o apuramento e as estimativas de IRS só usam despesas do âmbito Negócio. O Painel da loja também.
- **Privacidade:** em `GET api/despesas`, quem não é Admin só vê o Negócio.
- **`/financas` (Admin):**
  - saldo do mês face ao objetivo;
  - lucro por negócio e despesas por âmbito/afetação;
  - resultado por negócio;
  - atividade: vendas, faturado, margem, ticket, gasto/investimento;
  - maiores despesas;
  - fixas OK / EM FALTA (a partir das despesas recorrentes dos últimos 4 meses);
  - resumo do ano com gráfico e tabela.

## Por fazer
1. **Importar o histórico do Excel** (setembro a novembro): despesas com «Pertence a», trabalhos de Informática como vendas «sem fatura (registo interno)» marcadas como históricas, payouts e fixas.
2. **Cobranças** (recebido / por receber), quando a venda tiver «pago» explícito.
3. **Verificações de dados** (§5 da especificação).
4. **Camisolas**, depois do enquadramento fiscal.
