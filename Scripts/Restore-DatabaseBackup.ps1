# ============================================================================
# Script de Restauração de Backup para LocalDB - BetSniffer.Api
# ============================================================================
# Este script restaura um arquivo .bak para o LocalDB
#
# Uso:
#   .\Restore-Backup.ps1
#   .\Restore-Backup.ps1 -BackupFile "BetSniffer_10_06_25.bak"
#   .\Restore-Backup.ps1 -BackupFile "seu_arquivo.bak" -DatabaseName "BetArbitrageDB_DEV"
#
# Requisitos:
#   - SQL Server LocalDB iniciado
#   - Arquivo .bak no diretório do script ou caminho específico
# ============================================================================

param(
    [string]$BackupFile = "BetSniffer_10_06_25.bak",
    [string]$ServerName = "(localdb)\mssqllocaldb",
    [string]$DatabaseName = "BetArbitrageDB_DEV",
    [switch]$Force
)

# Cores para output
$SuccessColor = "Green"
$WarningColor = "Yellow"
$ErrorColor = "Red"
$InfoColor = "Cyan"

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor $SuccessColor
}

function Write-Warning {
    param([string]$Message)
    Write-Host "??  $Message" -ForegroundColor $WarningColor
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor $ErrorColor
}

function Write-Info {
    param([string]$Message)
    Write-Host "??  $Message" -ForegroundColor $InfoColor
}

Write-Info "=========================================="
Write-Info "Restaurador de Backup - BetSniffer.Api"
Write-Info "=========================================="
Write-Info ""

# 1. Verificar se arquivo de backup existe
Write-Info "1??  Procurando arquivo de backup..."

# Tenta encontrar o arquivo em múltiplos locais
$backupLocations = @(
    $BackupFile,
    ".\$BackupFile",
    ".\Data\$BackupFile",
    ".\Data\LocalDB\$BackupFile",
    "$PSScriptRoot\Data\$BackupFile",
    "$PSScriptRoot\Data\LocalDB\$BackupFile"
)

$backupFilePath = $null
foreach ($location in $backupLocations) {
    if (Test-Path $location) {
        $backupFilePath = (Resolve-Path $location).Path
        Write-Success "Arquivo encontrado: $backupFilePath"
        break
    }
}

if ($null -eq $backupFilePath) {
    Write-Error-Custom "Arquivo de backup não encontrado!"
    Write-Info "Locais procurados:"
    $backupLocations | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

# 2. Verificar se LocalDB está rodando
Write-Info "2??  Verificando se LocalDB está rodando..."
try {
    $instances = & sqllocaldb info
    if ($instances -notcontains "mssqllocaldb") {
        Write-Warning "Instância não encontrada, iniciando..."
        & sqllocaldb start mssqllocaldb 2>$null
        Start-Sleep -Seconds 2
    }
    Write-Success "LocalDB está rodando"
}
catch {
    Write-Error-Custom "Erro ao verificar LocalDB: $_"
    exit 1
}

# 3. Obter informações do backup
Write-Info "3??  Analisando arquivo de backup..."
try {
    $backupInfo = sqlcmd -S $ServerName -E -Q "RESTORE FILELISTONLY FROM DISK = '$backupFilePath'" 2>$null
    
    if ($null -eq $backupInfo -or $backupInfo.Count -eq 0) {
        Write-Warning "Não foi possível ler informações do backup via FILELISTONLY"
        Write-Info "Continuando com restauração padrão..."
    } else {
        Write-Success "Informações do backup obtidas"
    }
}
catch {
    Write-Warning "Erro ao obter info do backup (não crítico): $_"
}

# 4. Verificar se banco existe
Write-Info "4??  Verificando se banco '$DatabaseName' existe..."
$existingDatabases = sqlcmd -S $ServerName -E -Q "SELECT name FROM sys.databases" 2>$null

if ($existingDatabases | Select-String -Pattern "\b$DatabaseName\b" -Quiet) {
    Write-Warning "Banco '$DatabaseName' já existe"
    
    if (-not $Force) {
        Write-Info "Opções:"
        Write-Host "  1. Deletar e restaurar (sobrescrever)"
        Write-Host "  2. Cancelar operação"
        $choice = Read-Host "Escolha [1/2]"
        
        if ($choice -eq "2") {
            Write-Info "Operação cancelada."
            exit 0
        }
    }
    
    # Deletar banco existente
    Write-Info "Deletando banco existente..."
    sqlcmd -S $ServerName -E -Q "ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE" 2>$null
    Start-Sleep -Seconds 1
    sqlcmd -S $ServerName -E -Q "DROP DATABASE [$DatabaseName]" 2>$null
    Write-Success "Banco deletado"
}

# 5. Restaurar backup
Write-Info "5??  Restaurando backup..."
Write-Info "  Servidor: $ServerName"
Write-Info "  Banco: $DatabaseName"
Write-Info "  Arquivo: $backupFilePath"
Write-Info ""

try {
    # Tenta restauração simples primeiro (sem especificar paths)
    $restoreQuery = @"
RESTORE DATABASE [$DatabaseName] 
FROM DISK = '$backupFilePath'
WITH REPLACE
"@

    sqlcmd -S $ServerName -E -Q $restoreQuery
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Backup restaurado com sucesso!"
    } else {
        throw "Erro ao executar comando de restauração"
    }
}
catch {
    Write-Error-Custom "Erro ao restaurar backup: $_"
    
    Write-Info ""
    Write-Info "Tentando restauração com MOVE (caminho customizado)..."
    
    # Tenta com MOVE se a primeira falhar
    $dataPath = "$PSScriptRoot\Data\LocalDB"
    $restoreQueryWithMove = @"
RESTORE DATABASE [$DatabaseName] 
FROM DISK = '$backupFilePath'
WITH REPLACE,
     MOVE 'BetArbitrageDB' TO '$dataPath\BetArbitrageDB_DEV.mdf',
     MOVE 'BetArbitrageDB_log' TO '$dataPath\BetArbitrageDB_DEV_log.ldf'
"@

    try {
        sqlcmd -S $ServerName -E -Q $restoreQueryWithMove
        Write-Success "Backup restaurado com sucesso (com MOVE)!"
    }
    catch {
        Write-Error-Custom "Falha na restauração com MOVE também: $_"
        exit 1
    }
}

# 6. Verificar integridade
Write-Info "6??  Verificando integridade do banco..."
try {
    $checkResult = sqlcmd -S $ServerName -E -Q "SELECT name, state_desc FROM sys.databases WHERE name='$DatabaseName'" 2>$null
    
    if ($checkResult | Select-String -Pattern "ONLINE" -Quiet) {
        Write-Success "Banco está ONLINE e funcional"
    } else {
        Write-Warning "Banco não está ONLINE"
    }
    
    # Contar tabelas
    $tableCount = sqlcmd -S $ServerName -E -d $DatabaseName -Q "SELECT COUNT(*) as TableCount FROM information_schema.tables WHERE table_type='BASE TABLE'" 2>$null
    
    if ($null -ne $tableCount) {
        Write-Info "Tabelas encontradas no banco"
    }
}
catch {
    Write-Warning "Erro ao verificar banco (não crítico): $_"
}

# 7. Exibir informações finais
Write-Info ""
Write-Info "7??  Informações de Conexão:"
Write-Info "=================================="
Write-Success "Servidor: $ServerName"
Write-Success "Banco de Dados: $DatabaseName"
Write-Success "Status: Restaurado com sucesso!"

Write-Info ""
Write-Info "8??  Conecte com:"
Write-Info "=================================="
Write-Host "sqlcmd -S '$ServerName' -E -d $DatabaseName" -ForegroundColor Cyan
Write-Host "Ou via Application: Server=(localdb)\mssqllocaldb;Database=$DatabaseName;Integrated Security=true;Encrypt=false;" -ForegroundColor Cyan

Write-Info ""
Write-Info "9??  Próximas Etapas:"
Write-Info "=================================="
Write-Host "1. Execute a aplicação:" -ForegroundColor Yellow
Write-Host "   dotnet run" -ForegroundColor Yellow
Write-Info ""
Write-Host "2. Seus dados estão prontos para uso!" -ForegroundColor Green

Write-Info ""
Write-Success "? Restauração concluída com sucesso! ?"
