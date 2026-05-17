# Contexto — Índice de Documentação RepairDesk SaaS

Documentação estratégica para o produto RepairDesk SaaS. Mantida por sprint.

Última atualização: 2026-05-17

---

## Ordem de leitura recomendada

Para alguém novo a este projeto (futuro Bruno, futuro Claude, futuro sócio):

0. **[`01-Estado-Actual.md`](01-Estado-Actual.md)** — 🎯 **COMEÇA AQUI.** Snapshot vivo do que está implementado, o que falta para Beta, e menu de próximos passos. Actualizar a cada 3-5 sprints.
1. **[`RepairDesk_Novas_Ideias.md`](RepairDesk_Novas_Ideias.md)** — ideias raw, scratchpad histórico. Contém secção 5 com **estratégia SaaS** consolidada.
2. **[`02-Concorrentes.md`](02-Concorrentes.md)** — quem está no mercado, preços, features, vulnerabilidades. Atualizar quando descobrirmos coisas novas.
3. **[`03-Dores-Reais.md`](03-Dores-Reais.md)** — dores reais observadas (Reddit, Capterra, Bruno). **Citações literais** com análise crítica. **Esta é a bíblia das decisões de produto.**
4. **[`04-Roadmap-Detalhado.md`](04-Roadmap-Detalhado.md)** — mapping dor → feature → sprint. Sprints 14-22 + horizons 2/3/4.
5. **[`05-Reflexao-Critica.md`](05-Reflexao-Critica.md)** — onde concordo/discordo de Codex, ChatGPT e do próprio Bruno. Não aceitar tudo cegamente.
6. **[`06-Prompts-Codex.md`](06-Prompts-Codex.md)** — templates fortes para delegar tarefas a Codex. Anti-padrões a evitar.
7. **[`07-Pricing-Proposta.md`](07-Pricing-Proposta.md)** — pricing tiers (€19/39/89 por loja), CAC/LTV, comparação RO App/RepairDesk, trial/freemium. **Resultado da delegação ao Codex (task #56).**
8. **[`10-Compliance-PT.md`](10-Compliance-PT.md)** — SAF-T, ATCUD, e-Fatura, certificação AT. **Resultado da delegação ao Codex (Prompt #7).** Documento corrige erro grave: emissão de faturas pelo RepairDesk **obriga** certificação (DL 28/2019 art. 4.º n.º 1 b), independente do regime do utilizador.
9. **[`11-WhatsApp-Templates.md`](11-WhatsApp-Templates.md)** — templates WhatsApp por estado de reparação, com variações por categoria, casos especiais, defaults e notas RGPD/opt-in. **Resultado da delegação ao Codex (Prompt #10).**
10. **[`12-Onboarding-Wizard.md`](12-Onboarding-Wizard.md)** — especificação do wizard de onboarding de novas lojas (passos, mockups ASCII, métricas, plano B). **Resultado da delegação ao Codex (Prompt #11).**
11. **[`09-Customer-Acquisition.md`](09-Customer-Acquisition.md)** — estratégia de aquisição B2B PT (mercado, canais, CAC, roadmap 90 dias). **Resultado da delegação ao Codex (Prompt #9).**
12. **[`14-Storage-Fotos.md`](14-Storage-Fotos.md)** — decisão de storage para fotos antes/depois de reparações, custos, arquitetura, RGPD, segurança e plano de migração. **Resultado da delegação ao Codex (Prompt #13).**
13. **[`15-WhatsApp-Provider.md`](15-WhatsApp-Provider.md)** — decisão de provider WhatsApp Business API, custos, KYC, número dedicado, templates e plano de integração .NET. **Resultado da delegação ao Codex (Prompt #14).**
14. **[`13-IMEI-Autoridades.md`](13-IMEI-Autoridades.md)** — viabilidade de cruzar IMEI com bases de dados de roubados (GSMA CheckMEND, PSP/MAI). **Resultado da delegação ao Codex (Prompt #12).**
15. **[`16-Compliance-RGPD.md`](16-Compliance-RGPD.md)** — privacy policy, ToS, DPA, cookies banner, procedimentos breach. **Resultado da delegação ao Codex (Prompt #15).**
16. **[`17-Hosting-Deployment.md`](17-Hosting-Deployment.md)** — decisão de hosting/deployment para produção beta, custos EU, SQL Server licensing, DNS/SSL, deploy e plano de escala. **Resultado da delegação ao Codex (Prompt #16).**
17. **[`18-Backup-DR.md`](18-Backup-DR.md)** — runbook de backups, restore, RPO/RTO, off-site EU e disaster recovery. **Resultado da delegação ao Codex (Prompt #17).**
18. **[`19-Monitoring.md`](19-Monitoring.md)** — stack mínima de observabilidade, alertas P1/P2/P3, Sentry, Better Stack, health checks e checklist operacional. **Resultado da delegação ao Codex (Prompt #18).**
19. **[`20-Suporte-Cliente.md`](20-Suporte-Cliente.md)** — modelo de suporte cliente para fundador solo, canais, SLAs, KB, templates e métricas. **Resultado da delegação ao Codex (Prompt #19).**
20. **[`21-Certificacao-AT.md`](21-Certificacao-AT.md)** — plano operacional para certificação própria AT: processo, requisitos, timeline, custos, contactos e decisão provider vs certificado próprio. **Resultado da delegação ao Codex (Prompt #20).**
21. **[`22-Tabela-Precos-PT.md`](22-Tabela-Precos-PT.md)** — tabela base de preços PT 2026 para reparações, com PVP, custo de peça aproximado, margem, tempo e fontes. **Resultado da delegação ao Codex (Prompt #21).**
22. **[`23-Plano-Fiscal-Pessoal.md`](23-Plano-Fiscal-Pessoal.md)** — estratégia jurídico-fiscal pessoal LopesTech: Art. 53, contabilidade, Lda, IRS/SS e gatilhos de decisão. **Resultado da delegação ao Codex (Prompt #22).**
23. **[`24-PWA-Offline.md`](24-PWA-Offline.md)** — estratégia PWA/offline-first para operações críticas de balcão, IndexedDB, sync, conflitos e limitações Safari/iOS. **Resultado da delegação ao Codex (Prompt #23).**
24. **[`25-Distribuidores-Pecas-PT.md`](25-Distribuidores-Pecas-PT.md)** — mapa estratégico de distribuidores PT/EU de peças, modelos de parceria, abordagem comercial e plano de piloto. **Resultado da delegação ao Codex (Prompt #24).**
25. **[`26-Brand-Design-System.md`](26-Brand-Design-System.md)** — decisão de naming, claim, identidade visual e princípios de design para o produto. **Resultado da delegação ao Codex (Prompt #25).**
26. **[`27-Plano-Testes.md`](27-Plano-Testes.md)** — plano de testes automatizados backend/frontend/E2E/load/security para beta. **Resultado da delegação ao Codex (Prompt #26).**
27. **[`28-Performance-Caching.md`](28-Performance-Caching.md)** — estratégia de profiling, optimização EF/SQL, caching, CDN e escala 10→100→1000 lojas. **Resultado da delegação ao Codex (Prompt #27).**
28. **[`29-Privacy-By-Design-Audit.md`](29-Privacy-By-Design-Audit.md)** — audit técnico de privacy by design aplicado à arquitetura atual, com mapa de dados, gaps RGPD e plano de remediação pré-beta. **Resultado da delegação ao Codex (Prompt #28).**
29. **[`30-Release-Strategy.md`](30-Release-Strategy.md)** — estratégia operacional de releases, versioning, changelog público, migrations, deploy, rollback e comunicação a clientes. **Resultado da delegação ao Codex (Prompt #29).**
30. **[`31-Sales-Playbook.md`](31-Sales-Playbook.md)** — playbook de vendas founder-led: discovery, demo, objeções, pricing, follow-up, CRM simples e plano para 3 lojas amigas. **Resultado da delegação ao Codex (Prompt #30).**
31. **[`32-Audit-UX-UI.md`](32-Audit-UX-UI.md)** — audit UX/UI completo do RepairDesk, score por critério, quick wins, roadmap visual, mockups ASCII e inspiração SaaS B2B. **Resultado da delegação ao Codex (Prompt #31).**
32. **[`33-CI-CD-Setup.md`](33-CI-CD-Setup.md)** — setup operacional GitHub Actions, secrets, deploy staging/production. **Resultado do Codex coding #C4 (em curso).**
33. **[`34-Beta-Launch-Criteria.md`](34-Beta-Launch-Criteria.md)** — 🎯 critérios objectivos MUST/SHOULD/NICE-have para lançar beta, diferenciação real, gap analysis e timeline 6-8 semanas. Reflexão crítica anti-feature-creep.
34. **[`35-Faturacao-Decisao-Final.md`](35-Faturacao-Decisao-Final.md)** — 🔒 decisão fechada Path A (Moloni/InvoiceXpress). Explica Camada 1 (certificação) vs Camada 2 (webservices AT) — fecha confusão histórica. Prompt Codex preparado.
35. **[`36-Video-Demo-Script.md`](36-Video-Demo-Script.md)** — 🎬 script 90s pronto para gravar. 8 cenas + storyboard + notas técnicas + checklist + versão 15s para Stories. Materializar diferenciação que o produto já tem.

---

## Resumo rápido

### Quem somos
- Bruno Lopes — fundador LopesTech (Viseu, NIF 263758141)
- 22 anos, saiu do emprego em Abril/2026
- CAE 62100 + secundários 47401, 58290, 95101, 95102
- Regime fiscal: Isenção Art. 53 CIVA

### Que produto fazemos
- **RepairDesk SaaS** — backoffice multi-tenant para oficinas de reparação
- Foco vertical: telemóveis, computadores, eletrónica geral
- Portugal-first (SAFT-PT, IVA PT, WhatsApp, MBWay no roadmap)
- Stack: .NET 10 + EF Core 10 + SQL Server (backend), React 19 + Vite + Tailwind v4 (frontend)

### Quem é a concorrência (resumo)
- **RepairDesk Lahore** — incumbente $99/user/mês, 3000+ lojas, UX antiga
- **RO App** — moderno, €15-69/mês, generalista demais
- **BytePhase, RepairCMS, PC Repair Tracker** — outros do mercado, ver `02-Concorrentes.md`
- **Reparo (Kossano)** — gratuito offline, referência de UX simples

### Como nos vamos diferenciar
1. **Português nativo + compliance PT** (SAFT, IVA, AT)
2. **UX moderna**: portal cliente Uber-style, dark mode, mobile-first
3. **Verticalização**: eletrónica > genérico
4. **Honesto**: sem dark patterns, dados são do utilizador, sem lock-in
5. **Pricing transparente**: €19-49/mês público

### Próximos passos imediatos
Ver `01-Estado-Actual.md` secção 5 "menu de próximos passos". **Resumo:** comprar VPS + domínio → deploy → backups → publicar RGPD → convidar 1ª loja amiga.

---

## Status dos ficheiros

| Ficheiro | Status | Última atualização |
|---|---|---|
| 00-Index.md | Vivo | 2026-05-16 |
| RepairDesk_Novas_Ideias.md | Vivo (scratchpad) | 2026-05-13 |
| 02-Concorrentes.md | Vivo | 2026-05-13 |
| 03-Dores-Reais.md | Vivo (enriquecer com novas) | 2026-05-13 |
| 04-Roadmap-Detalhado.md | Vivo (revisitar a cada 6 sprints) | 2026-05-13 |
| 05-Reflexao-Critica.md | Vivo | 2026-05-13 |
| 06-Prompts-Codex.md | Vivo (adicionar templates novos) | 2026-05-13 |
| 07-Pricing-Proposta.md | Vivo (revisitar antes de lançamento) | 2026-05-15 |
| 09-Customer-Acquisition.md | Vivo | 2026-05-16 |
| 10-Compliance-PT.md | Vivo (validar com contabilista antes de produção) | 2026-05-15 |
| 11-WhatsApp-Templates.md | Vivo (testar 3 templates com clientes reais antes de automatizar) | 2026-05-16 |
| 12-Onboarding-Wizard.md | Vivo | 2026-05-16 |
| 13-IMEI-Autoridades.md | Vivo | 2026-05-16 |
| 14-Storage-Fotos.md | Vivo (revisitar preços antes de contratar provider) | 2026-05-16 |
| 15-WhatsApp-Provider.md | Vivo | 2026-05-16 |
| 16-Compliance-RGPD.md | Vivo | 2026-05-16 |
| 17-Hosting-Deployment.md | Vivo (confirmar preços finais no carrinho antes de contratar) | 2026-05-16 |
| 18-Backup-DR.md | Vivo | 2026-05-16 |
| 19-Monitoring.md | Vivo | 2026-05-16 |
| 20-Suporte-Cliente.md | Vivo | 2026-05-16 |
| 21-Certificacao-AT.md | Vivo (confirmar custos de consultoria com propostas reais antes de avançar) | 2026-05-16 |
| 22-Tabela-Precos-PT.md | Vivo (rever trimestralmente; preços de peças mudam muito) | 2026-05-16 |
| 23-Plano-Fiscal-Pessoal.md | Vivo (validar com contabilista antes de decisões fiscais) | 2026-05-16 |
| 24-PWA-Offline.md | Vivo (validar em Safari iOS real antes de implementar sync) | 2026-05-16 |
| 25-Distribuidores-Pecas-PT.md | Vivo (validar contactos antes de outreach) | 2026-05-16 |
| 26-Brand-Design-System.md | Vivo (validar marca/domínio/INPI antes de anunciar) | 2026-05-16 |
| 27-Plano-Testes.md | Vivo (implementar por fases antes de beta pública) | 2026-05-16 |
| 28-Performance-Caching.md | Vivo (medir baseline antes de optimizar/cachear) | 2026-05-16 |
| 29-Privacy-By-Design-Audit.md | Vivo (bloqueadores pré-beta identificados) | 2026-05-16 |
| 30-Release-Strategy.md | Vivo (implementar antes de clientes pagantes) | 2026-05-16 |
| 31-Sales-Playbook.md | Vivo (usar nas primeiras 3 demos e ajustar com feedback real) | 2026-05-16 |
| 32-Audit-UX-UI.md | Vivo (executar Sprint UX-1 antes da primeira demo paga) | 2026-05-16 |

### Pendente
- `08-Pagamentos-Comparacao.md` — Stripe/Mollie/Easypay/SIBS — Prompt #8 (**adiar até cobrarmos SaaS, ~6 meses**)

---

## Convenções

- **Linguagem:** PT-PT (não BR), nativo, não traduções
- **Datas:** sempre absolutas (2026-05-13), nunca relativas
- **Citações:** literais e atribuídas (quem, onde, quando)
- **Opinião própria:** sempre presente. Não aceitar cegamente conselhos.
- **Esforço:** S/M/L/XL (cf. roadmap)
- **Priorização:** 🔴 / 🟡 / 🟢 / ⚫

---

## Princípios fundadores (não-negociáveis)

1. Dados são SEMPRE do utilizador. Export grátis, soft-delete, ownership clara.
2. Sem lock-in contratual. Mensal cancelável. Sem multas.
3. Pricing público. Sem "contact sales" para preços standard.
4. Sem dark patterns. Cancelar conta com 2 cliques.
5. Sem ads in-app.
6. Backups diários. Audit logs imutáveis.
7. Português nativo. Compliance PT first-class.
8. Open APIs. Integrações de terceiros bem-vindas.
9. Comunicação honesta. Post-mortems quando crítico.
10. Não competir em features, competir em UX e em conhecer o cliente.
