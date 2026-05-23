# 67 - Auditoria de Customizacao Per-Tenant

Data: 2026-05-23  
Pedido: permitir bastante customizacao nas definicoes do Mender/RepairDesk para tudo o que seja preferencia pessoal da loja.  
Escopo: Fase 1 apenas. Este documento e auditoria + recomendacao; nao houve implementacao.

## Resumo executivo

O RepairDesk ja tem uma boa base per-tenant para identidade da empresa, fiscalidade, garantia, faturacao, campos personalizados, diagnostico, webhooks, retencao de faturas de fornecedor e BYOK de IA. O problema nao e falta total de settings; e que as automacoes mais "sentidas" pela loja ainda vivem como comportamento implicito no codigo.

Os maiores gaps de customizacao sao:

1. WhatsApp: templates, estados, on/off, repeticao e regras estao hardcoded no frontend.
2. Portal cliente + notificacoes push: mostra quase tudo por defeito e envia push em qualquer mudanca de estado.
3. Automacoes de reparacao/venda: garantia automatica e "Entregue = Pago" assumem um fluxo de loja.
4. PDFs: usam branding basico, mas footer/copy/termos por documento ainda nao sao configuraveis.
5. POS/stock/loja online: varios defaults operacionais sao escolhas pessoais da loja.

Recomendacao principal para Fase 2: nao espalhar 30 colunas novas no `Tenant`. Para preferencias operacionais que vao crescer, criar uma estrutura unica de preferencias por tenant, por exemplo `TenantPreferences`/`TenantPreferencesJson`, com defaults backward-compatible. Campos legais/fiscais estaveis podem continuar como colunas explicitas.

## Ja configuravel hoje

| Area | Estado actual | Referencias |
|---|---|---|
| Dados da empresa, IBAN, CAE, regime fiscal, termos, logo, cor primaria | Ja existe em `Tenant` e UI `/definicoes`. | `backend/src/RepairDesk.Core/Entities/Tenant.cs:7`, `backend/src/RepairDesk.Core/Entities/Tenant.cs:21`, `backend/src/RepairDesk.Services/TenantSettings/TenantSettingsService.cs:67`, `frontend/src/pages/definicoes/Definicoes.tsx:1678` |
| Garantia de reparacao | Dias, cobertura e exclusoes ja sao per-tenant. | `backend/src/RepairDesk.Core/Entities/Tenant.cs:28`, `frontend/src/pages/definicoes/Definicoes.tsx:1289` |
| Garantia de venda por condicao | Dias por Novo/Open Box/Recondicionado/Usado e textos ja sao per-tenant. | `backend/src/RepairDesk.Core/Entities/Tenant.cs:37`, `frontend/src/pages/definicoes/Definicoes.tsx:1331` |
| Google Reviews | URL ja e per-tenant, mas regra de quando pedir review ainda nao. | `backend/src/RepairDesk.Core/Entities/Tenant.cs:45`, `backend/src/RepairDesk.Services/PublicPortal/PublicPortalService.cs:132` |
| Faturacao provider/series/IDs Moloni/InvoiceXpress | Provider, sandbox, tipo documento, serie, IDs e motivo isencao ja sao per-tenant. | `backend/src/RepairDesk.Core/Entities/TenantBillingSettings.cs:10`, `backend/src/RepairDesk.Core/Entities/TenantBillingSettings.cs:18`, `backend/src/RepairDesk.Core/Entities/TenantBillingSettings.cs:22` |
| Auto-discovery Moloni | Ja existe, mas as heuristicas sao fixas. | `backend/src/RepairDesk.Services/Billing/TenantBillingSettingsService.cs:253` |
| Campos personalizados de equipamento | Templates e visibilidade no portal ja sao per-tenant. | `backend/src/RepairDesk.Services/EquipmentFields/EquipmentFieldService.cs:53`, `backend/src/RepairDesk.DAL/Persistence/EquipmentFieldRepository.cs:68` |
| Diagnostico | Templates sao per-tenant e podem ser default por categoria. | `backend/src/RepairDesk.Services/Diagnostico/DiagnosticoService.cs:31`, `backend/src/RepairDesk.Services/Diagnostico/DiagnosticoService.cs:91` |
| Webhooks | Subscricoes e eventos por subscription ja sao editaveis. | `backend/src/RepairDesk.Core/Entities/WebhookSubscription.cs:20`, `frontend/src/pages/definicoes/Webhooks.tsx:62` |
| Retencao de faturas de fornecedor | Retencao rejected/failed/approved PDF ja e per-tenant. | `backend/src/RepairDesk.Core/Entities/Tenant.cs:70`, `backend/src/RepairDesk.API/HostedServices/SupplierInvoiceRetentionHostedService.cs:13` |
| BYOK Anthropic | Tenant pode usar chave propria de Anthropic. | `backend/src/RepairDesk.Core/Entities/Tenant.cs:58`, `frontend/src/pages/definicoes/LlmUsage.tsx:130` |
| Regra fornecedor stock/despesa | Ja existe por fornecedor, nao por tenant global. | `backend/src/RepairDesk.Core/Entities/Fornecedor.cs:47`, `backend/src/RepairDesk.Services/Documents/SupplierInvoiceImportService.cs:995` |

## Auditoria de hardcodes / preferencias

| # | Feature | Localizacao | Como funciona hoje | Customizacao provavel | Esforco | Prioridade |
|---:|---|---|---|---|---:|---|
| 1 | WhatsApp - biblioteca de templates | `frontend/src/lib/whatsapp/templates.ts:51`, `frontend/src/lib/whatsapp/templates.ts:155` | Templates e mapeamento por estado estao hardcoded no frontend. | Ligar/desligar WhatsApp; editar texto por estado; variaveis permitidas; reset para default Mender. | 12-18h | Alta |
| 2 | WhatsApp - estados que disparam templates | `frontend/src/lib/whatsapp/templates.ts:155`, `frontend/src/components/WhatsAppMenu.tsx:54` | Lista contextual e decidida por estado; nao ha regra per-tenant. | Escolher estados com template activo, template default por estado e ordem de sugestoes. | 6-10h | Alta |
| 3 | WhatsApp - "sempre" vs "uma vez" | `frontend/src/components/WhatsAppMenu.tsx:30`, `frontend/src/components/WhatsAppMenu.tsx:63` | Abre `wa.me`; nao guarda historico de envio nem bloqueia repeticoes. | Modo manual, sugerir uma vez, permitir sempre, ou bloquear se ja enviado para esse estado. Exige tabela/log de notificacoes. | 10-16h | Alta |
| 4 | WhatsApp - lembrete de levantamento parado | `frontend/src/lib/whatsapp/templates.ts:157`, `frontend/src/pages/reparacoes/ReparacaoDetalhe.tsx:437` | Lembrete aparece se `staleDays >= 7`. | Threshold configuravel: 3/5/7/14 dias; desligar lembretes; texto proprio. | 3-5h | Media |
| 5 | Push notifications de estado | `backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs:525`, `backend/src/RepairDesk.Services/Push/PushNotificationService.cs:195` | Qualquer mudanca de estado enfileira push; titulo/body sao fixos. | On/off global; estados permitidos; texto por estado; nao enviar em estados internos. | 8-12h | Alta |
| 6 | Portal cliente - card de push | `frontend/src/pages/PortalCliente.tsx:100`, `backend/src/RepairDesk.Services/Push/PushOptions.cs:12` | Card de subscricao aparece sempre; retencao delivered e global. | Mostrar/esconder push no portal; retencao por tenant; copy do pedido. | 4-7h | Media |
| 7 | Portal cliente - visibilidade de secoes | `frontend/src/pages/PortalCliente.tsx:104`, `frontend/src/pages/PortalCliente.tsx:144`, `frontend/src/pages/PortalCliente.tsx:172`, `frontend/src/pages/PortalCliente.tsx:176` | Portal mostra orcamento, diagnostico, fotos, garantia e avaliacao quando dados existem. | Toggles: mostrar fotos, diagnostico, orcamento, garantia, timeline, avaliacao e Google review. | 8-12h | Alta |
| 8 | Portal cliente - aprovar orcamento online | `backend/src/RepairDesk.Services/PublicPortal/PublicPortalService.cs:195`, `frontend/src/pages/PortalCliente.tsx:54` | Se ha orcamento no estado Orcamento, cliente pode aceitar/recusar. | Tenant decide se portal permite aprovar online ou apenas ver/contactar loja. | 4-6h | Media |
| 9 | Pedido de avaliacao / Google Review | `backend/src/RepairDesk.Services/PublicPortal/PublicPortalService.cs:132`, `frontend/src/pages/PortalCliente.tsx:523` | Score 4-5 redirecciona para Google se URL existir. | Threshold 4/5; desligar pedido; texto e timing; pedir so apos Entregue+Pago. | 4-7h | Media |
| 10 | PDF Orcamento - layout/copy | `backend/src/RepairDesk.Services/Documents/OrcamentoPdfService.cs:197`, `backend/src/RepairDesk.Services/Documents/OrcamentoPdfRenderer.cs:130`, `backend/src/RepairDesk.Services/Documents/OrcamentoPdfRenderer.cs:146` | Usa logo/cor/termos/IBAN do tenant, mas estrutura, labels e footer sao fixos. | Termos por tipo documento; footer custom; mostrar/esconder IBAN; texto de validade do orcamento. | 8-12h | Alta |
| 11 | PDF Orcamento - "Gerado pelo Mender" | `backend/src/RepairDesk.Services/Documents/OrcamentoPdfRenderer.cs:233` | Footer fixo com Mender. | White-label parcial: "Gerado por Mender" on/off conforme plano; texto de rodape custom. | 2-4h | Media |
| 12 | PDF Garantia - cobertura/exclusoes fallback | `backend/src/RepairDesk.Services/Garantias/GarantiaService.cs:136`, `backend/src/RepairDesk.Services/Documents/GarantiaPdfRenderer.cs:206` | Cobertura/exclusoes podem vir do tenant, mas fallback e layout sao fixos. | Texto legal default editavel por reparacao/venda; campos de contacto RMA; CTA para acionar garantia. | 6-10h | Alta |
| 13 | PDF Venda / recibo nao fiscal | `backend/src/RepairDesk.Services/Documents/VendaPdfService.cs:136`, `backend/src/RepairDesk.Services/Documents/VendaPdfService.cs:168` | Texto "Documento nao fiscal..." e footer sao fixos. | Texto de recibo, footer, mostrar garantia, contacto, politica devolucoes. | 5-8h | Media |
| 14 | Reparacoes - estado inicial | `backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs:391`, `frontend/src/lib/reparacoes/types.ts:70` | Estado inicial so pode ser Recebido ou Orcamento; UI assume workflow fixo. | Default por tenant: criar em Recebido ou Orcamento; permitir esconder estados que a loja nao usa. | 8-14h | Media |
| 15 | Reparacoes - transicoes de estado | `backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs:903`, `frontend/src/lib/reparacoes/types.ts:70` | Transicoes validas estao hardcoded backend + frontend. | Cuidado: nao tornar tudo livre ja. Fase 2 pode permitir so "estados visiveis" e shortcuts, nao workflow arbitrario. | 16-28h | Baixa |
| 16 | Reparacoes - Entregue marca Pago | `backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs:493`, `frontend/src/pages/reparacoes/ReparacaoDetalhe.tsx:714` | Ao marcar Entregue, se estava NaoPago passa para Pago por defeito. | Toggle: "ao entregar, assumir pago"; "perguntar sempre"; "nunca alterar pagamento". | 4-7h | Alta |
| 17 | Reparacoes - garantia automatica | `backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs:507`, `backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs:532` | Ao Entregue, cria garantia se necessario. | Toggle: criar garantia automaticamente, perguntar, ou nunca criar; dias/textos ja existem. | 5-8h | Alta |
| 18 | Diagnostico - template default | `backend/src/RepairDesk.Services/Diagnostico/DiagnosticoService.cs:91`, `backend/src/RepairDesk.Services/Diagnostico/DiagnosticoService.cs:94` | Se nao for passado template, usa default por categoria; se faltar, falha. | Toggle "diagnostico obrigatorio"; skip para reparacoes triviais; template default por categoria ja quase existe. | 5-9h | Alta |
| 19 | Diagnostico - campos de equipamento | `backend/src/RepairDesk.Services/EquipmentFields/EquipmentFieldService.cs:25`, `backend/src/RepairDesk.Services/EquipmentFields/EquipmentFieldService.cs:307` | Templates editaveis; limite 10 templates e defaults seed hardcoded. | Limites ficam internos; talvez permitir "reset defaults" e duplicar template. Nao e urgente. | 3-5h | Baixa |
| 20 | Vendas - garantia automatica | `backend/src/RepairDesk.Services/Vendas/VendaService.cs:258`, `backend/src/RepairDesk.Services/Vendas/VendaService.cs:275` | Ao marcar venda como paga, emite garantia automaticamente. | Toggle: criar garantia automaticamente, perguntar no POS, ou nunca criar. | 5-8h | Alta |
| 21 | Vendas - condicao default do artigo | `backend/src/RepairDesk.Services/Vendas/VendaService.cs:188`, `backend/src/RepairDesk.Services/Vendas/VendaService.cs:369`, `frontend/src/pages/vendas/Vendas.tsx:442` | Se nao for definida, assume `NaoAplicavel`; calculo de garantia trata como novo. | Default por tenant: Novo, Usado, Recondicionado, Nao aplicavel; prompt obrigatorio em produtos usados. | 4-7h | Alta |
| 22 | POS - metodo pagamento default | `frontend/src/pages/vendas/Vendas.tsx:49`, `backend/src/RepairDesk.Core/Entities/Venda.cs:17` | UI default e MBWay; entity default e Outro. | Default POS por tenant: Dinheiro/MBWay/Multibanco/Cartao/Transferencia. | 2-4h | Alta |
| 23 | Vendas - emitir fatura ao cobrar | `backend/src/RepairDesk.Services/Vendas/VendaDtos.cs:27`, `backend/src/RepairDesk.Services/Vendas/VendaService.cs:262`, `frontend/src/pages/vendas/Vendas.tsx:306` | Pedido suporta `EmitirFatura`, mas UI principal e manual/confirmacao apos venda. | Preferencia: nunca, perguntar, emitir automaticamente se provider activo. | 4-8h | Alta |
| 24 | Faturacao - cliente fallback | `backend/src/RepairDesk.Services/Billing/MoloniBillingProvider.cs:217`, `backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs:975`, `backend/src/RepairDesk.Services/Vendas/VendaService.cs:745` | Se sem NIF, tenta Consumidor Final 999999990. | Ja ha `FallbackCustomerId`; adicionar politica: bloquear sem NIF, usar fallback, ou pedir cliente. | 4-7h | Media |
| 25 | Faturacao - heuristicas Moloni auto-discovery | `backend/src/RepairDesk.Services/Billing/TenantBillingSettingsService.cs:276`, `backend/src/RepairDesk.Services/Billing/TenantBillingSettingsService.cs:677`, `backend/src/RepairDesk.Services/Billing/TenantBillingSettingsService.cs:687`, `backend/src/RepairDesk.Services/Billing/TenantBillingSettingsService.cs:697` | Procura "Servico de reparacao", Numerario, Pronto pagamento, Consumidor Final. | Deixar como setup assistant, mas permitir override e re-run por categoria. Nao precisa toggle agora. | 3-6h | Baixa |
| 26 | Stock - baixo stock | `backend/src/RepairDesk.Services/Parts/PartService.cs:116`, `backend/src/RepairDesk.Services/Parts/PartService.cs:251`, `backend/src/RepairDesk.DAL/Persistence/PartRepository.cs:65` | `QtdMinima=0` desliga alerta por peca; eventos webhook stock baixo publicam quando cruza limite. | Defaults por categoria; ligar/desligar alerta global; canal de alerta; frequencia anti-spam. | 8-12h | Alta |
| 27 | Stock - janela reabastecer | `backend/src/RepairDesk.Services/Parts/PartService.cs:25`, `backend/src/RepairDesk.Services/Parts/PartService.cs:73`, `frontend/src/pages/Dashboard.tsx:86` | Service default 30d e UI dashboard chama 30d. | Preferencia 14/30/60/90 dias por tenant; aplicar em dashboard e stock. | 3-5h | Media |
| 28 | Stock - SKU automatico | `backend/src/RepairDesk.Services/Parts/PartService.cs:86`, `backend/src/RepairDesk.Services/Parts/PartService.cs:425` | Prefixos por categoria sao hardcoded. | Prefixo SKU por categoria, formato com ano/contador, ou desligar auto-SKU. | 6-10h | Baixa |
| 29 | Loja online - default publicar produtos | `backend/src/RepairDesk.Core/Entities/Product.cs:78`, `frontend/src/pages/produtos/Produtos.tsx:65`, `backend/src/RepairDesk.Services/Products/ProductService.cs:744`, `backend/src/RepairDesk.Services/Products/ProductService.cs:872` | Produtos manuais defaultam mostrar online true; imports dropship false; migracao online true. Pecas defaultam false. | Default por tenant por origem: produto manual, CSV, dropship, peca. | 5-8h | Media |
| 30 | Loja online - webhooks catalogo | `backend/src/RepairDesk.Services/Products/ProductService.cs:254`, `backend/src/RepairDesk.Services/Parts/PartService.cs:113`, `frontend/src/pages/definicoes/Webhooks.tsx:192` | Webhooks ja sao opt-in por evento; eventos catalogo publicam quando flag online muda. | Ja adequado. Talvez adicionar preset "Loja online" que selecciona eventos certos. | 3-5h | Baixa |
| 31 | Shop AI - persona da loja | `backend/src/RepairDesk.Services/Shop/ShopAiService.cs:63`, `backend/src/RepairDesk.Services/Shop/ShopAiService.cs:74`, `backend/src/RepairDesk.Services/Shop/ShopAiService.cs:81` | Prompt fixa LopesTech, Viseu, contacto e tom. | Persona por tenant: nome, cidade, contactos, tom, politica de recomendacao, fallback fora de escopo. | 8-14h | Media |
| 32 | SEO AI de produtos | `backend/src/RepairDesk.Services/Products/AnthropicAltTextService.cs:85`, `backend/src/RepairDesk.Services/Products/AnthropicAltTextService.cs:105`, `backend/src/RepairDesk.Services/Products/AnthropicAltTextService.cs:109` | Prompt menciona LopesTech e garantia 36 meses. | Brand/tom/estrutura/garantia por tenant; opcao "descricao curta vs completa". | 6-10h | Media |
| 33 | Importacao faturas fornecedor - LLM fallback | `backend/src/RepairDesk.Services/Documents/SupplierInvoiceImportService.cs:288`, `backend/src/RepairDesk.Services/Documents/SupplierInvoiceImportService.cs:794` | Usa LLM quando parser vazio/baixa confianca e key disponivel. | Toggle por tenant: permitir IA em faturas; usar sempre em baixa confianca; nunca enviar para IA. | 5-8h | Media |
| 34 | Retencao faturas fornecedor | `backend/src/RepairDesk.Core/Entities/Tenant.cs:70`, `frontend/src/pages/definicoes/Definicoes.tsx:1440` | Ja configuravel por tenant. | Manter; talvez presets "conservador / agressivo". | 1-2h | Baixa |
| 35 | Dashboard - janelas operacionais | `frontend/src/pages/Dashboard.tsx:80`, `frontend/src/pages/Dashboard.tsx:86`, `frontend/src/pages/Dashboard.tsx:247` | Dashboard usa 7d/30d fixos em varios widgets. | Preferencias: janela alertas 15/30/60d, top N, widgets visiveis. | 6-10h | Media |
| 36 | Tema/UI | `frontend/src/lib/theme.ts:1`, `frontend/src/components/Layout.tsx:85`, `backend/src/RepairDesk.Services/TenantSettings/TenantSettingsService.cs:67` | Tema e per-user localStorage; logo/cor existem mas nao governam toda a app. | Tema default por tenant, cor primaria aplicada a app/portal/PDF; user pode override light/dark. | 8-12h | Media |
| 37 | Onboarding wizard | `backend/src/RepairDesk.Services/TenantSettings/TenantSettingsService.cs:147`, `backend/src/RepairDesk.Services/TenantSettings/TenantSettingsService.cs:150`, `frontend/src/pages/OnboardingWizard.tsx:255` | 5 passos; dashboard/equipa marcados completos quando onboarding completo. | Wizard por perfil: loja pequena, loja com equipa, so reparacoes, com POS. Nao urgente para beta se wizard actual funcionar. | 8-12h | Baixa |
| 38 | Backups | `backend/src/RepairDesk.API/Program.cs:65`, `frontend/src/pages/definicoes/Definicoes.tsx:2077` | Config global/admin por ambiente. | Nao parece preferencia de tenant no SaaS MVP; manter global. | 0-2h | Baixa |

## Shortlist recomendada para Fase 2

Implementar por ordem de impacto percebido na loja:

1. Comunicacoes WhatsApp
   - `WhatsappEnabled`
   - templates por estado
   - estados activos
   - modo `sempre`/`uma vez`
   - threshold de lembrete para `Pronto`

2. Portal cliente e notificacoes
   - toggles de visibilidade: fotos, diagnostico, orcamento, garantia, avaliacao
   - permitir/desligar aprovacao de orcamento online
   - push on/off + estados que enviam

3. Automacoes criticas de reparacao/venda
   - Entregue marca Pago: sim/perguntar/nao
   - garantia automatica em reparacoes: sim/perguntar/nao
   - garantia automatica em vendas: sim/perguntar/nao

4. POS e faturacao operacional
   - metodo de pagamento default
   - condicao default do artigo
   - emitir fatura ao cobrar: nunca/perguntar/automatico

5. PDFs
   - footer custom
   - termos por documento
   - recibo venda / garantia com copy configuravel

6. Stock
   - QtdMinima default por categoria
   - janela de reabastecimento
   - alertas stock baixo on/off

## Estimativa

| Bloco | Estimativa |
|---|---:|
| Prioridade alta apenas | 56-86h |
| Prioridade media | 55-88h |
| Prioridade baixa | 45-78h |
| Total se fosse tudo | 156-252h |

Para uma Fase 2 realista antes de beta, eu faria so a shortlist 1-4: cerca de 35-55h, com defaults iguais ao comportamento actual para nao partir o tenant LopesTech.

## Modelo de implementacao sugerido

Criar um helper unico para preferencias, em vez de espalhar regras pelos componentes:

- `ITenantPreferencesService.GetAsync()` devolve defaults materializados.
- `TenantPreferences` guarda grupos: `Communication`, `Portal`, `Repairs`, `Sales`, `Documents`, `Stock`, `Shop`.
- Defaults sao os comportamentos actuais. Exemplo: WhatsApp ligado, todos os templates actuais disponiveis, push activo em mudanca de estado, Entregue marca Pago, garantia automatica ligada.
- UI em `/definicoes` usa auto-save, com "Restaurar default Mender" por grupo.
- Preferencias legais/fiscais sensiveis continuam em campos explicitos (`Tenant`, `TenantBillingSettings`) quando ja existem.

Opcao tecnica preferida: um JSON versionado por tenant para preferencias muito mutaveis (`PreferencesJson` + `PreferencesVersion`). Isto reduz migrations pequenas a cada novo toggle. Se Bruno preferir reporting SQL directo sobre settings, usar colunas/owned types, mas vai criar mais migrations.

## Cuidados para Fase 2

- Defaults devem reproduzir 100% o comportamento actual do tenant LopesTech.
- Nao criar coluna `NOT NULL` sem default.
- Nao deixar frontend decidir sozinho regras importantes; backend tem de validar preferencias de automacao.
- Para WhatsApp "uma vez", e preciso guardar historico de notificacao; so abrir `wa.me` nao prova envio real. O nome certo e "marcar como sugerido/enviado manualmente".
- Nao permitir customizacao que reduza garantia legal abaixo do minimo quando aplicavel a consumidor.
- Portal cliente precisa de defaults conservadores: esconder algo nunca deve apagar dados, apenas nao expor publicamente.
- Separar "preferencia pessoal" de "compliance/fiscal": faturacao legal e RGPD nao devem virar toggles perigosos.
