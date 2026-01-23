using BetSniffer.Api.Models;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using System.Globalization;
using PuppeteerSharp;
using BetSniffer.Api.Core.Sites.Pixbet;

namespace BetSniffer.Api.Core.Sites.Bet365
{
    public class PixbetScraping : IScrapingService
    {
        #region VariaveisGlobais

        private string homeTeam = string.Empty;
        private string awayTeam = string.Empty;
        private string leagueName = string.Empty;
        private DateTime gameDateTime;
        private Site site = null!;
        private GamesInfo gamesInfo = null!;

        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private readonly GameService _gameService;
        private readonly ILogService _logService;
        private WebScrapingServicePuppeteer _webScrapingService = null!;

        #endregion

        public PixbetScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
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

            System.Threading.Thread.Sleep(new Random().Next(6873, 7405));

            try
            {
                ClosePopup(page);
                ExtractGameInfo(page);
                SaveOrUpdateGame(siteName, url);
                ProcessTabsAndMarkets(page);

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
                else
                {
                    Console.WriteLine("Botão de fechar pop-up não encontrado.");
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

        private void ExtractGameInfo(IPage page)
        {
            try
            {
                Console.WriteLine("Capturando informações do jogo...");

                var gameContainer = page.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_gameContainer").GetAwaiter().GetResult();
                if (gameContainer == null)
                    throw new Exception("Container do jogo não encontrado.");

                var homeTeamElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_homeTeam > span").GetAwaiter().GetResult()
                    ?? throw new Exception("Elemento do time da casa não encontrado.");
                homeTeam = homeTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(homeTeam))
                    throw new Exception("Nome do time da casa não encontrado.");

                var awayTeamElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_awayTeam > span").GetAwaiter().GetResult()
                    ?? throw new Exception("Elemento do time visitante não encontrado.");
                awayTeam = awayTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(awayTeam))
                    throw new Exception("Nome do time visitante não encontrado.");

                var leagueElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_leagueName > span").GetAwaiter().GetResult()
                    ?? throw new Exception("Elemento da liga não encontrado.");
                leagueName = leagueElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(leagueName))
                    throw new Exception("Nome da liga não encontrado.");

                var gameDateElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_startDate").GetAwaiter().GetResult()
                    ?? throw new Exception("Elemento de data/hora não encontrado.");
                var gameDateText = gameDateElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(gameDateText))
                    throw new Exception("Data/hora do jogo não encontrada.");

                gameDateTime = ParseGameDateTime(gameDateText);

                Console.WriteLine($"🏆 Liga: {leagueName}");
                Console.WriteLine($"🏟 Times: {homeTeam} vs {awayTeam}");
                Console.WriteLine($"📅 Data e Hora: {gameDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao capturar informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao capturar informações do jogo", ex);
                throw;
            }
        }

        private DateTime ParseGameDateTime(string dateTimeText)
        {
            if (string.IsNullOrWhiteSpace(dateTimeText))
                throw new ArgumentException("Texto de data/hora está vazio.");

            try
            {
                string cleanedDate = Regex.Replace(dateTimeText, @"^[a-zA-ZÀ-ÿ\-]+, ", "").Trim();
                var parts = cleanedDate.Split(",");

                if (parts.Length != 2)
                    throw new Exception("Formato inválido para a data e hora.");

                var datePart = parts[0].Trim();
                if (!DateTime.TryParseExact(datePart, "d/MM", null, DateTimeStyles.None, out var date))
                    throw new Exception("Data inválida.");

                var timePart = parts[1].Trim();
                if (!TimeSpan.TryParse(timePart, out var time))
                    throw new Exception("Hora inválida.");

                var currentYear = DateTime.Now.Year;
                var fullDate = new DateTime(currentYear, date.Month, date.Day, time.Hours, time.Minutes, 0);

                if (fullDate < DateTime.Now)
                    fullDate = fullDate.AddYears(1);

                return fullDate;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao converter a data: {dateTimeText} - {ex.Message}");
                _logService.LogError("Erro ao converter data", ex);
                throw;
            }
        }

        private void SaveOrUpdateGame(string siteName, string url)
        {
            Console.WriteLine("Salvando ou atualizando informações do jogo...");

            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            PixbetTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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

                var existingBets = _dbContext.BetInfo.Where(b => b.GameId == gamesInfo.GameId).ToList();
                if (existingBets.Any())
                {
                    Console.WriteLine($"Encontradas {existingBets.Count} apostas associadas ao jogo: {gamesInfo.GameName}");
                    _dbContext.BetInfo.RemoveRange(existingBets);
                    Console.WriteLine($"Apostas removidas.");
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
                Console.WriteLine($"Novo jogo criado: {gamesInfo.GameName}");
            }

            _dbContext.SaveChanges();
            Console.WriteLine("Jogo salvo com sucesso.");
        }

        private void ProcessTabsAndMarkets(IPage page)
        {
            try
            {
                Console.WriteLine("Processando abas e mercados...");

                var marketTabsContainer = page.QuerySelectorAsync("div.eventpage_fe_Layout_marketTabsWrapper").GetAwaiter().GetResult();
                if (marketTabsContainer == null)
                {
                    Console.WriteLine("Contêiner de abas não encontrado.");
                    return;
                }

                var allMarketsButton = marketTabsContainer.QuerySelectorAllAsync("li.components-fe_Carousel_item span").GetAwaiter().GetResult()
                    .FirstOrDefault(tab => tab.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult() == "Todos os mercados");

                if (allMarketsButton == null)
                {
                    Console.WriteLine("Botão 'Todos os mercados' não encontrado.");
                    return;
                }

                allMarketsButton.EvaluateFunctionAsync("el => el.click()").GetAwaiter().GetResult();
                Console.WriteLine("Botão 'Todos os mercados' clicado.");

                System.Threading.Thread.Sleep(new Random().Next(873, 1405));

                ProcessMarkets(page);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar abas: {ex.Message}");
                _logService.LogError("Erro ao processar abas", ex);
                throw;
            }
        }

        private void ProcessMarkets(IPage page)
        {
            try
            {
                Console.WriteLine("Processando mercados...");

                var expandedMarketDivs = page.QuerySelectorAllAsync("div.eventpage_fe_Markets_expanded").GetAwaiter().GetResult();
                if (expandedMarketDivs == null || expandedMarketDivs.Length == 0)
                {
                    Console.WriteLine("Nenhum mercado expandido encontrado.");
                    return;
                }

                var tagNames = PixbetTags.TagNames;

                foreach (var marketDiv in expandedMarketDivs)
                {
                    try
                    {
                        var marketNameElement = marketDiv.QuerySelectorAsync("h3.eventpage_fe_Markets_marketName").GetAwaiter().GetResult();
                        var marketNameRaw = marketNameElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                        var marketName = marketNameRaw?.Split('|')[0].Trim();

                        if (string.IsNullOrEmpty(marketName))
                        {
                            Console.WriteLine("Nome do mercado não encontrado.");
                            continue;
                        }

                        Console.WriteLine($"Processando mercado: {marketName}");

                        var matchingTag = tagNames.FirstOrDefault(tag => tag.Value.Any(tagValue => _teamService.NormalizeText(tagValue) == _teamService.NormalizeText(marketName)));

                        if (matchingTag.Key == 0)
                        {
                            Console.WriteLine($"Tag não encontrada para: {marketName}");
                            continue;
                        }

                        try
                        {
                            marketDiv.EvaluateFunctionAsync("el => el.scrollIntoView({ behavior: 'smooth', block: 'center' })").GetAwaiter().GetResult();
                            System.Threading.Thread.Sleep(new Random().Next(911, 1422));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao centralizar mercado: {ex.Message}");
                        }

                        int tagId = matchingTag.Key;

                        var selectionRows = marketDiv.QuerySelectorAllAsync("button.eventpage_fe_HandicapSelection_line, button.eventpage_fe_OverUnderSelection_line").GetAwaiter().GetResult();
                        if (selectionRows == null || selectionRows.Length == 0)
                        {
                            Console.WriteLine($"Nenhuma seleção encontrada no mercado: {marketName}");
                            continue;
                        }

                        var currentBets = new List<BetInfo>();

                        foreach (var selection in selectionRows)
                        {
                            try
                            {
                                var meaningElement = selection.QuerySelectorAsync("div.eventpage_fe_HandicapSelection_name, span[title]").GetAwaiter().GetResult();
                                var meaning = meaningElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                var pointsSpan = selection.QuerySelectorAsync("span.eventpage_fe_HandicapSelection_points, span.eventpage_fe_OverUnderSelection_points").GetAwaiter().GetResult();
                                var points = pointsSpan?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                var oddsSpan = selection.QuerySelectorAsync("span.eventpage_fe_HandicapSelection_odds, span.eventpage_fe_OverUnderSelection_odds").GetAwaiter().GetResult();
                                var odds = oddsSpan?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                if (!string.IsNullOrEmpty(meaning) && !string.IsNullOrEmpty(points) && !string.IsNullOrEmpty(odds))
                                {
                                    Console.WriteLine($"Seleção: {meaning} {points} | Odds: {odds}");
                                    ProcessBetOdds(odds, meaning, points, marketName, tagId, currentBets);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar seleção: {ex.Message}");
                            }
                        }

                        if (currentBets.Count > 0)
                            SaveBets(currentBets);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
                        _logService.LogError("Erro ao processar mercado", ex);
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

        private void ProcessBetOdds(string odds, string overUnder, string betName, string marketTitle, int tagId, List<BetInfo> currentBets)
        {
            try
            {
                if (decimal.TryParse(odds?.Replace('.', ','), out var multiplier))
                {
                    currentBets.Add(new BetInfo
                    {
                        GamesInfo = gamesInfo,
                        TagName = marketTitle,
                        OverUnder = overUnder,
                        BetAmount = ParseBetAmount(betName),
                        Multiplier = multiplier,
                        GameDate = gamesInfo.GameDate,
                        CaptureDate = DateTime.Now,
                        Site = gamesInfo.Site,
                        TagId = tagId
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar odds: {ex.Message}");
            }
        }

        private decimal ParseBetAmount(string betName)
        {
            var match = Regex.Match(betName, @"[+-]?\d+[.,]?\d*");
            if (match.Success)
            {
                if (decimal.TryParse(match.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
                    return result;
            }
            throw new FormatException($"Formato de aposta inválido: {betName}");
        }

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
                    Console.WriteLine($"Aposta existente encontrada. Removendo duplicadas...");
                    _dbContext.BetInfo.RemoveRange(existingBets);
                }
            }

            _dbContext.BetInfo.AddRange(bets);
            _dbContext.SaveChanges();
            Console.WriteLine($"Salvas {bets.Count} novas apostas.");
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
