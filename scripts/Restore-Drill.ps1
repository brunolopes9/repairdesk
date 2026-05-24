# Sprint 250 (Doc 75 áreas 2+12 P0): restore drill que valida que o último .bak
# é restaurável SEM mexer na BD de produção.
#
# Restaura para uma BD temporária 'RepairDesk_DrillTest' no mesmo container SQL,
# corre umas queries de sanity check, e faz DROP no fim. Reporta OK/FAIL.
#
# Uso:
#   pwsh ./scripts/Restore-Drill.ps1
#   pwsh ./scripts/Restore-Drill.ps1 -BackupFile "./backups/RepairDesk-20260524.bak"
#
# Cron sugerido (Hetzner produção, no host):
#   0 4 1 * * cd /opt/repairdesk && pwsh scripts/Restore-Drill.ps1 > /var/log/restore-drill.log 2>&1
#   (mensal, dia 1 às 04:00 — uma hora depois do backup automático das 03:00)

[CmdletBinding()]
param(
    [string]$BackupFile,
    [string]$BackupDir = "./backups",
    [string]$Container = "repairdesk-db",
    [string]$TestDbName = "RepairDesk_DrillTest",
    [string]$SourceDbName = "RepairDesk"
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "  -> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red }

# Password SA do compose; ambiente pode override.
$saPassword = $env:DB_SA_PASSWORD
if (-not $saPassword) { $saPassword = "Repair!Desk2026" }

Write-Host ""
Write-Host "=== Restore Drill ===" -ForegroundColor Yellow
Write-Host "Container: $Container | Source: $SourceDbName | Test: $TestDbName"

# 1. Encontrar backup mais recente se não dado
if (-not $BackupFile) {
    Write-Step "Procurar último .bak em $BackupDir"
    $latest = Get-ChildItem -Path $BackupDir -Filter "*.bak" -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $latest) {
        Write-Fail "Nenhum .bak encontrado em $BackupDir"
        exit 2
    }
    $BackupFile = $latest.FullName
    $ageHours = [int]((Get-Date) - $latest.LastWriteTime).TotalHours
    Write-Ok "Último: $($latest.Name) ($ageHours h atrás)"
    if ($ageHours -gt 30) {
        Write-Fail "Backup tem mais de 30h — investigar BackupHostedService"
        exit 3
    }
}

if (-not (Test-Path $BackupFile)) {
    Write-Fail "Backup não existe: $BackupFile"
    exit 4
}

# Path dentro do container (volume bind-mount)
$backupName = Split-Path $BackupFile -Leaf
$containerPath = "/backups/$backupName"

function Invoke-Sqlcmd-InContainer($query) {
    $output = docker exec -i $Container /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $saPassword -No -h-1 -W -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd falhou (exit $LASTEXITCODE):`n$output"
    }
    return $output
}

# 2. Confirmar que o container está up e backup é visível lá dentro
Write-Step "Verificar container + backup acessível"
$check = docker exec $Container test -f $containerPath 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Fail "$containerPath não acessível dentro do container — bind mount falhou?"
    exit 5
}
Write-Ok "Backup acessível em $containerPath"

# 3. Drop DB de drill anterior se existir (idempotente)
Write-Step "Limpar DB drill anterior (se existir)"
Invoke-Sqlcmd-InContainer "IF DB_ID('$TestDbName') IS NOT NULL BEGIN ALTER DATABASE [$TestDbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TestDbName]; END" | Out-Null
Write-Ok "Pronto"

# 4. Ler logical filenames do backup
Write-Step "Inspeccionar logical filenames do .bak"
$fileListResult = Invoke-Sqlcmd-InContainer "RESTORE FILELISTONLY FROM DISK = N'$containerPath'"
$dataLogical = $null
$logLogical = $null
foreach ($line in $fileListResult) {
    if ($line -match "^\s*(\S+)\s+(\S+)\s+(D|L)\s") {
        if ($matches[3] -eq "D" -and -not $dataLogical) { $dataLogical = $matches[1] }
        if ($matches[3] -eq "L" -and -not $logLogical)  { $logLogical = $matches[1] }
    }
}
if (-not $dataLogical -or -not $logLogical) {
    Write-Fail "Não consegui parse de FILELISTONLY"
    Write-Host $fileListResult
    exit 6
}
Write-Ok "Data: $dataLogical | Log: $logLogical"

# 5. RESTORE para DB de drill com MOVE para ficheiros novos
Write-Step "RESTORE para $TestDbName"
$restoreQuery = @"
RESTORE DATABASE [$TestDbName]
FROM DISK = N'$containerPath'
WITH
    MOVE N'$dataLogical' TO N'/var/opt/mssql/data/${TestDbName}.mdf',
    MOVE N'$logLogical'  TO N'/var/opt/mssql/data/${TestDbName}_log.ldf',
    REPLACE, RECOVERY, STATS = 10
"@
$restoreOutput = Invoke-Sqlcmd-InContainer $restoreQuery
Write-Ok "RESTORE concluído"

# 6. Sanity checks — confirmar que existem tabelas e contagens razoáveis
Write-Step "Sanity checks"
$tableCount = (Invoke-Sqlcmd-InContainer "USE [$TestDbName]; SELECT COUNT(*) FROM sys.tables").Trim()
if ([int]$tableCount -lt 10) {
    Write-Fail "Apenas $tableCount tabelas restauradas — algo está errado"
    exit 7
}
Write-Ok "$tableCount tabelas restauradas"

$tenantCount = (Invoke-Sqlcmd-InContainer "USE [$TestDbName]; SELECT COUNT(*) FROM Tenants").Trim()
$userCount = (Invoke-Sqlcmd-InContainer "USE [$TestDbName]; SELECT COUNT(*) FROM AspNetUsers").Trim()
Write-Ok "Tenants: $tenantCount | Users: $userCount"

if ([int]$tenantCount -eq 0) {
    Write-Fail "0 tenants no backup — backup vazio?"
    exit 8
}

# 7. Cleanup
Write-Step "Cleanup DB drill"
Invoke-Sqlcmd-InContainer "ALTER DATABASE [$TestDbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TestDbName]" | Out-Null
Write-Ok "Drill DB removida"

Write-Host ""
Write-Host "=== DRILL OK ===" -ForegroundColor Green
Write-Host "Backup $backupName é restaurável."
Write-Host ""
exit 0
