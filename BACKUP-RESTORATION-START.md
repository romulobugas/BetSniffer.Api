# ?? RESTAURAÇÃO DE BACKUP - GUIA FINAL

## ? Comando Mágico (Copie e Cole)

```powershell
cd F:\repository\BetSniffer.Api && .\Scripts\Setup-Complete.ps1
```

**Pronto!** Tudo configurado em ~3 minutos ?

---

## ?? O que Acontece

```
1. Verifica LocalDB ?
2. Cria pasta ./Data/LocalDB ?
3. Cria instância mssqllocaldb ?
4. Cria banco BetArbitrageDB_DEV ?
5. Procura BetSniffer_10_06_25.bak ?
6. Restaura backup ?
7. Valida integridade ?
8. Exibe instruções finais ?
```

---

## ?? Resultado

```
? Servidor: (localdb)\mssqllocaldb
? Banco: BetArbitrageDB_DEV
? Dados: Restaurados do backup
? Status: Pronto para uso

Connection String:
Server=(localdb)\mssqllocaldb;Database=BetArbitrageDB_DEV;Integrated Security=true;Encrypt=false;
```

---

## ?? Próximo Passo

```bash
dotnet run
```

**Pronto!** Aplicação rodando com seus dados! ??

---

## ?? Onde Colocar o Arquivo

**Opção 1 (Recomendado):**
```
F:\repository\BetSniffer.Api\Scripts\Data\BetSniffer_10_06_25.bak
```

**Opção 2:**
```
F:\repository\BetSniffer.Api\Data\BetSniffer_10_06_25.bak
```

**Opção 3:**
```
F:\repository\BetSniffer.Api\Data\LocalDB\BetSniffer_10_06_25.bak
```

Script procura em todas automaticamente! ??

---

## ?? Problemas Comuns

### "Arquivo não encontrado"
? Coloque `BetSniffer_10_06_25.bak` em qualquer lugar dentro de `Data/`

### "LocalDB não instalado"
? Instale via [Visual Studio](https://learn.microsoft.com/pt-br/sql/database-engine/configure-windows/sql-server-express-localdb)

### "Banco já existe"
? Script pergunta, escolha opção 1 para sobrescrever

### "Quer resetar?"
? Use a flag `-Force`:
```powershell
.\Scripts\Setup-Complete.ps1 -Force
```

---

## ?? 3 Scripts Disponíveis

| Script | O que faz | Tempo |
|--------|-----------|-------|
| **Setup-Complete.ps1** | LocalDB + Banco + Backup | 3 min |
| **Initialize-LocalDB.ps1** | Apenas LocalDB | 1 min |
| **Restore-DatabaseBackup.ps1** | Apenas restaura | 30 seg |

---

## ? Verificar Restauração

```powershell
# Contar registros:
sqlcmd -S "(localdb)\mssqllocaldb" -E -d BetArbitrageDB_DEV -Q "SELECT COUNT(*) FROM GamesInfo"

# Deverá retornar um número (seus dados!)
```

---

## ??? Arquivos Criados

? `Scripts/Setup-Complete.ps1` - **Setup automático completo**  
? `Scripts/Restore-DatabaseBackup.ps1` - Restauração de backup  
? `Docs/RESTORE-BACKUP-QUICK.md` - Guia rápido  
? `Docs/BACKUP-RESTORATION-SUMMARY.md` - Este documento  
? `README.md` - Atualizado com novas opções

---

## ?? Fluxo Recomendado

```
1. Coloque BetSniffer_10_06_25.bak em ./Data/
2. Execute: .\Scripts\Setup-Complete.ps1
3. Aguarde ~3 minutos
4. Execute: dotnet run
5. Acesse: http://localhost:5001
6. Seus dados estão lá! ??
```

---

## ?? Mais Informações

- **[Docs/RESTORE-BACKUP-QUICK.md](RESTORE-BACKUP-QUICK.md)** - Detalhes da restauração
- **[Docs/LOCALDB-SETUP.md](LOCALDB-SETUP.md)** - Setup completo
- **[START-HERE.md](../START-HERE.md)** - Visão geral

---

## ?? Tempo Estimado

- **Primeira vez:** 3-5 minutos
- **Restaurações seguintes:** 30 segundos
- **Desenvolvimento:** Sem delays!

---

## ?? Conclusão

Você agora tem:

? **LocalDB automático** - Sem SQL Server completo  
? **Backup restaurado** - Todos seus dados  
? **Tudo parametrizado** - Dev/Prod automaticamente  
? **Scripts prontos** - Use sempre que precisar  

**Tudo em um comando! ??**

---

**Comece agora:**
```powershell
.\Scripts\Setup-Complete.ps1
```

**Depois:**
```bash
dotnet run
```

**Pronto! ?**
