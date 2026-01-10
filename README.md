# BetSniffer.Api

## ?? Setup Rápido com Dados

Se você tem o arquivo `BetSniffer_10_06_25.bak`:

```powershell
# PowerShell como Admin
cd F:\repository\BetSniffer.Api
.\Scripts\Setup-Complete.ps1
dotnet run
```

**Pronto!** LocalDB + Banco + Dados restaurados em ~3 minutos! ?

---

## ?? Documentação

### Setup e Configuração
- **[START-HERE.md](START-HERE.md)** - Guia inicial (você aqui!)
- **[LOCALDB-QUICKSTART.md](LOCALDB-QUICKSTART.md)** - Setup em 30 segundos
- **[Docs/LOCALDB-SETUP.md](Docs/LOCALDB-SETUP.md)** - Guia detalhado (350+ linhas)

### Backups e Restauração
- **[Docs/RESTORE-BACKUP-QUICK.md](Docs/RESTORE-BACKUP-QUICK.md)** - Restaurar BetSniffer_10_06_25.bak
- **[Docs/RESTORE-BACKUP.md](Docs/RESTORE-BACKUP.md)** - 3 opções de restauração

### Implementação
- **[Docs/IMPLEMENTATION-COMPLETE.md](Docs/IMPLEMENTATION-COMPLETE.md)** - Resumo técnico
- **[Docs/LOCALDB-IMPLEMENTATION-SUMMARY.md](Docs/LOCALDB-IMPLEMENTATION-SUMMARY.md)** - Arquitetura

### Refatoração (Opcional)
- **[Docs/REFACTORING-PLAN.md](Docs/REFACTORING-PLAN.md)** - Plano geral
- **[Docs/REFACTORING-PHASE-2.md](Docs/REFACTORING-PHASE-2.md)** - Próximas mudanças

---

## ?? Como Usar

### Opção 1: Setup Completo (com backup)
```powershell
.\Scripts\Setup-Complete.ps1
```
Cria LocalDB + Banco + Restaura dados em um comando!

### Opção 2: Setup LocalDB (sem backup)
```powershell
.\Scripts\Initialize-LocalDB.ps1
```
Apenas cria LocalDB + Banco vazio

### Opção 3: Restaurar Backup Existente
```powershell
.\Scripts\Restore-DatabaseBackup.ps1
```
Restaura `BetSniffer_10_06_25.bak` para LocalDB

---

## ?? Configuração

**Desenvolvimento (LocalDB):**
- Arquivo: `appsettings.json`
- Banco: `BetArbitrageDB_DEV`
- Servidor: `(localdb)\mssqllocaldb`

**Produção (SQL Server):**
- Arquivo: `appsettings.Production.json`
- Banco: `BetArbitrageDB_PROD`
- Servidor: `138.255.160.143`

Mesma aplicação, configuração automática! ??

---

## ? Funcionalidades

? LocalDB automático (sem SQL Server completo)  
? Configuração parametrizável (Dev/Prod)  
? Serviços consolidados (50% menos código)  
? 100% assincronismo (async/await)  
? Scripts de automação (PowerShell)  
? Documentação completa (8 guias)  

---

## ?? Estrutura

```
BetSniffer.Api/
??? Scripts/
?   ??? Setup-Complete.ps1           ? Setup completo
?   ??? Initialize-LocalDB.ps1       ? Setup LocalDB
?   ??? Restore-DatabaseBackup.ps1   ? Restaurar backup
??? Data/LocalDB/                    ? Banco local
??? Configuration/
?   ??? DatabaseSettings.cs
??? Core/Services/
?   ??? DatabaseConnectionService.cs
?   ??? PuppeteerPageService.cs
?   ??? DateTimeParsingService.cs
?   ??? BetSavingService.cs
??? appsettings.json                 ? Dev
??? appsettings.Production.json      ? Prod
??? Docs/                            ? Documentação
```

---

## ?? Suporte

**Problema?** Veja:
1. `Docs/LOCALDB-SETUP.md` (Troubleshooting)
2. `Docs/RESTORE-BACKUP-QUICK.md` (Restauração)
3. `START-HERE.md` (Visão geral)

---

## ?? Próximos Passos

1. Execute: `.\Scripts\Setup-Complete.ps1`
2. Verifique: `dotnet run`
3. Comece a desenvolver!

**Tudo pronto! ??**