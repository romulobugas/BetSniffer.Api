# ============================================================================
# Script de Inicialização do LocalDB para BetSniffer.Api
# ============================================================================
# Este script configura e inicializa o LocalDB para desenvolvimento local
# 
# Uso: 
#   .\Initialize-LocalDB.ps1
#   .\Initialize-LocalDB.ps1 -DataPath "C:\MyData\LocalDB" -InstanceName "BetSnifferDev"
#
# Requisitos:
#   - SQL Server LocalDB instalado (incluído no Visual Studio)
#   - PowerShell 5.0+ com permissões de administrador
#   - Entity Framework Core Tools
# ============================================================================

param(
    [string]$DataPath = "./Data/LocalDB",
    [string]$InstanceName = "mssqllocaldb",
    [string]$DatabaseName = "BetArbitrageDB_DEV"
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
Write-Info "Inicializador de LocalDB - BetSniffer.Api"
Write-Info "=========================================="
Write-Info ""

# 1. Verificar se LocalDB está instalado
Write-Info "1??  Verificando se LocalDB está instalado..."
try {
    $sqllocaldb = & sqllocaldb -?
    Write-Success "LocalDB encontrado!"
}
catch {
    Write-Error-Custom "LocalDB não encontrado! Instale o SQL Server LocalDB."
    Write-Warning "Acesse: https://learn.microsoft.com/pt-br/sql/database-engine/configure-windows/sql-server-express-localdb"
    exit 1
}

# 2. Criar pasta de dados se não existir
Write-Info "2??  Criando pasta de dados..."
$fullDataPath = (Resolve-Path -Path $DataPath -ErrorAction SilentlyContinue).Path
if (!$fullDataPath) {
    $fullDataPath = (Get-Item $DataPath -ErrorAction SilentlyContinue).FullName
    if (!$fullDataPath) {
        New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
        $fullDataPath = (Resolve-Path -Path $DataPath).Path
    }
}

Write-Success "Pasta de dados: $fullDataPath"

# 3. Verificar se instância LocalDB existe
Write-Info "3??  Verificando se instância LocalDB existe..."
$instances = & sqllocaldb info
if ($instances -contains $InstanceName) {
    Write-Warning "Instância '$InstanceName' já existe."
    
    # Parar a instância se estiver rodando
    Write-Info "Parando instância..."
    & sqllocaldb stop $InstanceName -k 2>$null
    Start-Sleep -Seconds 2
    
    # Deletar instância
    Write-Info "Removendo instância antiga..."
    & sqllocaldb delete $InstanceName -f 2>$null
    Start-Sleep -Seconds 2
}

# 4. Criar nova instância LocalDB
Write-Info "4??  Criando nova instância LocalDB..."
try {
    & sqllocaldb create $InstanceName -s 2>$null
    Write-Success "Instância '$InstanceName' criada!"
}
catch {
    Write-Error-Custom "Erro ao criar instância: $_"
    exit 1
}

# 5. Iniciar a instância
Write-Info "5??  Iniciando instância LocalDB..."
try {
    & sqllocaldb start $InstanceName 2>$null
    Start-Sleep -Seconds 2
    Write-Success "Instância iniciada!"
}
catch {
    Write-Error-Custom "Erro ao iniciar instância: $_"
    exit 1
}

# 6. Criar banco de dados
Write-Info "6??  Criando banco de dados '$DatabaseName'..."
$connectionString = "Server=(localdb)\$InstanceName;Integrated Security=true;Encrypt=false;"
try {
    $sqlCmd = "CREATE DATABASE [$DatabaseName] ON (NAME = '$DatabaseName', FILENAME = '$fullDataPath\$DatabaseName.mdf')"
    sqlcmd -S "(localdb)\$InstanceName" -E -Q "$sqlCmd" 2>$null
    
    # Se falhar, pode ser porque já existe, então tente dropar e recriar
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Banco pode já existir ou erro ocorreu. Tentando dropar e recriar..."
        sqlcmd -S "(localdb)\$InstanceName" -E -Q "DROP DATABASE IF EXISTS [$DatabaseName]" 2>$null
        Start-Sleep -Seconds 1
        sqlcmd -S "(localdb)\$InstanceName" -E -Q "$sqlCmd" 2>$null
    }
    
    Write-Success "Banco de dados criado!"
}
catch {
    Write-Error-Custom "Erro ao criar banco de dados: $_"
    exit 1
}

# 7. Exibir informações de conexão
Write-Info ""
Write-Info "7??  Informações de Conexão:"
Write-Info "=================================="
Write-Success "Instância LocalDB: $InstanceName"
Write-Success "Banco de Dados: $DatabaseName"
Write-Success "Caminho de Dados: $fullDataPath"
Write-Success "Connection String (Dev):"
Write-Host "   Server=(localdb)\$InstanceName;Database=$DatabaseName;Integrated Security=true;Encrypt=false;" -ForegroundColor Green

# 8. Instruções finais
Write-Info ""
Write-Info "8??  Próximas Etapas:"
Write-Info "=================================="
Write-Info "1. Atualize 'appsettings.json' em seu projeto:"
Write-Host @"
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\$InstanceName;Database=$DatabaseName;Integrated Security=true;Encrypt=false;"
  },
  "DatabaseSettings": {
    "UseLocalDb": true,
    "LocalDbDataPath": "$fullDataPath"
  }
}
"@ -ForegroundColor Gray

Write-Info ""
Write-Info "2. Execute as migrations do EF Core:"
Write-Host "   dotnet ef database update" -ForegroundColor Yellow

Write-Info ""
Write-Info "3. Restaure seu backup (se necessário):"
Write-Host "   sqlcmd -S '(localdb)\$InstanceName' -E -i seu_script.sql" -ForegroundColor Yellow

Write-Info ""
Write-Success "? LocalDB está pronto para desenvolvimento! ?"
