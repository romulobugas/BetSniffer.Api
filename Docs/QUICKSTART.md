# ?? Guia de Início Rápido - Implementar Novo Scraper

## ?? Objetivo
Criar uma nova implementação de `IScrapingService` seguindo os padrões estabelecidos.

---

## ?? Pré-requisitos

- [ ] Site estudado e páginas mapeadas
- [ ] Seletores CSS identificados
- [ ] Estrutura HTML do site entendida
- [ ] Tags do mercado documentadas

---

## ?? Passo 1: Criar Arquivo de Scraping

**Arquivo**: `Core/Sites/{SiteName}/{SiteName}Scraping.cs`

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

        private string homeTeam;
        private string awayTeam;
        private string leagueName;
        private DateTime gameDateTime;
        private Site site;
        private GamesInfo gamesInfo;

        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private readonly GameService _gameService;
        private readonly ILogService _logService;
        private WebScrapingServicePuppeteer _webScrapingService;

        #endregion

        public {SiteName}Scraping(
            ApplicationDbContext dbContext,
            TeamService teamService,
            IRepositoryService<GamesInfo> gamesInfoRepository,
            IRepositoryService<BetInfo> betInfoRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _gameService = new GameService(_dbContext);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _logService = new LogService(configuration);
        }

        public List<TagInfo> ScrapeTags(string url, string siteName)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrEmpty(siteName))
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ??
                   AddNewSite(siteName);

            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize();
            using var browser = _webScrapingService;
            var page = browser.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(9873, 11405));

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

        // TODO: Implementar métodos auxiliares
        // - ExtractGameInfo(IPage page)
        // - SaveOrUpdateGame(string siteName, string url)
        // - ProcessTabsAndMarketViews(IPage page)
        // - ProcessMarketViews(IPage page)
        // - SaveBets(List<BetInfo> bets)
        // - ParseGameDateTime(string text)
        // - AddNewSite(string siteName)

        private async Task ExtractGameInfo(IPage page)
        {
            throw new NotImplementedException();
        }

        private void SaveOrUpdateGame(string siteName, string url)
        {
            throw new NotImplementedException();
        }

        private async Task ProcessTabsAndMarketViews(IPage page)
        {
            throw new NotImplementedException();
        }

        private void SaveBets(List<BetInfo> bets)
        {
            throw new NotImplementedException();
        }

        private Site AddNewSite(string siteName)
        {
            var site = new Site { Name = siteName };
            _dbContext.Site.Add(site);
            _dbContext.SaveChanges();
            Console.WriteLine($"Novo site adicionado: {siteName}");
            return site;
        }
    }
}
```

---

## ??? Passo 2: Criar Arquivo de Tags

**Arquivo**: `Core/Sites/{SiteName}/{SiteName}Tags.cs`

```csharp
namespace BetSniffer.Api.Core.Sites.{SiteName}
{
    public static class {SiteName}Tags
    {
        /// <summary>
        /// TagId -> Lista de variações do nome do mercado
        /// </summary>
        public static Dictionary<int, List<string>> TagNames { get; private set; } = new()
        {
            { 1, new List<string> { "Vencedor da Partida", "Match Winner", "Resultado Final" } },
            { 2, new List<string> { "Total de Gols", "Over/Under Gols", "Total Goals" } },
            { 3, new List<string> { "Ambos Marcam", "Both Teams to Score", "GG" } },
            // ... adicionar mais tags conforme necessário
        };

        /// <summary>
        /// Adiciona tags dinâmicas específicas para o jogo (ex: vitória de cada time)
        /// </summary>
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            // Exemplo:
            // TagId 10 -> ["Vitória de {homeTeam}"]
            // TagId 11 -> ["Vitória de {awayTeam}"]
            // TagId 12 -> ["Empate"]
        }
    }
}
```

---

## ?? Passo 3: Mapear Seletores CSS

### Identifique e documente:

```
1. Seletor raiz da página (onde estão os dados)
2. Seletor do time mandante
3. Seletor do time visitante
4. Seletor do nome da liga
5. Seletor da data/hora
6. Seletor das abas de mercados
7. Seletor de cada mercado
8. Seletor das odds dentro de cada mercado
```

### Exemplo:
```
HOME TEAM: "div.scoreboard span.home-team" ? .textContent
AWAY TEAM: "div.scoreboard span.away-team" ? .textContent
LEAGUE: "div.header span.league-name" ? .textContent
DATE: "div.event-info span.date-time" ? .textContent
MARKETS: "div.market-section h3.market-name" ? .textContent
ODDS: "span.odd-value" ? .textContent
```

---

## ? Passo 4: Implementar `ExtractGameInfo`

```csharp
private async Task ExtractGameInfo(IPage page)
{
    try
    {
        Console.WriteLine("Capturando informações do jogo...");
        await Task.Delay(new Random().Next(1423, 2687));

        // 1. Validar elemento raiz
        var headerElement = await page.WaitForSelectorAsync("div.seletor-raiz", 
            new WaitForSelectorOptions { Timeout = 10000 });
        if (headerElement == null)
            throw new Exception("Elemento com informações gerais do jogo não encontrado.");

        // 2. Extrair time mandante
        var homeTeamElement = await headerElement.QuerySelectorAsync("span.seletor-home");
        homeTeam = homeTeamElement != null
            ? (await (await homeTeamElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim()
            : null;
        
        if (string.IsNullOrWhiteSpace(homeTeam))
            throw new Exception("Nome do time mandante não encontrado.");

        // 3. Extrair time visitante
        var awayTeamElement = await headerElement.QuerySelectorAsync("span.seletor-away");
        awayTeam = awayTeamElement != null
            ? (await (await awayTeamElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim()
            : null;
        
        if (string.IsNullOrWhiteSpace(awayTeam))
            throw new Exception("Nome do time visitante não encontrado.");

        // 4. Extrair liga
        var leagueElement = await headerElement.QuerySelectorAsync("span.seletor-league");
        leagueName = leagueElement != null
            ? (await (await leagueElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim()
            : null;

        // 5. Extrair data/hora
        var dateElement = await page.WaitForSelectorAsync("span.seletor-date", 
            new WaitForSelectorOptions { Timeout = 8000 });
        if (dateElement == null)
            throw new Exception("Elemento com data e hora do jogo não encontrado.");

        var dateTimeText = (await (await dateElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
        gameDateTime = ParseGameDateTime(dateTimeText);

        Console.WriteLine($"? Times identificados: {homeTeam} vs {awayTeam}");
        Console.WriteLine($"? Liga: {leagueName}");
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

---

## ?? Passo 5: Implementar `SaveOrUpdateGame`

```csharp
private void SaveOrUpdateGame(string siteName, string url)
{
    Console.WriteLine("Salvando ou atualizando informações do jogo...");

    var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
    var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

    {SiteName}Tags.AddDynamicTags(_teamService.NormalizeText(homeTeam), 
                                  _teamService.NormalizeText(awayTeam));

    var existingGame = _dbContext.GamesInfo
        .FirstOrDefault(g =>
            g.HomeTeamId == homeTeamDb &&
            g.AwayTeamId == awayTeamDb &&
            g.GameDate == gameDateTime &&
            g.Site.SiteId == site.SiteId);

    if (existingGame != null)
    {
        gamesInfo = existingGame;
        gamesInfo.Status = 1;
        gamesInfo.LastUpdated = DateTime.Now;
        gamesInfo.GameName = _teamService.NormalizeText(homeTeam) + " - " + 
                            _teamService.NormalizeText(awayTeam);

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

## ?? Passo 6: Implementar Processamento de Mercados

```csharp
private async Task ProcessTabsAndMarketViews(IPage page)
{
    try
    {
        Console.WriteLine("Processando abas de mercados...");

        var tabs = await page.QuerySelectorAllAsync("button[data-testid^='tab-']");
        if (tabs == null || tabs.Length == 0)
        {
            Console.WriteLine("Nenhuma aba encontrada.");
            return;
        }

        foreach (var tab in tabs)
        {
            try
            {
                var tabName = await tab.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
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

        Console.WriteLine("Processamento de abas concluído.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro geral: {ex.Message}");
        _logService.LogError("Erro ao processar abas", ex);
    }
}

private async Task ProcessMarketViews(IPage page)
{
    try
    {
        Console.WriteLine("Processando mercados...");

        var marketElements = await page.QuerySelectorAllAsync("div[data-testid='market']");
        if (marketElements == null || marketElements.Length == 0)
        {
            Console.WriteLine("Nenhum mercado encontrado.");
            return;
        }

        var tagNames = {SiteName}Tags.TagNames;

        foreach (var market in marketElements)
        {
            try
            {
                var marketNameElement = await market.QuerySelectorAsync("h3.market-title");
                var marketName = marketNameElement != null
                    ? (await (await marketNameElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim()
                    : null;

                if (string.IsNullOrWhiteSpace(marketName))
                    continue;

                var matchingTag = tagNames.FirstOrDefault(tag => 
                    tag.Value.Any(v => _teamService.NormalizeText(v) == _teamService.NormalizeText(marketName)));

                if (matchingTag.Key == 0)
                {
                    Console.WriteLine($"?? Tag não encontrada para: {marketName}");
                    continue;
                }

                Console.WriteLine($"? Mercado encontrado: {marketName} (TagId: {matchingTag.Key})");

                var bets = new List<BetInfo>();
                
                // Extrair odds do mercado
                var oddElements = await market.QuerySelectorAllAsync("span.odd-value");
                foreach (var oddElement in oddElements)
                {
                    var oddText = (await (await oddElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                    
                    if (decimal.TryParse(oddText?.Replace(".", ","), out var multiplier))
                    {
                        bets.Add(new BetInfo
                        {
                            GameId = gamesInfo.GameId,
                            SiteId = site.SiteId,
                            TagName = marketName,
                            OverUnder = "Mais de",  // Ajustar conforme necessário
                            BetAmount = 0,  // Extrair se disponível
                            Multiplier = multiplier,
                            CaptureDate = DateTime.Now,
                            GameDate = gamesInfo.GameDate,
                            TagId = matchingTag.Key
                        });
                    }
                }

                if (bets.Count > 0)
                    SaveBets(bets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
            }
        }

        Console.WriteLine("Mercados processados com sucesso.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao processar mercados: {ex.Message}");
        _logService.LogError("Erro ao processar mercados", ex);
    }
}
```

---

## ?? Passo 7: Implementar `SaveBets`

```csharp
private void SaveBets(List<BetInfo> bets)
{
    if (bets == null || !bets.Any())
        return;

    var betKeys = bets
        .Select(bet => new { bet.TagName, bet.OverUnder, bet.TagId, bet.Site, bet.GamesInfo })
        .ToHashSet();

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
            Console.WriteLine($"Aposta duplicada encontrada. Removendo...");
            _dbContext.BetInfo.RemoveRange(existingBets);
        }
    }

    _dbContext.BetInfo.AddRange(bets);
    _dbContext.SaveChanges();
    Console.WriteLine($"Salvas {bets.Count} novas apostas.");
}
```

---

## ?? Passo 8: Implementar `ParseGameDateTime`

```csharp
private DateTime ParseGameDateTime(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        throw new ArgumentException("Texto de data/hora vazio.");

    text = text.Trim();

    try
    {
        // Padrão: "14:00"
        if (TimeSpan.TryParse(text, out var time))
        {
            var today = DateTime.Today;
            var dateTime = today.Add(time);
            if (dateTime <= DateTime.Now)
                dateTime = dateTime.AddDays(1);
            return dateTime;
        }

        // Padrão: "10/12/2024 14:00"
        if (DateTime.TryParse(text, out var dateTime2))
            return dateTime2;

        throw new FormatException($"Formato não reconhecido: {text}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? Erro ao converter data: {text} - {ex.Message}");
        _logService.LogError("Erro ao converter data", ex);
        throw;
    }
}
```

---

## ?? Passo 9: Testar

### Criar URL de teste
```
{URL_DO_JOGO_FUTURO}
```

### Chamar via BatchScrapingController
```csharp
POST /api/batchscraping/scrape
{
  "requests": [
    {
      "URL": "{URL_DO_JOGO}",
      "SiteName": "{SiteName}",
      "GameDate": "2024-12-10T14:00:00"
    }
  ]
}
```

### Verificar logs
- Console mostra progresso
- Banco de dados contém dados novos
- Não há erros críticos

---

## ?? Passo 10: Registrar no DI Container

**Arquivo**: `Program.cs`

```csharp
// Adicionar ao método de registro de serviços
services.AddScoped<{SiteName}Scraping>();
```

---

## ? Checklist Final

- [ ] Arquivo `{SiteName}Scraping.cs` criado
- [ ] Arquivo `{SiteName}Tags.cs` criado
- [ ] `ExtractGameInfo` implementado
- [ ] `SaveOrUpdateGame` implementado
- [ ] `ProcessMarketViews` implementado
- [ ] `SaveBets` implementado
- [ ] `ParseGameDateTime` implementado
- [ ] Tags mapeadas corretamente
- [ ] Delays aleatórios adicionados
- [ ] Logging com emojis
- [ ] Try-finally com dispose
- [ ] Registrado no DI container
- [ ] Testado com URL real
- [ ] Dados salvos no banco
- [ ] Sem erros críticos

---

## ?? Troubleshooting

### Problema: "Seletor não encontrado"
**Solução**: Inspecionar página com F12, validar seletor, adicionar delay

### Problema: "Data inválida"
**Solução**: Adicionar mais formatos no `ParseGameDateTime`

### Problema: "Tag não encontrada"
**Solução**: Adicionar variação do nome em `{SiteName}Tags.cs`

### Problema: "Duplicação de apostas"
**Solução**: Verificar lógica de deduplicação em `SaveBets`

---

## ?? Referências

- **Padrão**: `Core/Sites/Betnacional/BetnacionalScraping.cs`
- **Arquitetura**: `Docs/ARCHITECTURE.md`
- **Padrões Detalhados**: `Docs/PATTERNS.md`

