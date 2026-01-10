# ?? Padrões de Implementação - BetSniffer.Api

## Overview

Este guia define os padrões de codificação para criar novas implementações de `IScrapingService` ou refatorar existentes.

---

## 1?? Estrutura de Arquivo (Template)

```csharp
using BetSniffer.Api.Models;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using PuppeteerSharp;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BetSniffer.Api.Core.Sites.{SiteName}
{
    public class {SiteName}Scraping : IScrapingService
    {
        #region VariaveisGlobais
        // ... campos
        #endregion

        public {SiteName}Scraping(/* ... parâmetros ... */) { }

        public List<TagInfo> ScrapeTags(string url, string siteName) { }

        // Métodos privados auxiliares
        #region Métodos Auxiliares
        // ...
        #endregion
    }
}
```

---

## 2?? Região de Variáveis Globais

### Ordem Recomendada

1. **Dados do Jogo** (strings, DateTime)
2. **Entidades do Banco** (Site, GamesInfo)
3. **Identificadores Temporários** (contadores, flags)
4. **Serviços Injetados** (readonly, com underscore)
5. **Serviços de Scraping** (WebScrapingService)

### Exemplo Completo

```csharp
#region VariaveisGlobais

// Dados do Jogo
private string homeTeam;
private string awayTeam;
private string leagueName;
private DateTime gameDateTime;

// Entidades
private Site site;
private GamesInfo gamesInfo;

// Serviços Injetados (readonly)
private readonly ApplicationDbContext _dbContext;
private readonly TeamService _teamService;
private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
private readonly IRepositoryService<BetInfo> _betInfoRepository;
private readonly GameService _gameService;
private readonly ILogService _logService;

// Serviço de Scraping
private WebScrapingServicePuppeteer _webScrapingService;

#endregion
```

---

## 3?? Método de Entrada: `ScrapeTags`

### Estrutura Obrigatória

```csharp
public List<TagInfo> ScrapeTags(string url, string siteName)
{
    // PASSO 1: Validar Entrada
    if (string.IsNullOrEmpty(url))
        throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
    if (string.IsNullOrEmpty(siteName))
        throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

    // PASSO 2: Buscar ou Criar Site
    site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ??
           AddNewSite(siteName);

    // PASSO 3: Inicializar Browser
    _webScrapingService = new WebScrapingServicePuppeteer();
    _webScrapingService.Initialize();
    
    using var browser = _webScrapingService;
    var page = browser.NavigateTo(url);

    // PASSO 4: Delay Aleatório
    System.Threading.Thread.Sleep(new Random().Next(9873, 11405));

    // PASSO 5: Try-Finally com Lógica Principal
    try
    {
        ExtractGameInfo(page).GetAwaiter().GetResult();
        SaveOrUpdateGame(siteName, url);
        ProcessTabsAndMarketViews(page).GetAwaiter().GetResult();

        Console.WriteLine("Processo de raspagem concluído.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro durante o scraping: {ex.Message}");
        _logService.LogError("Erro durante o scraping", ex);
        throw;
    }
    finally
    {
        _webScrapingService?.Dispose();
    }

    return new List<TagInfo>();
}
```

### Características Obrigatórias

? Validação de parâmetros com `ArgumentException`  
? Buscar Site existente com fallback para criar novo  
? Inicializar browser dentro do método (NÃO no construtor)  
? Usar `using` para disposição automática  
? Delay aleatório para evitar detecção  
? Try-finally com limpeza de recursos  
? Log de erros com `_logService`  

---

## 4?? Métodos Auxiliares

### A. `ExtractGameInfo(IPage page)`

**Responsabilidade**: Extrair dados do jogo da página

**Assinatura Recomendada**:
```csharp
private async Task ExtractGameInfo(IPage page)
{
    try
    {
        await Task.Delay(new Random().Next(1423, 2687));
        
        // 1. Validar elemento raiz
        var headerElement = await page.WaitForSelectorAsync("div.header-selector", 
            new WaitForSelectorOptions { Timeout = 10000 });
        if (headerElement == null)
            throw new Exception("Elemento com informações gerais não encontrado.");

        // 2. Extrair homeTeam
        // 3. Extrair awayTeam
        // 4. Extrair leagueName
        // 5. Extrair e parsear gameDateTime

        Console.WriteLine($"? Times identificados: {homeTeam} vs {awayTeam}");
        Console.WriteLine($"? Data/Hora: {gameDateTime}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Erro ao extrair informações: {ex.Message}");
        _logService.LogError("Erro ao extrair informações", ex);
        throw;
    }
}
```

**Padrão de Extração de Texto**:
```csharp
// Novo (recomendado - mais seguro)
var element = await page.QuerySelectorAsync("selector");
var text = (await (await element.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();

// Antigo (menos seguro)
var text = await element.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
```

---

### B. `SaveOrUpdateGame(string siteName, string url)`

**Responsabilidade**: Persistir jogo no banco (criar ou atualizar)

**Padrão Completo**:
```csharp
private void SaveOrUpdateGame(string siteName, string url)
{
    Console.WriteLine("Salvando ou atualizando informações do jogo...");

    // 1. Garantir times existem
    var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
    var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

    // 2. Adicionar tags dinâmicas
    {SiteName}Tags.AddDynamicTags(_teamService.NormalizeText(homeTeam), 
                                   _teamService.NormalizeText(awayTeam));

    // 3. Buscar jogo existente
    var existingGame = _dbContext.GamesInfo
        .FirstOrDefault(g =>
            g.HomeTeamId == homeTeamDb &&
            g.AwayTeamId == awayTeamDb &&
            g.GameDate == gameDateTime &&
            g.Site.SiteId == site.SiteId);

    // 4. Atualizar ou criar
    if (existingGame != null)
    {
        gamesInfo = existingGame;
        gamesInfo.Status = 1;
        gamesInfo.LastUpdated = DateTime.Now;
        gamesInfo.GameName = _teamService.NormalizeText(homeTeam) + " - " + 
                            _teamService.NormalizeText(awayTeam);

        // Remover apostas antigas
        var existingBets = _dbContext.BetInfo.Where(b => b.GameId == gamesInfo.GameId).ToList();
        if (existingBets.Any())
        {
            Console.WriteLine($"Encontradas {existingBets.Count} apostas. Removendo...");
            _dbContext.BetInfo.RemoveRange(existingBets);
        }
    }
    else
    {
        gamesInfo = new GamesInfo
        {
            HomeTeamId = homeTeamDb,
            AwayTeamId = awayTeamDb,
            GameDate = gameDateTime,
            League = leagueName,
            Site = site,
            URL = url,
            Status = 1,
            LastUpdated = DateTime.Now,
            GameName = _teamService.NormalizeText(homeTeam) + " - " + 
                      _teamService.NormalizeText(awayTeam)
        };
        _dbContext.GamesInfo.Add(gamesInfo);
        Console.WriteLine($"Novo jogo criado: {gamesInfo.GameName}");
    }

    _dbContext.SaveChanges();
    Console.WriteLine("Jogo salvo com sucesso.");
}
```

---

### C. `ProcessTabsAndMarketViews(IPage page)` / `ProcessMarketViews(IPage page)`

**Responsabilidade**: Iterar abas de mercados e processar cada uma

**Exemplo Síncrono**:
```csharp
private void ProcessTabsAndMarketViews(IPage page)
{
    try
    {
        Console.WriteLine("Iniciando processamento dos mercados...");

        var tabs = page.QuerySelectorAllAsync("button[data-testid^='eventTab-']")
            .GetAwaiter().GetResult();
        
        if (tabs == null || tabs.Length == 0)
        {
            Console.WriteLine("Nenhuma aba encontrada.");
            return;
        }

        var processedTabs = new HashSet<string>();

        foreach (var tab in tabs)
        {
            try
            {
                var tabName = tab.EvaluateFunctionAsync<string>(
                    "el => el.textContent.trim()").GetAwaiter().GetResult();

                if (processedTabs.Contains(tabName))
                {
                    Console.WriteLine($"Aba já processada: {tabName}");
                    continue;
                }

                Console.WriteLine($"Processando aba: {tabName}");
                
                tab.ClickAsync().GetAwaiter().GetResult();
                System.Threading.Thread.Sleep(new Random().Next(421, 684));

                processedTabs.Add(tabName);

                ProcessMarketViews(page);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar aba: {ex.Message}");
            }
        }

        Console.WriteLine("Processamento de abas concluído.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro geral no processamento: {ex.Message}");
        _logService.LogError("Erro no processamento", ex);
    }
}
```

**Exemplo Assíncrono**:
```csharp
private async Task ProcessTabsAndMarketViews(IPage page)
{
    try
    {
        Console.WriteLine("Iniciando processamento dos mercados...");

        var tabs = await page.QuerySelectorAllAsync("button[data-testid^='eventTab-']");
        
        if (tabs == null || tabs.Length == 0)
            return;

        foreach (var tab in tabs)
        {
            try
            {
                var tabName = await tab.EvaluateFunctionAsync<string>(
                    "el => el.textContent.trim()");

                Console.WriteLine($"Processando aba: {tabName}");
                
                await tab.ClickAsync();
                await Task.Delay(new Random().Next(421, 684));

                await ProcessMarketViews(page);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar aba: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro geral: {ex.Message}");
        _logService.LogError("Erro no processamento", ex);
    }
}
```

---

### D. `SaveBets(List<BetInfo> bets)`

**Responsabilidade**: Persistir apostas com deduplicação

**Padrão Obrigatório**:
```csharp
private void SaveBets(List<BetInfo> bets)
{
    if (bets == null || !bets.Any())
        return;

    // Criar HashSet com chaves únicas
    var betKeys = bets
        .Select(bet => new { bet.TagName, bet.OverUnder, bet.TagId, bet.Site, bet.GamesInfo })
        .ToHashSet();

    // Para cada chave única, remover apostas antigas
    foreach (var betKey in betKeys)
    {
        var existingBets = _dbContext.BetInfo.Where(b =>
            b.GamesInfo.GameId == betKey.GamesInfo.GameId &&
            b.TagName == betKey.TagName &&
            b.OverUnder == betKey.OverUnder &&
            b.TagId == betKey.TagId &&
            b.Site.SiteId == betKey.Site.SiteId).ToList();

        if (existingBets.Count > 0)
        {
            Console.WriteLine($"Aposta existente encontrada. Removendo duplicada...");
            _dbContext.BetInfo.RemoveRange(existingBets);
            Console.WriteLine($"Removidas {existingBets.Count} apostas antigas.");
        }
    }

    // Adicionar novas apostas
    _dbContext.BetInfo.AddRange(bets);
    _dbContext.SaveChanges();

    Console.WriteLine($"Salvas {bets.Count} novas apostas.");
}
```

**Critérios de Deduplicação** (ajustar conforme necessário):
- `GameId`: ID do jogo
- `TagName`: Nome da tag/mercado
- `OverUnder`: Tipo de aposta ("Mais de" ou "Menos de")
- `TagId`: ID da tag (categoria)
- `SiteId`: ID do site

---

### E. `ParseGameDateTime(string text)`

**Responsabilidade**: Converter string de data em DateTime

**Exemplo Completo**:
```csharp
private DateTime ParseGameDateTime(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        throw new ArgumentException("Texto de data/hora vazio ou nulo.");

    text = text.Trim();
    var now = DateTime.Now;
    var culture = CultureInfo.InvariantCulture;

    try
    {
        // Caso: "Hoje às 14:00"
        if (text.StartsWith("Hoje"))
        {
            var hour = text.Replace("Hoje às", "").Trim();
            var dateString = $"{now:dd/MM/yyyy} {hour}";
            if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, 
                DateTimeStyles.None, out var today))
                return today;
        }

        // Caso: "Amanhã às 10:00"
        else if (text.StartsWith("Amanhã"))
        {
            var hour = text.Replace("Amanhã às", "").Trim();
            var dateString = $"{now.AddDays(1):dd/MM/yyyy} {hour}";
            if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, 
                DateTimeStyles.None, out var tomorrow))
                return tomorrow;
        }

        // Caso: "sábado, 29 março às 10:00"
        else
        {
            var regex = new Regex(@"(\d{1,2})\s+([a-zç]+)\s+às\s+(\d{2}:\d{2})", 
                RegexOptions.IgnoreCase);
            var match = regex.Match(text);

            if (match.Success)
            {
                int day = int.Parse(match.Groups[1].Value);
                string monthName = match.Groups[2].Value.ToLower();
                string time = match.Groups[3].Value;

                var monthMap = new Dictionary<string, int>
                {
                    { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 },
                    // ... mais meses
                };

                if (monthMap.TryGetValue(monthName, out int month))
                {
                    var dateString = $"{day:D2}/{month:D2}/{now.Year} {time}";
                    if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, 
                        DateTimeStyles.None, out var parsedDate))
                        return parsedDate;
                }
            }
        }

        throw new FormatException($"Formato inesperado: '{text}'");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Erro ao converter data: '{text}' - {ex.Message}");
        _logService.LogError("Erro ao converter data", ex);
        throw;
    }
}
```

---

### F. `AddNewSite(string siteName)`

**Responsabilidade**: Criar novo site no banco

```csharp
private Site AddNewSite(string siteName)
{
    var site = new Site { Name = siteName };
    _dbContext.Site.Add(site);
    _dbContext.SaveChanges();
    Console.WriteLine($"Novo site adicionado: {siteName}");
    return site;
}
```

---

## 5?? Construtor (Injeção de Dependências)

### Padrão Obrigatório

```csharp
public {SiteName}Scraping(
    ApplicationDbContext dbContext,
    TeamService teamService,
    IRepositoryService<GamesInfo> gamesInfoRepository,
    IRepositoryService<BetInfo> betInfoRepository)
{
    _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
    _gamesInfoRepository = gamesInfoRepository ?? 
        throw new ArgumentNullException(nameof(gamesInfoRepository));
    _betInfoRepository = betInfoRepository ?? 
        throw new ArgumentNullException(nameof(betInfoRepository));
    _gameService = new GameService(_dbContext);

    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .Build();

    _logService = new LogService(configuration);
}
```

### Características

? Validar TODOS os parâmetros com null-check  
? NÃO inicializar `_webScrapingService` aqui  
? Sempre criar `_logService`  
? Sempre criar `_gameService`  

---

## 6?? Tratamento de Erros

### Erros Críticos (Relançar)

```csharp
try
{
    ExtractGameInfo(page).GetAwaiter().GetResult();
}
catch (Exception ex)
{
    Console.WriteLine($"? Erro crítico: {ex.Message}");
    _logService.LogError("Erro crítico", ex);
    throw;  // ? Relançar para parar processamento
}
```

### Erros Não-Críticos (Continuar)

```csharp
try
{
    ConfirmAgeVerification(page);
}
catch (TimeoutException)
{
    Console.WriteLine("Nenhuma verificação de idade necessária.");
}
catch (Exception ex)
{
    Console.WriteLine($"Erro ao confirmar idade (não crítico): {ex.Message}");
    // ? NÃO relançar, apenas log
}
```

---

## 7?? Delays e Aleatoriedad

### Padrão para Evitar Detecção

```csharp
// Delay entre 9.8 e 11.4 segundos no início
System.Threading.Thread.Sleep(new Random().Next(9873, 11405));

// Delay entre 421 e 684 ms entre cliques
await Task.Delay(new Random().Next(421, 684));

// Delay entre 522 e 833 ms para esperas
await Task.Delay(new Random().Next(522, 833));
```

### Por que Aleatoriedad?

- Evita padrões detectáveis
- Simula comportamento humano
- Reduz bloqueios por IP

---

## 8?? Logging e Console

### Exemplo Completo

```csharp
Console.WriteLine("? Times identificados: {homeTeam} vs {awayTeam}");
Console.WriteLine("?? Liga detectada: {leagueName}");
Console.WriteLine("?? Data/Hora: {gameDateTime}");
Console.WriteLine("?? Tag não encontrada para: {marketTitle}");
Console.WriteLine("? Erro ao extrair: {message}");

_logService.LogError("Descrição do erro", ex);
```

### Emojis Recomendados

| Emoji | Uso |
|-------|-----|
| ? | Sucesso |
| ? | Erro crítico |
| ?? | Aviso/não crítico |
| ?? | Liga |
| ?? | Times |
| ?? | Data/hora |
| ?? | Jogos |

---

## 9?? Checklist de Implementação

### Antes de Commitar

- [ ] Implementa `IScrapingService`
- [ ] Valida parâmetros de entrada
- [ ] Browser inicializado no método público
- [ ] Try-finally com dispose
- [ ] ExtractGameInfo funcional
- [ ] SaveOrUpdateGame funcional
- [ ] ProcessMarkets funcional
- [ ] SaveBets com deduplicação
- [ ] ParseGameDateTime implementado
- [ ] Delays aleatórios adicionados
- [ ] Logging com emojis
- [ ] Erros críticos são relançados
- [ ] Erros não-críticos têm fallback
- [ ] Todas as variáveis globais inicializadas
- [ ] Construtor valida todos os parâmetros
- [ ] Sem inicialização de browser no construtor
- [ ] Site e Tags configuradas

---

## ?? Exemplo Mínimo Funcional

```csharp
using BetSniffer.Api.Models;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using PuppeteerSharp;

namespace BetSniffer.Api.Core.Sites.Example
{
    public class ExampleScraping : IScrapingService
    {
        #region VariaveisGlobais
        private string homeTeam;
        private string awayTeam;
        private DateTime gameDateTime;
        private Site site;
        private GamesInfo gamesInfo;

        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly GameService _gameService;
        private readonly ILogService _logService;
        private WebScrapingServicePuppeteer _webScrapingService;
        #endregion

        public ExampleScraping(ApplicationDbContext dbContext, TeamService teamService, 
            IRepositoryService<GamesInfo> gamesInfoRepository, 
            IRepositoryService<BetInfo> betInfoRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gameService = new GameService(_dbContext);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            _logService = new LogService(configuration);
        }

        public List<TagInfo> ScrapeTags(string url, string siteName)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL não pode ser nula.", nameof(url));

            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) 
                ?? AddNewSite(siteName);

            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize();
            
            using var browser = _webScrapingService;
            var page = browser.NavigateTo(url);
            System.Threading.Thread.Sleep(new Random().Next(9873, 11405));

            try
            {
                ExtractGameInfo(page);
                SaveOrUpdateGame(siteName, url);
                Console.WriteLine("Processo concluído.");
            }
            finally
            {
                _webScrapingService?.Dispose();
            }

            return new List<TagInfo>();
        }

        private void ExtractGameInfo(IPage page)
        {
            // Implementar extração
        }

        private void SaveOrUpdateGame(string siteName, string url)
        {
            // Implementar salvamento
        }

        private Site AddNewSite(string siteName)
        {
            var site = new Site { Name = siteName };
            _dbContext.Site.Add(site);
            _dbContext.SaveChanges();
            return site;
        }
    }
}
```

---

## ?? Referências Rápidas

- **Modelo**: `Core/Sites/Betnacional/BetnacionalScraping.cs`
- **Refatorado**: `Core/Sites/Novibet/NovibetScraping.cs`
- **Refatorado**: `Core/Sites/Pixbet/PixbetScraping.cs`
- **Arquitetura**: `Docs/ARCHITECTURE.md`

