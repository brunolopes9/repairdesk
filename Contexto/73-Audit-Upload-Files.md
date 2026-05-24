# 73 — Auditoria de Uploads de Arquivos

**Data:** 2026-05-24
**Pedido Bruno:** análise de todos os endpoints de upload cobrindo 6 áreas (MIME+extensão, tamanho, sanitização nome, magic bytes, storage fora public, anti-executável).

---

## Inventário de endpoints com `IFormFile`

| # | Endpoint | Auth | Tipo aceite | Storage |
|---|---|---|---|---|
| 1 | `POST /api/reparacoes/{id}/fotos` | JWT | imagem | `/data/photos` via `IStorageProvider` |
| 2 | `POST /api/supplier-invoices/upload-photo` | JWT | imagem (fatura) | via `SupplierInvoiceImportService` |
| 3 | `POST /api/supplier-invoices/upload` | JWT Admin | PDF (fatura B2B) | via `SupplierInvoiceImportService` |
| 4 | `POST /api/products/images/upload-pending` | JWT | imagem | R2/local |
| 5 | `POST /api/products/{id}/images/upload` | JWT | imagem | R2/local |
| 6 | `POST /api/external/ai-image-search` | ApiKey (read scope) | imagem (não persiste) | — (envia a Claude Vision) |
| 7 | `POST /api/parts/extract-pdf` | JWT | PDF (não persiste) | — (lê texto e descarta) |

Plus os endpoints `external/supplier-invoices/ingest` recebem `byte[]` num DTO (n8n IMAP forwarding) — fora desta análise porque não usam `IFormFile`.

---

## Matriz de cobertura vs 6 requisitos

Legenda: ✅ tem  ⚠️ parcial/fraco  ❌ não tem  ⊘ N/A (não persiste)

| Endpoint | (1) MIME+ext | (2) Limite | (3) Sanit nome | (4) Magic bytes | (5) Storage seg. | (6) Anti-exec |
|---|---|---|---|---|---|---|
| Fotos `POST /api/reparacoes/{id}/fotos` | ✅ MIME whitelist; ext mapeada via MIME | ✅ 10 MB | ✅ `SafeFileName` ASCII-only | ❌ confia em `ContentType` cliente | ✅ `/data/photos` | ⚠️ extensão controlada mas sem magic |
| SupplierInv `upload-photo` | ❌ Sem validação MIME no controller; service passa adiante | ✅ 10 MB | ❌ `file.FileName` direto | ❌ | ✅ via storage provider | ❌ aceita qualquer ContentType |
| SupplierInv `upload` (PDF) | ⚠️ `ContentType==pdf` **OR** `ext==.pdf` — OR é fraco | ✅ 20 MB | ⚠️ `file.FileName` em subject | ❌ não verifica `%PDF-` | ✅ | ⚠️ |
| Products `images/upload-pending` | ✅ MIME whitelist (jpeg/png/webp/gif) | ✅ 12 MB | ✅ usa `Guid.NewGuid()` em vez de FileName | ❌ | ✅ R2/local | ⚠️ |
| Products `{id}/images/upload` | ✅ MIME whitelist | ✅ 12 MB | ✅ `Guid.NewGuid()` | ❌ | ✅ | ⚠️ |
| External `ai-image-search` | ✅ MIME whitelist | ✅ 6 MB | ⊘ não persiste | ❌ | ⊘ | ✅ não persiste |
| Parts `extract-pdf` | ⚠️ OR fraco | ✅ 10 MB + 30 páginas max | ⊘ | ❌ | ⊘ | ⚠️ |

---

## Análise por pergunta

### (1) Validação MIME + extensão

- **OK:** FotosService, ProductsController, ExternalController — todos têm whitelist explícita.
- **Gap:** SupplierInvoicesController `upload-photo` não valida MIME no controller (passa para o serviço sem filtro).
- **Gap:** Validação "`MIME==X` **OR** `ext==Y`" em PDF é permissiva — atacante pode mandar `.pdf` com ContentType arbitrário e passa. Devia ser **AND**.

### (2) Limite de tamanho

Todos têm `[RequestSizeLimit]` razoável:
- Fotos reparação: 10 MB (+ 8 KB overhead)
- Supplier photo: 10 MB
- Supplier PDF: 20 MB
- Products image: 12 MB
- External AI image: 6 MB
- Parts PDF: 10 MB

Adequados aos casos de uso. Sem gaps.

### (3) Sanitização nome (path traversal)

- **OK:** FotoService.SafeFileName remove non-alphanumerics e trunca a 100 chars. Storage key usa GUID — `tenants/{tid}/reparacoes/{rid}/{fotoId}{ext}`.
- **OK:** ProductsController nunca usa `FileName` — gera `{Guid:N}` como key.
- **Gap:** SupplierInvoicesController usa `file.FileName` em `emailMeta.Subject` sem sanitização. Não é path mas pode ter XSS/log injection.
- **Risco residual baixo:** ASP.NET Core já decodifica multipart e `IFormFile.FileName` vem como `Content-Disposition.filename` que é tipicamente seguro, mas defesa em profundidade é boa.

### (4) Magic bytes (conteúdo real)

**Gap sistémico:** NENHUM endpoint valida magic bytes. Todos confiam em `IFormFile.ContentType` que é fornecido pelo cliente.

Cenário de ataque: atacante envia `evil.exe` renomeado para `selfie.jpg` com `Content-Type: image/jpeg`. Passa validação MIME e extensão. Armazena no R2/disco. Se algum dia for servido com `Content-Type: image/jpeg` por reverse proxy, browsers não executam — mas se proxy não respeitar headers ou se for entregue em flow inesperado, é risco.

Magic bytes a verificar:
- JPEG: `FF D8 FF`
- PNG: `89 50 4E 47 0D 0A 1A 0A`
- WebP: `52 49 46 46 ?? ?? ?? ?? 57 45 42 50` (RIFF...WEBP)
- GIF: `47 49 46 38 37 61` ou `47 49 46 38 39 61`
- HEIC: bytes 4-11 = `ftypheic` / `ftypheix` / `ftypmif1`
- PDF: `25 50 44 46 2D` (%PDF-)

### (5) Storage fora directório público

✅ Confirmado: API não serve static files (`UseStaticFiles` não está em `Program.cs`). Storage local é `/data/photos` (volume Docker), acessível apenas via endpoints `[Authorize]` ou signed URLs (FotosController `/export-content`). R2 é bucket privado também.

### (6) Anti-executável

Hoje a defesa é apenas via MIME/extensão whitelist (jpeg/png/webp/gif/pdf). Atacante pode:
- Subir `.exe` com `Content-Type: image/jpeg` — passa (gap #4)
- Subir `.svg` com `Content-Type: image/png` — passa o whitelist mas podia conter `<script>` (SVG é XML/JS)
- Subir `.html` renomeado — bloqueado por whitelist actual

**SVG NÃO está no whitelist** — bom. Mas se um dia for adicionado, requer sanitização explícita (e.g., DOMPurify equivalent no servidor).

---

## Risco real avaliado

Os gaps identificados são **defesa em profundidade**, não vulnerabilidades imediatamente exploráveis porque:
1. Storage não é servido como static — qualquer ficheiro tem que passar por endpoint `[Authorize]` que define `Content-Type` baseado no que foi guardado.
2. Endpoints retornam ContentType controlado pelo servidor (de DB), não pelo upload original.
3. Public endpoints (`PublicFotoController`, `/export-content`) só servem ficheiros já validados no upload + signed URLs.

**Mas** o sistema fica frágil se:
- Adicionarem um endpoint que sirva `Content-Type` baseado em FileName (extensão).
- Mudarem proxy/nginx para servir `/data/photos` diretamente.
- Adicionarem upload de SVG.

Severidade: **Média (defesa em profundidade) — não crítica imediatamente.**

---

## Plano de refactor

### Fase A — `IFileValidator` central (P0)

**A.1** Criar `RepairDesk.Services.Files.IFileValidator` com método:
```csharp
ValidatedFile Validate(Stream content, string declaredMime, string fileName, FileKind kind);
public enum FileKind { Image, Pdf }
public sealed record ValidatedFile(string DetectedMime, string SafeExtension, byte[] Buffer);
```

Implementação faz:
1. Lê primeiros 16 bytes
2. Compara contra tabela de magic bytes
3. Verifica que `DetectedMime` está no whitelist do `kind`
4. Verifica consistência com `declaredMime` (warn-log se divergente, rejeitar se incompatível)
5. Devolve buffer completo + extensão segura derivada da DETECÇÃO real (não da FileName)
6. Lança `ValidationException("file_invalid", detail)` em qualquer falha

**A.2** Adicionar testes unitários `FileValidatorTests`:
- 6 testes happy path (1 por formato)
- 6 testes "fake header" (eg PNG header em ficheiro renomeado)
- 1 teste "header mas truncated"
- 1 teste "MIME declarado divergente"

### Fase B — Refactor endpoints (P0)

Cada controller injecta `IFileValidator` e usa antes de processar:

**B.1** FotosController + FotoService — substituir whitelist atual + adicionar magic bytes
**B.2** SupplierInvoicesController upload-photo — adicionar validação completa (falta tudo)
**B.3** SupplierInvoicesController upload PDF — substituir OR por validação
**B.4** ProductsController upload-pending + upload — adicionar magic bytes (já tem MIME)
**B.5** ExternalController ai-image-search — adicionar magic bytes (não persiste mas defende contra abuso de Claude Vision com lixo)
**B.6** PartsController extract-pdf — substituir OR por validação completa

### Fase C — Sanitização nome global (P1)

**C.1** Mover SafeFileName para utility `FileNameSanitizer.Safe(raw)` (Common)
**C.2** Aplicar em todos os endpoints que persistam FileName (Fotos + SupplierInvoices)
**C.3** Tests cobrindo casos: `../../../etc/passwd`, `con.txt` (Windows reserved), `nome com espaços.jpg`, null/empty

### Fase D — Tests defensivos (P1)

**D.1** `UploadSecurityTests` por endpoint:
- Magic mismatch → 400
- Extensão whitelist mas magic errado → 400
- Tamanho 0 → 400
- Path traversal em FileName → ignorado, GUID usado
- SVG explícito → 400 (não está no whitelist)

---

## Tarefas criadas (TaskTool)

- **Sprint 246** — Fase A (FileValidator central + tests)
- **Sprint 247** — Fase B (refactor 6 controllers)
- **Sprint 248** — Fase C+D (FileNameSanitizer + tests defensivos)

Estimativa total: 6-9h.

[[reference-docker-setup]] [[feedback-codex-bugs-recorrentes]]
