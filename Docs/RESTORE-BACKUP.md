# Restaurar Backup no LocalDB

## ?? Opção 1: Restaurar arquivo SQL (.sql)

### Via PowerShell

```powershell
# 1. Certifique-se de que o LocalDB está rodando
sqllocaldb start mssqllocaldb

# 2. Execute o arquivo SQL
sqlcmd -S "(localdb)\mssqllocaldb" -E -i "caminho\para\seu\backup.sql"

# 3. Verifique o resultado
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "SELECT name FROM sys.databases;"
```

### Via SQL Server Management Studio (SSMS)

```
1. Abra SSMS
2. Conecte a: (localdb)\mssqllocaldb
3. Abra arquivo: File ? Open ? sua_query.sql
4. Execute: Ctrl + Shift + E
```

## ??? Opção 2: Restaurar arquivo backup (.bak)

### Via PowerShell

```powershell
$serverName = "(localdb)\mssqllocaldb"
$backupPath = "C:\caminho\para\seu\backup.bak"
$databaseName = "BetArbitrageDB_DEV"

# 1. Deletar banco existente se necessário
sqlcmd -S $serverName -E -Q "DROP DATABASE IF EXISTS [$databaseName]"

# 2. Restaurar do backup
sqlcmd -S $serverName -E -Q @"
RESTORE DATABASE [$databaseName] 
FROM DISK = '$backupPath'
WITH MOVE 'seu_log_logical_name' 
TO 'C:\dados\seu_arquivo.ldf',
     MOVE 'seu_data_logical_name' 
TO 'C:\dados\seu_arquivo.mdf'
"@

# 3. Verificar
sqlcmd -S $serverName -E -Q "SELECT name FROM sys.databases;"
```

**Nota:** Substitua `seu_log_logical_name` e `seu_data_logical_name` pelos nomes lógicos reais (veja passo 3 abaixo)

### Descobrir nomes lógicos do backup

```powershell
$backupPath = "C:\caminho\para\seu\backup.bak"
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "RESTORE FILELISTONLY FROM DISK = '$backupPath'"
```

Você verá algo como:
```
LogicalName         PhysicalName
BetArbitrageDB      C:\...\BetArbitrageDB.mdf
BetArbitrageDB_log  C:\...\BetArbitrageDB_log.ldf
```

Use esses nomes no comando RESTORE.

## ?? Opção 3: Copiar arquivo .mdf direto

### Passos

```powershell
# 1. Parar LocalDB
sqllocaldb stop mssqllocaldb -k

# 2. Copiar arquivo .mdf para pasta de dados
Copy-Item -Path "C:\seu\backup\BetArbitrageDB.mdf" -Destination "./Data/LocalDB/" -Force
Copy-Item -Path "C:\seu\backup\BetArbitrageDB_log.ldf" -Destination "./Data/LocalDB/" -Force

# 3. Iniciar LocalDB
sqllocaldb start mssqllocaldb

# 4. Verificar
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q "SELECT name FROM sys.databases;"
```

## ? Verificar Integridade do Banco

```sql
-- Conecte e execute:
DBCC CHECKDB (BetArbitrageDB_DEV);

-- Ou para reconstruir índices:
DBCC DBREINDEX (BetArbitrageDB_DEV);
```

## ?? Troubleshooting

### "Cannot restore because database is in use"

```powershell
# Fechar todas as conexões
sqllocaldb stop mssqllocaldb -k
Start-Sleep -Seconds 2
sqllocaldb start mssqllocaldb
```

### "File cannot be overwritten because it is in use"

```powershell
# Usar permissões elevadas
# Ou deletar o arquivo manualmente
Remove-Item "./Data/LocalDB/*.mdf" -Force
Remove-Item "./Data/LocalDB/*.ldf" -Force
```

### "The logical file 'xxx' is not part of database 'yyy'"

```powershell
# Use REPLACE na restauração
sqlcmd -S "(localdb)\mssqllocaldb" -E -Q @"
RESTORE DATABASE [BetArbitrageDB_DEV] 
FROM DISK = 'C:\backup.bak'
WITH REPLACE
"@
```

## ?? Script Automático de Restauração

Salve como `Restore-Backup.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$BackupFile,
    
    [string]$ServerName = "(localdb)\mssqllocaldb",
    [string]$DatabaseName = "BetArbitrageDB_DEV"
)

Write-Host "?? Restaurando backup: $BackupFile" -ForegroundColor Cyan

# 1. Parar conexões
Write-Host "??  Parando LocalDB..." -ForegroundColor Yellow
sqlcmd -S $ServerName -E -Q "ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE" 2>$null

# 2. Deletar banco
Write-Host "???  Deletando banco antigo..." -ForegroundColor Yellow
sqlcmd -S $ServerName -E -Q "DROP DATABASE IF EXISTS [$DatabaseName]" 2>$null

# 3. Restaurar
Write-Host "?? Restaurando backup..." -ForegroundColor Yellow
sqlcmd -S $ServerName -E -Q @"
RESTORE DATABASE [$DatabaseName] 
FROM DISK = '$BackupFile'
WITH REPLACE
"@

# 4. Verificar
Write-Host "? Verificando banco..." -ForegroundColor Green
sqlcmd -S $ServerName -E -Q "SELECT name, state_desc FROM sys.databases WHERE name='$DatabaseName';"

Write-Host "? Restauração concluída!" -ForegroundColor Green
```

**Uso:**
```powershell
.\Restore-Backup.ps1 -BackupFile "C:\seu\backup.sql"
```

---

**Pronto para restaurar!** ??
