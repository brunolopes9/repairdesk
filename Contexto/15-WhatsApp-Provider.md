# WhatsApp Business API - Provider para automação

Atualizado: 2026-05-16

Objetivo: decidir como o RepairDesk deve automatizar mensagens WhatsApp por estado da reparação, saindo dos links `wa.me` manuais para uma integração API segura, barata e escalável.

Resumo curto: **Fase 1 deve usar Meta WhatsApp Cloud API direta**, com número dedicado LopesTech/RepairDesk e templates utility mínimos. Só mudar para 360dialog/Twilio/Bird quando houver dor operacional clara: suporte, multi-tenant onboarding, shared inbox, SLAs ou muitos números de lojas.

## 1. Mudança importante de pricing

Historicamente a WhatsApp Business Platform cobrava por "conversa" de 24h. Em 2025/2026 a Meta passou a comunicar pricing mais orientado a **mensagem/template entregue** para marketing, utility e authentication em vários mercados. A própria página oficial diz que as empresas são cobradas por mensagem entregue, com preço por país e categoria.

Para Portugal, a tabela oficial interativa da Meta deve ser sempre a fonte final antes de produção. Como Portugal aparece muitas vezes agregado em **Rest of Western Europe**, usar estes valores como cenário de planeamento:

| Categoria | Valor base usado no plano | Nota |
|---|---:|---|
| Utility | 0,0142 EUR/mensagem entregue | Rest of Western Europe, EUR, Jan 2026. Confirmar na rate card oficial Meta antes de ativar cobrança. |
| Marketing | 0,0490 EUR/mensagem entregue | Evitar quase tudo no RepairDesk cedo. |
| Authentication | 0,0142 EUR/mensagem entregue | Não é prioridade no RepairDesk agora. |
| Service | 0 EUR Meta dentro da janela de 24h iniciada pelo cliente | Respostas livres a mensagens do cliente; provider pode cobrar fee própria. |

Importante: se um template for submetido como utility mas a Meta o reclassificar como marketing, o custo pode multiplicar por ~3,5x. Esta é uma das maiores armadilhas.

## 2. Comparação de providers

| Opção | Base mensal | Fee provider | Meta fees | Setup | .NET | Suporte PT-PT | Lock-in | Veredicto |
|---|---:|---:|---:|---|---|---|---|---|
| Meta Cloud API direta | 0 EUR | 0 | Direto Meta | Médio | REST/HttpClient | Não | Baixo | Melhor Fase 1. |
| Twilio WhatsApp | 0 EUR | 0,005 USD por mensagem inbound/outbound + Meta | Pass-through | Fácil/médio | SDK .NET forte | Não PT-PT | Médio | Bom DX, fica caro com volume. |
| Bird/MessageBird | 0-49 USD/mês conforme plano | Fee por 1000 mensagens + Meta | Pass-through | Médio | API/SDKs, confirmar .NET | Não PT-PT | Médio/alto | Bom omnichannel, pode ser overkill. |
| 360dialog | 49 EUR/mês/número | 0 markup declarado nas Meta fees | Pass-through | Fácil/médio | REST | Não PT-PT | Médio | Melhor BSP developer/EU se direta doer. |
| Wati | ~59 USD+/mês {{confirmar}} | Plano + messaging fees | Pass-through/markup {{confirmar}} | Fácil | API, menos developer-first | Não PT-PT | Alto | Bom low-code, mau para SaaS controlado. |
| Vonage | 0 EUR base clara {{confirmar}} | Platform fee por mensagem + Meta | Pass-through | Médio | SDK .NET existe para APIs Vonage | Não PT-PT | Médio | Sólido, mas menos simples que Twilio/360dialog. |

### Meta Cloud API direta

**Preço**

- Sem mensalidade de provider.
- Pagas diretamente as tarifas Meta por mensagem/template entregue.
- Para Portugal, usar cenário de planeamento Rest of Western Europe: utility 0,0142 EUR; marketing 0,0490 EUR; authentication 0,0142 EUR; service gratuito dentro da janela de atendimento.

**Setup time**

- Teste com número sandbox: no próprio dia.
- Produção com número real, Business Manager, display name, pagamento e templates: 1-7 dias se a conta Meta estiver limpa; 1-3 semanas se houver problemas de verificação.

**Verificação Meta Business**

Passos:

1. Criar/usar Meta Business Portfolio da LopesTech.
2. Confirmar dados legais: nome, morada, website/domínio, telefone.
3. Adicionar método de pagamento.
4. Criar app Meta Developers.
5. Adicionar produto WhatsApp.
6. Criar WhatsApp Business Account.
7. Adicionar número dedicado.
8. Verificar número por SMS/chamada.
9. Registar número com PIN de 2FA.
10. Configurar webhook HTTPS público.
11. Submeter templates PT-PT.

**Templates**

- Criados no WhatsApp Manager ou via Graph API.
- A aprovação pode ser rápida, às vezes minutos/horas para utility; assumir 24-48h.
- Marketing é mais sujeito a rejeição e reclassificação.

**Webhooks**

- Suporta inbound messages e status de mensagens.
- Estados típicos: `sent`, `delivered`, `read`, `failed`.
- Regra: resposta 200 OK rápida, processamento assíncrono em job.

**.NET**

- Não há SDK .NET oficial relevante.
- Usar `HttpClient` + typed options + background jobs.
- Isto é aceitável: a API é HTTP/JSON simples.

**Suporte**

- Suporte Meta é fraco para small business.
- Vantagem: zero intermediário e zero markup.

**Opinião**

É o caminho certo para Bruno agora. O RepairDesk ainda não precisa de inbox omnichannel nem partner platform. Precisa de validar 3-5 templates, opt-in, logs e custos reais.

### Twilio WhatsApp

**Preço**

- Twilio cobra 0,005 USD por mensagem WhatsApp inbound ou outbound.
- Além disso, passa as fees Meta por template.
- Failed message processing fee: 0,001 USD quando termina em failed.
- Sem mensalidade obrigatória clara para Programmable Messaging.

Exemplo: utility para Portugal fica aproximadamente `Meta 0,0142 EUR + Twilio 0,005 USD` por mensagem, antes de câmbio/impostos.

**Setup time**

- Sandbox rápido.
- Produção: 1-7 dias se Meta/WABA/número estiverem OK.

**Templates**

- Submissão via Twilio Console.
- Twilio ajuda a abstrair parte do processo.
- Categoria pode ser alterada pela Meta.

**Webhooks**

- Muito bons: status callbacks, inbound webhooks, retries.

**.NET**

- Twilio tem SDK .NET maduro.
- Excelente DX para equipa pequena.

**Suporte PT-PT**

- Não há suporte PT-PT dedicado para pequena conta.

**Opinião**

Bom se o Bruno quiser menos fricção técnica e aceitar pagar markup por mensagem. Para 100 lojas, o fee fixo por mensagem começa a pesar. Eu não começaria por aqui, mas manteria como Plano B.

### Bird / MessageBird

**Preço**

- Bird mostra plano Free e Pro a 49 USD/mês.
- WhatsApp tem Bird processing fees por volume + vendor passthrough Meta.
- Fee publicada: 0,001 USD por mensagem nos primeiros 1.000; 0,005 USD por mensagem de 1.001 a 100.000; 0,0045 depois; 0,004 a partir de 500.001.
- Detalhe por país/categoria exige pricing page/login/contact sales em alguns fluxos.

**Setup time**

- 1-7 dias típico; pode ser mais se precisar aprovação/contrato.

**Templates e webhooks**

- Suporta templates, automations, inbox e webhooks.
- Mais plataforma do que "pipe".

**.NET**

- API HTTP utilizável; SDK .NET deve ser confirmado.

**Suporte PT-PT**

- Sem suporte PT-PT real para Bruno.

**Opinião**

Bom se o RepairDesk quiser omnichannel, CRM/inbox e automações visuais. Para enviar 3-6 notificações por reparação, é pesado.

### 360dialog

**Preço**

- Regular: 49 EUR/mês por número.
- Premium: 99 EUR/mês por número.
- High Throughput: 299 EUR/mês por número.
- Declara pass-through das fees Meta sem markup em messaging.

**Setup time**

- Embedded signup pode ser rápido quando a conta Meta está pronta.
- Produção real: assumir 1-5 dias.

**Templates**

- Via 360dialog hub/API.
- Continua dependente da aprovação Meta.

**Webhooks**

- Suporta webhooks de mensagens e status.

**.NET**

- REST simples. Sem necessidade de SDK específico.

**Suporte PT-PT**

- Não PT-PT, mas empresa europeia/developer-focused.

**Opinião**

Melhor alternativa à Cloud API direta. Se Bruno ficar preso em verificação Meta, webhooks, suporte ou número, 360dialog é o BSP mais limpo para developer. O custo fixo de 49 EUR/mês por número só compensa quando o WhatsApp já estiver a trazer valor.

### Wati

**Preço**

- Planos Growth/Pro/Business; fontes de mercado apontam Growth ~59 USD/mês anual, mas confirmar no site no momento de compra.
- Além do plano, há messaging fees por país/categoria.
- Pode existir markup/créditos internos; confirmar antes de usar.

**Setup time**

- Rápido para PMEs: 1-3 dias se tudo correr bem.

**Templates e webhooks**

- Interface low-code boa para equipas não técnicas.
- API existe, mas a proposta de valor é inbox/automação pronta.

**.NET**

- Usável via API, mas menos natural para produto SaaS custom.

**Suporte PT-PT**

- Não.

**Opinião**

Não recomendo para RepairDesk core. Wati é útil para uma loja que quer uma ferramenta WhatsApp pronta. O RepairDesk precisa de controlar o fluxo dentro do seu produto, evitar lock-in e evitar pagar por UI duplicada.

### Vonage WhatsApp Business

**Preço**

- Duas camadas: Meta fee + Vonage platform fee.
- Vonage publicou platform fees 2026 por país/região e categoria em USD.
- Para países/regiões europeias, a platform fee pode ser significativa. Portugal específico deve ser confirmado com pricing page/contact sales.

**Setup time**

- 3-10 dias típico.

**Templates e webhooks**

- Suporta templates, interactive messages, inbound e status.
- Developer docs boas.

**.NET**

- Vonage tem SDKs e APIs maduras; para Messages API pode ser SDK ou HTTP direto.

**Suporte PT-PT**

- Não.

**Opinião**

Boa plataforma CPaaS, mas para RepairDesk não vejo vantagem clara sobre Twilio ou 360dialog. Manter como opção se já houver conta Vonage ou necessidade forte de fallback SMS/voice no mesmo fornecedor.

## 3. Decisão sobre número dedicado

### Bruno pode usar o número pessoal/LopesTech atual?

Recomendação: **não**.

Razões:

1. O número fica preso ao WABA/API/provider e migrações podem ser chatas.
2. Se houver problemas de qualidade, bloqueios ou template abuse, afeta o número principal da LopesTech.
3. Separar manual/humano de automação reduz risco operacional.
4. SaaS multi-tenant no futuro precisa de arquitetura por número/tenant, não o telefone pessoal do Bruno.

Nota: existe "coexistence" em alguns fluxos, permitindo WhatsApp Business App + API no mesmo número, mas eu não usaria no número principal nesta fase. Ainda há restrições, edge cases e dependência do provider/Embedded Signup.

### Onde comprar número dedicado em PT?

Opções práticas:

| Opção | Custo estimado | Prós | Contras |
|---|---:|---|---|
| SIM/eSIM pré-pago MEO/NOS/Vodafone/low-cost | 5-15 EUR/mês ou carregamentos | Número português real, fácil de verificar por SMS/chamada. | Precisa manter ativo; gestão manual. |
| Número Twilio PT clean mobile | 15 USD/mês | Programático, SMS/voice API. | Mais caro; confirmar se elegível para WhatsApp e documentação PT. |
| Número virtual Vonage/outro CPaaS | {{confirmar}} | API-friendly. | Pode falhar verificação/WhatsApp se virtual/IVR; validar antes. |

Recomendação: comprar um **SIM/eSIM português dedicado** em nome da LopesTech, sem WhatsApp pessoal instalado. Exemplo de nome público: `LopesTech Reparações` ou futuro nome do SaaS. Manter o cartão ativo e guardado para receber códigos de verificação.

## 4. Estratégia de templates

Fonte: `Contexto/11-WhatsApp-Templates.md`.

### Submeter primeiro como utility

Utility deve ser transacional, específico ao serviço e esperado pelo cliente.

| Template | Categoria sugerida | Automatizar? | Nota |
|---|---|---|---|
| Recebido | Utility | Sim | Confirma entrada do equipamento. |
| Orcamento | Utility | Sim/sugestão forte | Inclui valor e link de aprovação; evitar linguagem promocional. |
| AguardaPeca | Utility | Sim se peça/prazo definidos | Atualização operacional. |
| Pronto | Utility | Sim | Notificação mais valiosa. |
| Lembrete de levantamento | Utility | Sim, 7 dias depois | Cuidado para soar operacional, não pressão comercial. |
| Atrasou-se a peça | Utility | Sugestão/manual | Sensível, mas transacional. |
| Prazo vai derrapar | Utility | Sugestão/manual | Transacional. |
| Não conseguimos reparar | Utility | Manual | Pode gerar resposta emocional; humano revê. |

### Evitar ou tratar como marketing

| Template | Categoria provável | Recomendação |
|---|---|---|
| Pedido de Google Review | Marketing ou utility rejeitado/reclassificado | Não automatizar na Fase 1; usar manual ou email. |
| Follow-up 30/90/180 dias | Marketing/utility sensível | Adiar; precisa opt-in separado. |
| Promoções, upsell bateria, campanhas | Marketing | Fora da Fase 1. |

### Service messages

Service não é template iniciada pela loja. É resposta livre dentro de 24h depois do cliente mandar mensagem.

Uso no RepairDesk:

- Se cliente responde "Aprovo", abre janela de atendimento.
- Dentro das 24h, a loja pode responder livremente.
- RepairDesk deve registar a janela, mas não depender dela para mensagens críticas.

### Regras de variáveis

Boas práticas:

- Usar poucas variáveis: `{{1}}`, `{{2}}`, `{{3}}`.
- Fornecer exemplos reais na submissão.
- Não começar ou acabar a mensagem só com variável.
- Não usar variáveis para esconder conteúdo promocional.
- Evitar IMEI, passwords, PINs, moradas completas e detalhes sensíveis.
- Texto fixo deve explicar o contexto: "confirmamos a entrada", "está pronto", "aguarda peça".

Template inicial recomendado:

```text
Olá {{1}}, confirmamos a entrada do teu {{2}} na {{3}}. Vamos começar a análise e avisamos-te por aqui quando houver novidades.
```

Exemplos:

- `{{1}}` = Bruno
- `{{2}}` = telemóvel iPhone 12
- `{{3}}` = LopesTech

## 5. Compliance RGPD e opt-in

### Opt-in

No RepairDesk, no momento de criar reparação:

- Checkbox 1: `Aceito receber atualizações sobre esta reparação por WhatsApp.`
- Checkbox 2 separado: `Aceito receber pedidos de avaliação e comunicações ocasionais da loja.`

Guardar:

- texto aceite;
- data/hora;
- telefone;
- tenant/loja;
- utilizador que recolheu;
- origem: balcão, portal cliente, importação, link público.

### STOP / descadastramento

Implementar lista de bloqueio por tenant:

- Se cliente responder `STOP`, `PARAR`, `REMOVER`, `NÃO`, `NAO`, `CANCELAR`, marcar `WhatsAppOptOutAt`.
- Bloquear envios automáticos futuros.
- Permitir envio manual apenas com aviso forte.
- Para mensagens transacionais críticas, usar fallback: chamada, SMS ou email conforme consentimento/base legal.

### Onde ficam os dados

| Opção | Dados passam por | RGPD |
|---|---|---|
| Meta Cloud API direta | RepairDesk + Meta/WhatsApp | Menos subcontratantes; Meta entra na política de privacidade/subprocessadores. |
| Twilio | RepairDesk + Twilio + Meta | Twilio como subprocessador adicional. |
| Bird | RepairDesk + Bird + Meta | Bird como subprocessador adicional. |
| 360dialog | RepairDesk + 360dialog + Meta | 360dialog como subprocessador adicional, EU-friendly. |
| Wati | RepairDesk + Wati + Meta | Wati fora da UE; validar DPA/transferências. |
| Vonage | RepairDesk + Vonage + Meta | Vonage como subprocessador adicional. |

Regra de produto: minimizar conteúdo da mensagem. A mensagem deve dizer estado, equipamento genérico e link; o detalhe sensível fica no portal RepairDesk autenticado/tokenizado.

## 6. Integração técnica RepairDesk

### Onde ligar no código

O ponto natural é depois de `ChangeEstadoAsync` em:

- `RepairDesk/backend/src/RepairDesk.Services/Reparacoes/ReparacaoService.cs`

Hoje o serviço:

- valida transição;
- cria `ReparacaoEstadoLog`;
- atualiza `rep.Estado`;
- guarda alterações.

Não enviar WhatsApp síncrono dentro do request HTTP. Fazer assim:

1. `ChangeEstadoAsync` muda estado e grava log.
2. Cria `OutboundMessage`/`NotificationJob` pendente na base de dados.
3. Background worker processa a fila.
4. Provider envia.
5. Webhook atualiza status.

### Modelo de dados sugerido

Tabela `OutboundMessages`:

| Campo | Tipo | Nota |
|---|---|---|
| Id | Guid | |
| TenantId | Guid | multi-tenant |
| RepairId | Guid? | ligação à reparação |
| CustomerId | Guid? | ligação ao cliente |
| Channel | enum | WhatsApp, SMS, Email |
| Provider | enum | MetaCloud, Twilio, Dialog360, etc. |
| TemplateKey | string | `repair_received_pt`, `repair_ready_pt` |
| TemplateCategory | enum | Utility, Marketing, Authentication, Service |
| ToPhoneE164 | string | ex. `+3519...` |
| PayloadJson | string | variáveis e preview renderizado |
| Status | enum | Pending, Sending, Sent, Delivered, Read, Failed, Cancelled |
| ProviderMessageId | string? | wamid/twilio sid/etc. |
| ErrorCode | string? | |
| ErrorMessage | string? | |
| AttemptCount | int | |
| NextAttemptAt | DateTime? | |
| SentAt/DeliveredAt/ReadAt/FailedAt | DateTime? | auditoria |

Tabela `WhatsAppOptIns` ou campos no cliente:

- `WhatsAppTransactionalOptInAt`
- `WhatsAppMarketingOptInAt`
- `WhatsAppOptOutAt`
- `WhatsAppOptInTextVersion`

### Serviço .NET

Interfaces:

```csharp
public interface IWhatsAppProvider
{
    Task<SendWhatsAppResult> SendTemplateAsync(SendWhatsAppTemplateCommand command, CancellationToken ct);
}

public interface INotificationQueue
{
    Task EnqueueRepairStatusChangedAsync(Guid repairId, RepairStatus from, RepairStatus to, CancellationToken ct);
}
```

Implementações:

- `MetaCloudWhatsAppProvider` com `HttpClient`.
- `TwilioWhatsAppProvider` opcional se mudar.
- `NullWhatsAppProvider` para dev/test.

### Quando enviar

| Evento | Envio automático Fase 1 | Condição |
|---|---|---|
| Recebido | Sim | opt-in transacional e telefone válido. |
| Diagnostico | Não por default | sugestão manual; pode cansar. |
| Orcamento | Sim/sugestão | exige valor ou link de aprovação. |
| AguardaPeca | Sim | exige `peca_nome` ou prazo. |
| EmReparacao | Não por default | ruído para muitas lojas. |
| Pronto | Sim | mais importante. |
| Entregue | Sim curto | sem pedir review. |
| Cancelado | Manual | contexto sensível. |

### Retries

| Erro | Retry? | Ação |
|---|---|---|
| 429/rate limit | Sim | exponential backoff. |
| 5xx provider | Sim | 3-5 tentativas. |
| Timeout | Sim | idempotência por `OutboundMessage.Id`. |
| Template not approved | Não | marcar failed + alerta admin. |
| Cliente opt-out | Não | bloquear. |
| Número inválido/sem WhatsApp | Não | sugerir SMS/email/chamada. |

### Webhooks

Endpoint:

- `POST /api/webhooks/whatsapp/meta`

Regras:

- Validar assinatura/token.
- Responder 200 OK rápido.
- Guardar payload bruto por tempo limitado para debugging.
- Atualizar `OutboundMessages` por `ProviderMessageId`.
- Deduplicar eventos.
- Nunca confiar só no `accepted`; sucesso real é `delivered`/`read`.

### Fallback

Fase 1:

- Se WhatsApp falhar, mostrar alerta na reparação: "WhatsApp não entregue".
- Botão manual `Abrir wa.me`.
- Email se o cliente tiver email e opt-in/necessidade operacional.

Fase 2:

- SMS fallback para `Pronto` e `Orcamento`, se autorizado.
- Twilio/Vonage podem facilitar SMS no mesmo stack, mas SMS PT é caro (~0,05 USD por segmento em Twilio para Portugal). Usar só para eventos críticos.

## 7. Estimativa de custo a 100 lojas

Assunções:

- 100 lojas.
- 3 reparações/dia/loja.
- 26 dias úteis/mês.
- 3 mensagens utility automáticas por reparação: Recebido, Orçamento/AguardaPeça, Pronto.
- Total: `100 * 3 * 26 * 3 = 23.400 mensagens utility/mês`.
- Custo Meta utility usado: 0,0142 EUR/mensagem.

### Meta Cloud API direta

| Item | Cálculo | Custo |
|---|---:|---:|
| Meta utility | 23.400 * 0,0142 EUR | 332,28 EUR/mês |
| Provider | 0 | 0 EUR |
| Número dedicado LopesTech | SIM/eSIM | 5-15 EUR/mês |
| Total estimado | | 337-347 EUR/mês |

Custo por loja: ~3,37 EUR/mês.  
Custo por reparação: ~0,043 EUR se 7.800 reparações/mês.

### Twilio

| Item | Cálculo | Custo |
|---|---:|---:|
| Meta utility | 23.400 * 0,0142 EUR | 332,28 EUR |
| Twilio fee | 23.400 * 0,005 USD | ~117 USD |
| Total estimado | câmbio não incluído | ~440-460 EUR/mês {{confirmar}} |

### 360dialog

Se houver **um número central**:

| Item | Cálculo | Custo |
|---|---:|---:|
| Meta utility | 23.400 * 0,0142 EUR | 332,28 EUR |
| 360dialog Regular | 1 * 49 EUR | 49 EUR |
| Total | | 381,28 EUR/mês |

Se houver **um número por loja**, 100 lojas * 49 EUR = 4.900 EUR/mês só de licença. Inviável no pricing atual. Para SaaS multi-tenant, no futuro cada loja deve ligar o seu próprio WABA/provider ou o custo tem de ser add-on pago.

### Wati/Bird/Vonage

Não fechar orçamento sem simular no pricing page/conta, porque há planos, credits, platform fees e possíveis markups.

Regra de margem:

- No Starter, WhatsApp automático deve ser add-on ou limitado.
- No Pro, incluir pacote pequeno: ex. 100 mensagens utility/mês.
- Acima disso, cobrar créditos a custo + 20-30%.

## 8. Recomendação

### Fase 1 - validar com Bruno/LopesTech

Usar:

- Meta Cloud API direta.
- Número dedicado português novo.
- 3 templates utility:
  - Recebido
  - Orçamento
  - Pronto
- Envio automático desligado por default; ativar por tenant/estado.
- Logs/auditoria desde o primeiro dia.
- Fallback manual `wa.me`.

Porquê:

- Sem mensalidade.
- Sem lock-in.
- Aprende-se a mecânica real da Meta.
- Custo mensal quase zero enquanto há baixo volume.
- Arquitetura fica limpa para trocar provider depois.

### Fase 2 - 100+ lojas

Há duas possibilidades:

1. **Continuar Cloud API direta** se o onboarding for centralizado e o RepairDesk enviar em nome de um número/notificação comum ou se cada loja configurar o seu WABA via documentação.
2. **Adicionar 360dialog Partner Platform ou Embedded Signup** se for necessário ligar números de muitas lojas com menos suporte manual.

Evitar:

- Um número 360dialog pago por loja sem cobrar add-on.
- Wati como core provider do SaaS.
- Marketing WhatsApp automático cedo.

## 9. Plano de setup em 1 dia

Dia 1:

1. Comprar SIM/eSIM dedicado LopesTech/RepairDesk.
2. Criar ou limpar Meta Business Portfolio da LopesTech.
3. Confirmar domínio/website público.
4. Criar app Meta Developers.
5. Adicionar produto WhatsApp.
6. Testar envio com número de teste Meta.
7. Configurar webhook local via ngrok/cloudflared só para teste.
8. Criar `MetaCloudWhatsAppProvider` experimental em branch.
9. Submeter primeiro template utility `repair_received_pt`.

Dia 2-3:

1. Registar número dedicado.
2. Configurar pagamento.
3. Submeter `repair_quote_pt` e `repair_ready_pt`.
4. Guardar status dos templates.
5. Testar webhooks `sent`, `delivered`, `read`, `failed`.

Semana 1:

1. Implementar `OutboundMessages`.
2. Implementar fila/retries.
3. Adicionar opt-in no cliente/reparação.
4. Enviar só para Bruno e 2-3 clientes reais com consentimento.
5. Medir custo, entrega e respostas.

Critério de saída:

- Bruno muda estado para `Pronto`.
- Cliente recebe WhatsApp sem Bruno abrir app.
- RepairDesk mostra `Sent -> Delivered/Read`.
- Se falhar, aparece fallback manual.

## 10. Fontes

- Meta/WhatsApp Business Platform Pricing: https://whatsappbusiness.com/products/platform-pricing/
- Meta pricing EUR Jan 2026, via rate card espelhada pela Gupshup, marcada como futura e a confirmar na Meta oficial: https://www.gupshup.ai/resources/wp-content/uploads/2025/12/EUR_Jan2026.pdf
- Twilio WhatsApp Pricing: https://www.twilio.com/en-us/whatsapp/pricing
- Bird Pricing: https://bird.com/pricing/
- Bird WhatsApp Pricing: https://bird.com/en-nl/pricing/whatsapp
- 360dialog Pricing docs: https://docs.360dialog.com/docs/pricing
- 360dialog public pricing: https://360dialog.com/pricing/
- Wati pricing structure: https://support.wati.io/en/articles/11462993-understanding-wati-s-pricing-structure
- Vonage WhatsApp platform fees: https://api.support.vonage.com/hc/en-us/articles/20773952146460-WhatsApp-Pricing-Vonage-Platform-Fees
- Vonage WhatsApp guide: https://developer.vonage.com/en/messages/concepts/whatsapp
- Twilio Portugal SMS/number pricing for fallback/number cost reference: https://www.twilio.com/en-us/sms/pricing/pt
- `Contexto/11-WhatsApp-Templates.md`
