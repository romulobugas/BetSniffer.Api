# ??? Guia de Configuração LocalDB - BetSniffer.Api

## ?? Sumário

1. [O que é LocalDB?](#o-que-é-localdb)
2. [Pré-requisitos](#pré-requisitos)
3. [Instalação](#instalação)
4. [Configuração Automática](#configuração-automática)
5. [Configuração Manual](#configuração-manual)
6. [Alternância entre Desenvolvimento e Produção](#alternância-entre-desenvolvimento-e-produção)
7. [Troubleshooting](#troubleshooting)
8. [Comandos Úteis](#comandos-úteis)

---

## ?? O que é LocalDB?

**LocalDB** é uma versão leve do SQL Server Express especialmente projetada para desenvolvimento local. Características:

- ? Não requer instalação de um serviço Windows
- ? Usa arquivos de banco local (`.mdf` e `.ldf`)
- ? Totalmente compatível com SQL Server
- ? Ideal para múltiplos desenvolvedores sem conflitos
- ? Gratuito e incluído no Visual Studio

---

## ?? Pré-requisitos

### 1. SQL Server LocalDB Instalado

**Opção A: Via Visual Studio**
```
Visual Studio Installer ? Modificar ? 
Ferramentas de Banco de Dados SQL Server ? 
SQL Server Express LocalDB
```

**Opção B: Download Independente**
```
https://learn.microsoft.com/pt-br/sql/database-engine/configure-windows/sql-server-express-localdb
```

**Verificar instalação:**
```powershell
sqllocaldb versions
# Deve listar versões instaladas (v13.0, v14.0, v15.0, etc.)
```

### 2. PowerShell 5.0+ (Windows)

```powershell
$PSVersionTable.PSVersion
# Deve retornar versão 5.0 ou superior
```

### 3. Entity Framework Core Tools (Opcional)

```bash
dotnet tool install --global dotnet-ef
```

---

## ?? Instalação

### Passo 1: Verificar LocalDB

```powershell
# Listar todas as instâncias LocalDB
sqllocaldb info

# Listar versões disponíveis
sqllocaldb versions
```

### Passo 2: Criar Banco Local

#### Opção A: Automática (Recomendado)

```powershell
# Abra PowerShell como Administrador
cd F:\repository\BetSniffer.Api

# Execute o script de inicialização
.\Scripts\Initialize-LocalDB.ps1

# Ou com parâmetros customizados:
.\Scripts\Initialize-LocalDB.ps1 -DataPath "C:\MyLocalDB" -DatabaseName "BetSnifferDev"
```

#### Opção B: Manual

```powershell
# 1. Criar instância LocalDB
sqllocaldb create mssqllocaldb -s

# 2. Iniciar instância
sqllocaldb start mssqllocaldb

# 3. Criar banco de dados
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "CREATE DATABASE [BetArbitrageDB_DEV]"

# 4. Verificar criação
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "SELECT name FROM sys.databases"
```

---

## ?? Configuração Automática

### 1. Arquivo `appsettings.json` (Desenvolvimento)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BetArbitrageDB_DEV;Integrated Security=true;Encrypt=false;",
    "ProductionConnection": "Server=138.255.160.143;Database=BetArbitrageDB_PROD;User ID=sa;Password=bTRw&&SNwP8us+Se"
  },
  "DatabaseSettings": {
    "Environment": "Development",
    "UseLocalDb": true,
    "LocalDbDataPath": "./Data/LocalDB",
    "LocalDbInstanceName": "mssqllocaldb",
    "LocalDbInitialCatalog": "BetArbitrageDB_DEV"
  }
}
```

### 2. Arquivo `appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=138.255.160.143;Database=BetArbitrageDB_PROD;User ID=sa;Password=bTRw&&SNwP8us+Se",
    "ProductionConnection": "Server=138.255.160.143;Database=BetArbitrageDB_PROD;User ID=sa;Password=bTRw&&SNwP8us+Se"
  },
  "DatabaseSettings": {
    "Environment": "Production",
    "UseLocalDb": false,
    "LocalDbDataPath": null,
    "LocalDbInstanceName": null,
    "LocalDbInitialCatalog": null
  }
}
```

### 3. Program.cs (Configuração)

```csharp
// Configurar DatabaseSettings
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("DatabaseSettings"));

// Registrar serviço de conexão
builder.Services.AddScoped<DatabaseConnectionService>();

// Usar connection string dinâmica
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var connectionService = serviceProvider.GetRequiredService<DatabaseConnectionService>();
    var connectionString = connectionService.GetConnectionString();
    
    options.UseSqlServer(connectionString + ";TrustServerCertificate=True;");
});
```

---

## ?? Alternância entre Desenvolvimento e Produção

### Desenvolvimento Local

```bash
cd F:\repository\BetSniffer.Api

# Executar com appsettings.json (padrão)
dotnet run

# Ou explicitamente:
set ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

### Produção

```bash
# Publicar para produção
dotnet publish -c Release

# Copiar appsettings.Production.json para a pasta de publicação
# Executar:
set ASPNETCORE_ENVIRONMENT=Production
dotnet run
```

---

## ?? Restaurar Backup do Banco de Dados

Se você tem um backup do banco (arquivo `.sql`):

### Opção 1: Via Script SQL

```powershell
# Restaurar usando sqlcmd
sqlcmd -S "(localdb)\mssqllocaldb" -E -i seu_backup.sql
```

### Opção 2: Via SQL Server Management Studio (SSMS)

```
1. Abra SSMS
2. Conexão: (localdb)\mssqllocaldb
3. Botão direito em "Databases" ? "Restore Database"
4. Selecione seu arquivo de backup
```

### Opção 3: Via PowerShell

```powershell
$backupPath = "C:\seu\backup\database.bak"
$connectionString = "Server=(localdb)\mssqllocaldb;Integrated Security=true;"

# Copiar arquivo de backup para LocalDB
Copy-Item -Path $backupPath -Destination "./Data/LocalDB/"

# Restaurar (padrão para backup .sql)
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q `
    "RESTORE DATABASE [BetArbitrageDB_DEV] FROM DISK = 'C:\your\path\database.bak'"
```

---

## ??? Troubleshooting

### Problema 1: "LocalDB não encontrado"

**Solução:**
```powershell
# Instale LocalDB via SQL Server Express
# Ou via Visual Studio Installer
```

### Problema 2: "Cannot open database requested by the login"

**Solução:**
```powershell
# Verificar se o banco foi criado
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "SELECT name FROM sys.databases"

# Se não estiver listado, recrie:
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "CREATE DATABASE [BetArbitrageDB_DEV]"
```

### Problema 3: "Instância LocalDB não está respondendo"

**Solução:**
```powershell
# Parar instância
sqllocaldb stop mssqllocaldb -k

# Reiniciar
sqllocaldb start mssqllocaldb

# Ou reiniciar completamente
sqllocaldb delete mssqllocaldb
sqllocaldb create mssqllocaldb -s
sqllocaldb start mssqllocaldb
```

### Problema 4: "The database file is in use"

**Solução:**
```powershell
# Fechar todas as conexões
# Usar Process Explorer para ver quem está usando o arquivo

# Ou deletar e recriar:
sqllocaldb stop mssqllocaldb -k
sqllocaldb delete mssqllocaldb
# Limpar pasta: rm ./Data/LocalDB/*
sqllocaldb create mssqllocaldb -s
sqllocaldb start mssqllocaldb
```

---

## ?? Comandos Úteis

### Status do LocalDB

```powershell
# Listar todas as instâncias
sqllocaldb info

# Verificar se está rodando
sqllocaldb info mssqllocaldb

# Listar versões
sqllocaldb versions
```

### Gerenciar Instâncias

```powershell
# Criar instância
sqllocaldb create mssqllocaldb -s

# Iniciar
sqllocaldb start mssqllocaldb

# Parar
sqllocaldb stop mssqllocaldb

# Parar forçadamente
sqllocaldb stop mssqllocaldb -k

# Deletar
sqllocaldb delete mssqllocaldb

# Reset completo
sqllocaldb stop mssqllocaldb -k
sqllocaldb delete mssqllocaldb
rm ./Data/LocalDB/*
```

### Gerenciar Banco de Dados

```powershell
# Conectar ao LocalDB
sqlcmd -S "(localdb)\mssqllocaldb" -E

# Listar bancos
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "SELECT name FROM sys.databases"

# Criar banco
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "CREATE DATABASE [BetArbitrageDB_DEV]"

# Deletar banco
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "DROP DATABASE [BetArbitrageDB_DEV]"

# Executar arquivo SQL
sqlcmd -S "(localdb)\mssqllocaldb" -E -i seu_arquivo.sql
```

### Entity Framework Core

```bash
# Criar migration
dotnet ef migrations add InitialCreate

# Atualizar banco (aplicar migrations)
dotnet ef database update

# Remover última migration
dotnet ef migrations remove

# Listar migrations
dotnet ef migrations list

# Desfazer última atualização
dotnet ef database update PreviousMigration
```

---

## ?? Estrutura de Pastas

```
BetSniffer.Api/
??? Data/
?   ??? LocalDB/                 ? Arquivos do banco local (.mdf, .ldf)
?   ??? ApplicationDbContext.cs
?   ??? ApplicationDbContextFactory.cs
??? Configuration/
?   ??? DatabaseSettings.cs      ? Configurações de banco
?   ??? ScrapingSettings.cs
??? Core/
?   ??? Services/
?       ??? DatabaseConnectionService.cs  ? Gerencia conexão dinâmica
??? Scripts/
?   ??? Initialize-LocalDB.ps1   ? Script de inicialização
??? appsettings.json             ? Desenvolvimento (LocalDB)
??? appsettings.Production.json  ? Produção (SQL Server)
??? Program.cs                   ? Registra configurações
```

---

## ? Checklist de Setup

- [ ] LocalDB instalado (`sqllocaldb versions`)
- [ ] PowerShell executado como Administrador
- [ ] Script `Initialize-LocalDB.ps1` executado com sucesso
- [ ] `appsettings.json` atualizado
- [ ] `appsettings.Production.json` criado
- [ ] `Program.cs` configura `DatabaseSettings`
- [ ] Pasta `./Data/LocalDB` criada automaticamente
- [ ] `dotnet run` executa sem erros de conexão
- [ ] Banco de dados visível em `sqlcmd`

---

## ?? Recursos Adicionais

- [Documentação Oficial LocalDB](https://learn.microsoft.com/pt-br/sql/database-engine/configure-windows/sql-server-express-localdb)
- [Entity Framework Core Docs](https://learn.microsoft.com/pt-br/ef/core/)
- [Configuration em ASP.NET Core](https://learn.microsoft.com/pt-br/aspnet/core/fundamentals/configuration)

---

## ?? Dicas Pro

1. **Backup Automático**: Configure backup automático da pasta `./Data/LocalDB`
2. **Git**: Adicione `./Data/LocalDB/**` ao `.gitignore`
3. **CI/CD**: Use `appsettings.Production.json` no pipeline
4. **Múltiplos Desenvolvedores**: Cada um pode ter sua instância local
5. **Performance**: LocalDB é ideal para dev, mas SQL Server é melhor para produção

---

**Pronto para começar!** ??
