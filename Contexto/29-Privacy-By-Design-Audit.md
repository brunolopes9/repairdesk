# Privacy by Design Audit Técnico

Atualizado: 2026-05-16

Projeto: RepairDesk SaaS PT  
Foco: arquitetura técnica, RGPD art. 25, 32, 17, 20, 33 e 34.  
Documento relacionado: `Contexto/16-Compliance-RGPD.md`.

> Isto não é parecer jurídico. É um audit técnico para tornar a arquitetura defensável antes de abrir a 2-3 lojas reais.

## Conclusão executiva

O RepairDesk já tem boas bases:

- multi-tenant por `TenantId` + global query filter em EF Core;
- soft-delete transversal em `BaseEntity`;
- refresh tokens guardados por hash, não plaintext;
- portal público limitado por slug não sequencial + rate limiting;
- DTO público evita NIF, telefone do cliente, custos internos e notas financeiras;
- Serilog já tem retenção curta de 14 ficheiros diários no arranque atual.

Mas ainda **não está pronto para beta com dados reais** do ponto de vista privacy by design. Os bloqueadores não são burocracia; são controlos técnicos em falta:

1. **Anonimização/apagamento RGPD real**: soft-delete não chega.
2. **Retenção e redaction de logs**: há pelo menos um log de login falhado com email em texto claro.
3. **Audit log funcional**: não existe trilho imutável de export, acesso admin, alteração de roles, leitura via portal público e pedidos RGPD.
4. **Backups encriptados + restore testado**: planeado, mas não implementado.
5. **Sub-processadores reais e regiões**: têm de estar fechados antes de dados reais.
6. **Portal público**: bom desenho inicial, mas deve ter opção PIN/últimos 4 dígitos de telefone para lojas que queiram mais privacidade.

Decisão recomendada: antes da beta, implementar **mínimo defensável**, não compliance enterprise.

## Estado observado no código

| Área | Estado |
|---|---|
| Multi-tenant | `AppDbContext` aplica query filter para `ITenantEntity.TenantId == CurrentTenantId` quando há tenant. |
| Soft-delete | `BaseEntity.IsDeleted`; deletes viram `EntityState.Modified` com `IsDeleted = true`. |
| Auth | ASP.NET Identity, password policy mínima, lockout 5 tentativas/15 min. |
| Refresh tokens | `TokenHash`, `CreatedByIp`, `RevokedByIp`; plaintext só em cookie HttpOnly. |
| Roles | Existem `Admin` e `Tecnico`, mas controllers usam apenas `[Authorize]`; sem policies finas por ação. |
| Public portal | `[AllowAnonymous]`, rate limit 30/min/IP, slug 10 chars com ~57 bits de entropia. |
| Logs | Serilog console + file, `retainedFileCountLimit: 14`; request logging genérico. |
| Dados públicos | `PublicRepairDto` expõe primeiro nome, equipamento, avaria, diagnóstico, orçamento/preço final e timeline. |
| Export | Export CSV geral de clientes e reparações existe; export individual RGPD não existe. |
| Audit | Campos `CreatedBy`/`UpdatedBy` existem mas não são preenchidos; não há tabela `AuditLog`. |
| Fotos/R2 | Planeado, ainda não implementado. |
| Backups | Planeado nos docs, ainda não implementado no código. |

## Mapa de dados pessoais

| Entidade / sistema | Dados pessoais | Categoria | Onde está | Retenção recomendada | Acesso normal | Risco |
|---|---|---|---|---|---|---|
| `AppUser` | email, username, display name, `LastLoginIp`, roles, password hash | identificação, contacto, segurança | SQL Server `Auth_Users` | duração da conta + 90 dias; security logs 12 meses | utilizador próprio, admin tenant, Bruno técnico limitado | conta SaaS comprometida ou enumeração por email |
| `RefreshToken` | userId, tenantId, hash token, IP criação/revogação | segurança | SQL Server `Auth_RefreshTokens` | até expirar + 30 dias; eventos segurança 12 meses | sistema, Bruno técnico em incidente | IP é dado pessoal; bom estar hash token |
| `Tenant` | nome legal, NIF, morada, telefone, email, IBAN, CAE, website, logo | identificação empresa/ENI/contacto/financeiro | SQL Server `Tenants`, futuro R2 para logo | contrato + obrigações fiscais/contabilísticas | admin tenant, Bruno billing/suporte | IBAN/NIF sensíveis para ENI |
| `Cliente` | nome, telefone, email, NIF, notas | identificação, contacto, fiscal, texto livre | SQL Server `Clientes`; export CSV | enquanto loja precisar; inativos 24-36 meses configurable; anonimizar sob pedido | users do tenant | NIF/telefone em claro; notas podem conter excesso |
| `Reparacao` | clienteId, equipamento, IMEI, avaria, diagnóstico, notas, estado, preço, slug público | identificação indireta, dispositivo, operacional | SQL Server `Reparacoes`; portal público; export CSV | ciclo da loja; portal público máx. 2 anos; anonimização por pedido | users do tenant; cliente final via slug | IMEI + avaria pode identificar pessoa/dispositivo |
| `ReparacaoEstadoLog` | userId, notas estado, timestamps | operacional/audit | SQL Server | igual à reparação; pode manter após anonimização se sem PII | users do tenant | notas livres podem conter PII |
| `DiagnosticoExecucao` | reparacaoId, notas gerais, score | operacional; pode conter PII em notas | SQL Server | igual à reparação; anonimizar notas se pedido | users do tenant; parte resumida no portal | diagnóstico exposto ao cliente final |
| `DiagnosticoExecucaoItem` | labels/notas de teste | operacional | SQL Server | igual à reparação | users tenant; labels problemáticos no portal se avaria/marginal | notas podem ter excesso |
| `Garantia` | reparacaoId, slug público, cobertura/exclusões, motivo anulação | operacional/legal | SQL Server; portal público garantia | prazo garantia + período defesa; anonimizar ligação ao cliente quando aplicável | users tenant; público por slug | URL pública pode revelar equipamento/loja |
| `Avaliacao` | comentário, score, publicar testemunho | opinião/comentário | SQL Server; futuro widget público | até retirar consentimento/publicação; 24 meses se privado | tenant; público se autorizado | comentário pode conter nome/telefone |
| `Trabalho` | clienteId, título, descrição, notas, preços | operacional | SQL Server | igual à política da loja | users tenant | texto livre pode conter PII |
| `Despesa` | fornecedor, número encomenda, notas, relação com reparação/trabalho | comercial; pode conter contacto em texto livre | SQL Server | 10 anos se suporte contabilístico; senão política loja | users tenant | mistura contabilidade/PII |
| PDFs orçamento | cliente nome, telefone, email, NIF, equipamento, preços | identificação/fiscal/operacional | gerado on-demand; não persistido atualmente | se persistir, igual à reparação | tenant; cliente se enviado | PDF fora do sistema perde controlo |
| Logs Serilog | path, request data, exceptions, email em login falhado, IP | técnico/segurança | ficheiros `logs/repairdesk-.log`; futuro Better Stack/Sentry | app logs 14-30 dias; security 12 meses | Bruno técnico | PII acidental em exceções/logs |
| Backups | cópia completa DB/logos/fotos futuras | todos os dados | storage backup/R2/host | 30 dias diários + 3 mensais beta | Bruno técnico | maior concentração de risco |
| R2 fotos futuro | fotos antes/depois, metadados EXIF, possivelmente IMEI/rosto/documentos | imagem, dispositivo, potencialmente sensível se captar pessoa/documento | Cloudflare R2 | por reparação; apagar/anonimizar sob pedido; remover EXIF | tenant; cliente via portal se ativado | fotos têm risco alto de excesso |
| WhatsApp futuro | telefone, templates, estado reparação, delivery/read status | contacto/comunicação | provider WhatsApp + RepairDesk logs | mínimo operacional; logs 90 dias | tenant; provider | opt-in, metadados e transferências |

Nota: dados de saúde, biométricos ou categorias especiais não são objetivo do produto. O risco real vem de **texto livre e fotos**: técnicos podem escrever ou fotografar documentos, rostos, moradas ou conversas.

## Gap analysis por princípio

| Princípio | Estado atual | Gap | Prioridade |
|---|---|---|---|
| Data minimization | Campos essenciais para oficina existem; NIF é opcional; IMEI é opcional. | Notas livres sem aviso; export geral inclui slug; portal mostra diagnóstico completo `rep.Diagnostico`. | Importante |
| Purpose limitation | `16-Compliance-RGPD.md` diz que dados da loja não são para marketing. | Falta enforcement técnico: eventos/analytics devem excluir clientes finais por design. | Beta |
| Storage limitation | Soft-delete existe; portal oculta reparações >2 anos. | Sem retention jobs para clientes/reparações/logs/backups; soft-delete mantém PII. | Crítico |
| Default privacy | Portal público limita campos e tem rate limit. | Portal é público por slug sem PIN; avaliação pode publicar comentário por checkbox, precisa copy clara. | Importante |
| Transparency | Export CSV geral existe. | Sem export individual por cliente final; sem página sub-processadores; sem consent/version tracking. | Beta |
| Integrity/confidentiality | Tenant filter, auth, lockout, refresh hash. | Sem TDE/backup encryption; sem RBAC fino; sem audit log; logs podem conter email/PII. | Crítico |
| Accountability | Documentos legais existem. | Campos `CreatedBy/UpdatedBy` não preenchidos; sem `PrivacyRequests`, `SecurityIncidents`, `AuditLog`. | Crítico |

## Encryption

### Em trânsito

Produção deve exigir:

- HTTPS público com TLS 1.2+; TLS 1.3 quando suportado pelo proxy/host.
- `Secure = true` sempre nos cookies em produção.
- HSTS no reverse proxy.
- ligação SQL com encriptação (`Encrypt=True;TrustServerCertificate=False`) quando DB estiver fora do mesmo container/host privado.

Gap atual:

- `RefreshCookieName` usa `Secure = Request.IsHttps`; correto localmente, mas em produção atrás de proxy tens de garantir `ForwardedHeaders` para `Request.IsHttps` não ficar falso.

### Em repouso: base de dados

Recomendação beta:

1. **TDE ou disco/volume encriptado** para DB e backups.
2. **Application-level encryption** para NIF/IMEI/IBAN só se conseguires aceitar perda de pesquisa parcial.
3. **Always Encrypted** como fase 2 para NIF/IBAN, não para tudo no MVP.

Leitura prática:

- TDE protege ficheiros, logs e backups se alguém roubar disco/backup, mas **não impede um SQL admin com acesso à DB de ler dados**.
- Always Encrypted protege colunas sensíveis contra DB admin/cloud operator, porque as chaves ficam fora do motor SQL, mas traz limitações de queries e exige desenho cuidadoso.
- Para Bruno solo, começar por TDE/volume + backup encryption + least privilege é mais realista que Always Encrypted em todas as colunas.

Colunas candidatas:

| Campo | MVP | Fase 2 |
|---|---|---|
| `Tenant.Iban` | application encryption ou ocultar no UI | Always Encrypted randomized |
| `Cliente.Nif` | plaintext com acesso restrito; não logar | deterministic encryption se precisa dedupe/equality |
| `Reparacao.Imei` | plaintext para histórico por IMEI; mascarar UI/logs | deterministic encryption + hash lookup |
| `Cliente.Telefone` | plaintext para pesquisa/contacto; mascarar logs | hash normalizado auxiliar para lookup |
| `Cliente.Email` | plaintext para contacto; mascarar logs | hash auxiliar se necessário |

### Snippet TDE SQL Server

```sql
USE master;
GO
CREATE MASTER KEY ENCRYPTION BY PASSWORD = '<password-forte-guardada-fora-do-servidor>';
GO
CREATE CERTIFICATE RepairDeskTdeCert
    WITH SUBJECT = 'RepairDesk production TDE certificate';
GO

USE RepairDeskProd;
GO
CREATE DATABASE ENCRYPTION KEY
WITH ALGORITHM = AES_256
ENCRYPTION BY SERVER CERTIFICATE RepairDeskTdeCert;
GO
ALTER DATABASE RepairDeskProd SET ENCRYPTION ON;
GO
```

Obrigatório depois:

```sql
USE master;
GO
BACKUP CERTIFICATE RepairDeskTdeCert
TO FILE = '/secure/repairdesk-tde-cert.cer'
WITH PRIVATE KEY (
    FILE = '/secure/repairdesk-tde-cert.pvk',
    ENCRYPTION BY PASSWORD = '<password-diferente>'
);
GO
```

Sem backup do certificado, podes perder acesso aos backups.

### Snippet application-level encryption simples

Para MVP, se não quiseres mexer já em Always Encrypted, podes criar um value converter para campos raramente pesquisados como `Tenant.Iban`.

```csharp
public interface IFieldProtector
{
    string? Protect(string? value);
    string? Unprotect(string? protectedValue);
}

public sealed class AesGcmFieldProtector : IFieldProtector
{
    // Guardar chave em env var/secret manager, não em appsettings.
    // Incluir key version no payload para rotação futura.
}
```

No EF:

```csharp
builder.Property(x => x.Iban)
    .HasConversion(
        v => protector.Protect(v),
        v => protector.Unprotect(v))
    .HasMaxLength(512);
```

Não aplicar isto cegamente a telefone/email/IMEI se precisas pesquisar. Para esses, usar `NormalizedHash` separado:

```csharp
public string? ImeiProtected { get; set; }
public string? ImeiHash { get; set; } // HMAC-SHA256(normalizedImei)
```

## Access control

### Multi-tenant

O global query filter é uma boa defesa, mas tem uma nuance:

```csharp
tenantCheck = CurrentTenantId == null || entity.TenantId == CurrentTenantId
```

Isto é útil para design-time/admin/background jobs, mas perigoso se algum request autenticado ficar sem tenant por bug. Medida defensiva:

- controllers de tenant data devem falhar se `!_tenantContext.HasTenant`;
- criar teste automático: utilizador sem `tenant_id` não lê `Clientes`, `Reparacoes`, `Trabalhos`, `Despesas`, `Diagnostico`, `Garantias`;
- para admin interno, usar serviço/admin context separado e auditado, não depender de `TenantId == null`.

### Roles

Estado atual:

- controllers usam `[Authorize]`;
- não há policies por role;
- `Admin` e `Tecnico` existem.

Mínimo beta:

| Ação | Role recomendada |
|---|---|
| Ver/criar/editar reparações | Admin, Tecnico |
| Ver clientes | Admin, Tecnico |
| Export CSV | Admin |
| Import CSV | Admin |
| Tenant settings | Admin |
| Ver financeiro/dashboard | Admin |
| Apagar/anonymizar cliente | Admin |
| Gerir users/roles | Admin |

Snippet:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantAdmin", p => p.RequireRole("Admin"));
    options.AddPolicy("TenantStaff", p => p.RequireRole("Admin", "Tecnico"));
});
```

Controllers:

```csharp
[Authorize(Policy = "TenantAdmin")]
[HttpGet("export")]
public async Task<IActionResult> Export(CancellationToken ct) { ... }
```

### Audit log

Campos `CreatedBy`/`UpdatedBy` existem mas não são preenchidos no `AppDbContext`. Corrigir:

```csharp
private readonly ICurrentUser _currentUser;

private void StampAuditFields()
{
    var now = DateTime.UtcNow;
    var userId = _currentUser.UserId;

    foreach (var entry in ChangeTracker.Entries<BaseEntity>())
    {
        if (entry.State == EntityState.Added)
        {
            entry.Entity.CreatedAt = now;
            entry.Entity.CreatedBy = userId;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Entity.UpdatedAt = now;
            entry.Entity.UpdatedBy = userId;
        }
        else if (entry.State == EntityState.Deleted)
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.UpdatedAt = now;
            entry.Entity.UpdatedBy = userId;
        }
    }
}
```

Criar também tabela `AuditEvents`, porque `UpdatedBy` não chega:

```csharp
public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid? EntityId { get; set; }
    public string? RiskLevel { get; set; }
    public string? MetadataJson { get; set; } // Sem PII direta.
    public string? IpHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

Eventos mínimos:

- `ClientExported`
- `RepairsExported`
- `ClientAnonymized`
- `RepairPublicPortalViewed`
- `QuoteApprovedPublic`
- `ReviewSubmittedPublic`
- `RoleChanged`
- `TenantSettingsChanged`
- `LoginFailed`
- `PasswordResetRequested`

Não guardar telefone/email/NIF/IMEI em `MetadataJson`; usar ids internos e counts.

## Portal público e garantia

Pontos positivos:

- slug aleatório 10 chars;
- rate limit 30/min/IP;
- DTO público exclui dados financeiros internos, NIF, IBAN, telefone/email do cliente;
- reparações >2 anos deixam de ser reveladas;
- garantia pública não mostra cliente.

Gaps:

- `PublicRepairDto` mostra `ClientePrimeiroNome`, `AvariaPublica`, `Diagnostico`, `OrcamentoCents`, `PrecoFinalCents`. Em algumas oficinas isto é esperado; noutras pode ser demasiado.
- Aprovação de orçamento por slug não pede confirmação extra.
- Avaliação pública depende de checkbox/copy, mas comentário livre pode ter PII.

Recomendação:

| Setting tenant | Default beta | Efeito |
|---|---|---|
| `PublicPortalEnabled` | true | loja pode desligar |
| `PublicPortalRequirePin` | false beta, true para lojas sensíveis | pede últimos 4 dígitos telefone ou PIN |
| `ShowClientFirstName` | false | reduz identificação |
| `ShowDiagnosisPublicly` | false | só status e orçamento |
| `ShowFinalPricePublicly` | false | mostrar apenas orçamento pendente/aprovado |
| `PublicPortalMaxAgeDays` | 730 | já implementado como 2 anos |

PIN simples:

```csharp
public string? PublicPinHash { get; set; } // HMAC últimos 4 telefone ou PIN gerado
```

Fluxo:

- link continua `/r/{slug}`;
- para ver detalhes, cliente introduz PIN;
- para orçamento, exigir PIN se tenant ativou.

## Right to be forgotten

Soft-delete atual:

- bom para UX;
- bom para evitar perda acidental;
- **não é apagamento RGPD**, porque SQL admin/backups continuam com PII.

Estratégia correta:

1. **Soft-delete operacional** para ações normais da loja.
2. **Anonimização por cliente** para pedido RGPD validado pela loja.
3. **Hard-delete seletivo** só para dados sem obrigação de retenção/defesa.
4. **Backups por rotação**: não editar backups históricos, mas garantir expiração e não reintroduzir dados apagados em restore.

### Snippet de anonimização

```csharp
public async Task AnonymizeClienteAsync(Guid clienteId, string reason, CancellationToken ct)
{
    var cliente = await _clientes.FindByIdAsync(clienteId, ct)
        ?? throw new NotFoundException("Cliente", clienteId);

    var anonId = cliente.Id.ToString("N")[..8];

    cliente.Nome = $"Cliente removido {anonId}";
    cliente.Telefone = null;
    cliente.Email = null;
    cliente.Nif = null;
    cliente.Notas = null;

    var reparacoes = await _repo.ListByClienteAsync(cliente.Id, ct);
    foreach (var r in reparacoes)
    {
        r.Imei = null;
        r.Notas = null;
        // manter equipamento/estado/preços se a loja precisar de histórico operacional
        // opcional: r.Avaria = Redact(r.Avaria);
        r.PublicSlug = null; // desativa portal público
    }

    await _audit.LogAsync("ClientAnonymized", cliente.Id, new { reason }, ct);
    await _unitOfWork.SaveChangesAsync(ct);
}
```

Regras:

- Não apagar `Reparacao.Numero`, valores, estados e garantia se a loja precisa para defesa/contabilidade.
- Remover ou anonimizar campos que identifiquem a pessoa: nome, telefone, email, NIF, IMEI, notas.
- Desativar slugs públicos.
- Guardar audit event sem PII.

### Backups

Resposta padrão defensável:

- apagamento/anonimização aplica-se ao sistema ativo;
- backups expiram em 30-90 dias;
- se houver restore, correr job `ReapplyPrivacyActions` com lista de anonimizacões efetuadas desde o backup.

Tabela:

```csharp
public sealed class PrivacyAction
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Type { get; set; } = ""; // AnonymizeClient/DeleteUser
    public Guid TargetId { get; set; }
    public DateTime AppliedAt { get; set; }
    public string? ReasonCode { get; set; }
}
```

## Data portability

Estado atual:

- export CSV geral de clientes;
- export CSV geral de reparações;
- não existe export individual de cliente final;
- export reparações inclui `PublicSlug`, IMEI, NIF, email, notas.

Mínimo beta:

1. Export tenant completo: CSV/ZIP.
2. Export cliente individual: JSON + CSV.
3. Export utilizador SaaS: conta, sessões, eventos segurança básicos.

Formato recomendado:

```json
{
  "exportedAt": "2026-05-16T12:00:00Z",
  "tenantId": "...",
  "cliente": {
    "id": "...",
    "nome": "...",
    "telefone": "...",
    "email": "...",
    "nif": "..."
  },
  "reparacoes": [],
  "garantias": [],
  "avaliacoes": []
}
```

Não incluir:

- dados de outros clientes;
- tokens;
- password hashes;
- audit logs internos com IPs de terceiros;
- custos internos se o pedido vier de cliente final e a loja não autorizar.

Como subcontratante, quando o pedido vem de cliente final, o RepairDesk deve encaminhar para a loja e só exportar mediante instrução da loja.

## Breach detection & notification

### Como detetar

Mínimo antes beta:

- Sentry para exceptions novas;
- Better Stack uptime e heartbeat backups;
- security events para login failed, lockout, export, role changes;
- alertas para spikes:
  - muitos 404 no portal público;
  - muitas tentativas de slug;
  - export CSV fora de horário;
  - login falhado repetido por IP/email hash;
  - alteração de role/admin.

### Procedimento 72h

Se dados da loja forem afetados, o RepairDesk é normalmente **subcontratante** e deve avisar a loja sem demora injustificada. A loja decide/notifica CNPD como responsável, com apoio do RepairDesk.

Se forem dados próprios da LopesTech/contas SaaS, a LopesTech atua como responsável e avalia notificação à CNPD.

Primeiras 24h:

1. abrir `SecurityIncident`;
2. conter: revogar tokens, bloquear endpoint, rodar chaves;
3. congelar logs;
4. identificar tenants, categorias de dados e volume;
5. avisar lojas afetadas se houver dados delas;
6. decidir risco baixo/risco/elevado.

Até 72h:

- notificação CNPD se aplicável;
- comunicação a titulares se risco elevado;
- atualização por fases se ainda não houver todos os factos.

### Template para loja afetada

```text
Assunto: Incidente de segurança no RepairDesk - ação em curso

Olá {Nome},

Detetámos em {data/hora} um incidente que pode ter afetado dados tratados no RepairDesk da tua loja.

O que sabemos até agora:
- Sistema afetado: {sistema}
- Período provável: {período}
- Dados potencialmente envolvidos: {categorias}
- Titulares estimados: {número aproximado ou "em apuramento"}

O que já fizemos:
- {contenção 1}
- {contenção 2}
- {medida 3}

Como a tua loja é responsável pelo tratamento dos dados dos teus clientes, enviamos esta informação para poderes avaliar obrigações de notificação. Vamos apoiar-te com os detalhes técnicos necessários.

Próxima atualização: até {hora}.

Bruno Lopes
LopesTech / RepairDesk
privacidade@lopestech.pt
```

### Template para titulares

```text
Assunto: Informação sobre incidente de segurança

Olá,

Estamos a contactar-te porque dados relacionados com uma reparação registada pela {Loja} podem ter sido afetados por um incidente de segurança.

Dados potencialmente envolvidos:
- {categorias, sem dramatizar nem esconder}

Medidas já tomadas:
- {medidas}

O que podes fazer:
- estar atento a contactos suspeitos;
- contactar a loja se tiveres dúvidas;
- pedir informação adicional através de {contacto}.

Lamentamos o sucedido e estamos a trabalhar para reduzir qualquer impacto.
```

## Sub-processadores

Lista atual/futura esperada:

| Sub-processador | Função | Região desejada | Estado | DPA |
|---|---|---|---|---|
| Hetzner | hosting/VPS/DB se escolhido | EU, Alemanha/Finlândia | planeado | obter antes beta |
| Cloudflare R2 | storage fotos/backups se escolhido | EU preferido quando configurável | planeado | obter antes fotos/backups |
| Better Stack | uptime/logs/status | EU/US conforme plano `{{verificar}}` | planeado | avaliar |
| Sentry | errors/APM | EU se projeto/região permitir `{{verificar}}` | planeado | avaliar |
| Provider email | transacional | EU preferido | futuro | obrigatório |
| Provider WhatsApp/Meta | mensagens | pode envolver fora EEE | futuro | DPA + avaliação específica |
| Provider faturação certificado | faturas | PT/EU | futuro | obrigatório |

Regras:

- publicar página `/subprocessadores`;
- avisar lojas 30 dias antes de mudança material, quando possível;
- permitir objeção razoável em DPA para mudanças de alto risco;
- não ativar fotos/WhatsApp sem atualizar lista.

## Logs e redaction

### Estado atual

Serilog tem:

- console;
- ficheiro diário;
- `retainedFileCountLimit: 14`;
- request logging.

Problema observado:

```csharp
_log.LogWarning("Failed login for {Email} from {Ip}", req.Email, ip);
```

Isto grava email em texto claro em logs. Para segurança, basta hash/mascara.

### Snippet corrigido

```csharp
_log.LogWarning("Failed login for emailHash={EmailHash} from ipHash={IpHash}",
    PrivacyHash.Email(req.Email),
    PrivacyHash.Ip(ip));
```

Helper:

```csharp
public static class PrivacyHash
{
    public static string Email(string? value) => Hmac(NormalizeEmail(value));
    public static string Ip(string? value) => Hmac(value ?? "unknown");

    private static string NormalizeEmail(string? value)
        => (value ?? "").Trim().ToLowerInvariant();

    private static string Hmac(string value)
    {
        var key = Convert.FromBase64String(
            Environment.GetEnvironmentVariable("REPAIRDESK_LOG_HASH_KEY")
            ?? throw new InvalidOperationException("Missing log hash key"));
        using var hmac = new System.Security.Cryptography.HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value)))[..16];
    }
}
```

Regras:

- não logar request bodies;
- não logar query strings com IMEI/NIF/telefone;
- não logar CSV import raw;
- não mandar PII para Sentry breadcrumbs;
- truncar exception messages se vierem de validação com input do cliente.

Retenção:

| Log | Retenção beta | Nota |
|---|---:|---|
| App file logs | 14-30 dias | atual 14 ok para beta |
| Security events | 12 meses | em tabela própria sem PII direta |
| Audit business | 24 meses | necessário para accountability |
| Public portal access | 90 dias agregado/hash | detectar abuso |
| Breach records | 5 anos | defesa/accountability |

## Gaps críticos antes da beta

| Gap | Risco | Remediação |
|---|---|---|
| Sem anonimização/apagamento real | pedido art. 17 não executável | `AnonymizeClienteAsync`, desativar slugs, `PrivacyActions`. |
| Logs podem conter PII | fuga silenciosa em ficheiros/Sentry | redaction/hashing para email/IP/IMEI/NIF/telefone; proibir body logs. |
| Sem audit log | não provas quem exportou/acedeu/alterou | tabela `AuditEvents`, eventos mínimos e admin export auditado. |
| Sem backups encriptados/testados | perda/roubo de dados e falha art. 32 | backups diários encriptados + restore test. |
| Sem RBAC fino | técnico pode exportar/ver financeiro/settings | policies `TenantAdmin`/`TenantStaff`. |
| Sub-processadores indefinidos | DPA incompleto | escolher hosting/storage/email e listar antes beta. |

## Importantes no próximo mês

| Gap | Remediação |
|---|---|
| Portal público sem PIN | setting por tenant `RequirePin`; default false beta, recomendar true para lojas sensíveis. |
| `CreatedBy/UpdatedBy` não preenchidos | injetar `ICurrentUser` no `AppDbContext` e preencher. |
| Export individual inexistente | endpoint admin para export cliente JSON/CSV. |
| Consent/version tracking | `TermsAcceptedAt`, `PrivacyVersion`, `DpaAcceptedAt`. |
| Fotos sem política técnica | antes de R2: remover EXIF, limitar MIME/tamanho, URLs assinadas, retention. |
| Dados em texto livre | placeholders/avisos UI: "não escrevas passwords/documentos". |

## Nice-to-have

| Item | Quando |
|---|---|
| Always Encrypted para NIF/IBAN | depois de beta, antes de escala/enterprise |
| MFA admin tenant | antes de público pago |
| Admin internal access workflow | quando Bruno precisar aceder tenants para suporte |
| DPIA completa | antes de fotos + WhatsApp + IMEI externo em escala |
| Security headers report-only | quando houver domínio público |
| Pentest leve | antes de 10+ lojas pagantes |

## Plano de remediação

### Semana 1 - Bloqueia beta

1. Corrigir logs de login falhado para hash/mascara.
2. Desativar request body logging, garantir que Sentry não recebe PII.
3. Criar `AuditEvents`.
4. Auditar export/import/delete/login/portal public actions.
5. Criar `AnonymizeClienteAsync`.
6. Criar `PrivacyActions`.
7. Configurar backup encriptado e testar restore.
8. Definir sub-processadores reais no DPA.

### Semana 2

1. RBAC: `TenantAdmin` para export/import/settings/financeiro.
2. Preencher `CreatedBy/UpdatedBy`.
3. Criar export individual cliente final.
4. Criar página/admin view de `PrivacyRequests`.
5. Criar settings portal: show first name, show diagnosis, require PIN.

### Semanas 3-4

1. Retention job para logs/security/audit.
2. Job para desativar slugs antigos.
3. Página pública de sub-processadores.
4. Mini tabletop breach: simular incidente e preencher template.
5. Rever textos UI para evitar recolha excessiva em notas/fotos.

## Riscos legais identificados

| Risco | Severidade | Comentário |
|---|---|---|
| Soft-delete apresentado como apagamento | Alto | Não prometer apagamento imediato se backups retêm dados. Prometer anonimização ativa + expiração backups. |
| Portal por slug expõe informação de reparação | Alto | Mitigar com rate limit, slug forte, dados mínimos e PIN opcional. |
| Fotos futuras com documentos/rostos/EXIF | Alto | Sem upload público antes de remover EXIF e UX orientar. |
| WhatsApp futuro sem opt-in | Alto | Só ativar com consentimento/base legal controlada pela loja. |
| Logs/Sentry com PII | Alto | Redaction antes de beta. |
| Bruno acede DB completo sem trilho | Alto | Conta admin DB separada, access log/manual support access. |
| Dados de clientes finais usados para analytics/benchmark | Alto | Só dados agregados/anónimos; nunca marketing LopesTech. |

## Checklist de aceitação para abrir 2-3 lojas

- [ ] DPA assinado com cada loja.
- [ ] Sub-processadores listados.
- [ ] Backups encriptados e restore testado.
- [ ] Logs sem email/telefone/NIF/IMEI em claro.
- [ ] `AuditEvents` ativo.
- [ ] Export CSV auditado e apenas Admin.
- [ ] Anonimização de cliente testada.
- [ ] Portal público revisto com dados mínimos.
- [ ] Slugs antigos expiram/desativam.
- [ ] Processo breach testado em seco.
- [ ] `privacidade@lopestech.pt` funcional.

## Fontes

- RGPD, Regulamento (UE) 2016/679, art. 25, 17, 20, 32, 33 e 34: https://eur-lex.europa.eu/legal-content/EN-PT/TXT/?uri=CELEX%3A32016R0679
- CNPD, notificação de violações de dados pessoais: https://www.cnpd.pt/databreach/
- CNPD, outras obrigações incluindo violação de dados e registo de atividades: https://www.cnpd.pt/organizacoes/outras-obrigacoes/
- Microsoft Learn, Transparent Data Encryption SQL Server: https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/transparent-data-encryption?view=sql-server-ver17
- Microsoft Learn, Always Encrypted SQL Server: https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-database-engine?view=sql-server-ver17

## Veredicto

Arquitetura atual: **boa base para dogfooding, ainda não defensável para beta externa sem remediação curta**.

O trabalho antes da beta não é enorme. É uma semana disciplinada:

- redigir logs;
- criar audit events;
- implementar anonimização;
- bloquear exports/settings por role;
- garantir backups encriptados;
- documentar sub-processadores reais.

Depois disso, para 2-3 lojas reais, o risco fica razoável e explicável.
