# ============================================================================
# Setup Completo - LocalDB + Restauração de Backup
# ============================================================================
# Este script faz todo o setup em um único comando:
# 1. Cria instância LocalDB
# 2. Cria banco de dados
# 3. Restaura backup
#
# Uso:
#   .\Setup-Complete.ps1
#
# ============================================================================

param(
    [string]$BackupFileName = "BetSniffer_10_06_25.bak",
    [string]$DataPath = "./Data/LocalDB",
    [string]$InstanceName = "mssqllocaldb",
    [string]$DatabaseName = "BetArbitrageDB_DEV"
)

# Cores
$SuccessColor = "Green"
$WarningColor = "Yellow"
$ErrorColor = "Red"
$InfoColor = "Cyan"

function Write-Success { param([string]$M) Write-Host "? $M" -ForegroundColor $SuccessColor }
function Write-Warning { param([string]$M) Write-Host "??  $M" -ForegroundColor $WarningColor }
function Write-Error-C { param([string]$M) Write-Host "? $M" -ForegroundColor $ErrorColor }
function Write-Info { param([string]$M) Write-Host "??  $M" -ForegroundColor $InfoColor }

Write-Info "=========================================="
Write-Info "Setup Completo - BetSniffer.Api"
Write-Info "=========================================="
Write-Info ""

# PHASE 1: Setup LocalDB
Write-Info "PHASE 1??  - Configurando LocalDB"
Write-Info "=================================="

# Verificar LocalDB
Write-Info "Verificando LocalDB..."
try {
    $sqllocaldb = & sqllocaldb -? 2>$null
    Write-Success "LocalDB encontrado"
} catch {
    Write-Error-C "LocalDB não encontrado!"
    exit 1
}

# Criar pasta
Write-Info "Criando pasta de dados..."
$fullDataPath = (Resolve-Path -Path $DataPath -ErrorAction SilentlyContinue).Path
if (!$fullDataPath) {
    New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
    $fullDataPath = (Resolve-Path -Path $DataPath).Path
}
Write-Success "Pasta: $fullDataPath"

# Verificar instância
Write-Info "Configurando instância LocalDB..."
$instances = & sqllocaldb info
if ($instances -contains $InstanceName) {
    Write-Warning "Instância existe, removendo..."
    & sqllocaldb stop $InstanceName -k 2>$null
    Start-Sleep -Seconds 2
    & sqllocaldb delete $InstanceName -f 2>$null
    Start-Sleep -Seconds 2
}

# Criar instância
Write-Info "Criando instância '$InstanceName'..."
& sqllocaldb create $InstanceName -s 2>$null
Start-Sleep -Seconds 2
Write-Success "Instância criada"

# Iniciar instância
Write-Info "Iniciando instância..."
& sqllocaldb start $InstanceName 2>$null
Start-Sleep -Seconds 2
Write-Success "Instância iniciada"

# Criar banco
Write-Info "Criando banco '$DatabaseName'..."
sqlcmd -S "(localdb)\$InstanceName" -E -Q "CREATE DATABASE [$DatabaseName] ON (NAME = '$DatabaseName', FILENAME = '$fullDataPath\$DatabaseName.mdf')" 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Banco pode já existir, tentando dropar..."
    sqlcmd -S "(localdb)\$InstanceName" -E -Q "DROP DATABASE IF EXISTS [$DatabaseName]" 2>$null
    Start-Sleep -Seconds 1
    sqlcmd -S "(localdb)\$InstanceName" -E -Q "CREATE DATABASE [$DatabaseName] ON (NAME = '$DatabaseName', FILENAME = '$fullDataPath\$DatabaseName.mdf')" 2>$null
}

Write-Success "Banco criado"

Write-Info ""
Write-Success "? PHASE 1 Concluída"
Write-Info ""

# PHASE 2: Restauração de Backup
Write-Info "PHASE 2??  - Restaurando Backup"
Write-Info "=================================="

# Procurar arquivo
Write-Info "Procurando arquivo de backup '$BackupFileName'..."

$backupLocations = @(
    $BackupFileName,
    ".\$BackupFileName",
    ".\Data\$BackupFileName",
    ".\Data\LocalDB\$BackupFileName",
    "$PSScriptRoot\Data\$BackupFileName",
    "$PSScriptRoot\Data\LocalDB\$BackupFileName"
)

$backupFilePath = $null
foreach ($location in $backupLocations) {
    if (Test-Path $location) {
        $backupFilePath = (Resolve-Path $location).Path
        break
    }
}

if ($null -eq $backupFilePath) {
    Write-Error-C "Arquivo não encontrado: $BackupFileName"
    Write-Warning "Continuando sem restauração..."
    Write-Info "Para restaurar depois, execute:"
    Write-Host ".\Scripts\Restore-DatabaseBackup.ps1 -BackupFile '$BackupFileName'" -ForegroundColor Yellow
} else {
    Write-Success "Arquivo encontrado: $backupFilePath"
    
    # Restaurar
    Write-Info "Restaurando backup..."
    try {
        $restoreQuery = "RESTORE DATABASE [$DatabaseName] FROM DISK = '$backupFilePath' WITH REPLACE"
        sqlcmd -S "(localdb)\$InstanceName" -E -Q $restoreQuery
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Backup restaurado"
        } else {
            throw "Erro na restauração"
        }
    } catch {
        Write-Error-C "Erro ao restaurar: $_"
        Write-Warning "Banco foi criado mas não contém dados"
    }
    
    Write-Info ""
    Write-Success "? PHASE 2 Concluída"
}

Write-Info ""

# SUMMARY
Write-Info "?? RESUMO FINAL"
Write-Info "=================================="
Write-Success "Instância LocalDB: $InstanceName"
Write-Success "Banco de Dados: $DatabaseName"
Write-Success "Caminho de Dados: $fullDataPath"
Write-Success "Connection String:"
Write-Host "  Server=(localdb)\$InstanceName;Database=$DatabaseName;Integrated Security=true;Encrypt=false;" -ForegroundColor Cyan

Write-Info ""
Write-Info "?? PRÓXIMOS PASSOS"
Write-Info "=================================="
Write-Host "1. Execute a aplicação:" -ForegroundColor Yellow
Write-Host "   dotnet run" -ForegroundColor Yellow

Write-Info ""
Write-Host "2. Verifique se tudo está funcionando:" -ForegroundColor Yellow
Write-Host "   sqlcmd -S '(localdb)\$InstanceName' -E -d $DatabaseName -Q 'SELECT COUNT(*) FROM GamesInfo'" -ForegroundColor Yellow

Write-Info ""
Write-Success "? Setup completo! ?"
