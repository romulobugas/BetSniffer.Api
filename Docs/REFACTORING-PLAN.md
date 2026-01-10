# Plano de Refatoração: Sincronização para Assincronização

## ?? Resumo Executivo

Refatorar **todos os métodos síncronos dos scraper** para **assincronismo (async/await)**, consolidar duplicação de funcionalidades em serviços compartilhados, e garantir consistência arquitetural.

---

## ?? Objetivo Principal

Transformar de um modelo **síncrono com mix assíncrono** para um modelo **completamente assíncrono**, melhorando:
- ? Performance (operações I/O não bloqueantes)
- ? Escalabilidade (mais requisições simultâneas)
- ? Mantenibilidade (código mais limpo)
- ? Reutilização (consolidar duplicatas)

---

## ?? ANÁLISE ATUAL

### Métodos Síncronos Duplicados

#### 1. **Manipulação de Pop-ups e Cookies** ?
- `GameService.ClosePopup()` (síncrono + assíncrono)
- `SuperbetScraping.HandleCookies()`
- `BetfairScraping.RemoveObstruction()`
- `PixbetScraping.ClosePopup()`
- `KTOScraping` (sem métodos, usa inline)

**Ação**: Consolidar em `PuppeteerPageService`

#### 2. **Parsing de Data e Hora** ?
- `Betfair/BetfairScraping.ParseGameDateTime()` (síncrono)
- `Superbet/SuperbetScraping.ParseGameDateTime()` (síncrono)
- `Betnacional/BetnacionalScraping.ParseGameDateTime()` (síncrono)
- `Pixbet/PixbetScraping.ParseGameDateTime()` (síncrono)
- `KTO/KTOScraping.ParseKtoDate()` (estático)
- `GameTimeService` (genérico, não usado)

**Ação**: Criar `DateTimeParsingService` unificado

#### 3. **Extração de Informações do Jogo** ?
- Alguns assincronos (`KTO`, `Betnacional`)
- Alguns síncronos (`Superbet`, `Betfair`, `Pixbet`)

**Ação**: Padronizar para assíncrono em todos

#### 4. **Processamento de Abas e Mercados** ?
- Mix de síncrono/assíncrono em todos
- Várias estruturas/padrões diferentes

**Ação**: Criar `MarketProcessingService` assíncrono

#### 5. **Salvamento de Apostas** ?
- Todos síncronos com mesma lógica

**Ação**: Criar `BetSavingService` assíncrono

---

## ??? PLANO DE REFATORAÇÃO

### FASE 1: Criar Serviços Compartilhados

#### 1.1 PuppeteerPageService
```csharp
// Consolidar:
- ClosePopup (de GameService, SuperbetScraping, PixbetScraping, BetfairScraping)
- HandleCookies (de SuperbetScraping)
- RemoveObstruction (de BetfairScraping)
- Método assíncrono para ambos
```

**Arquivo**: `Core/Services/PuppeteerPageService.cs`

#### 1.2 DateTimeParsingService
```csharp
// Consolidar:
- ParseGameDateTime (Betfair, Superbet, Betnacional, Pixbet)
- ParseKtoDate (KTO)
- GameTimeService.ExtractGameInfo (genérico)

// Criar métodos específicos para cada site
public async Task<DateTime> ParseAsync(string dateText, DateParsingStrategy strategy)
```

**Arquivo**: `Core/Services/DateTimeParsingService.cs`

#### 1.3 MarketProcessingService
```csharp
// Consolidar lógica de:
- ProcessTabsAndMarketViews
- ProcessMarketViews
// Reutilizar estruturas comuns
```

**Arquivo**: `Core/Services/MarketProcessingService.cs`

#### 1.4 BetSavingService
```csharp
// Consolidar:
- SaveBets (todos os scrapers têm logica igual)
// Versão assíncrona
public async Task SaveBetsAsync(List<BetInfo> bets)
```

**Arquivo**: `Core/Services/BetSavingService.cs`

---

### FASE 2: Refatorar Scrapers Individuais

#### Por Site (Ordem de Prioridade):
1. **KTOScraping** ? (já tem alguns async)
2. **BetnacionalScraping** ? (já tem alguns async)
3. **PixbetScraping** ?? (mix)
4. **SuperbetScraping** ? (principalmente sync)
5. **BetfairScraping** ? (principalmente sync)

**Padrão para cada**:
```csharp
public async Task<List<TagInfo>> ScrapeTagsAsync(string url, string siteName)
{
    // Usar await em vez de .GetAwaiter().GetResult()
    var page = await browser.NavigateTo(url);
    await ExtractGameInfoAsync(page);
    // ...
}
```

---

### FASE 3: Atualizar Controller

`BatchScrapingController.cs` já usa `Task.Run()` corretamente, mas precisa:
- Chamar métodos assincronos com `await`
- Garantir que `ScrapeTagsAsync` seja chamado corretamente

---

## ?? CHECKLIST DETALHADO

### ? Serviços a Criar

- [ ] `PuppeteerPageService.cs` - Manipulação de página
- [ ] `DateTimeParsingService.cs` - Parsing de datas
- [ ] `MarketProcessingService.cs` - Processamento de mercados
- [ ] `BetSavingService.cs` - Salvamento de apostas

### ? Scrapers a Refatorar

- [ ] `KTOScraping.cs` - Completar async
- [ ] `BetnacionalScraping.cs` - Completar async
- [ ] `PixbetScraping.cs` - Converter para async
- [ ] `SuperbetScraping.cs` - Converter para async
- [ ] `BetfairScraping.cs` - Converter para async

### ? Interfaces a Atualizar

- [ ] `IScrapingService.cs` - Adicionar `ScrapeTags Async`
- [ ] `ILeagueScrapingService.cs` - Adicionar `ScrapeLeague Async`

### ? Remoções

- [ ] `GameTimeService.cs` - Será consolidado
- [ ] Métodos duplicados em `GameService.cs`

### ? Testes

- [ ] Compilação sem erros
- [ ] Todas as funcionalidades operacionais
- [ ] Sem memory leaks

---

## ?? Padrão de Refatoração

### ANTES (Síncrono)
```csharp
private void ExtractGameInfo(IPage page)
{
    var element = page.QuerySelectorAsync("selector")
        .GetAwaiter().GetResult(); // ? Bloqueante!
}
```

### DEPOIS (Assíncrono)
```csharp
private async Task ExtractGameInfoAsync(IPage page)
{
    var element = await page.QuerySelectorAsync("selector"); // ? Não-bloqueante
}
```

---

## ?? Estrutura Final dos Serviços

```
Core/Services/
??? PuppeteerPageService.cs      ? NOVO
??? DateTimeParsingService.cs    ? NOVO
??? MarketProcessingService.cs   ? NOVO
??? BetSavingService.cs          ? NOVO
??? TeamService.cs               (sem alterações)
??? GameService.cs               (remover métodos duplificados)
??? GameTimeService.cs           ? REMOVER
??? WebScrapingServicePuppeteer.cs (sem alterações)
```

---

## ?? Priorização

1. **Crítico** (Primeira): Criar serviços de consolidação
2. **Alto** (Segunda): Refatorar KTO e Betnacional (já parcialmente async)
3. **Médio** (Terceira): Refatorar Pixbet e Superbet
4. **Baixo** (Quarta): Refatorar Betfair

---

## ?? Considerações Técnicas

- Manter compatibilidade com `IScrapingService` e `ILeagueScrapingService`
- Usar `async/await` em vez de `.GetAwaiter().GetResult()`
- Remover `System.Threading.Thread.Sleep()` em favor de `await Task.Delay()`
- Validar que não há bloqueios de thread

---

## ?? Próximos Passos

1. Ler este documento e validar plano
2. Criar novos serviços (Fase 1)
3. Refatorar scrapers (Fase 2)
4. Testes e validação
5. Deploy e monitoramento
