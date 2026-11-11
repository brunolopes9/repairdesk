# 97 — Loja no lopestech.pt (arquitetura acordada, 11/11/2026)

Proposta do Claude do Mender, revista pelo Claude da Cortex. Consenso fechado. **Decisões do Bruno pendentes no fim.**

## 1. O que é (e o que não é)
- **É** uma secção nova do lopestech.pt, em `/loja`. O site atual (serviços, reparações, websites, blog) fica igual, sem redesign.
- **Não é** o lopestech-shop (Next.js + Neon, de maio). Esse punha a loja no centro e tinha uma 2.ª base de dados. Serve só de referência de UI: escala de estados, «porque este preço?», blocos de confiança e guias.
- Dois tipos de produto:
  1. **Do fornecedor**: sem stock físico. O Bruno recebe a lista (specs, estado, garantia e preço de custo), põe a margem e publica como «disponível por encomenda», com entrega no próprio dia ou no seguinte.
  2. **Próprios**: telemóveis comprados, reparados e revendidos. Unidade única, com fotos reais; 360° numa fase posterior.

## 2. Arquitetura

```
 Bruno ─▶ Mender (app.lopestech.pt) ── módulo «Loja»: produtos, encomendas, clientes, funil
              │  catálogo público (JSON, sem custo/margem)       ▲ checkout + webhooks Stripe
              ▼                                                  │
   gerador Python do lopestech.pt ── /loja estático (HTML completo, JSON-LD, sitemap, feed Merchant)
              │                                                  │
              └──────────────▶ visitante: carrinho lateral (JS) ─┘
```

- **Fonte única: o Mender.** Produtos, preços, stock e encomendas vivem lá. O lopestech.pt nunca guarda preços próprios, porque isso levaria a preços dessincronizados.
- **Páginas estáticas**, para o SEO e o Merchant não dependerem de JS:
  - páginas de produto e de categoria em HTML completo, com JSON-LD Product/Offer (`itemCondition` New/Refurbished/Used, `availability`, `priceValidUntil`);
  - sitemap e IndexNow;
  - feed XML do Google Merchant com a política de envio (6,50 €) e de devolução (14 dias).
- **Rebuild quando o catálogo muda**: gatilho do Mender + o processo atómico de publicação já usado pelo artigo diário, com verificação da página inicial e volta à versão anterior se falhar.
- **Filtros no cliente**, sobre um JSON do catálogo: marca, memória (RAM), armazenamento, cor, estado e preço. As listagens base são estáticas, para o Google.
- **Imagens**: WebP/AVIF em 3 tamanhos com `srcset`, para um bom LCP. Ficam no R2, como as do módulo Conteúdo.
- **Carrinho lateral** em JS simples:
  - persistente em `localStorage`;
  - mostra 6,50 € de envio (Asfalto Largo, dia seguinte);
  - no checkout, o servidor revalida preço e disponibilidade.

## 3. Pagamentos
- **Stripe**, verificado na tabela oficial de 11/11/2026 para empresa sediada em PT:
  - cobre: cartão, Apple Pay, Google Pay, **MB WAY** (Checkout desde out/2025), **Multibanco** e **PayPal**;
  - **não cobre Klarna**: PT não está nem nos países de empresa nem nos de cliente.
  - Se o Bruno quiser prestações, avalia-se depois Cofidis Pay / SeQura / Scalapay, atrás da mesma interface de pagamento do Mender.
- A encomenda só passa a «paga» por **webhook**, idempotente pelo id do evento Stripe.
- **Produtos do fornecedor**: confirmar a disponibilidade antes de cobrar.
  - Cartão, wallets e PayPal: autorização com **captura manual**. Captura quando o Bruno confirma no Mender; se não confirmar em 24 h, a autorização é cancelada, sem reembolso.
  - MB WAY e Multibanco não permitem captura manual. A encomenda fica «a confirmar disponibilidade»; o Bruno confirma no telemóvel (push) e só então o Mender envia o pedido MB WAY ou a referência Multibanco.
- **Produtos próprios em stock**: pagam logo, com qualquer método.

## 4. Mender — módulo «Loja» (novo)
- **ProdutoLoja**:
  - origem: lote próprio OU oferta de fornecedor;
  - estado: novo / novo caixa aberta / recondicionado / usado; grau (Premium / Excelente / Muito Bom / Bom);
  - **garantia: tipo e meses, obrigatório**, mostrado no selo;
  - **regime fiscal: normal ou margem**, marcado nos usados próprios;
  - specs chave-valor + atributos filtráveis; fotos; campos SEO; preço; custo (privado); disponibilidade.
- **Encomenda**: a confirmar → aguarda pagamento → paga → em preparação → enviada → entregue | cancelada | reembolsada. À entrega gera a **Venda**, com IVA e nº de fatura. A fatura continua a ser emitida à mão no Moloni ON até o módulo de API estar ativo.
- **Clientes, newsletter e funil**:
  - newsletter só com consentimento explícito e double opt-in;
  - eventos first-party: viu → carrinho → checkout → email → pagou, sem pixel de terceiros antes do consentimento;
  - email de carrinho abandonado só a quem deu email e consentiu.
- O JSON público **nunca** leva custo nem margem.

## 5. Legal (obrigatório antes de vender online)
- 14 dias de livre resolução (DL 24/2014), Livro de Reclamações Eletrónico (DL 74/2017), termos e condições, privacidade e cookies.
- Garantia (DL 84/2021): 3 anos para bens novos. Nos usados pode ser reduzida a 18 meses por acordo expresso.

## 6. Fases
1. **F1 — catálogo e Google** (sem checkout): produtos no Mender → `/loja` estática, filtros, páginas de produto, SEO e feed Merchant. Botão «Encomendar» por WhatsApp/formulário. Melhor retorno e zero risco de pagamentos.
2. **F2 — carrinho, checkout e Stripe**, com o fluxo de confirmação para produtos do fornecedor.
3. **F3 — funil, newsletter e carrinhos abandonados.**
4. **F4 — fotos 360° dos próprios** (depois, eventualmente, 3D).

## 7. Decisões do Bruno (pendentes)
1. **Garantia dos portáteis «novos caixa aberta»**: se forem bens novos, a lei dá 3 anos ao consumidor; «18 meses como 2.ª mão» só vale se forem de facto usados. Confirmar com o contabilista/fornecedor como estão faturados (novo vs. usado).
2. **IVA**: os usados próprios (comprados a particulares) vão no regime da margem? Isto define o preço e a fatura.
3. **Klarna** não existe para empresas PT no Stripe. Avançamos sem prestações, ou avalia-se Cofidis Pay / SeQura / Scalapay?
4. **Começar pela F1** (Google já, encomenda por mensagem)?
5. **Conta Stripe** em nome da LopesTech. Criada pelo Bruno; o Claude não cria contas.
