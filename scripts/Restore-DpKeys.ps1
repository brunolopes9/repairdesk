# Sprint 352 (Doc 76 gap crítico): restaura dp-keys encriptadas do R2 para o
# volume local /data/dp-keys. Inverso do DpKeysBackupHostedService.
#
# Workflow do incidente "VPS perdida":
#   1. Provisionar nova VPS + clone repo
#   2. Recuperar .env de prod (1Password) com DPKEYS_BACKUP_PASSWORD
#   3. Pwsh este script para repor as keys ANTES de arrancar a API
#   4. Continuar restore SQL (Restore-from-R2.sh)
#   5. docker compose up -d
#
# Uso:
#   pwsh ./scripts/Restore-DpKeys.ps1 \
#     -R2Key "dp-keys/2026/05/dp-keys-20260524-0330.tar.aes" \
#     -DestPath "./data/dp-keys"
#
# Env vars obrigatórias:
#   R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET
#   DPKEYS_BACKUP_PASSWORD

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)][string]$R2Key,
    [string]$DestPath = "./data/dp-keys",
    [string]$TempFile = "",
    # Sprint 254 (Doc 78 cron mensal): valida apenas que o backup é recuperável
    # SEM tocar em ./data/dp-keys. Modo drill — usado para o cron mensal.
    # Quando -ValidateOnly: pega no último .tar.aes do R2 (mais recente por LastModified),
    # decrypt, confirma tar válido, e sai 0. Sem -ValidateOnly: precisa de -R2Key.
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "  -> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red }

# Pré-condições
foreach ($v in @("R2_ACCOUNT_ID", "R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY", "R2_BUCKET", "DPKEYS_BACKUP_PASSWORD")) {
    if (-not (Test-Path "Env:$v")) {
        Write-Fail "Env var $v em falta. Recupera do 1Password."
        exit 1
    }
}

if ($DPKEYS_BACKUP_PASSWORD.Length -lt 16) {
    # Variável vai vir de env, não inline — verificar comprimento
    Write-Fail "DPKEYS_BACKUP_PASSWORD deve ter pelo menos 16 chars"
    exit 1
}

if (-not $TempFile) {
    $TempFile = Join-Path ([System.IO.Path]::GetTempPath()) "dp-keys-restore-$([Guid]::NewGuid().ToString('N')).tar.aes"
}

# Validate mode: encontra o último .tar.aes automaticamente
if ($ValidateOnly -and -not $R2Key) {
    Write-Host "=== DP keys backup VALIDATION drill ===" -ForegroundColor Yellow
    Write-Step "Listar últimos backups dp-keys em R2..."
    $awsCmd = Get-Command aws -ErrorAction SilentlyContinue
    if (-not $awsCmd) { Write-Fail "AWS CLI obrigatório"; exit 2 }
    $env:AWS_ACCESS_KEY_ID = $env:R2_ACCESS_KEY_ID
    $env:AWS_SECRET_ACCESS_KEY = $env:R2_SECRET_ACCESS_KEY
    $endpoint = "https://$($env:R2_ACCOUNT_ID).r2.cloudflarestorage.com"
    $list = aws s3api list-objects-v2 `
        --bucket $env:R2_BUCKET `
        --prefix "dp-keys/" `
        --endpoint-url $endpoint `
        --region auto `
        --query "reverse(sort_by(Contents, &LastModified))[0].Key" `
        --output text 2>&1
    if ($LASTEXITCODE -ne 0 -or -not $list -or $list -eq "None") {
        Write-Fail "Nenhum dp-keys backup encontrado em R2"
        exit 3
    }
    $R2Key = $list.Trim()
    Write-Ok "Último backup: $R2Key"
}

if (-not $R2Key) {
    Write-Fail "Falta -R2Key (ou usar -ValidateOnly para detectar último automaticamente)"
    exit 1
}

Write-Host ""
Write-Host "=== Restore DataProtection keys ===" -ForegroundColor Yellow
Write-Host "R2 key: $R2Key"
Write-Host "Dest:   $DestPath"

# 1. Download R2 → tempfile (usa AWS CLI se disponível, senão curl + signed request)
Write-Step "Download de R2..."
$awsCmd = Get-Command aws -ErrorAction SilentlyContinue
if ($awsCmd) {
    $env:AWS_ACCESS_KEY_ID = $env:R2_ACCESS_KEY_ID
    $env:AWS_SECRET_ACCESS_KEY = $env:R2_SECRET_ACCESS_KEY
    $endpoint = "https://$($env:R2_ACCOUNT_ID).r2.cloudflarestorage.com"
    aws s3 cp "s3://$($env:R2_BUCKET)/$R2Key" $TempFile `
        --endpoint-url $endpoint `
        --region auto
    if ($LASTEXITCODE -ne 0) { Write-Fail "aws s3 cp falhou"; exit 2 }
} else {
    Write-Fail "AWS CLI não encontrado. Instala 'aws' ou usa rclone com R2 configurado."
    exit 2
}
Write-Ok "Downloaded para $TempFile"

# 2. Ler payload + parse header
Write-Step "Validar magic + version..."
$encrypted = [System.IO.File]::ReadAllBytes($TempFile)
if ($encrypted.Length -lt (8 + 1 + 16 + 12 + 16)) {
    Write-Fail "Payload demasiado curto ($($encrypted.Length) bytes)"
    exit 3
}
$magic = [System.Text.Encoding]::ASCII.GetString($encrypted, 0, 8)
if ($magic -ne "MDRDP_K1") {
    Write-Fail "Magic inválido: '$magic' (esperava MDRDP_K1)"
    exit 4
}
if ($encrypted[8] -ne 0x01) {
    Write-Fail "Versão desconhecida: $($encrypted[8])"
    exit 5
}
Write-Ok "Magic + version OK"

# 3. Decrypt (AES-GCM via .NET nativo)
Write-Step "Derivar chave + decrypt..."
$salt = $encrypted[9..24]      # 16 bytes
$nonce = $encrypted[25..36]    # 12 bytes
$tag = $encrypted[37..52]      # 16 bytes
$cipher = $encrypted[53..($encrypted.Length - 1)]

Add-Type -AssemblyName System.Security.Cryptography
$pbkdf2 = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
    $env:DPKEYS_BACKUP_PASSWORD,
    $salt,
    100000,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256)
$key = $pbkdf2.GetBytes(32)
$pbkdf2.Dispose()

$plain = New-Object byte[] $cipher.Length
$aes = [System.Security.Cryptography.AesGcm]::new($key, 16)
try {
    $aes.Decrypt($nonce, $cipher, $tag, $plain, $null)
}
catch {
    Write-Fail "Decrypt falhou — password errada ou payload tampered: $_"
    exit 6
}
finally { $aes.Dispose() }
Write-Ok "Decrypt OK ($($plain.Length) bytes plaintext)"

# 4. Modo validate-only: confirma tar válido + conta entries, sem extrair
if ($ValidateOnly) {
    Write-Step "Modo VALIDATE-ONLY: verificar tar sem extrair..."
    $tarTmp = "$TempFile.tar"
    [System.IO.File]::WriteAllBytes($tarTmp, $plain)
    $entries = tar -tf $tarTmp 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "tar -tf falhou — payload corrompido"
        Remove-Item $tarTmp -Force -ErrorAction SilentlyContinue
        Remove-Item $TempFile -Force -ErrorAction SilentlyContinue
        exit 9
    }
    $count = ($entries | Measure-Object -Line).Lines
    Remove-Item $tarTmp -Force
    Remove-Item $TempFile -Force
    Write-Ok "$count entries no tarball — payload OK"
    Write-Host ""
    Write-Host "=== Validate OK ===" -ForegroundColor Green
    Write-Host "Backup $R2Key recuperável. dp-keys NÃO foi tocado."
    Write-Host ""
    exit 0
}

# 5. Extrair tar para DestPath (restore real)
Write-Step "Extrair tar para $DestPath..."
if (Test-Path $DestPath) {
    $existing = (Get-ChildItem -Path $DestPath -ErrorAction SilentlyContinue).Count
    if ($existing -gt 0) {
        Write-Host "AVISO: $DestPath já contém $existing ficheiros." -ForegroundColor Yellow
        $confirm = Read-Host "Sobrepor? (yes/no)"
        if ($confirm -ne "yes") { Write-Host "Cancelado."; exit 7 }
    }
}
New-Item -ItemType Directory -Path $DestPath -Force | Out-Null

$tarTmp = "$TempFile.tar"
[System.IO.File]::WriteAllBytes($tarTmp, $plain)
tar -xf $tarTmp -C $DestPath
if ($LASTEXITCODE -ne 0) { Write-Fail "tar extract falhou"; exit 8 }
Remove-Item $tarTmp -Force

$restored = (Get-ChildItem -Path $DestPath -Recurse -File).Count
Write-Ok "$restored keys extraídas"

# 5. Limpar
Remove-Item $TempFile -Force

Write-Host ""
Write-Host "=== Restore OK ===" -ForegroundColor Green
Write-Host "Próximo passo: docker compose up -d (container vai montar $DestPath em /data/dp-keys)"
Write-Host ""
exit 0
