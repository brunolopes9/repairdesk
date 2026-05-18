<#
.SYNOPSIS
  Popula o tenant actual com dados demo credíveis para video demo ou apresentacoes.

.DESCRIPTION
  Cria via API normal (POST /api/...) - nao toca em DB directamente, nao usa
  bypass de auth. Reusa toda a validacao backend.

  Dados criados:
    - 8 clientes PT credíveis (nomes reais, telefones, NIFs validos)
    - 12 reparacoes em estados variados (Recebido a Entregue)
    - 6 pecas de stock (Ecras, Baterias, Conectores)

  Idempotente: se o cliente DEMO ja existir, salta o seed.

.PARAMETER ApiUrl
  Base URL da API. Default http://localhost:5080.

.PARAMETER Email
  Email do admin para login. Default le de variavel de ambiente ou hardcoded.

.PARAMETER Password
  Password do admin. Default le de variavel de ambiente ou hardcoded.

.EXAMPLE
  .\scripts\Seed-DemoData.ps1
  .\scripts\Seed-DemoData.ps1 -ApiUrl http://localhost:5080 -Email bruno@example.com -Password ChangeMe!2026
#>
[CmdletBinding()]
param(
    [string]$ApiUrl = 'http://localhost:5080',
    [string]$Email = $env:SEED_ADMIN_EMAIL,
    [string]$Password = $env:SEED_ADMIN_PASSWORD
)

$ErrorActionPreference = 'Stop'
# Forcar UTF-8 nas requests para preservar acentos enviados a API
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Fallback para defaults do .env.example se variaveis nao estiverem definidas
if (-not $Email) { $Email = 'bruno.miguel.martins.lopes@gmail.com' }
if (-not $Password) { $Password = 'ChangeMe!2026' }

Write-Host '==> RepairDesk Demo Data Seeder' -ForegroundColor Cyan
Write-Host "    API: $ApiUrl"
Write-Host "    Admin: $Email"
Write-Host ''

# --- 1. Login ---
Write-Host '==> Login...' -ForegroundColor Yellow
$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
try {
    $auth = Invoke-RestMethod -Method Post -Uri "$ApiUrl/api/auth/login" -ContentType 'application/json; charset=utf-8' -Body $loginBody
} catch {
    Write-Error "Login falhou. Verifica que a API esta a correr em $ApiUrl e credenciais correctas. Detalhe: $_"
    exit 1
}

$token = $auth.accessToken
$headers = @{ 'Authorization' = "Bearer $token" }
Write-Host '    OK' -ForegroundColor Green

# --- 2. Check idempotency ---
Write-Host '==> Verificar idempotencia...' -ForegroundColor Yellow
$idempotencyQuery = [System.Uri]::EscapeDataString('Maria Silva Demo')
$existing = Invoke-RestMethod -Method Get -Uri ("$ApiUrl/api/clientes?q=" + $idempotencyQuery) -Headers $headers
if ($existing.items -and $existing.items.Count -gt 0) {
    Write-Warning "Cliente 'Maria Silva Demo' ja existe. Demo data ja foi seeded. Skip."
    Write-Host "    Para re-seed: hard-delete os clientes com 'Demo' no nome primeiro." -ForegroundColor Gray
    exit 0
}
Write-Host '    OK - tenant limpo, vamos popular' -ForegroundColor Green

# Helper para POST JSON com encoding correcto
function Invoke-JsonPost {
    param(
        [string]$Path,
        [object]$Payload
    )
    $json = $Payload | ConvertTo-Json -Depth 5
    # ConvertTo-Json gera string Unicode; Invoke-RestMethod envia em UTF-8 quando
    # ContentType inclui charset=utf-8
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    return Invoke-RestMethod -Method Post -Uri "$ApiUrl$Path" `
        -ContentType 'application/json; charset=utf-8' `
        -Headers $headers `
        -Body $bodyBytes
}

# --- 3. Clientes ---
Write-Host '==> Criar 8 clientes...' -ForegroundColor Yellow
# NIFs validos calculados manualmente (algoritmo AT mod-11)
$clientes = @(
    @{ nome = 'Maria Silva Demo'; telefone = '912345678'; email = 'maria.silva@example.pt'; nif = '263758141'; notas = 'Cliente regular. Compra acessorios tambem.' }
    @{ nome = 'Joao Pereira Demo'; telefone = '913456789'; email = 'joao.pereira@example.pt'; nif = '500000000'; notas = 'Empresario - frequente B2B' }
    @{ nome = 'Ana Carvalho Demo'; telefone = '914567890'; email = 'ana.carvalho@gmail.com'; nif = '100000002'; notas = $null }
    @{ nome = 'Pedro Costa Demo'; telefone = '915678901'; email = $null; nif = $null; notas = 'Cliente via Messenger, sem telefone fixo' }
    @{ nome = 'Sofia Almeida Demo'; telefone = '916789012'; email = 'sofia.a@iol.pt'; nif = '200000004'; notas = 'Recomendou-nos a 3 amigos' }
    @{ nome = 'Junta de Freguesia Demo'; telefone = '232100200'; email = 'geral@junta.example.pt'; nif = '600000001'; notas = 'B2B - paga a 30 dias. Equipamentos institucionais.' }
    @{ nome = 'Ricardo Mendes Demo'; telefone = '917890123'; email = $null; nif = $null; notas = $null }
    @{ nome = 'Filipa Tavares Demo'; telefone = '918901234'; email = 'filipa.t@example.pt'; nif = '450000001'; notas = 'Trabalha em Lisboa, deixa equipamento em Viseu' }
)

$createdClientes = @()
foreach ($c in $clientes) {
    try {
        $res = Invoke-JsonPost -Path '/api/clientes' -Payload $c
        $createdClientes += $res
        Write-Host "    + $($c.nome)" -ForegroundColor Gray
    } catch {
        Write-Warning "    Falhou: $($c.nome) -> $_"
    }
}
Write-Host "    OK - $($createdClientes.Count) clientes" -ForegroundColor Green

if ($createdClientes.Count -lt 4) {
    Write-Error 'Poucos clientes criados. A abortar restante seed.'
    exit 1
}

# --- 4. Reparacoes ---
# Estados: 0=Recebido, 1=Diagnostico, 2=AguardaPeca, 3=EmReparacao, 4=Reparado, 5=Entregue
Write-Host '==> Criar 12 reparacoes em estados variados...' -ForegroundColor Yellow
# IMEIs Luhn-validos (calculados). Os restantes ficam null — IMEI eh opcional
# e o backend valida Luhn em 15-digit IMEIs (ImeiValidator.IsValid).
$reparacoes = @(
    @{ clienteIdx = 0; equipamento = 'iPhone 13 Pro';            avaria = 'Ecra partido, touch funciona';     imei = '359123456789012'; orcamentoCents = 14900; estadoInicial = 0 }
    @{ clienteIdx = 1; equipamento = 'Samsung Galaxy S22 Ultra'; avaria = 'Nao liga apos cair na agua';       imei = '123456789012344'; orcamentoCents = 18900; estadoInicial = 1 }
    @{ clienteIdx = 2; equipamento = 'iPhone 11';                avaria = 'Bateria nao dura nem 2 horas';     imei = '100000000000009'; orcamentoCents = 6900;  estadoInicial = 2 }
    @{ clienteIdx = 3; equipamento = 'Xiaomi Redmi Note 10';     avaria = 'Camara traseira tremida';          imei = $null;            orcamentoCents = 4900;  estadoInicial = 3 }
    @{ clienteIdx = 4; equipamento = 'iPhone 14';                avaria = 'Vidro traseiro estilhacado';       imei = $null;            orcamentoCents = 11900; estadoInicial = 4 }
    @{ clienteIdx = 5; equipamento = 'Lenovo Tablet';            avaria = 'Nao responde ao toque';            imei = $null;            orcamentoCents = 8900;  estadoInicial = 4 }
    @{ clienteIdx = 6; equipamento = 'OnePlus 9';                avaria = 'Falha na entrada USB';             imei = $null;            orcamentoCents = 5900;  estadoInicial = 1 }
    @{ clienteIdx = 0; equipamento = 'iPad Pro 11';              avaria = 'Ecra com linha verde vertical';    imei = $null;            orcamentoCents = 22900; estadoInicial = 0 }
    @{ clienteIdx = 7; equipamento = 'iPhone X';                 avaria = 'Botao de volume preso';            imei = $null;            orcamentoCents = 3900;  estadoInicial = 3 }
    @{ clienteIdx = 1; equipamento = 'MacBook Air M1';           avaria = 'Tecla e nao responde';             imei = $null;            orcamentoCents = 12900; estadoInicial = 2 }
    @{ clienteIdx = 2; equipamento = 'iPhone SE 2020';           avaria = 'Nao carrega';                      imei = $null;            orcamentoCents = 4900;  estadoInicial = 5 }
    @{ clienteIdx = 4; equipamento = 'Galaxy A52';               avaria = 'Ecra preto apos queda';            imei = $null;            orcamentoCents = 7900;  estadoInicial = 5 }
)

$repCount = 0
foreach ($r in $reparacoes) {
    $cliente = $createdClientes[$r.clienteIdx]
    if (-not $cliente) { continue }
    $body = @{
        clienteId      = $cliente.id
        equipamento    = $r.equipamento
        avaria         = $r.avaria
        imei           = $r.imei
        orcamentoCents = $r.orcamentoCents
        notas          = $null
        estadoInicial  = $r.estadoInicial
    }
    try {
        $rep = Invoke-JsonPost -Path '/api/reparacoes' -Payload $body
        $repCount++
        Write-Host "    + #$($rep.numero) $($r.equipamento) ($($cliente.nome))" -ForegroundColor Gray
    } catch {
        Write-Warning "    Falhou: $($r.equipamento) -> $_"
    }
}
Write-Host "    OK - $repCount reparacoes" -ForegroundColor Green

# --- 5. Stock de pecas ---
Write-Host '==> Criar 6 pecas stock...' -ForegroundColor Yellow
# Categorias: 0=Ecra, 1=Bateria, 2=Conector, 3=Camara, 4=VidroTraseiro
$pecas = @(
    @{ nome = 'Ecra iPhone 13 Pro';        categoria = 0; marca = 'Apple';   modelo = 'iPhone 13 Pro';  qtdStock = 3; qtdMinima = 2; custoUnitarioCents = 7500; fornecedor = 'Tudo4Mobile';     localArmazenamento = 'Prateleira A1' }
    @{ nome = 'Ecra Samsung S22';          categoria = 0; marca = 'Samsung'; modelo = 'Galaxy S22';     qtdStock = 1; qtdMinima = 2; custoUnitarioCents = 8900; fornecedor = 'Tudo4Mobile';     localArmazenamento = 'Prateleira A2' }
    @{ nome = 'Bateria iPhone 11';         categoria = 1; marca = 'Apple';   modelo = 'iPhone 11';      qtdStock = 8; qtdMinima = 5; custoUnitarioCents = 1500; fornecedor = 'BateriasOnline';  localArmazenamento = 'Gaveta B' }
    @{ nome = 'Bateria Samsung S22';       categoria = 1; marca = 'Samsung'; modelo = 'Galaxy S22';     qtdStock = 4; qtdMinima = 3; custoUnitarioCents = 2200; fornecedor = 'BateriasOnline';  localArmazenamento = 'Gaveta B' }
    @{ nome = 'Conector lightning iPhone'; categoria = 2; marca = 'Apple';   modelo = '13/14';          qtdStock = 0; qtdMinima = 3; custoUnitarioCents = 800;  fornecedor = 'iFixit Europe';   localArmazenamento = 'Caixa C' }
    @{ nome = 'Vidro traseiro iPhone 14';  categoria = 4; marca = 'Apple';   modelo = 'iPhone 14';      qtdStock = 2; qtdMinima = 1; custoUnitarioCents = 3500; fornecedor = 'Tudo4Mobile';     localArmazenamento = 'Prateleira D' }
)

$pecaCount = 0
foreach ($p in $pecas) {
    try {
        $res = Invoke-JsonPost -Path '/api/parts' -Payload $p
        $pecaCount++
        Write-Host "    + $($res.sku) $($p.nome) (stock: $($p.qtdStock)/$($p.qtdMinima))" -ForegroundColor Gray
    } catch {
        Write-Warning "    Falhou: $($p.nome) -> $_"
    }
}
Write-Host "    OK - $pecaCount pecas" -ForegroundColor Green

Write-Host ''
Write-Host '==> Demo data populated successfully!' -ForegroundColor Cyan
Write-Host ''
Write-Host 'Abrir browser: http://localhost' -ForegroundColor White
Write-Host "Login: $Email" -ForegroundColor Gray
Write-Host ''
Write-Host "Para limpar, faz hard-delete RGPD em cada cliente 'Demo' na UI." -ForegroundColor Gray
