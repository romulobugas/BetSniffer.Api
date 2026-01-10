# ?? GUIA FINAL - Tudo o que Você Precisa Saber

## ? Comece Agora (5 Minutos)

```powershell
# 1. PowerShell como Admin
# 2. Navegue:
cd F:\repository\BetSniffer.Api

# 3. Execute:
.\Scripts\Initialize-LocalDB.ps1

# 4. Pronto:
dotnet run
```

**Resultado:** LocalDB configurado, pasta `./Data/LocalDB` criada, app rodando! ?

---

## ?? Documentação (Escolha sua)

### ?? Tenho 5 minutos
? **`LOCALDB-QUICKSTART.md`**
- Setup passo a passo
- Valores padrão pré-configurados
- Apenas comece!

### ?? Tenho 30 minutos
? **`Docs/LOCALDB-SETUP.md`**
- Guia completo (8 seções)
- Troubleshooting incluído
- Comandos úteis listados

### ????? Sou desenvolvedor
? **`Docs/LOCALDB-IMPLEMENTATION-SUMMARY.md`**
- Arquitetura explicada
- Patterns implementados
- Código-fonte comentado

### ?? Preciso de overview
? **`Docs/IMPLEMENTATION-COMPLETE.md`**
- Estatísticas completas
- Benefícios resumidos
- Próximos passos claros

---

## ??? O que Mudou

### ? Novo - Configuração Centralizada
```
appsettings.json              (Dev - LocalDB)
appsettings.Production.json   (Prod - SQL Server)
Configuration/DatabaseSettings.cs
```

### ? Novo - Serviço de Conexão
```
Core/Services/DatabaseConnectionService.cs
? Seleciona LocalDB ou SQL Server automaticamente
? Valida conexão ao iniciar
```

### ? Novo - 3 Serviços Consolidados
```
Core/Services/PuppeteerPageService.cs        (117 linhas)
Core/Services/DateTimeParsingService.cs      (390 linhas)
Core/Services/BetSavingService.cs            (145 linhas)
```

### ? Novo - Automação
```
Scripts/Initialize-LocalDB.ps1
? Verifica instalação
? Cria instância e banco
? Exibe instruções
```

### ? Atualizado - Program.cs
```
? Registra DatabaseSettings via DI
? Usa DatabaseConnectionService
? Valida conexão ao iniciar
```

---

## ?? Como Funciona

### Desenvolvimento (Automático)
```
Program.cs
  ?
Lê appsettings.json
  ?
DatabaseSettings: UseLocalDb = true
  ?
DatabaseConnectionService
  ?
Constrói: Server=(localdb)\mssqllocaldb;Database=BetArbitrageDB_DEV
  ?
DbContext conecta ao LocalDB
  ?
? App roda localmente
```

### Produção (Automático)
```
Program.cs (com appsettings.Production.json)
  ?
Lê appsettings.Production.json
  ?
DatabaseSettings: UseLocalDb = false
  ?
DatabaseConnectionService
  ?
Lê: Server=138.255.160.143;Database=BetArbitrageDB_PROD;...
  ?
DbContext conecta ao SQL Server
  ?
? App roda em produção
```

---

## ?? Checklist de Setup

- [ ] LocalDB instalado? (vem com Visual Studio)
- [ ] Script executado? (`.\Scripts\Initialize-LocalDB.ps1`)
- [ ] Pasta criada? (`./Data/LocalDB/` deve existir)
- [ ] Banco visível? (`sqlcmd` deve listar `BetArbitrageDB_DEV`)
- [ ] App roda? (`dotnet run` sem erros)

---

## ?? Problemas Comuns

### "LocalDB não encontrado"
? Instale via Visual Studio ou [aqui](https://learn.microsoft.com/pt-br/sql/database-engine/configure-windows/sql-server-express-localdb)

### "Pasta de dados incorreta"
? Edite `appsettings.json`: `"LocalDbDataPath": "./Data/LocalDB"`

### "Cannot connect to database"
? Rode script novamente: `.\Scripts\Initialize-LocalDB.ps1`

### Mais problemas?
? Veja `Docs/LOCALDB-SETUP.md` seção Troubleshooting

---

## ?? Estrutura de Pastas

```
F:\repository\BetSniffer.Api\
??? appsettings.json                    ? Dev (LocalDB)
??? appsettings.Production.json         ? Prod (SQL)
??? Program.cs                          ? Registra DI
?
??? Configuration/
?   ??? DatabaseSettings.cs             ? Config tipada
?
??? Core/Services/
?   ??? DatabaseConnectionService.cs    ? Connection dinâmica
?   ??? PuppeteerPageService.cs         ? Pop-ups, cookies
?   ??? DateTimeParsingService.cs       ? Parsing de datas
?   ??? BetSavingService.cs             ? Salvamento de apostas
?
??? Scripts/
?   ??? Initialize-LocalDB.ps1          ? Setup automático
?
??? Data/
?   ??? LocalDB/
?       ??? BetArbitrageDB_DEV.mdf
?       ??? BetArbitrageDB_DEV_log.ldf
?
??? Docs/
    ??? IMPLEMENTATION-COMPLETE.md      ? Resumo técnico
    ??? LOCALDB-SETUP.md                ? Guia detalhado
    ??? LOCALDB-QUICKSTART.md           ? Rápido (aqui)
    ??? RESTORE-BACKUP.md               ? Restaurar dados
    ??? (+ 4 guias de refatoração)
```

---

## ?? Segurança

### Senhas em Logs
? Mascaradas automaticamente
```csharp
Connection String: Server=(localdb)\mssqllocaldb;Database=BetArbitrageDB_DEV;Password=****
```

### appsettings.Production.json
?? **NÃO commitar** para Git!
```bash
# No .gitignore adicione:
appsettings.Production.json
appsettings.*.json
```

---

## ?? Ambientes

### Development
```bash
# Automático ao executar
dotnet run
# Usa: appsettings.json
# Conecta: LocalDB local
```

### Production
```bash
set ASPNETCORE_ENVIRONMENT=Production
dotnet run
# Usa: appsettings.Production.json
# Conecta: SQL Server remoto
```

### Staging (Opcional)
```bash
set ASPNETCORE_ENVIRONMENT=Staging
# Crie: appsettings.Staging.json
```

---

## ?? Backups

### Fazer Backup
```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "BACKUP DATABASE [BetArbitrageDB_DEV] TO DISK='./backup.bak'"
```

### Restaurar Backup
```bash
# 3 opções em: Docs/RESTORE-BACKUP.md
.\Scripts\Restore-Backup.ps1 -BackupFile "backup.sql"
```

---

## ?? Próximos Passos

### Agora
1. ? Execute setup: `.\Scripts\Initialize-LocalDB.ps1`
2. ? Teste app: `dotnet run`
3. ? Confirme funcionamento

### Depois (Opcional)
1. ?? Leia `Docs/REFACTORING-PHASE-2.md`
2. ?? Refatore um scraper de cada vez
3. ? Teste cada mudança

---

## ?? Benefícios (Resumido)

| Antes | Depois |
|-------|--------|
| ? SQL Server instalado | ? LocalDB leve |
| ? GB de espaço | ? MB de espaço |
| ? Configuração manual | ? Script automático |
| ? Código duplicado | ? Código centralizado |
| ? Mix sync/async | ? 100% async |

---

## ?? Aprendeu?

### Sim! Faça isto:
1. Share com equipe: `LOCALDB-QUICKSTART.md`
2. Documente setup time: ~5 minutos
3. Marque compilação: ? SUCCESS

### Confuso? Leia isto:
1. `Docs/LOCALDB-SETUP.md` (guia completo)
2. Seção "Troubleshooting" (soluções)
3. Scripts funcionando (validado)

---

## ? Status

```
? LocalDB: Implementado
? Configuração: Parametrizável
? Scripts: Funcionando
? Documentação: Completa
? Compilação: Sucesso
? Validação: Executada
```

---

## ?? Pronto!

Você tem tudo para:
- ?? Desenvolver localmente sem SQL Server
- ?? Alternar dev/prod sem código
- ?? Fazer backup e restaurar facilmente
- ?? Múltiplos devs sem conflitos
- ? Código limpo e assincronizado

**Comece com:** `dotnet run` ??

---

## ?? Referência Rápida

| Tarefa | Comando |
|--------|---------|
| Setup | `.\Scripts\Initialize-LocalDB.ps1` |
| Iniciar App | `dotnet run` |
| Ver banco | `sqlcmd -S "(localdb)\mssqllocaldb" -E` |
| Reset LocalDB | `sqllocaldb delete mssqllocaldb` |
| Backup | `Docs/RESTORE-BACKUP.md` |
| Produção | `set ASPNETCORE_ENVIRONMENT=Production && dotnet run` |

---

**Você está pronto! Boa sorte! ??**
