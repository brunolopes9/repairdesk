# 89 — Billing & Compliance: arquitectura de faturação PT do Mender

**Data:** 2026-05-28. **Contexto:** Bruno viu o RO App + B2Brouter e duvidou se a estratégia
actual de delegar emissão a um provider certificado (Moloni) ainda é a correcta. Esta nota
fecha a questão a preto no branco, para não voltarmos a confundir as camadas.

Referências: `feedback_certificacao_fiscal_pt.md` (memória), [Vendus DL 28/2019](https://www.vendus.pt/blog/decreto-lei-28-2019-regras-de-faturacao/),
[Sovos PT e-invoicing](https://sovos.com/vat/tax-rules/portugal-e-invoicing/), [B2Brouter PT](https://www.b2brouter.net/global/international/portugal/),
[RO App + B2Brouter article (Abril 2026)](https://help.roapp.io/pt/articles/14701547-suporte-a-faturacao-eletronica-para-portugal).

## TL;DR

**Não há atalho para fugir à certificação AT em PT.** O que vimos no concorrente RO App
não evita certificação — eles delegam a um provider certificado (B2Brouter). Mender já faz
o mesmo via Moloni. **A arquitectura actual está certa.** Auto-certificar o Mender é marco
enterprise futuro, não agora.

## As 3 camadas (independentes — não se substituem entre si)

| Camada | Pergunta que responde | Quem trata no Mender hoje | Obrigatória? |
|--------|------------------------|---------------------------|--------------|
| **A — Software de faturação certificado AT** (DL 28/2019 + Portaria 363/2010): número sequencial · hash encadeado · ATCUD · QR code · SAF-T(PT) | *"Posso criar o documento fiscal legal em PT?"* | **Moloni** (provider externo, via API) | **SIM**, sempre. Bruno tem contabilidade organizada. |
| **B — Comunicação à AT** (webservices, séries) | *"Como aviso a AT que esta série existe?"* | **Moloni** comunica por nós. (Bruno tem o cert `ChaveCifraPublicaAT2027.cer` mas é "Produtor de Software" — não certifica emissão.) | SIM, mas resolvida pela Camada A. |
| **C — Faturação eletrónica estruturada** (CIUS-PT XML UBL · Peppol · QES · FE-AP) | *"Como ENTREGO uma fatura em formato estruturado a quem o exige?"* | Não implementado. Não é preciso para B2C. | **Só B2G** (Estado) ou **B2B estruturado / cross-border UE**. |

**Certificar (A) ≠ Peppol (C). Não há transferência de obrigações.**

## Para o Bruno (oficina LopesTech — B2C)

| Cenário | Camada A | Camada C |
|---------|---------|---------|
| Vender ao consumidor final em PT (B2C) | ✅ Obrigatório | ❌ NÃO precisa |
| Vender a empresa portuguesa (B2B) | ✅ Obrigatório | ❌ Hoje opcional (QES em PDF B2B foi adiada — fontes divergem 1 Jan 2026 vs 2027) |
| Vender ao Estado / câmara / hospital (B2G) | ✅ Obrigatório | ✅ **Obrigatório** (CIUS-PT via FE-AP/Peppol) |
| Vender internacional (B2B noutros países UE) | ✅ Obrigatório | ✅ Peppol — UE caminha para mandatório (ViDA ~2030+) |

**Conclusão prática:** o B2Brouter/Peppol que o RO App publicita **não é o que falta ao Bruno**
hoje. Moloni já faz a Camada A; não há clientes B2G nem internacional.

## RO App ↔ Mender (mesma arquitectura, providers diferentes)

Confirmado em conversa com o Miguel (RO App, 27 Maio 2026) + research no artigo deles:
> *"É uma integração sim, através da B2Brouter."*

```
RO App (ERP, não certificado)  + B2Brouter (Peppol AP + emissão certificada)
Mender  (ERP, não certificado)  + Moloni     (emissão certificada PT)        ← actual
```

**Diferença:** apenas o nome do provider. Funcionalmente equivalentes para B2C/B2B PT.
B2Brouter ganha em B2G + transmissão Peppol cross-border, mas a página deles é
maioritariamente B2G/B2B estruturado — story B2C não documentado.

## Arquitectura Mender (decisão)

```
Mender (não certificado) — dono de TUDO menos o ato fiscal
 ├─ UX faturação · ciclo do documento · estados
 ├─ Modelo dados cliente/contabilístico · numeração lógica (abstração)
 ├─ Relatórios gestão · IVA estimado · staging dados para SAF-T
 ├─ Catálogo · Stock · Reparações · Vendas · Balcão · etc.
 └─ IBillingProvider (abstração pluggable)  ← já implementado
      ├─ MoloniProvider           ← actual (Camada A)
      ├─ InvoiceXpressProvider    ← alternativa (já planeada — task #142, #355)
      ├─ VendusProvider           ← POS-friendly, futuro
      └─ EInvoicingProvider       ← FUTURO, apenas se B2G/UE
           └─ B2BrouterProvider → CIUS-PT + Peppol + QES + FE-AP
```

**O que o Mender NÃO faz (e bem):** emitir o documento legal, gerar SAF-T(PT) oficial,
calcular hash encadeado, gerir ATCUD. Tudo isto vive no provider certificado.

## Roadmap

**Curto prazo (já feito + a consolidar):**
- ✅ Moloni provider: emissão de fatura, NC, recibo, anular. Auto-discovery IDs, retry, password grant.
- ✅ `IBillingProvider` abstraction (Sprint 164/172).
- ⚠️ InvoiceXpress provider (task #142 + #355 pendentes — não bloqueante).

**Médio prazo (só se Bruno tiver clientes B2G ou cross-border):**
- B2BrouterProvider como provider adicional. Não substitui Moloni; complementa-o para emitir
  CIUS-PT + entrega Peppol/FE-AP.

**Longo prazo (marco enterprise — quando Mender for SaaS multi-tenant pago):**
- Auto-certificar o Mender como software de faturação AT. Custo: €0 taxa, ~€15-35k dev +
  recertificação contínua a cada alteração que toque hash/ATCUD/SAF-T. Owns-the-fiscal-core
  → margem maior, white-label possível, independência de providers. **Não agora.**

## O que muda se/quando o Miguel responder

A pergunta enviada (27 Maio 2026): se o RO App tem número de certificação AT próprio, ou
se a B2Brouter é que emite legalmente. Possíveis respostas:

| Resposta do Miguel | Implicação |
|---|---|
| RO App tem cert AT próprio + B2Brouter só para Peppol | Confirma 3 camadas. Mender pode aprender com a estratégia, mas decisão não muda. |
| B2Brouter é que emite (RO App não certifica) | Confirma que B2Brouter É emissor B2C PT. Abre a porta a usá-la como **provider alternativo** ao Moloni no Mender (médio prazo). |
| Resposta ambígua | Sem mudança. |

**Em nenhum cenário** isto valida "emitir fatura no Mender sem certificar". O ato de emitir
(número + hash + SAF-T) sempre tem de vir de software certificado.

## Erros a evitar (lições)

1. **Não confundir camadas.** "Peppol" não substitui certificação AT. Já me enganei nisto.
2. **Não inventar legalidade.** Quando há dúvida fiscal, fazer research; nunca improvisar.
3. **Não auto-certificar prematuramente.** O custo de manter certificação (testing, recerts,
   auditorias, responsabilidade fiscal) é um produto inteiro. Para um fundador solo, é uma
   armadilha de oportunidade.
4. **Manter `IBillingProvider` limpo.** Quando entrar B2Brouter um dia, deve plugar sem
   reescrever o Mender — exactamente como Moloni hoje.

## TL;DR (de novo)

> Bruno não precisa de fazer nada agora. A arquitectura está certa. Quando o Miguel
> responder, anexamos aqui. Quando entrarem clientes B2G/UE, adicionamos B2Brouter como
> provider. Auto-certificação é problema do Mender-SaaS-de-€10k/mês, não do Mender-de-hoje.

---

## ✅ RESPOSTA DO MIGUEL (RO App, 28-05-2026) — CONFIRMADO

Miguel respondeu à pergunta enviada. Texto verbatim:

> *"Atualmente, nem nós nem a B2Brouter temos uma certificação completa de Portugal, então
> não cobrimos todo o fluxo, já que em Portugal funciona um pouco diferente de outros
> países e, em vez de enviar a fatura para a agência tributária após a criação, primeiro
> é preciso obter o ATCUD e, em seguida, o faturamento eletrônico é enviado via Peppol ou
> e-mail, basicamente.*
>
> *Para clientes de Portugal, têm que usar um software adicional para obter esse número
> ATCUD, e a nossa integração de faturamento eletrônico cobre apenas o envio via Peppol
> por enquanto. Infelizmente, essa é uma limitação da B2Brouter. Só poderemos cobrir essa
> parte da criação de faturas depois que eles a implementarem do lado deles, mas, por
> enquanto, não há um cronograma exato."*

### O que isto valida (a nosso favor)
1. **A análise do Doc 89 estava 100% certa.** As 3 camadas separadas, B2Brouter como
   Camada C apenas. O Miguel chamou "fluxo PT" a exactamente o que chamámos Camada A.
2. **RoApp não cobre PT.** Cliente PT que use RoApp **TEM** de ter outro software
   certificado para o ATCUD/emissão. Exactamente como Mender + Moloni.
3. **B2Brouter está limitada a Peppol-delivery.** Sem timeline para acrescentarem a
   parte Camada A (ATCUD + assinatura de série + SAF-T). Não esperar por eles.
4. **Mender + Moloni em B2C-PT é vantagem competitiva real**, não duplicação do RoApp.
   Vendamos isto: "ERP completo para oficinas PT, **com** compliance fiscal nativa".

### Decisão arquitectural (confirmada, sem mudanças)
- Manter `IBillingProvider` com Moloni como provider principal.
- **Não** auto-certificar Mender agora.
- **Não** integrar B2Brouter como provider hoje (cobre só Camada C — irrelevante p/ B2C-PT).
- **Considerar** B2BrouterProvider só se/quando aparecerem clientes B2G ou cross-border UE,
  e como **complemento** ao Moloni (não substituto).

### Hook de marketing
- Para clientes PT: *"Software de gestão de oficina que **inclui** faturação certificada
  AT (via Moloni). Não precisas de aplicação extra para o ATCUD."*
- Para qualquer concorrente internacional (RoApp/Repairshopr/etc.): *"Funciona em PT
  out-of-the-box. Eles não."*

Ver [[90-Analise-RoApp]] para análise competitiva completa.
