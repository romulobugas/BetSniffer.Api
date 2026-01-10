using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;
using PuppeteerSharp;
using System.Text;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public class NovibetScraping : IScrapingService
    {
        #region VariaveisGlobais

        private string leagueName;
        private string homeTeam;
        private string awayTeam;
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

        public NovibetScraping(ApplicationDbContext dbContext, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository, TeamService teamService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gameService = new GameService(_dbContext);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _logService = new LogService(configuration);
        }

        public List<TagInfo> ScrapeTags(string url, string siteName)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrWhiteSpace(siteName))
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ??
                   AddNewSite(siteName);

            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize();
            using var browser = _webScrapingService;
            var page = browser.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(9222, 11533));

            try
            {
                ConfirmAgeVerification(page);
                CloseLoginPopup(page);
                ExtractGameInfo(page).GetAwaiter().GetResult();
                SaveOrUpdateGame(siteName, url);
                ProcessCategoriesAndMarkets(page).GetAwaiter().GetResult();

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

        private void ConfirmAgeVerification(IPage page)
        {
            try
            {
                Console.WriteLine("Verificando necessidade de confirmação de idade...");
                
                // Novos seletores baseados na estrutura atual (Angular components)
                var confirmButton = page.WaitForSelectorAsync("nds-button.ageRestrictionModal_button button.black", new WaitForSelectorOptions { Timeout = 5000 }).GetAwaiter().GetResult();

                if (confirmButton != null)
                {
                    page.EvaluateFunctionAsync("(el) => el.click()", confirmButton).GetAwaiter().GetResult();
                    System.Threading.Thread.Sleep(new Random().Next(1998, 2715));
                    Console.WriteLine("✅ Confirmação de idade (18+) realizada.");
                }
            }
            catch (PuppeteerSharp.WaitTaskTimeoutException)
            {
                Console.WriteLine("⚠️ Nenhuma verificação de idade necessária.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao confirmar idade (não crítico): {ex.Message}");
            }
        }

        private void CloseLoginPopup(IPage page)
        {
            try
            {
                System.Threading.Thread.Sleep(new Random().Next(1115, 2388));
                Console.WriteLine("Tentando fechar pop-up de login...");
                
                // Tenta múltiplos seletores possíveis para fechar pop-up
                var closeButton = page.WaitForSelectorAsync("button[aria-label='Fechar'], .registerOrLogin_closeButton, [class*='close'][class*='button']", new WaitForSelectorOptions { Timeout = 3000 }).GetAwaiter().GetResult();
                
                if (closeButton != null)
                {
                    page.EvaluateFunctionAsync("(el) => el.click()", closeButton).GetAwaiter().GetResult();
                    System.Threading.Thread.Sleep(new Random().Next(500, 1000));
                    Console.WriteLine("✅ Pop-up de login fechado.");
                }
            }
            catch (PuppeteerSharp.WaitTaskTimeoutException)
            {
                Console.WriteLine("⚠️ Nenhum pop-up de login detectado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao fechar pop-up (não crítico): {ex.Message}");
            }
        }

        private async Task ExtractGameInfo(IPage page)
        {
            try
            {
                Console.WriteLine("Capturando informações do jogo...");
                await Task.Delay(new Random().Next(522, 833));

                var gameView = await page.WaitForSelectorAsync("div.prelive_main.u-fullWidth", new WaitForSelectorOptions { Timeout = 10000 });
                if (gameView == null)
                    throw new Exception("Elemento 'div.prelive_main.u-fullWidth' não carregado.");

                await Task.Delay(new Random().Next(522, 833));

                var eventPresentation = await page.WaitForSelectorAsync("app-event-presentation", new WaitForSelectorOptions { Timeout = 10000 });
                if (eventPresentation == null)
                    throw new Exception("Elemento 'app-event-presentation' não carregado.");

                CheckIfLive(page).GetAwaiter().GetResult();

                var leagueElement = await eventPresentation.QuerySelectorAsync("div.eventPresentation_caption");
                if (leagueElement == null)
                    throw new Exception("Nome da liga não encontrado.");

                leagueName = (await (await leagueElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                Console.WriteLine($"🏆 Liga detectada: {leagueName}");

                var teamElements = await eventPresentation.QuerySelectorAllAsync("span.eventPresentation_text");
                if (teamElements == null || teamElements.Length < 2)
                    throw new Exception("Nomes dos times não encontrados.");

                homeTeam = (await (await teamElements[0].GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                awayTeam = (await (await teamElements[1].GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                Console.WriteLine($"🏟 Times detectados: {homeTeam} vs {awayTeam}");

                var gameDateElement = await eventPresentation.QuerySelectorAsync("div.eventPresentation_time");
                if (gameDateElement == null)
                    throw new Exception("Data/hora do evento não encontrada.");

                var gameDateText = (await (await gameDateElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                gameDateTime = ParseGameDateTime(gameDateText);
                Console.WriteLine($"✅ Data/Hora interpretada: {gameDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao extrair informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao extrair informações do jogo", ex);
                throw;
            }
        }

        private void SaveOrUpdateGame(string siteName, string url)
        {
            Console.WriteLine("Salvando ou atualizando informações do jogo...");

            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            NovibetTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

            // 🔍 Busca por jogo existente com datas próximas (±1 dia)
            // Isso evita duplicatas quando a página só mostra horário de início
            var existingGame = FindExistingGameByNearbyDate(homeTeamDb, awayTeamDb, gameDateTime, site.SiteId);

            if (existingGame != null)
            {
                gamesInfo = existingGame;
                gamesInfo.Status = 1;
                gamesInfo.LastUpdated = DateTime.Now;
                gamesInfo.GameName = _teamService.NormalizeText(homeTeam) + " - " + _teamService.NormalizeText(awayTeam);

                // Se a URL estava vazia, atualiza
                if (string.IsNullOrWhiteSpace(gamesInfo.URL))
                {
                    gamesInfo.URL = url;
                }

                var existingBets = _dbContext.BetInfo.Where(b => b.GameId == gamesInfo.GameId).ToList();
                if (existingBets.Any())
                {
                    Console.WriteLine($"Encontradas {existingBets.Count} apostas associadas ao jogo: {gamesInfo.GameName}");
                    _dbContext.BetInfo.RemoveRange(existingBets);
                    Console.WriteLine($"Apostas removidas.");
                }
                
                Console.WriteLine($"♻️ Jogo existente encontrado e atualizado: {gamesInfo.GameName}");
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
                Console.WriteLine($"🆕 Novo jogo criado: {gamesInfo.GameName}");
            }

            _dbContext.SaveChanges();
            Console.WriteLine("Jogo salvo com sucesso.");
        }

        /// <summary>
        /// Busca um jogo existente com datas próximas (±1 dia)
        /// Isso evita duplicatas quando páginas mostram apenas horário de início
        /// </summary>
        private GamesInfo FindExistingGameByNearbyDate(int homeTeamId, int awayTeamId, DateTime gameDateTime, int siteId)
        {
            // Define range de busca: ±1 dia
            var dateRangeStart = gameDateTime.Date.AddDays(-1);
            var dateRangeEnd = gameDateTime.Date.AddDays(1).AddTicks(-1); // até 23:59:59 do próximo dia

            var existingGame = _dbContext.GamesInfo
                .FirstOrDefault(g =>
                    g.HomeTeamId == homeTeamId &&
                    g.AwayTeamId == awayTeamId &&
                    g.GameDate >= dateRangeStart &&
                    g.GameDate <= dateRangeEnd &&
                    g.Site.SiteId == siteId);

            if (existingGame != null)
            {
                Console.WriteLine($"🔍 Jogo encontrado com data próxima: " +
                    $"Data do banco: {existingGame.GameDate:yyyy-MM-dd HH:mm}, " +
                    $"Data extraída: {gameDateTime:yyyy-MM-dd HH:mm}");
            }

            return existingGame;
        }

        private async Task CheckIfLive(IPage page)
        {
            try
            {
                // Busca APENAS dentro do app-event-presentation (elemento pai)
                var eventPresentation = await page.QuerySelectorAsync("app-event-presentation");
                if (eventPresentation == null)
                    return;

                // A diferença crucial: jogo ao vivo contém sb-event-scoreboard
                // Jogo futuro contém cm-card
                var scoreboardElement = await eventPresentation.QuerySelectorAsync("sb-event-scoreboard");
                
                if (scoreboardElement != null)
                {
                    // Se há scoreboard, é jogo ao vivo
                    throw new Exception($"Evento está ao vivo (scoreboard detectado)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Verificação de evento ao vivo falhou: {ex.Message}");
                throw;
            }
        }

        private DateTime ParseGameDateTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Texto de data/hora inválido.");

            text = text.Trim().ToLower();

            // Formato: "em 23'" (jogo começando em X minutos)
            if (Regex.IsMatch(text, @"em (\d+)'"))
            {
                Match match = Regex.Match(text, @"em (\d+)'");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int minutesToAdd))
                    return DateTime.Now.AddMinutes(minutesToAdd);
            }

            // Formato: "22:00" (apenas hora - hoje ou amanhã)
            if (Regex.IsMatch(text, @"^\d{1,2}:\d{2}$"))
            {
                var today = DateTime.Today;
                if (TimeSpan.TryParse(text, out var gameTime))
                {
                    var dateTime = today.Add(gameTime);
                    if (dateTime <= DateTime.Now)
                        dateTime = dateTime.AddDays(1);
                    return dateTime;
                }
            }

            // Formato: "1 de fev. 20:00" (dia de mês com hora)
            if (Regex.IsMatch(text, @"^\d{1,2} de [a-z]{3}\. \d{1,2}:\d{2}$"))
            {
                var meses = new Dictionary<string, int>
                {
                    { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
                    { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
                };

                // Padrão: "1 de fev. 20:00"
                var match = Regex.Match(text, @"^(\d{1,2}) de ([a-z]{3})\. (\d{1,2}:\d{2})$");
                if (match.Success && 
                    int.TryParse(match.Groups[1].Value, out int day) && 
                    meses.TryGetValue(match.Groups[2].Value, out int month) &&
                    TimeSpan.TryParse(match.Groups[3].Value, out var time))
                {
                    var dateTime = new DateTime(DateTime.Today.Year, month, day).Add(time);
                    if (dateTime <= DateTime.Now)
                        dateTime = dateTime.AddYears(1);
                    return dateTime;
                }
            }

            // Formato: "sáb. 09:15" (dia da semana abreviado com hora)
            if (Regex.IsMatch(text, @"^[a-z]{3}\. \d{1,2}:\d{2}$"))
            {
                var diasSemana = new Dictionary<string, int>
                {
                    { "dom", 0 }, { "seg", 1 }, { "ter", 2 }, { "qua", 3 },
                    { "qui", 4 }, { "sex", 5 }, { "sáb", 6 }, { "sab", 6 }
                };

                // Padrão: "sáb. 09:15"
                var match = Regex.Match(text, @"^([a-z]{3})\. (\d{1,2}:\d{2})$");
                if (match.Success && 
                    diasSemana.TryGetValue(match.Groups[1].Value, out int targetDayOfWeek) &&
                    TimeSpan.TryParse(match.Groups[2].Value, out var time))
                {
                    var today = DateTime.Today;
                    int todayDayOfWeek = (int)today.DayOfWeek;
                    int daysToAdd = (targetDayOfWeek - todayDayOfWeek + 7) % 7;

                    if (daysToAdd == 0)
                    {
                        var eventTimeToday = today.Date.Add(time);
                        if (eventTimeToday <= DateTime.Now)
                            daysToAdd = 7;
                    }

                    return today.AddDays(daysToAdd).Date.Add(time);
                }
            }

            throw new Exception($"Formato de data/hora não reconhecido: {text}");
        }

        private string NormalizeMarketName(string text)
        {
            return text?
                .Trim()
                .ToLowerInvariant()
                .Replace("🚀", "")
                .Replace("'", "'")
                .Replace("–", "-")
                .Replace("º", "o")
                .Normalize(NormalizationForm.FormC) ?? "";
        }

        private Site AddNewSite(string siteName)
        {
            var site = new Site { Name = siteName };
            _dbContext.Site.Add(site);
            _dbContext.SaveChanges();
            Console.WriteLine($"Novo site adicionado: {siteName}");
            return site;
        }

        private async Task ProcessCategoriesAndMarkets(IPage page)
        {
            Console.WriteLine("Iniciando processamento das categorias de mercado...");

            var processedCategories = new HashSet<int>();
            await Task.Delay(new Random().Next(522, 833));

            var categoriesCarousel = await page.QuerySelectorAsync("app-event-market-categories");
            if (categoriesCarousel == null)
                throw new Exception("Carrossel de categorias não encontrado.");

            var categoryElements = await categoriesCarousel.QuerySelectorAllAsync(".swiper-slide .marketCategories_carouselItem");
            int totalCategories = categoryElements.Length;
            Console.WriteLine($"Total de categorias encontradas: {totalCategories}");

            if (totalCategories == 0)
                throw new Exception("Nenhuma categoria encontrada.");

            var nextButton = await page.QuerySelectorAsync(".marketCategories_arrowRight.nextBtn.u-flex.u-flexCenter");

            const int maxRetries = 3;
            const int retryDelayMs = 1000;

            for (int i = 0; i < totalCategories; i++)
            {
                if (processedCategories.Contains(i))
                    continue;

                bool categoryProcessed = false;
                int attempts = 0;

                while (!categoryProcessed && attempts < maxRetries)
                {
                    try
                    {
                        var categoryElement = await categoriesCarousel.QuerySelectorAllAsync(".swiper-slide .marketCategories_carouselItem");
                        var currentCategory = categoryElement.ElementAtOrDefault(i);

                        if (currentCategory == null)
                            throw new Exception($"Categoria {i} não encontrada.");

                        await Task.Delay(new Random().Next(522, 833));
                        await page.EvaluateFunctionAsync("(el) => el.click()", currentCategory);

                        processedCategories.Add(i);

                        bool elementFound = false;
                        for (int loadAttempt = 0; loadAttempt < maxRetries; loadAttempt++)
                        {
                            try
                            {
                                var marketViews = await page.QuerySelectorAllAsync("app-event-marketview");
                                if (marketViews.Length > 0)
                                {
                                    elementFound = true;
                                    break;
                                }
                            }
                            catch
                            {
                                await Task.Delay(retryDelayMs);
                            }
                        }

                        if (!elementFound)
                            throw new Exception("Mercados não carregaram.");

                        Console.WriteLine($"Processando categoria {i + 1}...");
                        await ProcessMarketViews(page);

                        categoryProcessed = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar categoria {i}: {ex.Message}");
                        attempts++;

                        if (nextButton != null)
                        {
                            await Task.Delay(new Random().Next(522, 833));
                            var nextClass = await (await nextButton.GetPropertyAsync("className")).JsonValueAsync<string>();
                            if (!string.IsNullOrEmpty(nextClass) && !nextClass.Contains("swiper-button-disabled"))
                            {
                                await nextButton.ClickAsync();
                                await Task.Delay(retryDelayMs);
                            }
                        }
                    }
                }

                if (!categoryProcessed)
                    Console.WriteLine($"Não foi possível processar a categoria {i} após {maxRetries} tentativas.");
            }

            Console.WriteLine("Processamento de categorias concluído.");
        }

        private async Task ProcessMarketViews(IPage page)
        {
            Console.WriteLine("Processando mercados visíveis...");

            var marketViews = await page.QuerySelectorAllAsync("app-event-marketview");
            if (marketViews == null || marketViews.Length == 0)
            {
                Console.WriteLine("Nenhum mercado encontrado.");
                return;
            }

            var tagNames = NovibetTags.TagNames;
            var normalizedTags = tagNames
                .SelectMany(kv => kv.Value.Select(name => new { TagId = kv.Key, NormalizedName = NormalizeMarketName(name) }))
                .ToList();

            foreach (var marketView in marketViews)
            {
                try
                {
                    var marketNameElement = await marketView.QuerySelectorAsync("span.eventMarketview_title");
                    if (marketNameElement == null)
                        continue;

                    var marketTitle = (await (await marketNameElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                    if (string.IsNullOrWhiteSpace(marketTitle))
                        continue;

                    var normalizedMarketName = NormalizeMarketName(marketTitle);
                    var matchingTag = normalizedTags.FirstOrDefault(x => x.NormalizedName == normalizedMarketName);

                    if (matchingTag == null)
                    {
                        Console.WriteLine($"⚠️ Tag não encontrada para: {marketTitle}");
                        continue;
                    }

                    int tagId = matchingTag.TagId;
                    Console.WriteLine($"✅ Mercado aceito: {marketTitle} (TagId {tagId})");

                    var oddsElements = await marketView.QuerySelectorAllAsync("span.marketBetItem_caption");
                    var valueElements = await marketView.QuerySelectorAllAsync("span.marketBetItem_price");

                    if (oddsElements == null || valueElements == null || oddsElements.Length != valueElements.Length)
                    {
                        Console.WriteLine($"Inconsistência em apostas no mercado '{marketTitle}'.");
                        continue;
                    }

                    var bets = new List<BetInfo>();

                    for (int i = 0; i < oddsElements.Length; i++)
                    {
                        var betElement = oddsElements[i];
                        var oddElement = valueElements[i];

                        var betNameRaw = (await (await betElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                        var oddText = (await (await oddElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();

                        if (string.IsNullOrWhiteSpace(betNameRaw) || string.IsNullOrWhiteSpace(oddText))
                            continue;

                        if (!decimal.TryParse(oddText.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal multiplier))
                        {
                            Console.WriteLine($"Odd inválida '{oddText}'.");
                            continue;
                        }

                        string overUnder = null;
                        decimal? betAmount = null;

                        betNameRaw = betNameRaw.ToLowerInvariant();

                        if (betNameRaw.Contains("mais de"))
                        {
                            overUnder = "Mais de";
                            betAmount = ExtractNumericValue(betNameRaw.Replace("mais de", "").Trim());
                        }
                        else if (betNameRaw.Contains("menos de"))
                        {
                            overUnder = "Menos de";
                            betAmount = ExtractNumericValue(betNameRaw.Replace("menos de", "").Trim());
                        }
                        else if (betNameRaw.EndsWith("+"))
                        {
                            overUnder = "Mais de";
                            betAmount = ExtractNumericValue(betNameRaw.Replace("+", "").Trim());
                        }

                        if (string.IsNullOrEmpty(overUnder) || !betAmount.HasValue)
                        {
                            Console.WriteLine($"Aposta '{betNameRaw}' não reconhecida.");
                            continue;
                        }

                        var betInfo = new BetInfo
                        {
                            GameId = gamesInfo.GameId,
                            SiteId = gamesInfo.SiteId ?? 0,
                            TagName = marketTitle,
                            OverUnder = overUnder,
                            BetAmount = betAmount.Value,
                            Multiplier = multiplier,
                            CaptureDate = DateTime.Now,
                            GameDate = gamesInfo.GameDate,
                            TagId = tagId
                        };

                        bets.Add(betInfo);
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

        private void SaveBets(List<BetInfo> bets)
        {
            if (bets == null || !bets.Any())
                return;

            var betKeys = bets
                .Select(bet => new { bet.TagName, bet.OverUnder, bet.GameId, bet.SiteId })
                .ToHashSet();

            foreach (var betKey in betKeys)
            {
                var existingBets = _dbContext.BetInfo.Where(b =>
                    b.GameId == betKey.GameId &&
                    b.TagName == betKey.TagName &&
                    b.OverUnder == betKey.OverUnder &&
                    b.SiteId == betKey.SiteId).ToList();

                if (existingBets.Any())
                {
                    Console.WriteLine($"Aposta duplicada encontrada. Removendo antigas...");
                    _dbContext.BetInfo.RemoveRange(existingBets);
                }
            }

            _dbContext.BetInfo.AddRange(bets);
            _dbContext.SaveChanges();
            Console.WriteLine($"Salvas {bets.Count} novas apostas.");
        }

        private static decimal? ExtractNumericValue(string text)
        {
            var match = Regex.Match(text, @"[\d\.,]+");
            if (match.Success)
            {
                if (decimal.TryParse(match.Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                    return number;
            }
            return null;
        }
    }
}
