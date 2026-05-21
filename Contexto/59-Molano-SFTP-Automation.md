# 59 — Molano SFTP automation + error visibility

**Pedido Bruno (2026-05-21):** Molano vai disponibilizar CSV de produtos via SFTP. O importer
manual do Sprint 153 (upload UI) é OK como fallback mas o fluxo principal tem de ser
**automático** com **erros sempre visíveis** ao utilizador.

## Arquitectura

```
┌──────────────────────────┐
│  Molano SFTP server      │
│  sftp.molano.eu          │
│  /catalog/products.csv   │
│  (actualizado nightly)   │
└────────────┬─────────────┘
             │ pull cron (RepairDesk side)
             ▼
┌──────────────────────────┐
│  RepairDesk Worker       │
│  (HostedService BackgroundService)│
│                          │
│  1. Cron a cada 6h       │
│  2. SFTP pull CSV        │
│  3. Hash → skip se igual │
│  4. Chama Sprint 153     │
│     ImportMolanoCsvAsync │
│  5. Cria SyncRun record  │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  SupplierSyncRun entity  │
│   - tenantId             │
│   - fornecedorId         │
│   - startedAt/endedAt    │
│   - status (Ok/Failed)   │
│   - csvSha256            │
│   - created/updated      │
│   - errorsJson           │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│  UI /importacoes inclui  │
│  secção "Sync automáticas"│
│   - tabela últimos 30 runs│
│   - badge sucesso/erro    │
│   - botão "Re-correr agora"│
│   - botão "Manual: upload"│
└──────────────────────────┘
```

## Tabela SupplierSyncRun

```csharp
public class SupplierSyncRun : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FornecedorId { get; set; }
    public string Source { get; set; } = "sftp";  // sftp | manual | api
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public SyncStatus Status { get; set; }       // Running | Ok | Failed | Skipped
    public string? CsvSha256 { get; set; }       // hash do ficheiro processado
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public string? ErrorsJson { get; set; }      // List<{line, field, message, sku}>
    public string? Message { get; set; }         // mensagem amigável (ex: "SFTP timeout")
}
```

## Configuração por tenant

Em `Definições → Fornecedores → Molano` (UI nova):
- ☑ "Sincronizar automaticamente via SFTP"
- Host: `sftp.molano.eu`
- Port: `22`
- Username: `lopestech-rd`
- Password: `••••••` (encriptada via DataProtection)
- Remote path: `/catalog/products.csv`
- Frequência: `[ a cada 6h | diária | manual ]`
- Email para alertas falhados: `bruno@lopestech.pt`

## HostedService

Worker `.NET BackgroundService` que:
1. Acorda a cada 5 min, verifica que tenants têm Molano SFTP activo + cron deu
2. Para cada um:
   - Conecta SFTP (SSH.NET)
   - Lê ficheiro, calcula SHA256
   - Se hash igual ao último run Ok → skip + cria SyncRun(Skipped)
   - Senão chama `ImportMolanoCsvAsync(csv, fornecedorId)`
   - Guarda SyncRun com result counts
3. Se falhar 3× consecutivas → envia email Bruno

## Visibilidade dos erros

**Princípio Bruno:** erros NUNCA escondidos. UI mostra sempre, mesmo que utilizador
não esteja a olhar para a página.

- Dashboard widget novo: "Sync automáticas — última falha há 2h" (clica → leva a /importacoes)
- `/importacoes` ganha tab "Sync automáticas" com timeline de runs
- Badge vermelho no menu lateral quando há sync failed nos últimos 7 dias
- Email automático quando 3 runs falham consecutivamente

## Roadmap implementação

| Sprint | Entrega | Esforço |
|---|---|---|
| **170** | Entidade SupplierSyncRun + Migration EF | 0.5 dia |
| **171** | SftpClient wrapper (SSH.NET) + credenciais encriptadas | 1.5 dias |
| **172** | BackgroundService cron + hash dedup | 1 dia |
| **173** | UI Definições Fornecedor para configurar SFTP | 1 dia |
| **174** | UI /importacoes tab "Sync automáticas" + dashboard widget | 1.5 dias |
| **175** | Email alerts (3 failures consecutivos) | 0.5 dia |

**Total: ~6 dias** depois da killer feature ingest (Sprint 157-164).

## Dependências externas

- **SSH.NET** NuGet package (3.0+) — biblioteca SFTP madura, well-tested
- Conta SFTP Molano (Bruno pede a eles)

## Próximo

Implementar depois da killer feature ingest V2 estar madura. SFTP é caso particular
de "ingest automation"; ambos partilham infra (SupplierSyncRun pode generalizar para
outros sources como API REST de fornecedores futuros).
