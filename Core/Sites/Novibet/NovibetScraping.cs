using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;
using PuppeteerSharp; // ✅ Agora Puppeteer
using System;
using System.Text;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public class NovibetScraping : IScrapingService
    {
        #region ServiçosInjetados

        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private readonly TeamService _teamService;
        private readonly ApplicationDbContext _dbContext;
        private readonly GameService _gameService;
        private readonly ILogService _logService;

        #endregion

        #region VariaveisDeEstado

        private WebScrapingServicePuppeteer _webScrapingService; // ✅ Agora Puppeteer
        private string gameName;
        private string leagueName;
        private string gameDateText;
        private string homeTeam;
        private string awayTeam;
        private GamesInfo gamesInfo;
        private DateTime gameDateTime;

        #endregion

        public NovibetScraping(
            ApplicationDbContext dbContext,
            IRepositoryService<GamesInfo> gamesInfoRepository,
            IRepositoryService<BetInfo> betInfoRepository,
            TeamService teamService
        )
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gameService = new GameService(dbContext);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _logService = new LogService(configuration);

            // NÃO cria o _webScrapingService aqui! (criaremos dentro do ScrapeTags depois)
        }
        
        public List<TagInfo> ScrapeTags(string url, string siteName)
        {
            return ScrapeTagsAsync(url, siteName).GetAwaiter().GetResult();
        }

        // Método real (interno) que faz tudo de forma assíncrona
        private async Task<List<TagInfo>> ScrapeTagsAsync(string url, string siteName)
        {
            Console.WriteLine($"Iniciando scraping para {siteName} com URL: {url}");

            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrWhiteSpace(siteName))
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            var allTagInfos = new List<TagInfo>();

            _webScrapingService = new WebScrapingServicePuppeteer();

            try
            {
                await InitializeScrapingAsync(url);
                await LoadEventPresentationAsync();
                await CaptureGameInfoAsync(siteName, url);
                await SaveOrUpdateGameAsync(siteName, url);
                var tags = await ProcessCategoriesAndMarketsAsync();
                allTagInfos.AddRange(tags);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro durante o scraping: {ex.Message}");
                throw;
            }
            finally
            {
                _webScrapingService?.Dispose();
                Console.WriteLine("Navegador fechado com sucesso.");
            }

            return allTagInfos;
        }

        private async Task InitializeScrapingAsync(string url)
        {
            Console.WriteLine("Inicializando navegador e carregando página...");

            // Inicializa Puppeteer
            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize(); // Aqui ainda é síncrono na sua implementação de Puppeteer

            // Navega até a URL
            var page = _webScrapingService.NavigateTo(url);

            // Aguarda um pequeno tempo aleatório
            await Task.Delay(new Random().Next(9222, 11533));

            // Confirmar verificação de idade
            try
            {
                Console.WriteLine("Verificando necessidade de confirmação de idade...");
                string checkboxSelector = "div.ageRestrictionOptions_option:nth-of-type(1)";
                string buttonSelector = "nds-button.ageRestrictionModal_button button.button.large.teal";

                var checkbox = await page.WaitForSelectorAsync(checkboxSelector, new WaitForSelectorOptions { Timeout = 5000 });
                var button = await page.WaitForSelectorAsync(buttonSelector, new WaitForSelectorOptions { Timeout = 5000 });

                if (checkbox != null && button != null)
                {
                    await page.EvaluateFunctionAsync("(el) => el.click()", checkbox);
                    await Task.Delay(new Random().Next(511, 1222));
                    await page.EvaluateFunctionAsync("(el) => el.click()", button);
                    Console.WriteLine("Confirmação de idade realizada.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nenhuma verificação de idade necessária ou erro leve: {ex.Message}");
            }

            // Fechar pop-up de login/cadastro (se aparecer)
            try
            {
                await Task.Delay(new Random().Next(2222, 5533));
                Console.WriteLine("Tentando fechar pop-up de login...");
                var closeButton = await page.WaitForSelectorAsync(".registerOrLogin_closeButton", new WaitForSelectorOptions { Timeout = 5000 });
                if (closeButton != null)
                {
                    await page.EvaluateFunctionAsync("(el) => el.click()", closeButton);
                    Console.WriteLine("Pop-up de login fechado.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nenhum pop-up de login detectado ou erro leve: {ex.Message}");
            }

            Console.WriteLine("Página inicializada e pronta para processamento.");
        }

        private async Task LoadEventPresentationAsync()
        {
            Console.WriteLine("Aguardando carregamento dos elementos principais...");

            var page = _webScrapingService.GetPage(); // Pegamos a referência da página aberta

            try
            {
                await Task.Delay(new Random().Next(522, 833));

                // Aguarda o elemento principal do jogo
                var gameView = await page.WaitForSelectorAsync("div.prelive_main.u-fullWidth", new WaitForSelectorOptions
                {
                    Timeout = 10000 // 10 segundos
                });

                if (gameView == null)
                {
                    throw new Exception("Elemento 'div.prelive_main.u-fullWidth' não carregado.");
                }

                Console.WriteLine("'div.prelive_main.u-fullWidth' carregado com sucesso.");

                await Task.Delay(new Random().Next(522, 833));

                // Aguarda o elemento que apresenta o evento (informações do jogo)
                var eventPresentation = await page.WaitForSelectorAsync("app-event-presentation", new WaitForSelectorOptions
                {
                    Timeout = 10000
                });

                if (eventPresentation == null)
                {
                    throw new Exception("Elemento 'app-event-presentation' não carregado.");
                }

                Console.WriteLine("'app-event-presentation' carregado com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro durante o carregamento dos elementos da página: {ex.Message}");
                throw;
            }
        }

        private async Task CaptureGameInfoAsync(string siteName, string url)
        {
            Console.WriteLine("Capturando informações do jogo...");

            var page = _webScrapingService.GetPage();

            try
            {
                await Task.Delay(new Random().Next(522, 833));

                // Verificação sb-event-time (estrutura antiga para evento ao vivo)
                var timerElement = await page.QuerySelectorAsync("sb-event-time");
                if (timerElement != null)
                {
                    var timerText = (await (await timerElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                    if (!string.IsNullOrWhiteSpace(timerText))
                    {
                        throw new Exception($"❌ Evento está ao vivo (sb-event-time detectado): {timerText}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Verificação sb-event-time falhou (não crítico): {ex.Message}");
            }

            try
            {
                await Task.Delay(new Random().Next(522, 833));

                // Trabalha dentro da estrutura atualizada
                var eventPresentation = await page.QuerySelectorAsync("app-event-presentation");
                if (eventPresentation == null)
                {
                    throw new Exception("❌ Elemento 'app-event-presentation' não encontrado.");
                }

                // Verifica se o evento já está ao vivo ou prestes a começar
                var timeElement = await eventPresentation.QuerySelectorAsync("div.eventPresentation_time");
                if (timeElement != null)
                {
                    var timeText = (await (await timeElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim().ToLower();

                    if (!string.IsNullOrEmpty(timeText))
                    {
                        if (timeText.Contains("’") || timeText.Contains("ao vivo") || timeText.Contains("live") || timeText.Contains("em"))
                        {
                            throw new Exception($"❌ Evento está ao vivo ou prestes a iniciar (eventPresentation_time detectado): {timeText}");
                        }
                    }
                }

                // Captura o nome da Liga (evento)
                var leagueElement = await eventPresentation.QuerySelectorAsync("div.eventPresentation_caption");
                if (leagueElement == null)
                {
                    throw new Exception("❌ Elemento 'eventPresentation_caption' (nome da liga) não encontrado.");
                }

                leagueName = (await (await leagueElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                Console.WriteLine($"🏆 Liga detectada: {leagueName}");

                // Captura nomes dos times
                var teamElements = await eventPresentation.QuerySelectorAllAsync("span.eventPresentation_text");
                if (teamElements == null || teamElements.Length < 2)
                {
                    throw new Exception("❌ Não foi possível localizar os nomes dos times.");
                }

                homeTeam = (await (await teamElements[0].GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                awayTeam = (await (await teamElements[1].GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();

                Console.WriteLine($"🏟 Times detectados: {homeTeam} vs {awayTeam}");

                // Captura a hora/data do evento
                var gameDateElement = await eventPresentation.QuerySelectorAsync("div.eventPresentation_time");
                if (gameDateElement == null)
                {
                    throw new Exception("❌ Elemento da data/hora do evento não encontrado.");
                }

                gameDateText = (await (await gameDateElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                Console.WriteLine($"📅 Texto de data/hora capturado: {gameDateText}");

                // Interpreta a data/hora
                gameDateTime = ParseGameDateTime(gameDateText);

                Console.WriteLine($"✅ Data/Hora interpretada: {gameDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro capturando informações do jogo: {ex.Message}");
                throw; // Se der erro crítico na estrutura principal, aborta
            }
        }

        private DateTime ParseGameDateTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Texto de data/hora inválido.");

            text = text.Trim().ToLower();

            // Caso "em XX'" (evento começando em alguns minutos)
            if (Regex.IsMatch(text, @"em (\d+)'"))
            {
                Match match = Regex.Match(text, @"em (\d+)'");
                if (match.Success)
                {
                    int minutesToAdd = int.Parse(match.Groups[1].Value);
                    return DateTime.Now.AddMinutes(minutesToAdd);
                }
            }

            // Caso só horário (ex: "14:00")
            if (Regex.IsMatch(text, @"^\d{1,2}:\d{2}$"))
            {
                var today = DateTime.Today;
                var gameTime = TimeSpan.Parse(text);
                var dateTime = today.Add(gameTime);

                // Se já passou hoje, coloca para o dia seguinte
                if (dateTime <= DateTime.Now)
                    dateTime = dateTime.AddDays(1);

                return dateTime;
            }

            // Caso "10 de dez 14:45"
            if (Regex.IsMatch(text, @"^\d{1,2} de [a-z]{3} \d{2}:\d{2}$"))
            {
                Dictionary<string, int> meses = new()
        {
            { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
            { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
        };

                string[] parts = text.Split(' ');
                int day = int.Parse(parts[0]);
                string monthText = parts[2];
                string hourText = parts[3];

                if (!meses.TryGetValue(monthText, out int month))
                    throw new Exception($"Mês inválido encontrado: {monthText}");

                var dateTime = new DateTime(DateTime.Today.Year, month, day)
                    .Add(TimeSpan.Parse(hourText));

                if (dateTime <= DateTime.Now)
                    dateTime = dateTime.AddYears(1); // Evento no próximo ano se já passou

                return dateTime;
            }

            // Caso "ter. 21:30" (dia da semana)
            if (Regex.IsMatch(text, @"^[a-z]{3}\. \d{1,2}:\d{2}$"))
            {
                Dictionary<string, int> diasSemana = new()
        {
            { "dom", 0 }, { "seg", 1 }, { "ter", 2 }, { "qua", 3 },
            { "qui", 4 }, { "sex", 5 }, { "sáb", 6 }, { "sab", 6 } // "sab" normalizado para "sáb"
        };

                var parts = text.Split(' ');
                var dayOfWeekAbbr = parts[0].Replace(".", "");
                var hourText = parts[1];

                if (!diasSemana.TryGetValue(dayOfWeekAbbr, out int targetDayOfWeek))
                    throw new Exception($"Dia da semana inválido encontrado: {dayOfWeekAbbr}");

                var today = DateTime.Today;
                int todayDayOfWeek = (int)today.DayOfWeek;

                int daysToAdd = (targetDayOfWeek - todayDayOfWeek + 7) % 7;
                if (daysToAdd == 0)
                {
                    // Se já passou a hora hoje, joga para semana que vem
                    var eventTimeToday = today.Date.Add(TimeSpan.Parse(hourText));
                    if (eventTimeToday <= DateTime.Now)
                        daysToAdd = 7;
                }

                var eventDate = today.AddDays(daysToAdd).Date.Add(TimeSpan.Parse(hourText));

                return eventDate;
            }

            // Se não bateu com nenhum formato conhecido
            throw new Exception($"Formato de data/hora não reconhecido: {text}");
        }

        private async Task SaveOrUpdateGameAsync(string siteName, string url)
        {
            Console.WriteLine("Salvando ou atualizando informações do jogo...");

            var page = _webScrapingService.GetPage();

            // Captura o site
            var site = await _dbContext.Site.FirstOrDefaultAsync(s => s.Name.ToLower() == siteName.ToLower());
            if (site == null)
            {
                site = new Site { Name = siteName };
                _dbContext.Site.Add(site);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"Novo site adicionado: {siteName}");
            }

            // Captura times
            var home = await _teamService.EnsureTeamExistsAsync(homeTeam);
            var away = await _teamService.EnsureTeamExistsAsync(awayTeam);            

            // Tenta localizar o jogo no banco
            gamesInfo = await _dbContext.GamesInfo
                .Include(g => g.Site)
                .FirstOrDefaultAsync(g =>
                    g.HomeTeamId == home &&
                    g.AwayTeamId == away &&
                    g.GameDate == gameDateTime &&
                    g.Site.SiteId == site.SiteId);

            if (gamesInfo == null)
            {
                // Novo jogo
                gamesInfo = new GamesInfo
                {
                    HomeTeamId = home,
                    AwayTeamId = away,
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
            else
            {
                // Atualiza jogo existente
                gamesInfo.Status = 1;
                gamesInfo.LastUpdated = DateTime.Now;
                gamesInfo.URL = url;
                Console.WriteLine($"Jogo atualizado: {gamesInfo.GameName}");

                // Remove apostas antigas se existirem
                var existingBets = await _dbContext.BetInfo.Where(b => b.GameId == gamesInfo.GameId).ToListAsync();
                if (existingBets.Any())
                {
                    _dbContext.BetInfo.RemoveRange(existingBets);
                    Console.WriteLine($"Apostas antigas removidas do jogo: {gamesInfo.GameName}");
                }
            }

            await _dbContext.SaveChangesAsync();
            Console.WriteLine("Informações de jogo salvas/atualizadas com sucesso.");
        }

        private async Task<List<TagInfo>> ProcessMarketViewsAsync()
        {
            Console.WriteLine("Processando mercados visíveis...");

            var page = _webScrapingService.GetPage();
            var tagInfos = new List<TagInfo>();

            var marketViews = await page.QuerySelectorAllAsync("app-event-marketview");

            if (marketViews == null || marketViews.Length == 0)
            {
                Console.WriteLine("Nenhum mercado encontrado para processar.");
                return tagInfos;
            }

            NovibetTags.AddDynamicTags(homeTeam, awayTeam);

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
                    {
                        Console.WriteLine("Mercado sem nome. Pulando...");
                        continue;
                    }

                    var marketTitle = (await (await marketNameElement.GetPropertyAsync("textContent")).JsonValueAsync<string>())?.Trim();
                    if (string.IsNullOrWhiteSpace(marketTitle))
                    {
                        Console.WriteLine("Nome do mercado vazio. Pulando...");
                        continue;
                    }

                    var normalizedMarketName = NormalizeMarketName(marketTitle);

                    // Procura o TagId correto
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
                        Console.WriteLine($"Inconsistência em apostas e odds no mercado '{marketTitle}'. Pulando...");
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
                            Console.WriteLine($"Odd inválida '{oddText}' no mercado '{marketTitle}'. Pulando...");
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
                            Console.WriteLine($"Aposta '{betNameRaw}' não reconhecida no mercado '{marketTitle}'. Pulando...");
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
                    {
                        var tagInfo = new TagInfo(gamesInfo, bets);
                        tagInfos.Add(tagInfo);

                        SaveBets(bets);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
                }
            }

            Console.WriteLine($"Total de TagInfo gerados: {tagInfos.Count}");
            return tagInfos;
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
                    Console.WriteLine($"Aposta duplicada encontrada. Removendo apostas antigas...");
                    _dbContext.BetInfo.RemoveRange(existingBets);
                    Console.WriteLine($"Removidas {existingBets.Count} apostas antigas.");
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
                if (decimal.TryParse(
                    match.Value.Replace(",", "."),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var number))
                {
                    return number;
                }
            }
            return null;
        }

        // Função auxiliar para normalizar o nome dos mercados
        private string NormalizeMarketName(string text)
        {
            return text?
                .Trim()
                .ToLowerInvariant()
                .Replace("🚀", "") // Remove foguinho
                .Replace("’", "'") // Corrige aspas
                .Replace("–", "-") // Corrige traço
                .Replace("º", "o") // Corrige ordinal
                .Normalize(NormalizationForm.FormC) ?? "";
        }

        private async Task<List<TagInfo>> ProcessCategoriesAndMarketsAsync()
        {
            Console.WriteLine("Iniciando processamento das categorias de mercado...");

            var page = _webScrapingService.GetPage();
            var allTagInfos = new List<TagInfo>();
            var processedCategories = new HashSet<int>();

            await Task.Delay(new Random().Next(522, 833));

            // Localiza o carrossel de categorias
            var categoriesCarousel = await page.QuerySelectorAsync("app-event-market-categories");
            if (categoriesCarousel == null)
            {
                throw new Exception("Carrossel de categorias não encontrado.");
            }

            // Coleta todas as categorias disponíveis
            var categoryElements = await categoriesCarousel.QuerySelectorAllAsync(".swiper-slide .marketCategories_carouselItem");
            int totalCategories = categoryElements.Length;
            Console.WriteLine($"Total de categorias encontradas: {totalCategories}");

            if (totalCategories == 0)
            {
                throw new Exception("Nenhuma categoria encontrada no carrossel.");
            }

            // Localiza o botão "Next" para navegar no carrossel
            var nextButton = await page.QuerySelectorAsync(".marketCategories_arrowRight.nextBtn.u-flex.u-flexCenter");

            int i = 0;
            const int maxRetries = 3;
            const int retryDelayMs = 1000;

            while (i < totalCategories)
            {
                if (processedCategories.Contains(i))
                {
                    i++;
                    continue;
                }

                bool categoryProcessed = false;
                int attempts = 0;

                while (!categoryProcessed && attempts < maxRetries)
                {
                    try
                    {
                        var categoryElement = await categoriesCarousel.QuerySelectorAllAsync(".swiper-slide .marketCategories_carouselItem");
                        var currentCategory = categoryElement.ElementAtOrDefault(i);

                        if (currentCategory == null)
                        {
                            throw new Exception($"Categoria {i} não encontrada no carrossel.");
                        }

                        await page.EvaluateFunctionAsync("(el) => el.click()", currentCategory);

                        processedCategories.Add(i);

                        // Espera mercados aparecerem após o clique
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
                        {
                            throw new Exception("Falha ao carregar mercados para a categoria.");
                        }

                        Console.WriteLine($"Processando categoria {i + 1}...");

                        // Processa os mercados desta categoria
                        var tagInfos = await ProcessMarketViewsAsync();
                        allTagInfos.AddRange(tagInfos);

                        categoryProcessed = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar categoria {i}: {ex.Message}");
                        attempts++;

                        // Tenta navegar para próxima categoria se possível
                        if (nextButton != null)
                        {
                            await Task.Delay(new Random().Next(522, 833));

                            var nextClass = await (await nextButton.GetPropertyAsync("className")).JsonValueAsync<string>();
                            if (!string.IsNullOrEmpty(nextClass) && !nextClass.Contains("swiper-button-disabled"))
                            {
                                await nextButton.ClickAsync();
                                await Task.Delay(retryDelayMs);
                            }
                            else
                            {
                                Console.WriteLine("Botão 'Next' desabilitado. Não é possível navegar mais.");
                                break;
                            }
                        }
                    }
                }

                if (!categoryProcessed)
                {
                    Console.WriteLine($"Não foi possível processar a categoria {i} após {maxRetries} tentativas.");
                }

                i++;
            }

            Console.WriteLine("Processamento de categorias concluído.");
            return allTagInfos;
        }
    }
}
