using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Betano;
using System.Globalization;

namespace BetSniffer.Api.Core.Sites.Betnacional
{
    public class BetnacionalScraping : IScrapingService
    {
        #region VariaveisGlobais

        private string gameName;
        private string gameDayText;
        private string gameHourText;
        private string homeTeam;
        private string awayTeam;
        private string leagueName;
        private DateTime gameDateTime;
        private Site site;
        private GamesInfo gamesInfo;
        private readonly GameService _gameService;

        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private WebScrapingServicePuppeteer _webScrapingService;
        private readonly ILogService _logService;

        #endregion

        public BetnacionalScraping(
            ApplicationDbContext dbContext,
            TeamService teamService,
            IRepositoryService<GamesInfo> gamesInfoRepository,
            IRepositoryService<BetInfo> betInfoRepository
            )
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _gameService = new GameService(_dbContext);

            // Inicializa o serviço de log diretamente
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

            ExtractGameInfo(page).GetAwaiter().GetResult();

            //string popupSelector = ".overlay.new-message.visible .popup span.close";

            //_gameService.ClosePopup(page,popupSelector);

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            BetnacionalTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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
                gamesInfo.GameName = _teamService.NormalizeText(homeTeam) + " - " + _teamService.NormalizeText(awayTeam);

                // Verifica se existem apostas associadas ao jogo
                var existingBets = _dbContext.BetInfo.Where(b => b.GameId == gamesInfo.GameId).ToList();

                if (existingBets.Any())
                {
                    Console.WriteLine($"Encontradas {existingBets.Count} apostas associadas ao jogo: {gamesInfo.GameName}");

                    // Remove todas as apostas associadas ao jogo
                    _dbContext.BetInfo.RemoveRange(existingBets);
                    Console.WriteLine($"Apostas associadas ao jogo {gamesInfo.GameName} da casa {gamesInfo.Site.Name} foram removidas.");
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
                    GameName = _teamService.NormalizeText(homeTeam) + " - " + _teamService.NormalizeText(awayTeam)
                };
                _dbContext.GamesInfo.Add(gamesInfo);

                // Como o jogo é novo, nenhuma aposta estará associada a ele ainda.
                Console.WriteLine($"Nenhuma aposta associada ao jogo: {gamesInfo.GameName} (novo jogo adicionado).");
            }

            Console.WriteLine($"Salvando Jogo: {gamesInfo.GameName}");
            _dbContext.SaveChanges();
            Console.WriteLine("Jogo salvo com sucesso.");

            ProcessTabsAndMarketViews(page).GetAwaiter().GetResult();


            Console.WriteLine("Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return new List<TagInfo>();
        }

        public void ClosePopup(IPage page, string popupSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                System.Threading.Thread.Sleep(new Random().Next(1855, 3626)); // Espera aleatória

                var element = page.WaitForSelectorAsync(popupSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                }).GetAwaiter().GetResult();

                if (element != null)
                {
                    element.ClickAsync().GetAwaiter().GetResult();
                    Console.WriteLine("Pop-up fechado com sucesso.");
                }
                else
                {
                    Console.WriteLine("Pop-up não encontrado.");
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Tempo de espera para fechar o pop-up expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao fechar o pop-up: {ex.Message}");
            }
        }

        public void ConfirmAgeVerification(IPage page, string ageVerificationSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                System.Threading.Thread.Sleep(new Random().Next(1511, 3522)); // Espera aleatória

                var element = page.WaitForSelectorAsync(ageVerificationSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                }).GetAwaiter().GetResult();

                if (element != null)
                {
                    element.ClickAsync().GetAwaiter().GetResult();
                    Console.WriteLine("Botão 'Sim' clicado com sucesso.");
                }
                else
                {
                    Console.WriteLine("Botão 'Sim' não encontrado.");
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Tempo de espera para o botão 'Sim' expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao confirmar verificação de idade: {ex.Message}");
            }
        }

        private Site AddNewSite(string siteName)
        {
            var site = new Site { Name = siteName };
            _dbContext.Site.Add(site);
            _dbContext.SaveChanges();
            Console.WriteLine($"Novo site adicionado: {siteName}");
            return site;
        }

        private async Task ExtractGameInfo(IPage page)
        {
            try
            {
                await Task.Delay(new Random().Next(1423, 2687));

                var headerElement = await page.WaitForSelectorAsync("div.bg-header", new WaitForSelectorOptions { Timeout = 10000 });
                if (headerElement == null)
                    throw new Exception("Elemento com informações gerais do jogo não encontrado.");

                // 🎯 Captura dos times
                var matchNameElement = await headerElement.QuerySelectorAsync("span.text-sm.md\\:text-lg.text-text-light-primary.font-bold");
                var matchName = matchNameElement != null
                    ? await matchNameElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()")
                    : null;

                if (string.IsNullOrWhiteSpace(matchName) || !matchName.Contains(" x "))
                    throw new Exception($"Formato de nome de jogo inválido: '{matchName}'");

                var teams = matchName.Split(" x ");
                if (teams.Length != 2)
                    throw new Exception($"Erro ao dividir os nomes dos times: '{matchName}'");

                homeTeam = teams[0].Trim();
                awayTeam = teams[1].Trim();

                Console.WriteLine($"✅ Times identificados: {homeTeam} vs {awayTeam}");

                // 🌍 Captura da Liga, Categoria e Esporte
                var breadcrumbElements = await headerElement.QuerySelectorAllAsync("div.inline a");
                if (breadcrumbElements.Length >= 3)
                {
                    string esporte = await breadcrumbElements[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    string categoria = await breadcrumbElements[1].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    leagueName = await breadcrumbElements[2].EvaluateFunctionAsync<string>("el => el.textContent.trim()");

                    Console.WriteLine($"✅ Esporte: {esporte}, Categoria: {categoria}, Liga: {leagueName}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar todos os breadcrumbs da liga.");
                }

                // 🕒 Data e Hora do jogo
                var dateElement = await page.WaitForSelectorAsync("div.bg-odds-subheader .text-base.text-text-light-tertiary", new WaitForSelectorOptions { Timeout = 8000 });
                if (dateElement == null)
                    throw new Exception("Elemento com data e hora do jogo não encontrado.");

                var dateTimeText = await dateElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                gameDateTime = ParseGameDateTime(dateTimeText);

                Console.WriteLine($"✅ Data e Hora do Jogo: {gameDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao extrair informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao extrair informações do jogo", ex);
            }
        }


        private DateTime ParseGameDateTime(string text)
        {
            var now = DateTime.Now;
            var culture = CultureInfo.InvariantCulture;

            try
            {
                if (string.IsNullOrWhiteSpace(text))
                    throw new ArgumentException("Texto de data/hora vazio ou nulo.");

                text = text.Trim();

                if (text.StartsWith("Hoje"))
                {
                    var hour = text.Replace("Hoje às", "").Trim();
                    var dateString = $"{now:dd/MM/yyyy} {hour}";

                    if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, DateTimeStyles.None, out var today))
                        return today;
                }
                else if (text.StartsWith("Amanhã"))
                {
                    var hour = text.Replace("Amanhã às", "").Trim();
                    var dateString = $"{now.AddDays(1):dd/MM/yyyy} {hour}";

                    if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, DateTimeStyles.None, out var tomorrow))
                        return tomorrow;
                }
                else
                {
                    // Exemplo: "sábado, 29 março às 10:00"
                    var regex = new Regex(@"(\d{1,2})\s+([a-zç]+)\s+às\s+(\d{2}:\d{2})", RegexOptions.IgnoreCase);
                    var match = regex.Match(text);

                    if (match.Success)
                    {
                        int day = int.Parse(match.Groups[1].Value);
                        string monthName = match.Groups[2].Value.ToLower();
                        string time = match.Groups[3].Value;

                        var monthMap = new Dictionary<string, int>
                {
                    { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
                    { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
                    { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 }
                };

                        if (!monthMap.TryGetValue(monthName, out int month))
                            throw new Exception($"Mês inválido: {monthName}");

                        var year = now.Year; // Presume o ano atual
                        var dateString = $"{day:D2}/{month:D2}/{year} {time}";

                        if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, DateTimeStyles.None, out var parsedDate))
                            return parsedDate;
                    }
                }

                throw new FormatException($"Formato inesperado para data/hora: '{text}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao converter data/hora: '{text}' - {ex.Message}");
                _logService.LogError("Erro ao converter data/hora", ex);
                throw;
            }
        }

        private async Task ProcessTabsAndMarketViews(IPage page)
        {
            var ignoredTabs = new HashSet<string> { "Criar aposta", "Buscar" };
            var processedTabs = new HashSet<string>();
            var tabSelector = "button[data-testid^='eventTab-']";
            var marketContainerSelector = "section[data-testid='market-outcome-list']";

            await Task.Delay(new Random().Next(625, 1684));

            var tagNames = BetnacionalTags.TagNames;

            try
            {
                bool finished = false;

                while (!finished)
                {
                    try
                    {
                        var tabs = await page.QuerySelectorAllAsync(tabSelector);
                        if (tabs == null || tabs.Length == 0)
                        {
                            Console.WriteLine("Nenhuma aba encontrada na página.");
                            return;
                        }

                        bool allTabsProcessed = true;

                        foreach (var tab in tabs)
                        {
                            try
                            {
                                var tabName = await tab.EvaluateFunctionAsync<string>(
                                    "el => el.textContent.trim().replace(/\\s+Novo$/, '')"
                                );

                                if (string.IsNullOrEmpty(tabName))
                                {
                                    Console.WriteLine("Nome da aba não pôde ser capturado.");
                                    continue;
                                }

                                if (ignoredTabs.Contains(tabName) || processedTabs.Contains(tabName))
                                {
                                    Console.WriteLine($"Ignorando aba: {tabName}");
                                    continue;
                                }

                                Console.WriteLine($"Processando aba: {tabName}");

                                int retries = 0;
                                bool clicked = false;

                                while (!clicked && retries < 3)
                                {
                                    try
                                    {
                                        await page.EvaluateFunctionAsync("el => el.click()", tab);
                                        await Task.Delay(new Random().Next(421, 684));
                                        clicked = true;
                                        Console.WriteLine($"Aba '{tabName}' clicada com sucesso via JS.");
                                    }
                                    catch (Exception)
                                    {
                                        retries++;
                                        Console.WriteLine($"Erro ao clicar na aba '{tabName}', tentativa {retries}.");
                                    }
                                }

                                if (!clicked)
                                {
                                    Console.WriteLine($"Falha ao clicar na aba '{tabName}' após 3 tentativas.");
                                    allTabsProcessed = false;
                                    continue;
                                }

                                processedTabs.Add(tabName);

                                var marketsLoaded = await page.WaitForSelectorAsync(marketContainerSelector, new WaitForSelectorOptions
                                {
                                    Timeout = 10000
                                });

                                if (marketsLoaded == null)
                                {
                                    Console.WriteLine("Mercados não carregaram após o clique na aba.");
                                    continue;
                                }

                                await ProcessMarketViews(page, tagNames, tabName);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar aba: {ex.Message}");
                                allTabsProcessed = false;
                            }
                        }

                        finished = allTabsProcessed;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar as abas principais: {ex.Message}");
                        finished = true;
                    }
                }

                Console.WriteLine("Todas as abas foram processadas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro geral ao processar as abas: {ex.Message}");
            }
        }

        private async Task ProcessMarketViews(IPage page, Dictionary<int, List<string>> tagNames, string tabName = null)
        {
            try
            {
                Console.WriteLine("Iniciando processamento dos mercados...");
                await Task.Delay(new Random().Next(851, 1132));

                var marketContainers = await page.QuerySelectorAllAsync("[data-testid='outcomes-by-market']");
                if (marketContainers == null || !marketContainers.Any())
                {
                    Console.WriteLine("Nenhum mercado encontrado.");
                    return;
                }

                foreach (var marketContainer in marketContainers)
                {
                    try
                    {
                        var titleElement = await marketContainer.QuerySelectorAsync("[data-testid='market-name']");
                        if (titleElement == null) continue;

                        var marketTitleRaw = await titleElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                        var marketTitle = _teamService.NormalizeText(marketTitleRaw);

                        if (!tagNames.Values.Any(list => list.Contains(marketTitle)))
                        {
                            Console.WriteLine($"Mercado ignorado: {marketTitle}");
                            continue;
                        }

                        var normalizedTagName = _teamService.NormalizeText(marketTitle);
                        var matchingTag = tagNames
                            .FirstOrDefault(tag => tag.Value.Any(tagValue => _teamService.NormalizeText(tagValue) == normalizedTagName));

                        if (matchingTag.Key == 0)
                        {
                            Console.WriteLine($"Tag não encontrada para: {marketTitle}");
                            continue;
                        }

                        int tagId = matchingTag.Key;
                        var currentBets = new List<BetInfo>();

                        // 🧠 Estrutura de grid (como "Total")
                        var gridItems = await marketContainer.QuerySelectorAllAsync("div.grid.grid-cols-3 > div");
                        if (gridItems != null && gridItems.Length >= 3 && gridItems.Length % 3 == 0)
                        {
                            for (int i = 0; i + 2 < gridItems.Length; i += 3)
                            {
                                try
                                {
                                    var lineText = await gridItems[i].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                                    if (!decimal.TryParse(lineText.Replace(".", ","), out var betAmount))
                                    {
                                        Console.WriteLine($"Linha de aposta inválida: {lineText}");
                                        continue;
                                    }

                                    var overSpan = await gridItems[i + 1].QuerySelectorAsync("span");
                                    var underSpan = await gridItems[i + 2].QuerySelectorAsync("span");

                                    var overText = overSpan != null ? await overSpan.EvaluateFunctionAsync<string>("el => el.textContent.trim()") : null;
                                    var underText = underSpan != null ? await underSpan.EvaluateFunctionAsync<string>("el => el.textContent.trim()") : null;

                                    if (decimal.TryParse(overText?.Replace(".", ","), out var multiplierOver))
                                    {
                                        currentBets.Add(new BetInfo
                                        {
                                            GamesInfo = gamesInfo,
                                            TagName = marketTitle,
                                            OverUnder = "Mais de",
                                            BetAmount = betAmount,
                                            Multiplier = multiplierOver,
                                            GameDate = gamesInfo.GameDate,
                                            CaptureDate = DateTime.Now,
                                            Site = gamesInfo.Site,
                                            TagId = tagId
                                        });
                                    }

                                    if (decimal.TryParse(underText?.Replace(".", ","), out var multiplierUnder))
                                    {
                                        currentBets.Add(new BetInfo
                                        {
                                            GamesInfo = gamesInfo,
                                            TagName = marketTitle,
                                            OverUnder = "Menos de",
                                            BetAmount = betAmount,
                                            Multiplier = multiplierUnder,
                                            GameDate = gamesInfo.GameDate,
                                            CaptureDate = DateTime.Now,
                                            Site = gamesInfo.Site,
                                            TagId = tagId
                                        });
                                    }
                                }
                                catch (Exception exGrid)
                                {
                                    Console.WriteLine($"Erro ao processar linha de mercado em grid: {exGrid.Message}");
                                    _logService.LogError("Erro ao processar linha de mercado em grid", exGrid);
                                }
                            }
                        }
                        else
                        {
                            // 🧠 Estrutura tradicional
                            var oddElements = await marketContainer.QuerySelectorAllAsync("[data-testid^='odd-']");
                            if (oddElements == null || oddElements.Length == 0)
                            {
                                Console.WriteLine($"Sem odds encontradas para o mercado: {marketTitle}");
                                continue;
                            }

                            foreach (var oddElement in oddElements)
                            {
                                try
                                {
                                    var span = await oddElement.QuerySelectorAsync("span");
                                    if (span == null) continue;

                                    var multiplierText = await span.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                                    if (!decimal.TryParse(multiplierText.Replace(".", ","), out var multiplier))
                                    {
                                        Console.WriteLine($"Multiplicador inválido: {multiplierText}");
                                        continue;
                                    }

                                    var containerDiv = await oddElement.QuerySelectorAsync("div");
                                    var fullOddText = containerDiv != null
                                        ? await containerDiv.EvaluateFunctionAsync<string>("el => el.textContent.trim()")
                                        : await oddElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");

                                    string betName = fullOddText.Replace(multiplierText, "").Trim();
                                    string overUnder = "";

                                    if (betName.StartsWith("Mais de") || betName.StartsWith("Acima"))
                                        overUnder = "Mais de";
                                    else if (betName.StartsWith("Menos de") || betName.StartsWith("Abaixo"))
                                        overUnder = "Menos de";

                                    decimal betAmount = 0;
                                    var match = Regex.Match(betName, @"(\d+(?:[.,]\d+)?)");
                                    if (match.Success)
                                    {
                                        decimal.TryParse(match.Value.Replace(".", ","), out betAmount);
                                    }

                                    if ((overUnder == "Mais de" || overUnder == "Menos de") && betAmount == 0)
                                    {
                                        Console.WriteLine($"Valor da aposta não identificado: {betName}");
                                        continue;
                                    }

                                    currentBets.Add(new BetInfo
                                    {
                                        GamesInfo = gamesInfo,
                                        TagName = marketTitle,
                                        OverUnder = overUnder,
                                        BetAmount = betAmount,
                                        Multiplier = multiplier,
                                        GameDate = gamesInfo.GameDate,
                                        CaptureDate = DateTime.Now,
                                        Site = gamesInfo.Site,
                                        TagId = tagId
                                    });
                                }
                                catch (Exception exOdd)
                                {
                                    Console.WriteLine($"Erro ao processar odd: {exOdd.Message}");
                                    _logService.LogError("Erro ao processar odd", exOdd);
                                }
                            }
                        }

                        // ✅ Salva apostas extraídas
                        SaveBets(currentBets);
                    }
                    catch (Exception exMarket)
                    {
                        Console.WriteLine($"Erro ao processar mercado: {exMarket.Message}");
                        _logService.LogError("Erro ao processar mercado", exMarket);
                    }
                }

                Console.WriteLine("Processamento dos mercados finalizado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro geral no ProcessMarketViews: {ex.Message}");
                _logService.LogError("Erro no ProcessMarketViews", ex);
            }
        }

        private void SaveBets(List<BetInfo> bets)
        {
            if (bets == null || !bets.Any())
                return;

            // Cria um HashSet com combinações únicas dos critérios relevantes
            var betKeys = bets
                .Select(bet => new { bet.TagName, bet.OverUnder, bet.TagId, bet.Site, bet.GamesInfo })
                .ToHashSet();

            foreach(var betKey in betKeys) 
            {
                var existingBets = _dbContext.BetInfo.Where(b =>
                                    b.GamesInfo.GameId == betKey.GamesInfo.GameId &&
                                    b.TagName == betKey.TagName &&
                                    b.OverUnder == betKey.OverUnder &&
                                    b.TagId == betKey.TagId &&
                                    b.Site.SiteId == betKey.Site.SiteId).ToList();

                if (existingBets.Count > 0)
                {
                    // **Deletar apostas duplicadas que já estão no banco**
                    Console.WriteLine($"Aposta existente encontrada. Removendo a aposta duplicada...");
                    _dbContext.BetInfo.RemoveRange(existingBets);
                    Console.WriteLine($"Removidas {existingBets.Count} apostas antigas.");
                }
            }

            // Adiciona as novas apostas
            _dbContext.BetInfo.AddRange(bets);
            _dbContext.SaveChanges();

            Console.WriteLine($"Salvas {bets.Count} novas apostas.");
        }

    }
}
