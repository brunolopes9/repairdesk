# 63 - Estado da arquitectura RepairDesk - 2026-05-23

Documento para conversas com potenciais clientes, parceiros e investidores.

Estado verificado por leitura do git log ate Sprint 198 e dos documentos
`Contexto/` relevantes. Tom factual: descreve o que existe hoje, o que esta
preparado em arquitectura e o que continua em roadmap.

---

## Resumo executivo

RepairDesk e um SaaS de gestao para oficinas de reparacao em Portugal. Cobre o
fluxo operacional de balcão: clientes, reparacoes, trabalhos, diagnostico,
orcamentos, stock, vendas, garantias digitais, faturacao via provider
certificado e integracoes com loja online. O produto nasceu Portugal-first:
IVA, garantias legais, RGPD, faturacao certificada externa e linguagem de
oficina portuguesa estao no desenho desde cedo.

A stack actual combina backend .NET 10, EF Core 10 e SQL Server com frontend
React 19, TypeScript, Vite e Tailwind v4. O armazenamento de ficheiros esta
abstraido para local ou Cloudflare R2. As integracoes externas usam SDK
TypeScript, webhooks HMAC e endpoints server-side. A automacao de fornecedor e
imagem usa n8n e Anthropic Claude em pontos controlados.

O produto ja foi desenhado para evoluir para SaaS multi-tenant: isolamento por
tenant no modelo de dados, chaves API por tenant, webhooks por tenant,
politicas RGPD, DPA, sub-processadores, retention policy e auditoria. Ainda
nao ha clientes em producao; o estado e early/beta-ready em varias areas, mas
exige validacao comercial e hardening continuado antes de escala publica.

---

## Modulos implementados (por area)

### Reparacoes

- Criacao e gestao completa de reparacoes.
- Estados de oficina com transicoes validadas.
- Diagnostico guiado e historico tecnico.
- Orcamento e fatura ligados ao ciclo da reparacao.
- Garantia digital publica por slug/QR.
- Portal cliente sem login para acompanhar estado.
- Fotos antes/durante/depois com storage abstraido.
- Audit trail e logs de mudancas de estado.
- Base historica: Sprints 14-35 em [`01-Estado-Actual.md`](01-Estado-Actual.md).
- Bloco posterior: Sprints 58-96 no git log consolidam garantia, API externa e UI.

### Trabalhos

- Modulo paralelo a reparacoes para trabalhos/servicos.
- Criacao, historico, estados e detalhe.
- PDF de orcamento profissional.
- Emissao de orcamentos via provider quando aplicavel.
- Faturacao em lote tambem cobre Trabalhos.
- Integrado nos relatorios de IVA e faturacao.
- Referencia fiscal: [`35-Faturacao-Decisao-Final.md`](35-Faturacao-Decisao-Final.md).

### Vendas POS

- Venda de balcao com itens, cliente, pagamento e estado.
- Cancelamento one-click com reversao operacional.
- Integracao com faturacao Moloni/InvoiceXpress.
- Garantia digital gerada quando a venda fica paga.
- Checkout externo atomico para loja online via SDK.
- Snapshot de fornecedor B2B no item vendido.
- Suporte a devolucao/cancelamento por endpoint externo.
- Referencia SDK: [`53-External-API-SDK.md`](53-External-API-SDK.md).
- Webhooks de venda: [`54-Webhooks-Integration.md`](54-Webhooks-Integration.md).

### Stock e pecas tecnicas

- Entidade `Part` com SKU, fornecedor, localizacao e minimo.
- `PartMovimentos` para entradas, saidas e historico.
- Decremento automatico quando uma peca e usada.
- Alertas de stock baixo com semantica de threshold.
- Webhooks `parts.stock-baixo` e `phones.stock-baixo`.
- Dashboard mostra pecas em stock baixo.
- Fuzzy matcher para mapear SKUs de fornecedor.
- `SkuMapping` aprende com aprovacoes manuais.
- Referencia ingest V2: [`58-Killer-Feature-Ingest-V2.md`](58-Killer-Feature-Ingest-V2.md).

### Produtos ecommerce

- Entidade `Product` para telemoveis revendidos e dropship.
- Campos de marca, modelo, armazenamento, cor e grading.
- `Origin`/`Grade` e canonical mapping para loja.
- Slug por tenant e campos SEO.
- Separacao entre stock fisico e stock virtual/dropship.
- CSV Molano como fallback manual.
- Payload de webhook alinhado com a loja online.
- SEO gerado antes de guardar em fluxos recentes.
- Documento single source: [`57-Single-Source-Truth-Fase-A.md`](57-Single-Source-Truth-Fase-A.md).
- Imagens ricas: [`62-Webhook-Image-Payload-Spec.md`](62-Webhook-Image-Payload-Spec.md).

### Compras e despesas

- Despesas operacionais registadas no RepairDesk.
- Distincao entre compras para stock e despesas gerais.
- `IsCogs` separa COGS de OpEx.
- IVA dedutivel tratado de forma distinta de custo operacional.
- UI clarificada para "Compras & Despesas".
- Importacoes de fornecedor podem aprovar como stock ou despesa.
- Sprints relevantes: 176-178 no git log.

### Importacoes fornecedor

- Endpoint de ingest para faturas de fornecedor.
- Fluxo n8n/IMAP para transportar PDFs/email para RepairDesk.
- Parser PDF especifico para fornecedores conhecidos.
- Fallback LLM para PDFs mal formatados.
- OCR Claude Vision para foto/scan de fatura.
- Fingerprinting de fornecedor.
- Validacoes pos-parse e warnings visiveis.
- UI `/importacoes` para rever e aprovar rascunhos.
- O sistema nao actualiza stock automaticamente sem aprovacao.
- Documento n8n: [`55-Supplier-Invoice-Ingest-n8n.md`](55-Supplier-Invoice-Ingest-n8n.md).
- Setup operacional: [`56-Setup-IMAP-Ingest-Passo-a-Passo.md`](56-Setup-IMAP-Ingest-Passo-a-Passo.md).

### Relatorio IVA

- Relatorio trimestral com vendas, reparacoes e trabalhos.
- Separacao entre IVA liquidado e IVA dedutivel.
- Correcao para IVA embutido em preco final.
- Drill-down para explicar cada valor.
- Suporte a credito de IVA quando dedutivel excede liquidado.
- Widget de IVA estimado no dashboard.
- Base fiscal: [`10-Compliance-PT.md`](10-Compliance-PT.md).

### Garantia digital

- Garantia publica por slug/QR.
- PDF publico de garantia.
- Suporte a garantia de reparacao e venda.
- Auto-emissao em venda paga.
- Garantia por condicao do artigo, conforme DL 84/2021.
- Defaults por tenant: novo, open box, recondicionado, usado.
- Portal cliente mostra garantia activa e dias restantes.
- Webhooks `garantia.emitida`, `garantia.anulada`, `garantia.expirada`.
- Referencia: [`51-Garantia-3-Anos-Vendas.md`](51-Garantia-3-Anos-Vendas.md).

### Loja online e integracao externa

- SDK TypeScript server-side para loja online.
- API key por tenant, nunca exposta no browser.
- Checkout externo que cria venda, fatura e garantia.
- Historico por NIF para "Os meus pedidos".
- Webhooks outbound com HMAC, retry e idempotencia.
- Eventos de catalogo para telemoveis e pecas.
- Helpers TS para validar payloads.
- Shop AI Bridge especificado para centralizar chamadas Anthropic.
- Referencias:
  - [`53-External-API-SDK.md`](53-External-API-SDK.md)
  - [`54-Webhooks-Integration.md`](54-Webhooks-Integration.md)
  - [`61-Shop-AI-Bridge-Spec.md`](61-Shop-AI-Bridge-Spec.md)

### Pipeline imagens SEO

- Upload de imagens de produto no RepairDesk.
- Resize automatico para WebP em multiplas larguras.
- `blurDataUrl` para placeholder LQIP.
- Width/height para reduzir CLS na loja.
- Alt text gerado por Claude Vision.
- Batch re-optimise para imagens legacy.
- Payload de webhook enriquecido para a loja consumir.
- Referencias:
  - [`60-Imagens-SEO-Pipeline.md`](60-Imagens-SEO-Pipeline.md)
  - [`62-Webhook-Image-Payload-Spec.md`](62-Webhook-Image-Payload-Spec.md)

### PWA

- Aplicacao instalavel.
- Workbox/Vite PWA configurado.
- App shell em cache.
- Indicador online/offline.
- Fase actual: PWA basica sem offline write.
- Offline write continua em roadmap por causa de conflitos, tenant isolation e
  risco operacional.
- Plano completo: [`24-PWA-Offline.md`](24-PWA-Offline.md).

### Multi-tenant e SaaS readiness

- `TenantId` em entidades de negocio.
- Global query filters no backend.
- Soft-delete em entidades principais.
- API keys por tenant.
- Webhooks por tenant.
- LLM usage tracked por tenant.
- Email ingest per-tenant.
- Retention policy configuravel.
- Cleanup cron diario para importacoes antigas.
- DPA, politica RGPD e lista de sub-processadores documentadas.
- Referencias:
  - [`16-Compliance-RGPD.md`](16-Compliance-RGPD.md)
  - [`29-Privacy-By-Design-Audit.md`](29-Privacy-By-Design-Audit.md)
  - [`18-Backup-DR.md`](18-Backup-DR.md)

---

## Stack tecnico

- Backend: .NET 10.
- ORM: EF Core 10.
- Base de dados: SQL Server.
- API: ASP.NET Core, OpenAPI/Swagger.
- Auth: Identity/JWT, claims de tenant.
- Logs: Serilog.
- Cache: Redis quando configurado.
- Frontend: React 19.
- Linguagem frontend: TypeScript.
- Build frontend: Vite.
- UI/CSS: Tailwind v4.
- State/data fetching: React Query + Axios.
- PWA: Vite PWA + Workbox.
- Storage ficheiros: Cloudflare R2 via API S3-compatible.
- Storage dev: filesystem local.
- LLM: Anthropic Claude Haiku/Sonnet, com tracking por tenant.
- Automacao: n8n para IMAP, workflows e ingest.
- Faturacao certificada: Moloni / InvoiceXpress.
- SDK externo: TypeScript, server-side only.
- Webhooks: HMAC-SHA256, retry e idempotencia por delivery id.

---

## Compliance PT

- DL 84/2021: garantia legal para bens moveis.
- Garantia de 36 meses para particulares em bens novos.
- Possibilidade de prazos diferentes por condicao, dentro dos limites legais.
- CIRS art. 123: horizonte de 10 anos para arquivo fiscal quando aplicavel.
- RGPD: DPA, sub-processadores, privacy policy e retention policy.
- Audit log para eventos sensiveis e alteracoes relevantes.
- Dados dos clientes finais pertencem a loja; RepairDesk actua como
  subcontratante.
- Faturacao: RepairDesk nao se apresenta como software certificado AT.
- Emissao fiscal e feita por providers certificados, Moloni ou InvoiceXpress.
- Estrategia fiscal decidida em [`35-Faturacao-Decisao-Final.md`](35-Faturacao-Decisao-Final.md).
- Pesquisa legal detalhada em [`10-Compliance-PT.md`](10-Compliance-PT.md).
- RGPD operacional em [`16-Compliance-RGPD.md`](16-Compliance-RGPD.md).

---

## Roadmap futuro

- PWA offline write.
- IndexedDB por tenant.
- Outbox local para criar reparacao, mudar estado e adicionar diagnostico.
- Sync push/pull com `RowVersion` e resolucao de conflitos.
- Fotos offline numa fase posterior.
- WhatsApp Business API com opt-in e templates.
- Multi-loja cross-location dentro do mesmo tenant.
- Inventario multi-localizacao.
- Marketplace de pecas entre lojas.
- Reconciliacao automatica de fornecedores via SFTP/API.
- Fase futura de certificacao AT propria apenas se houver traccao suficiente.
- Rebrand comercial antes de escala publica, se necessario.

---

## Metricas

- Git log verificado ate Sprint 198 em 2026-05-23.
- Aproximadamente 330 entregas/sprints curtos contabilizados no trabalho interno.
- Baseline comunicada para apresentacao: 243 testes passing.
- Revalidacao local em 2026-05-23 nao chegou a executar testes por 2 erros de
  compilacao em servicos recentes; corrigir antes de demonstracao tecnica.
- Zero clientes em producao.
- Estado comercial: early, ainda sem receita SaaS.
- Estado tecnico: produto funcional, com integracoes e compliance PT em fase
  beta/hardening.

---

## Notas de leitura

- Comecar por [`00-Index.md`](00-Index.md) para mapa de documentacao.
- Estado inicial ate Sprint 35: [`01-Estado-Actual.md`](01-Estado-Actual.md).
- Criterios beta: [`34-Beta-Launch-Criteria.md`](34-Beta-Launch-Criteria.md).
- Stack, deployment e backups:
  - [`17-Hosting-Deployment.md`](17-Hosting-Deployment.md)
  - [`18-Backup-DR.md`](18-Backup-DR.md)
  - [`19-Monitoring.md`](19-Monitoring.md)
- PWA/offline: [`24-PWA-Offline.md`](24-PWA-Offline.md).
- Testes: [`27-Plano-Testes.md`](27-Plano-Testes.md).
- Release: [`30-Release-Strategy.md`](30-Release-Strategy.md).

