# ?? Como Restaurar o Backup BetSniffer_10_06_25.bak

## ? Forma Mais Rápida (Recomendado)

```powershell
# 1. Abra PowerShell como Administrador
# 2. Navegue até o projeto:
cd F:\repository\BetSniffer.Api

# 3. Execute o setup completo:
.\Scripts\Setup-Complete.ps1

# Pronto! Tudo feito em um comando!
```

**O que ele faz:**
- ? Cria instância LocalDB
- ? Cria banco `BetArbitrageDB_DEV`
- ? Restaura `BetSniffer_10_06_25.bak` automaticamente
- ? Exibe instruções de próximos passos

---

## ?? Alternativa: Restaurar Apenas o Backup

Se o LocalDB já está configurado:

```powershell
# Execute apenas a restauração:
.\Scripts\Restore-DatabaseBackup.ps1

# Ou com arquivo customizado:
.\Scripts\Restore-DatabaseBackup.ps1 -BackupFile "seu_arquivo.bak"
```

---

## ?? Resultado Esperado

```
? ========================================
? Setup Completo - BetSniffer.Api
? ========================================

? LocalDB encontrado
? Pasta: F:\repository\BetSniffer.Api\Data\LocalDB
? Instância criada
? Instância iniciada
? Banco criado
? Arquivo encontrado: F:\...\BetSniffer_10_06_25.bak
? Backup restaurado

? RESUMO FINAL
? ====================================
? Instância LocalDB: mssqllocaldb
? Banco de Dados: BetArbitrageDB_DEV
? Caminho de Dados: F:\repository\BetSniffer.Api\Data\LocalDB
? Connection String: Server=(localdb)\mssqllocaldb;...

? Setup completo! ?
```

---

## ? Verificar Restauração

```powershell
# Conectar ao banco e contar registros
sqlcmd -S "(localdb)\mssqllocaldb" -E -d BetArbitrageDB_DEV -Q "SELECT COUNT(*) FROM GamesInfo"

# Deverá retornar um número maior que 0 (seus dados!)
```

---

## ?? Executar a Aplicação

Após restauração bem-sucedida:

```bash
cd F:\repository\BetSniffer.Api
dotnet run
```

**Resultado:**
```
? Ambiente: Development
? Banco de dados: LocalDB - BetArbitrageDB_DEV
? Conexão validada com sucesso!
? App rodando na porta 5001
```

---

## ?? Problemas?

### "Arquivo não encontrado"
Certifique-se que `BetSniffer_10_06_25.bak` está em:
- `F:\repository\BetSniffer.Api\Scripts\Data\`
- ou em qualquer uma dessas pastas:
  - `./Data/`
  - `./Data/LocalDB/`
  - Raiz do projeto

### "Banco já existe"
O script perguntará se quer sobrescrever. Escolha:
- `1` = Deletar e restaurar (sobrescrever)
- `2` = Cancelar

Ou use a flag `-Force`:
```powershell
.\Scripts\Setup-Complete.ps1 -Force
```

### "LocalDB não inicia"
```powershell
# Reset completo:
sqllocaldb stop mssqllocaldb -k
sqllocaldb delete mssqllocaldb
# Depois execute Setup-Complete.ps1 novamente
```

---

## ?? O Arquivo de Backup Contém:

O arquivo `BetSniffer_10_06_25.bak` restaurará todas as tabelas com seus dados:
- ? GamesInfo (Informações dos jogos)
- ? BetInfo (Informações das apostas)
- ? Site (Casas de apostas)
- ? Team (Times)
- ? BetArbitrage (Arbitragens)
- ? ArbitrageResults (Resultados)

---

## ?? Fluxo Automático do Script

```
Setup-Complete.ps1
  ??? PHASE 1: LocalDB
  ?   ??? Verifica LocalDB
  ?   ??? Cria pasta de dados
  ?   ??? Cria/recria instância
  ?   ??? Inicia instância
  ?   ??? Cria banco vazio
  ?
  ??? PHASE 2: Restauração
      ??? Procura arquivo .bak
      ??? Restaura se encontrado
      ??? Exibe status final
```

---

## ?? Tempo Estimado

- **Primeira vez (Setup completo):** 2-3 minutos
- **Apenas restauração:** 30-60 segundos (depende do tamanho)

---

## ?? Conclusão

Você agora tem:
- ? LocalDB configurado
- ? Banco restaurado com seus dados
- ? Aplicação pronta para rodar

**Comece com:** `dotnet run` ??

---

**Dúvidas?** Veja `Docs/LOCALDB-SETUP.md` para guia completo!
