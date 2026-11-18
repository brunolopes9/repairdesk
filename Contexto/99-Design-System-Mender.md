# 99 — Design system do Mender (upgrade visual, 18/11/2026)

**Pedido do Bruno:** o Mender está «bué branco e cinzento, feio, difícil de mexer». Quer um grande upgrade visual, com uma arquitetura definida e trabalho em direção a ela.

Acordado com o Claude «MyFxBook e desempenho 2026». Ele só mexe no Mender se o Bruno lhe disser diretamente.

## Regras
- **Mudanças pequenas.** Um ecrã por mudança, com captura antes/depois a 1366 px (portátil) e a 390 px (telemóvel), para o Bruno aprovar. Nada de redesenhar tudo de uma vez.
- **Tokens semânticos sempre.** Nunca cores Tailwind cruas nos ecrãs. Isto dá o modo escuro de graça mais tarde.

## Tokens (frontend/src/index.css)
- **Marca `#1366E0`:** só em ações primárias e na navegação ativa. Não espalhar.
- **Superfícies:**
  - fundo da página `#F4F6FA` (slate-50);
  - cartões brancos com borda de 1 px `#E3E8F0` e sombra suave;
  - cabeçalhos de secção com a marca a 6–8 % de alfa.
- **Estado:** `--ok`, `--warn`, `--danger` e `--info`, cada um com bg, fg e border em 3 níveis.
  - vendido / acabou = verde ou vermelho suave;
  - pendente / parcial = âmbar.

## Tabelas (o coração de um POS/stock)
- Linha com 44–48 px de altura.
- Números alinhados à direita, com `tabular-nums`.
- Cabeçalho fixo e primeira coluna fixa.
- Faixa de estado de 4 px à esquerda, como no Excel do Bruno, **mais** um badge com texto (só a cor não é acessível).
- Zebra a 2–3 %, destaque da linha ao passar o rato.
- Ações da linha a aparecer ao passar o rato, em vez de uma coluna de botões.

## Tipografia
- Inter ou a fonte do sistema.
- 14 px nas tabelas.
- No máximo 3 tamanhos por ecrã.
- Totais e stock a negrito, com tabular-nums.

## Componentes base (primeiro)
- **SectionCard:** cabeçalho com tom e ícone.
- **Tabela base:** com faixa de estado.
- **Button:** hierarquia clara entre primário, secundário e perigo.
- **EstadoBadge:** um só componente para todos os estados.

## Ordem dos ecrãs
1. Vendas / Balcão
2. Compras / Stock
3. Artigos, com 1 foto por artigo:
   - puxada automaticamente do fornecedor;
   - guardada no R2 (sem hotlink);
   - com placeholder e lazy load.
4. Dashboard:
   - 4 KPIs: vendas de hoje, este mês contra o anterior, stock baixo, reparações pendentes;
   - barras dos 30 dias;
   - listas «a acabar stock» e «reparações paradas há mais de 3 dias»;
   - cada tile abre o ecrã já filtrado.

## Já feito (18/11)
- Faixa de cor nas linhas vendidas ou parciais (Compras).
- Badge «0 · acabou» no Stock.
