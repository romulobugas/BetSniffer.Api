# Guia de Refatoração dos Scrapers - FASE 2

## ?? Resumo

Este documento apresenta as instruções para refatorar cada scraper individual para usar os novos serviços consolidados e converter seus métodos para assincronismo.

---

## ?? Ordem de Priorização

1. ? **KTOScraping** (Prioridade 1) - Parcialmente async
2. ? **BetnacionalScraping** (Prioridade 2) - Parcialmente async
3. ?? **PixbetScraping** (Prioridade 3) - Mix sync/async
4. ? **SuperbetScraping** (Prioridade 4) - Principalmente sync
5. ? **BetfairScraping** (Prioridade 5) - Principalmente sync

---

## ??? Padrão de Refatoração

### Passo 1: Adicionar Injeção de Dependência no Construtor

**ANTES:**
```csharp
public KTOScraping(
    ApplicationDbContext dbContext,
    TeamService teamService,
    IRepositoryService<GamesInfo> gamesInfoRepository,
    IRepositoryService<BetInfo> betInfoRepository)
{
    _dbContext = dbContext;
    _teamService = teamService;
    _gamesInfoRepository = gamesInfoRepository;
    _betInfoRepository = betInfoRepository;
    _gameService = new GameService(_dbContext);
    
    // Inicializa o serviço de log
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .Build();
    
    _logService = new LogService(configuration);
}
```

**DEPOIS:**
```csharp
private readonly PuppeteerPageService _pageService;
private readonly DateTimeParsingService _dateTimeService;
private readonly BetSavingService _betSavingService;

public KTOScraping(
    ApplicationDbContext dbContext,
    TeamService teamService,
    IRepositoryService<GamesInfo> gamesInfoRepository,
    IRepositoryService<BetInfo> betInfoRepository,
    PuppeteerPageService pageService,
    DateTimeParsingService dateTimeService,
    BetSavingService betSavingService,
    ILogService logService)
{
    _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
    _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
    _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
    _pageService = pageService ?? throw new ArgumentNullException(nameof(pageService));
    _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
    _betSavingService = betSavingService ?? throw new ArgumentNullException(nameof(betSavingService));
    _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    
    _gameService = new GameService(_dbContext);
}
```

---

### Passo 2: Converter ScrapeTags para ScrapTagsAsync

**ANTES:**
```csharp
public List<TagInfo> ScrapeTags(string url, string siteName)
{
    // ... código síncrono
    _webScrapingService.Dispose();
    return new List<TagInfo>();
}
```

**DEPOIS:**
```csharp
public async Task<List<TagInfo>> ScrapeTagsAsync(string url, string siteName)
{
    // ... código assincronizado com await
    _webScrapingService.Dispose();
    return new List<TagInfo>();
}
```

---

### Passo 3: Substituir Métodos Duplicados pelos Serviços

#### 3.1 Fechar Pop-up

**ANTES:**
```csharp
private void ClosePopup(IPage page)
{
    try
    {
        System.Threading.Thread.Sleep(new Random().Next(855, 1226));
        Console.WriteLine("Tentando fechar pop-up...");

        var closeButton = page.WaitForSelectorAsync(".components-fe_Popup_iconClose", new WaitForSelectorOptions
        {
            Timeout = 10000,
            Visible = true
        }).GetAwaiter().GetResult();

        if (closeButton != null)
        {
            page.EvaluateFunctionAsync("element => element.click()", closeButton).GetAwaiter().GetResult();
            Console.WriteLine("Pop-up fechado com sucesso.");
        }
    }
    catch (TimeoutException)
    {
        Console.WriteLine("Nenhum pop-up detectado.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao fechar pop-up (não crítico): {ex.Message}");
    }
}
```

**DEPOIS:**
```csharp
private async Task ClosePopupAsync(IPage page)
{
    await _pageService.ClosePopupAsync(page, ".components-fe_Popup_iconClose", timeoutMilliseconds: 10000);
}
```

#### 3.2 Aceitar Cookies

**ANTES:**
```csharp
public void HandleCookies(IPage page, string cookieAcceptButtonSelector, int timeoutMilliseconds = 10000)
{
    try
    {
        System.Threading.Thread.Sleep(new Random().Next(981, 1758));
        var element = page.WaitForSelectorAsync(cookieAcceptButtonSelector, new WaitForSelectorOptions
        {
            Timeout = timeoutMilliseconds
        }).GetAwaiter().GetResult();

        if (element != null)
        {
            element.ClickAsync().GetAwaiter().GetResult();
            Console.WriteLine("Botão 'Aceitar todos os cookies' clicado com sucesso.");
        }
    }
    catch (TimeoutException)
    {
        Console.WriteLine("Tempo de espera para localizar o botão de aceitar cookies expirou.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao clicar no botão de aceitar cookies: {ex.Message}");
    }
}
```

**DEPOIS:**
```csharp
private async Task HandleCookiesAsync(IPage page, string selector)
{
    await _pageService.HandleCookiesAsync(page, selector);
}
```

#### 3.3 Parsing de Data

**ANTES:**
```csharp
private DateTime ParseGameDateTime(string dateTimeText)
{
    if (string.IsNullOrWhiteSpace(dateTimeText))
        throw new ArgumentException("O parâmetro 'dateTimeText' está vazio ou nulo.");

    try
    {
        var now = DateTime.Now;
        // ... 50+ linhas de lógica de parsing ...
    }
    catch (Exception ex)
    {
        throw new Exception($"Erro ao converter a data: {dateTimeText} - {ex.Message}");
    }
}
```

**DEPOIS:**
```csharp
private DateTime ParseGameDateTime(string dateTimeText)
{
    return _dateTimeService.Parse(dateTimeText, DateParsingStrategy.KTO);
}

// OU, se o método for assíncrono:
private async Task<DateTime> ParseGameDateTimeAsync(string dateTimeText)
{
    return await _dateTimeService.ParseAsync(dateTimeText, DateParsingStrategy.KTO);
}
```

#### 3.4 Salvar Apostas

**ANTES:**
```csharp
private void SaveBets(List<BetInfo> bets)
{
    if (bets == null || !bets.Any())
        return;

    var betKeys = bets
        .Select(bet => new { bet.TagName, bet.OverUnder, bet.TagId, bet.Site, bet.GamesInfo })
        .ToHashSet();

    foreach(var betKey in betKeys) 
    {
        var existingBets = _dbContext.BetInfo.Where(b =>
                            b.GamesInfo.GameId == betKey.GamesInfo.GameId &&
                            b.TagName == betKey.TagName &&
                            // ... mais 3 linhas ...
                            ).ToList();

        if (existingBets.Count > 0)
        {
            Console.WriteLine($"Aposta existente encontrada. Removendo a aposta duplicada...");
            _dbContext.BetInfo.RemoveRange(existingBets);
            Console.WriteLine($"Removidas {existingBets.Count} apostas antigas.");
        }
    }

    _dbContext.BetInfo.AddRange(bets);
    _dbContext.SaveChanges();

    Console.WriteLine($"Salvas {bets.Count} novas apostas.");
}
```

**DEPOIS:**
```csharp
private async Task SaveBetsAsync(List<BetInfo> bets)
{
    await _betSavingService.SaveBetsAsync(bets);
}
```

---

### Passo 4: Converter Métodos Síncronos para Assincronos

**ANTES:**
```csharp
private void ExtractGameInfo(IPage page)
{
    try
    {
        System.Threading.Thread.Sleep(new Random().Next(842, 1471));
        
        var gameContainer = page.QuerySelectorAsync("div.selector")
            .GetAwaiter().GetResult(); // ? Bloqueante!
        
        // ... resto do método
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao extrair informações do jogo: {ex.Message}");
    }
}
```

**DEPOIS:**
```csharp
private async Task ExtractGameInfoAsync(IPage page)
{
    try
    {
        await Task.Delay(new Random().Next(842, 1471)); // ? Não-bloqueante
        
        var gameContainer = await page.QuerySelectorAsync("div.selector"); // ? Aguarda
        
        // ... resto do método com await
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao extrair informações do jogo: {ex.Message}");
        _logService.LogError("Erro ao extrair informações do jogo", ex);
    }
}
```

---

### Passo 5: Substituir Chamadas de Métodos

**ANTES:**
```csharp
public List<TagInfo> ScrapeTags(string url, string siteName)
{
    // ... código anterior ...
    
    ExtractGameInfo(page); // ? Síncrono
    ProcessTabsAndMarketViews(page); // ? Síncrono
    SaveBets(currentBets); // ? Síncrono
    
    return new List<TagInfo>();
}
```

**DEPOIS:**
```csharp
public async Task<List<TagInfo>> ScrapeTagsAsync(string url, string siteName)
{
    // ... código anterior ...
    
    await ExtractGameInfoAsync(page); // ? Assincronizado
    await ProcessTabsAndMarketViewsAsync(page); // ? Assincronizado
    await SaveBetsAsync(currentBets); // ? Assincronizado
    
    return new List<TagInfo>();
}
```

---

## ?? Checklist por Scraper

### KTOScraping
- [ ] Adicionar injeção de dependência dos 3 novos serviços
- [ ] Converter `ScrapeTags` ? `ScrapeTagsAsync`
- [ ] Converter `ScrapeLeague` ? `ScrapeLeagueAsync`
- [ ] Remover método `ClosePopup` comentado
- [ ] Remover método `ConfirmAgeVerification` comentado
- [ ] Usar `_pageService` para Pop-ups/Cookies
- [ ] Usar `_dateTimeService.Parse()` para datas (já é para KTO)
- [ ] Usar `_betSavingService` para `SaveBets`
- [ ] Converter todos os `.GetAwaiter().GetResult()` para `await`
- [ ] Converter `System.Threading.Thread.Sleep()` para `await Task.Delay()`
- [ ] Remover inicialização inline de `_logService`

### BetnacionalScraping
- [ ] Adicionar injeção de dependência dos 3 novos serviços
- [ ] Converter `ScrapeTags` ? `ScrapeTagsAsync`
- [ ] Usar `_pageService` para Pop-ups/Cookies
- [ ] Usar `_dateTimeService.Parse(text, DateParsingStrategy.Betnacional)`
- [ ] Usar `_betSavingService` para `SaveBets`
- [ ] Remover métodos `ClosePopup` e `ConfirmAgeVerification` (usar `_pageService`)
- [ ] Remover método local `ParseGameDateTime`
- [ ] Converter `.GetAwaiter().GetResult()` para `await`
- [ ] Converter `System.Threading.Thread.Sleep()` para `await Task.Delay()`

### PixbetScraping
- [ ] Adicionar injeção de dependência dos 3 novos serviços
- [ ] Converter `ScrapeTags` ? `ScrapeTagsAsync`
- [ ] Usar `_pageService.ClosePopupAsync()` em vez de método local
- [ ] Usar `_dateTimeService.Parse(text, DateParsingStrategy.Pixbet)`
- [ ] Usar `_betSavingService` para `SaveBets`
- [ ] Remover método `ClosePopup`
- [ ] Remover método `ParseGameDateTime`
- [ ] Converter todos os `.GetAwaiter().GetResult()` para `await`
- [ ] Converter `System.Threading.Thread.Sleep()` para `await Task.Delay()`

### SuperbetScraping
- [ ] Adicionar injeção de dependência dos 3 novos serviços
- [ ] Converter `ScrapeTags` ? `ScrapeTagsAsync`
- [ ] Usar `_pageService.HandleCookiesAsync()` em vez de `HandleCookies`
- [ ] Usar `_dateTimeService.Parse(text, DateParsingStrategy.Superbet)`
- [ ] Usar `_betSavingService` para `SaveBets`
- [ ] Remover método `HandleCookies`
- [ ] Remover método `ParseGameDateTime`
- [ ] Converter `ExtractGameInfo` ? `ExtractGameInfoAsync`
- [ ] Converter `ProcessTabsAndMarketViews` ? `ProcessTabsAndMarketViewsAsync`
- [ ] Converter `ProcessMarketViews` ? `ProcessMarketViewsAsync`
- [ ] Converter `ProcessExpandedMarket` ? `ProcessExpandedMarketAsync`
- [ ] Remover `System.Threading.Thread.Sleep()`, usar `await Task.Delay()`

### BetfairScraping
- [ ] Adicionar injeção de dependência dos 3 novos serviços
- [ ] Converter `ScrapeTags` ? `ScrapeTagsAsync`
- [ ] Converter `ScrapeLeague` ? `ScrapeLeagueAsync`
- [ ] Usar `_pageService` para Pop-ups/Cookies/Obstruções
- [ ] Usar `_dateTimeService.Parse(text, DateParsingStrategy.Betfair)`
- [ ] Usar `_betSavingService` para `SaveBets`
- [ ] Remover métodos `ClosePopup`, `ConfirmAgeVerification`, `RemoveObstruction`
- [ ] Remover método `ParseGameDateTime`
- [ ] Converter `ExtractGameInfo` ? `ExtractGameInfoAsync`
- [ ] Converter `ProcessTabsAndMarketViews` ? `ProcessTabsAndMarketViewsAsync`
- [ ] Converter `ProcessMarketViews` ? `ProcessMarketViewsAsync`
- [ ] Converter todos os `.GetAwaiter().GetResult()` para `await`
- [ ] Converter `System.Threading.Thread.Sleep()` para `await Task.Delay()`

---

## ?? Atualizações Necessárias

### 1. Interfaces (`IScrapingService` e `ILeagueScrapingService`)

**ANTES:**
```csharp
public interface IScrapingService
{
    List<TagInfo> ScrapeTags(string url, string siteName);
}

public interface ILeagueScrapingService
{
    void ScrapeLeague(string url, string siteName);
}
```

**DEPOIS:**
```csharp
public interface IScrapingService
{
    Task<List<TagInfo>> ScrapeTagsAsync(string url, string siteName);
}

public interface ILeagueScrapingService
{
    Task ScrapeLeagueAsync(string url, string siteName);
}
```

### 2. BatchScrapingController

**ANTES:**
```csharp
scrapingService.ScrapeTags(request.URL, siteName);
```

**DEPOIS:**
```csharp
await scrapingService.ScrapeTagsAsync(request.URL, siteName);
```

---

## ?? Registrar Novos Serviços no DI (Program.cs)

```csharp
// Adicionar ao Program.cs ou arquivo de configuração de DI:
builder.Services.AddScoped<PuppeteerPageService>();
builder.Services.AddScoped<DateTimeParsingService>();
builder.Services.AddScoped<BetSavingService>();
```

---

## ?? Testes

Após refatorar cada scraper:

1. ? Compilar sem erros
2. ? Testar com URL real (1-2 jogos)
3. ? Verificar logs
4. ? Confirmar que dados estão salvos corretamente
5. ? Validar que não há memory leaks

---

## ?? Próximos Passos

1. Implementar **Passo 1** (Injeção de Dependência) para KTOScraping
2. Testar e validar
3. Implementar **Passo 2-5** (Conversão para async)
4. Testar e validar
5. Repetir para BetnacionalScraping
6. Repetir para PixbetScraping
7. Repetir para SuperbetScraping
8. Repetir para BetfairScraping
9. Atualizar Interfaces e Controller
10. Teste integrado final

---

## ?? Dicas

- Use find-and-replace para substituir `.GetAwaiter().GetResult()` com variações
- Certifique-se de que todos os métodos assincronos retornam `Task` ou `Task<T>`
- Mantenha o padrão de `async/await` consistente
- Execute build frequentemente durante a refatoração
- Faça commits pequenos a cada scraper refatorado

