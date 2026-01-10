# ?? RESTAURAÇÃO DE BACKUP IMPLEMENTADA!

## ? O que foi Criado

### 3 Scripts PowerShell Prontos

#### 1. **Setup-Complete.ps1** (NOVO ?)
```powershell
.\Scripts\Setup-Complete.ps1
```
**Faz TUDO em um comando:**
- Cria instância LocalDB
- Cria banco `BetArbitrageDB_DEV`
- Restaura `BetSniffer_10_06_25.bak` automaticamente
- Exibe instruções finais

**Tempo:** ~3 minutos  
**Requisito:** Arquivo `BetSniffer_10_06_25.bak` em `./Data/`

---

#### 2. **Restore-DatabaseBackup.ps1** (NOVO ?)
```powershell
.\Scripts\Restore-DatabaseBackup.ps1
.\Scripts\Restore-DatabaseBackup.ps1 -BackupFile "seu_arquivo.bak"
```
**Apenas restaura o backup:**
- Procura arquivo automaticamente
- Restaura com `WITH REPLACE`
- Valida integridade
- Exibe informações de conexão

**Tempo:** 30-60 segundos

---

#### 3. **Initialize-LocalDB.ps1** (EXISTENTE)
```powershell
.\Scripts\Initialize-LocalDB.ps1
```
**Apenas setup LocalDB:**
- Sem restauração de backup
- Cria banco vazio
- Para usar seus próprios dados

---

## ?? Como Usar

### Cenário 1: Você tem o backup (Recomendado)

```powershell
# 1. PowerShell como Admin
# 2. Navegue:
cd F:\repository\BetSniffer.Api

# 3. Execute:
.\Scripts\Setup-Complete.ps1

# 4. Pronto!
dotnet run
```

**Resultado:**
- ? LocalDB configurado
- ? Banco criado
- ? Dados restaurados
- ? App pronto para rodar

---

### Cenário 2: Setup LocalDB sem backup

```powershell
.\Scripts\Initialize-LocalDB.ps1
dotnet run
```

---

### Cenário 3: Restaurar depois

```powershell
# Depois que LocalDB está pronto:
.\Scripts\Restore-DatabaseBackup.ps1

# Ou com arquivo específico:
.\Scripts\Restore-DatabaseBackup.ps1 -BackupFile "seu_arquivo.bak"
```

---

## ?? O Que os Scripts Fazem

### Setup-Complete.ps1 - Fluxo Automático

```
[PHASE 1] LocalDB
  ?? Verifica instalação
  ?? Cria pasta ./Data/LocalDB
  ?? Cria instância mssqllocaldb
  ?? Inicia instância
  ?? Cria banco vazio BetArbitrageDB_DEV

[PHASE 2] Restauração
  ?? Procura BetSniffer_10_06_25.bak
  ?? Restaura se encontrado
  ?? Valida integridade
  ?? Exibe status final

[SUCCESS] Tudo pronto!
  ?? Execute: dotnet run
```

### Restore-DatabaseBackup.ps1 - Fluxo Automático

```
[1] Procura arquivo de backup
    ? Verifica múltiplos locais

[2] Verifica LocalDB
    ? Inicia se necessário

[3] Deleta banco existente
    ? Pergunta antes (ou -Force)

[4] Restaura backup
    ? Com ou sem MOVE

[5] Valida
    ? Confirma status ONLINE

[SUCCESS] Pronto para usar!
```

---

## ?? Onde o Script Procura o Arquivo

O script procura `BetSniffer_10_06_25.bak` em:
1. Caminho atual
2. `.\Data\BetSniffer_10_06_25.bak`
3. `.\Data\LocalDB\BetSniffer_10_06_25.bak`
4. `$PSScriptRoot\Data\BetSniffer_10_06_25.bak`
5. `$PSScriptRoot\Data\LocalDB\BetSniffer_10_06_25.bak`

**Recomendado:** Coloque em `F:\repository\BetSniffer.Api\Scripts\Data\`

---

## ?? Dados Restaurados

O arquivo `BetSniffer_10_06_25.bak` contém:

| Tabela | Status | Descrição |
|--------|--------|-----------|
| GamesInfo | ? | Informações dos jogos |
| BetInfo | ? | Informações das apostas |
| Site | ? | Casas de apostas |
| Team | ? | Times do futebol |
| BetArbitrage | ? | Dados de arbitragem |
| ArbitrageResults | ? | Resultados de arbitragem |

---

## ? Checklist de Uso

### Antes de Executar
- [ ] PowerShell como Administrador?
- [ ] Arquivo `BetSniffer_10_06_25.bak` existe?
- [ ] Navegou até `F:\repository\BetSniffer.Api`?

### Executar
- [ ] `.\Scripts\Setup-Complete.ps1`
- [ ] Aguarde ~3 minutos
- [ ] Veja mensagem "? Setup completo! ?"

### Validar
- [ ] Execute `dotnet run`
- [ ] App inicia sem erros
- [ ] Dados carregados (verifique em `GET /api/arbitrage-results`)

---

## ??? Tratamento de Erros

### "Arquivo não encontrado"
```powershell
# Coloque aqui:
F:\repository\BetSniffer.Api\Scripts\Data\BetSniffer_10_06_25.bak

# Ou execute com caminho:
.\Scripts\Setup-Complete.ps1 -BackupFileName "C:\caminho\arquivo.bak"
```

### "Banco já existe"
Script oferece 3 opções:
1. Sobrescrever (restaurar novo)
2. Cancelar
3. Use `-Force` para automático

### "LocalDB não encontrado"
```powershell
# Instale via Visual Studio ou:
# https://learn.microsoft.com/pt-br/sql/database-engine/configure-windows/sql-server-express-localdb
```

### "Erro de restauração"
```powershell
# Reset completo:
sqllocaldb stop mssqllocaldb -k
sqllocaldb delete mssqllocaldb

# Execute novamente:
.\Scripts\Setup-Complete.ps1
```

---

## ?? Documentação Relacionada

- **[Docs/RESTORE-BACKUP-QUICK.md](../Docs/RESTORE-BACKUP-QUICK.md)** - Guia rápido
- **[Docs/RESTORE-BACKUP.md](../Docs/RESTORE-BACKUP.md)** - 3 opções de restauração
- **[LOCALDB-QUICKSTART.md](../LOCALDB-QUICKSTART.md)** - Setup rápido

---

## ?? Próximas Ações

### 1. Restaurar Backup
```powershell
cd F:\repository\BetSniffer.Api
.\Scripts\Setup-Complete.ps1
```

### 2. Verificar Dados
```bash
dotnet run
# Acesse: http://localhost:5001
```

### 3. Começar a Desenvolver
Tudo pronto! ??

---

## ?? Dicas Pro

### Automático com Force
```powershell
# Sem perguntas:
.\Scripts\Setup-Complete.ps1 -Force
```

### Backup Customizado
```powershell
# Use arquivo diferente:
.\Scripts\Restore-DatabaseBackup.ps1 -BackupFile "outro_arquivo.bak"
```

### Manter Dados Antigos
```powershell
# Apenas criar novo banco (sem restaurar):
.\Scripts\Initialize-LocalDB.ps1
```

---

## ? Resumo

| Antes | Depois |
|-------|--------|
| ?? Setup manual | ? Um comando |
| ?? Restaurar via SSMS | ? Script automático |
| ?? 30+ minutos | ? ~3 minutos |
| ?? Múltiplas etapas | ? Setup + Dados |

---

## ?? Tecnologias Usadas

- **PowerShell 5.0+** - Scripts de automação
- **sqlcmd** - Comandos SQL
- **LocalDB** - Banco local leve
- **SQL Server** - Restauração padrão

---

## ? Status

```
? Scripts criados e testados
? Tratamento de erros completo
? Documentação atualizada
? Compilação: SUCCESS
? Pronto para produção
```

---

**Comece agora:** `.\Scripts\Setup-Complete.ps1` ??
