# ?? Quick Start - LocalDB Setup

## ? 30 Segundos para Começar

### Windows (PowerShell)

```powershell
# 1. Abra PowerShell como Administrador
# 2. Navegue até o diretório do projeto
cd F:\repository\BetSniffer.Api

# 3. Execute o script de setup
.\Scripts\Initialize-LocalDB.ps1

# 4. Pronto! Execute a aplicação
dotnet run
```

## ?? O que o Script Faz

? Verifica se LocalDB está instalado  
? Cria pasta de dados local  
? Cria instância do LocalDB  
? Cria banco de dados BetArbitrageDB_DEV  
? Exibe instruções de connection string  

## ?? Configuração Necessária

Nenhuma! O `Program.cs` já está configurado para:
- ? Ler `DatabaseSettings` do `appsettings.json`
- ? Usar LocalDB em Desenvolvimento
- ? Usar SQL Server em Produção
- ? Validar conexão ao iniciar

## ?? Valores Padrão

| Configuração | Valor | Arquivo |
|---|---|---|
| Instância LocalDB | `mssqllocaldb` | `appsettings.json` |
| Banco de Dados | `BetArbitrageDB_DEV` | `appsettings.json` |
| Pasta de Dados | `./Data/LocalDB` | `appsettings.json` |
| Ambiente | `Development` | `appsettings.json` |

## ?? Alternâncias

### Mudar para Produção

1. Edite `appsettings.Production.json`
2. Configure a connection string do seu SQL Server
3. Copie o arquivo para a pasta de publicação
4. Execute com: `set ASPNETCORE_ENVIRONMENT=Production && dotnet run`

### Voltar para Desenvolvimento

```bash
set ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

## ?? Verificar Conexão

```powershell
# Conecte ao LocalDB
sqlcmd -S "(localdb)\mssqllocaldb" -E

# Liste os bancos
SELECT name FROM sys.databases;

# Saia
EXIT
```

## ? Problema? Tente Isto

```powershell
# Reiniciar LocalDB completamente
sqllocaldb stop mssqllocaldb -k
sqllocaldb start mssqllocaldb

# Ou resetar tudo
.\Scripts\Initialize-LocalDB.ps1 -Force
```

## ?? Documentação Completa

Veja `Docs/LOCALDB-SETUP.md` para guia detalhado com:
- Instalação passo a passo
- Troubleshooting avançado
- Restauração de backups
- Comandos úteis
- Estrutura de pastas

---

**Pronto para desenvolver!** ?
