using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Betano;
using System.Globalization;
using System.Threading.Tasks;

namespace BetSniffer.Api.Core.Sites.Betfair
{
    public partial class BetfairScraping : IScrapingService, ILeagueScrapingService
    {
        #region VariaveisGlobais
        private string homeTeam = string.Empty;
        private string awayTeam = string.Empty;
        private string leagueName = string.Empty;
        private DateTime gameDateTime;
        private Site site = null!;
        private GamesInfo gamesInfo = null!;
        private readonly GameService _gameService;

        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private WebScrapingServicePuppeteer _webScrapingService = null!;
        private readonly LogService _logService;

        private static readonly char[] DateSplitSeparators = [' ', ',', '.'];

        #endregion

        public BetfairScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
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

            var siteSet = _dbContext.Site ?? throw new InvalidOperationException("DbSet<Site> não configurado no contexto.");
            site = siteSet.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ??
                   AddNewSite(siteName);

            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize();
            using var browser = _webScrapingService;
            var page = browser.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(6873, 7405));

            string obstructionSelector = ".onetrust-pc-dark-filter.ot-fade-in";

            RemoveObstruction(page, obstructionSelector);

            //string popupSelector = ".overlay.new-message.visible .popup span.close";

            //_gameService.ClosePopup(page,popupSelector);

            ExtractGameInfo(page).GetAwaiter().GetResult();

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            BetfairTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

            var gamesInfoSet = _dbContext.GamesInfo ?? throw new InvalidOperationException("DbSet<GamesInfo> não configurado no contexto.");
            var existingGame = gamesInfoSet
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
                var betInfoSet = _dbContext.BetInfo ?? throw new InvalidOperationException("DbSet<BetInfo> não configurado no contexto.");
                var existingBets = betInfoSet.Where(b => b.GameId == gamesInfo.GameId).ToList();

                if (existingBets.Count > 0)
                {
                    Console.WriteLine($"Encontradas {existingBets.Count} apostas associadas ao jogo: {gamesInfo.GameName}");

                    // Remove todas as apostas associadas ao jogo
                    betInfoSet.RemoveRange(existingBets);
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
                gamesInfoSet.Add(gamesInfo);

                // Como o jogo é novo, nenhuma aposta estará associada a ele ainda.
                Console.WriteLine($"Nenhuma aposta associada ao jogo: {gamesInfo.GameName} (novo jogo adicionado).");
            }

            Console.WriteLine($"Salvando Jogo: {gamesInfo.GameName}");
            _dbContext.SaveChanges();
            Console.WriteLine("Jogo salvo com sucesso.");


            ProcessTabsAndMarketViews(page);




            Console.WriteLine("Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return [];
        }

        public void ScrapeLeague(string url, string siteName)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrEmpty(siteName))
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            var siteSet = _dbContext.Site ?? throw new InvalidOperationException("DbSet<Site> não configurado no contexto.");
            site = siteSet.FirstOrDefault(s => string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase)) ?? AddNewSite(siteName);

            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize();

            using var browser = _webScrapingService;
            var page = browser.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(6873, 7405));

            // Remove overlays e cookies
            RemoveObstruction(page, ".onetrust-pc-dark-filter.ot-fade-in");

            // Lê nome da liga
            var leagueHeader = page.QuerySelectorAsync("h1, h4").GetAwaiter().GetResult();
            string league = leagueHeader?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult() ?? "Liga Desconhecida";
            Console.WriteLine($" Liga detectada: {league}");

            // Força carregamento de todos os jogos visíveis
            var elementHandle = page.WaitForSelectorAsync("#scrollable-desktop-container").GetAwaiter().GetResult();

            if (elementHandle != null)
            {
                elementHandle.FocusAsync().GetAwaiter().GetResult();

                for (int i = 0; i < 15; i++)
                {
                    page.Keyboard.PressAsync("PageDown").GetAwaiter().GetResult();
                    System.Threading.Thread.Sleep(new Random().Next(398, 575));
                }

                System.Threading.Thread.Sleep(new Random().Next(821, 1277));

                for (int i = 0; i < 15; i++)
                {
                    page.Keyboard.PressAsync("PageUp").GetAwaiter().GetResult();
                    System.Threading.Thread.Sleep(new Random().Next(357, 578));
                }
            }
            else
            {
                Console.WriteLine(" Elemento '#scrollable-desktop-container' não encontrado.");
            }

            // Captura todos os links de jogos com padrão estável na URL
            var fixtureNodes = page.QuerySelectorAllAsync("a[class$='-fixtureHeaderContainer']").GetAwaiter().GetResult()
                                   .Where(node => node.QuerySelectorAsync("time[datetime]").GetAwaiter().GetResult() != null)
                                   .ToList();


            if (fixtureNodes == null || fixtureNodes.Count == 0)
            {
                Console.WriteLine(" Nenhum jogo encontrado na página.");
                return;
            }

            var gamesInfoSet = _dbContext.GamesInfo ?? throw new InvalidOperationException("DbSet<GamesInfo> não configurado no contexto.");

            foreach (var fixture in fixtureNodes)
            {
                try
                {
                    // Foca visualmente no jogo para garantir renderização (evita problemas de lazy loading)
                    fixture.EvaluateFunctionAsync("el => el.scrollIntoView({ behavior: 'smooth', block: 'center' })").GetAwaiter().GetResult();
                    System.Threading.Thread.Sleep(new Random().Next(422, 685));

                    var gameInfoRoot = fixture.QuerySelectorAsync("section").GetAwaiter().GetResult();
                    if (gameInfoRoot == null) continue;

                    // Times
                    var teamLabels = gameInfoRoot.QuerySelectorAllAsync("p").GetAwaiter().GetResult();
                    if (teamLabels.Length < 2) continue;

                    var home = teamLabels[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    var away = teamLabels[1].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                    if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) continue;

                    // Data/hora
                    var timeElement = gameInfoRoot?.QuerySelectorAsync("time").GetAwaiter().GetResult();
                    if (timeElement == null) continue;

                    var datetimeRaw = timeElement
                        .EvaluateFunctionAsync<string>("el => el.getAttribute('datetime')")
                        .GetAwaiter()
                        .GetResult();

                    if (string.IsNullOrWhiteSpace(datetimeRaw)) continue;

                    DateTime gameDate;
                    try
                    {
                        var cleanDateString = datetimeRaw
                            .Replace("GMT", string.Empty, StringComparison.OrdinalIgnoreCase)
                            .Split('(')[0]
                            .Trim();

                        if (string.IsNullOrWhiteSpace(cleanDateString))
                        {
                            Console.WriteLine($" Data inválida (vazia após limpeza): {datetimeRaw}");
                            continue;
                        }

                        if (!DateTime.TryParse(cleanDateString, new CultureInfo("en-US"), out gameDate))
                        {
                            Console.WriteLine($" Falha ao converter data: {cleanDateString}");
                            continue;
                        }
                    }
                    catch
                    {
                        Console.WriteLine($" Data inválida: {datetimeRaw}");
                        continue;
                    }

                    // URL do jogo
                    var href = fixture.EvaluateFunctionAsync<string>("el => el.getAttribute('href')").GetAwaiter().GetResult();

                    var futebolIndex = url.IndexOf("/futebol/", StringComparison.OrdinalIgnoreCase);
                    var baseUrl = futebolIndex >= 0 ? url[..futebolIndex] : url;

                    // Monta o path completo do jogo, mantendo o prefixo correto (ex: "/apostas")
                    string fullUrl = href.StartsWith("http") ? href : $"{baseUrl}/futebol{href}";

                    // Verifica se os times já existem
                    var homeId = _teamService.EnsureTeamExists(home);
                    var awayId = _teamService.EnsureTeamExists(away);

                    // Verifica duplicidade
                    var existing = gamesInfoSet.FirstOrDefault(g =>
                        g.HomeTeamId == homeId &&
                        g.AwayTeamId == awayId &&
                        g.GameDate == gameDate &&
                        g.Site != null &&
                        g.Site.SiteId == site.SiteId);

                    if (existing != null)
                    {
                        existing.Status = 1;
                        existing.LastUpdated = DateTime.Now;
                        existing.URL = fullUrl;
                        existing.League = league;
                        Console.WriteLine($" Jogo atualizado: {home} vs {away}");
                    }
                    else
                    {
                        var game = new GamesInfo
                        {
                            HomeTeamId = homeId,
                            AwayTeamId = awayId,
                            GameDate = gameDate,
                            League = league,
                            Site = site,
                            URL = fullUrl,
                            Status = 1,
                            LastUpdated = DateTime.Now,
                            GameName = _teamService.NormalizeText(home) + " - " + _teamService.NormalizeText(away)
                        };
                        gamesInfoSet.Add(game);
                        Console.WriteLine($" Novo jogo salvo: {home} vs {away}");
                    }

                    _dbContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" Erro ao processar jogo: {ex.Message}");
                    _logService.LogError("Erro ao salvar jogo da liga", ex);
                }
            }

            _webScrapingService.Dispose();
            Console.WriteLine(" Finalizado salvamento dos jogos da liga.");
        }

        public void RemoveObstruction(IPage page, string obstructionSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                // Aguarda um tempo aleatório para simular comportamento humano
                System.Threading.Thread.Sleep(new Random().Next(981, 1758));

                // Localiza o elemento de obstrução
                var element = page.WaitForSelectorAsync(obstructionSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                }).GetAwaiter().GetResult();

                if (element != null)
                {
                    // Remove o elemento alterando seu estilo
                    page.EvaluateFunctionAsync("selector => { document.querySelector(selector).style.display = 'none'; }", obstructionSelector).GetAwaiter().GetResult();
                    Console.WriteLine("Elemento de obstrução removido com sucesso.");
                }
                else
                {
                    Console.WriteLine("Elemento de obstrução não encontrado.");
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Tempo de espera para localizar o elemento de obstrução expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao remover o elemento de obstrução: {ex.Message}");
            }
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
            var siteSet = _dbContext.Site ?? throw new InvalidOperationException("DbSet<Site> não configurado no contexto.");
            var site = new Site { Name = siteName };
            siteSet.Add(site);
            _dbContext.SaveChanges();
            Console.WriteLine($"Novo site adicionado: {siteName}");
            return site;
        }

        private async Task ExtractGameInfo(IPage page)
        {
            ArgumentNullException.ThrowIfNull(page);

            try
            {
                await Task.Delay(new Random().Next(842, 1471));

                var gameInfoElement = await page.QuerySelectorAsync("div > div > a > div > section");
                if (gameInfoElement == null)
                {
                    Console.WriteLine("Elemento principal de informações do jogo não encontrado.");
                    return;
                }

                var leagueElement = await gameInfoElement.QuerySelectorAsync("section > div > div > section > div > div:nth-child(2) > span");

                if (leagueElement == null)
                {
                    Console.WriteLine("Estrutura antiga não encontrada, tentando a nova estrutura...");
                    leagueElement = await gameInfoElement.QuerySelectorAsync("div > div:nth-child(2) > span");
                }

                if (leagueElement != null)
                {
                    leagueName = await leagueElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    Console.WriteLine($"Liga: {leagueName}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar o nome da liga em nenhuma das estruturas.");
                }

                var dateTimeElement = await gameInfoElement.QuerySelectorAsync("time");
                string gameDateTimeText = string.Empty;

                if (dateTimeElement != null)
                {
                    gameDateTimeText = await dateTimeElement.EvaluateFunctionAsync<string>("el => el.getAttribute('datetime')");
                }

                if (string.IsNullOrEmpty(gameDateTimeText) && dateTimeElement != null)
                {
                    gameDateTimeText = await dateTimeElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                }

                if (string.IsNullOrEmpty(gameDateTimeText))
                {
                    var alternativeDateElement = await gameInfoElement.QuerySelectorAsync("section > div > section > div:nth-child(2)");
                    if (alternativeDateElement != null)
                    {
                        gameDateTimeText = await alternativeDateElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    }
                }

                if (!string.IsNullOrEmpty(gameDateTimeText))
                {
                    if (FullDateRegex().IsMatch(gameDateTimeText))
                    {
                        var cleanedDateTime = GmtSuffixRegex().Replace(gameDateTimeText, string.Empty).Trim();

                        if (DateTime.TryParseExact(cleanedDateTime, "ddd MMM dd yyyy HH:mm:ss",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeLocal,
                                out var parsedDateTime))
                        {
                            gameDateTime = parsedDateTime;
                        }
                        else
                        {
                            throw new Exception($"Erro ao converter a data: {cleanedDateTime}");
                        }
                    }
                    else if (LegacyDateRegex().IsMatch(gameDateTimeText))
                    {
                        gameDateTime = ParseGameDateTime(gameDateTimeText);
                    }
                    else
                    {
                        throw new Exception($"Formato de data inesperado: {gameDateTimeText}");
                    }

                    Console.WriteLine($"Horário do Jogo: {gameDateTime}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar a data e hora do jogo em nenhuma das estruturas.");
                }

                var homeTeamElement = await gameInfoElement.QuerySelectorAsync("section > div > div > section > section > div:nth-child(2) > div:nth-child(1) > span > p");
                var awayTeamElement = await gameInfoElement.QuerySelectorAsync("section > div > div > section > section > div:nth-child(2) > div:nth-child(3) > span > p");

                if (homeTeamElement == null || awayTeamElement == null)
                {
                    Console.WriteLine("Estrutura antiga para nomes dos times não encontrada, tentando a nova estrutura...");
                    var teamsElement = await gameInfoElement.QuerySelectorAsync("section > div > section > div:nth-child(3)");

                    if (teamsElement != null)
                    {
                        var teamsText = await teamsElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                        var teams = teamsText.Split(" x ");
                        if (teams.Length == 2)
                        {
                            homeTeam = teams[0].Trim();
                            awayTeam = teams[1].Trim();
                        }
                        else
                        {
                            throw new Exception("O formato dos nomes dos times não corresponde ao esperado 'TimeCasa x TimeVisitante'.");
                        }
                    }
                }
                else
                {
                    homeTeam = await homeTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    awayTeam = await awayTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                }

                if (!string.IsNullOrEmpty(homeTeam) && !string.IsNullOrEmpty(awayTeam))
                {
                    Console.WriteLine($"Times: {homeTeam} vs {awayTeam}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar os nomes dos times em nenhuma das estruturas.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao extrair informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao extrair informações do jogo: ", ex);
                _webScrapingService?.Dispose();
                throw;
            }
        }

        private DateTime ParseGameDateTime(string dateTimeText)
        {
            if (string.IsNullOrWhiteSpace(dateTimeText))
                throw new ArgumentException("O parâmetro 'dateTimeText' está vazio ou nulo.");

            try
            {
                // Cultura brasileira para meses em português
                var culture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
                var currentYear = DateTime.Now.Year;

                // Verifica se o formato contém "Hoje" ou "Amanhã"
                if (dateTimeText.StartsWith("Hoje", StringComparison.OrdinalIgnoreCase))
                {
                    var timePart = dateTimeText.Replace("Hoje", "").Replace(",","").Trim();

                    if (DateTime.TryParseExact(timePart, "HH:mm", culture, System.Globalization.DateTimeStyles.None, out var parsedTime))
                    {
                        return DateTime.Today.AddHours(parsedTime.Hour).AddMinutes(parsedTime.Minute);
                    }
                }
                else if (dateTimeText.StartsWith("Amanhã", StringComparison.OrdinalIgnoreCase))
                {
                    var timePart = dateTimeText.Replace("Amanhã", "").Replace(",", "").Trim();
                    if (DateTime.TryParseExact(timePart, "HH:mm", culture, System.Globalization.DateTimeStyles.None, out var parsedTime))
                    {
                        return DateTime.Today.AddDays(1).AddHours(parsedTime.Hour).AddMinutes(parsedTime.Minute);
                    }
                }

                // Para formatos como "28 de dez.,17:30" ou "17 de Mai 17:15"
                var monthMappings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
                    { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
                };

                // Divide a string e extrai dia, mês e hora
                var parts = dateTimeText.Split(DateSplitSeparators, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 3 && int.TryParse(parts[0], out var day) && monthMappings.TryGetValue(parts[2], out var month))
                {
                    var timePart = parts[^1]; // Última parte deve ser o horário (HH:mm)

                    if (DateTime.TryParseExact(timePart, "HH:mm", culture, System.Globalization.DateTimeStyles.None, out var parsedTime))
                    {
                        var parsedDate = new DateTime(currentYear, month, day, parsedTime.Hour, parsedTime.Minute, 0);

                        // Ajusta para o próximo ano se a data estiver no passado
                        if (parsedDate < DateTime.Now)
                        {
                            parsedDate = parsedDate.AddYears(1);
                        }

                        return parsedDate;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Erro ao converter a data: {dateTimeText} - {ex.Message}", ex);
                throw new Exception($"Erro ao converter a data: {dateTimeText} - {ex.Message}");
            }

            throw new Exception($"Formato inesperado para 'dateTimeText': {dateTimeText}");
        }

        private void ProcessTabsAndMarketViews(IPage page)
        {
            var ignoredTabs = new HashSet<string> { "Criar Aposta", "Popular", "Jogador", "Todos os mercados" }; // Abas ignoradas
            var processedTabs = new HashSet<string>(); // Rastreia abas já processadas
            var marketCategoriesSelector = "div[role='tablist'] button"; // Ajuste o seletor para capturar abas relevantes
            var rightArrowSelector = "div.swiper-button-next"; // Exemplo de seta para navegação

            try
            {
                bool finished = false;

                while (!finished)
                {
                    try
                    {
                        // Captura as abas visíveis
                        var tabs = page.QuerySelectorAllAsync(marketCategoriesSelector).GetAwaiter().GetResult();

                        if (tabs == null || tabs.Length == 0)
                        {
                            Console.WriteLine("Nenhuma aba encontrada.");
                            break;
                        }

                        bool allTabsProcessed = true;
                        bool retryRightArrow = false;

                        foreach (var tab in tabs)
                        {
                            try
                            {
                                // Captura o nome da aba
                                var tabName = tab.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                if (string.IsNullOrEmpty(tabName))
                                {
                                    Console.WriteLine("Nome da aba não pôde ser capturado.");
                                    continue;
                                }

                                // Ignora abas já processadas ou na lista ignorada
                                if (ignoredTabs.Contains(tabName) || processedTabs.Contains(tabName))
                                {
                                    Console.WriteLine($"Ignorando aba: {tabName}");
                                    continue;
                                }

                                Console.WriteLine($"Processando aba: {tabName}");

                                // Tenta clicar na aba com até 3 tentativas
                                int retries = 0;
                                bool clicked = false;

                                while (!clicked && retries < 3)
                                {
                                    try
                                    {
                                        tab.EvaluateFunctionAsync(@"el => el.click()").GetAwaiter().GetResult();
                                        tab.FocusAsync().GetAwaiter().GetResult(); // Garante o foco no elemento ou aba
                                        page.FocusAsync("body").GetAwaiter().GetResult(); // Garante o foco no corpo da página

                                        System.Threading.Thread.Sleep(new Random().Next(421, 684)); // Pequena pausa
                                        clicked = true;
                                        Console.WriteLine($"Aba '{tabName}' clicada com sucesso.");
                                    }
                                    catch (PuppeteerException)
                                    {
                                        retries++;
                                        Console.WriteLine($"Falha ao clicar na aba '{tabName}', tentativa {retries}.");

                                        retryRightArrow = true; // Sinaliza para tentar o botão da direita
                                    }
                                }

                                if (!clicked)
                                {
                                    Console.WriteLine($"Não foi possível clicar na aba '{tabName}' após 3 tentativas.");
                                    allTabsProcessed = false;
                                    continue;
                                }

                                // Marca a aba como processada
                                processedTabs.Add(tabName);

                                // Trecho para rolar até o final da página e retornar ao topo utilizando PG DOWN e PG UP de forma simplificada.
                                // Localiza o elemento "scrollable-desktop-container"
                                var elementHandle = page.WaitForSelectorAsync("#scrollable-desktop-container").GetAwaiter().GetResult();

                                if (elementHandle != null)
                                {
                                    // Foca no elemento antes de começar o loop
                                    elementHandle.FocusAsync().GetAwaiter().GetResult();

                                    // Role até o final pressionando "PageDown" 15 vezes
                                    for (int i = 0; i < 15; i++)
                                    {
                                        // Pressiona "PageDown" no elemento focado
                                        page.Keyboard.PressAsync("PageDown").GetAwaiter().GetResult();
                                        System.Threading.Thread.Sleep(new Random().Next(398, 575)); // Pausa entre os comandos
                                    }

                                    System.Threading.Thread.Sleep(new Random().Next(821, 1277)); // Aguarda o carregamento

                                    // Role de volta ao topo pressionando "PageUp" 15 vezes
                                    for (int i = 0; i < 15; i++)
                                    {
                                        // Pressiona "PageUp" no elemento focado
                                        page.Keyboard.PressAsync("PageUp").GetAwaiter().GetResult();
                                        System.Threading.Thread.Sleep(new Random().Next(357, 578)); // Pausa entre os comandos
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Elemento 'scrollable-desktop-container' não encontrado.");
                                }

                                System.Threading.Thread.Sleep(new Random().Next(842, 1211)); // Aguarda a atualização

                                // Aguarda mercados carregarem
                                var marketsLoaded = page.WaitForSelectorAsync("div[role='tabpanel']", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                                if (marketsLoaded == null)
                                {
                                    Console.WriteLine("Nenhum mercado foi carregado para a aba selecionada.");
                                    continue;
                                }

                                // Lista todos os tabpanels
                                var allTabPanels = page.QuerySelectorAllAsync("div[role='tabpanel']").GetAwaiter().GetResult();
                                if (allTabPanels == null || allTabPanels.Length == 0)
                                {
                                    Console.WriteLine("Nenhum tabpanel encontrado.");
                                    return;
                                }

                                // Depuração: listar todos os tabpanels encontrados
                                foreach (var panel in allTabPanels)
                                {
                                    var isVisibleDebug = panel.EvaluateFunctionAsync<bool>("el => el.offsetParent !== null").GetAwaiter().GetResult();
                                    var hasDataUrnDebug = panel.QuerySelectorAsync("div[data-urn]").GetAwaiter().GetResult() != null;
                                    Console.WriteLine($"TabPanel encontrado - Visível: {isVisibleDebug}, Data URN Presente: {hasDataUrnDebug}");
                                }

                                // Filtra o tabpanel correto baseado nos critérios
                                var correctTabPanel = allTabPanels.FirstOrDefault(panel =>
                                {
                                    try
                                    {
                                        // Verifica se está visível
                                        var isVisible = panel.EvaluateFunctionAsync<bool>("el => el.offsetParent !== null").GetAwaiter().GetResult();

                                        // Verifica se há 'div[data-urn]' diretamente dentro do painel
                                        var hasDataUrn = panel.QuerySelectorAsync("div[data-urn]").GetAwaiter().GetResult() != null;

                                        // Critério: Deve estar visível e ter 'data-urn'
                                        return isVisible && hasDataUrn;
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Erro ao verificar tabpanel: {ex.Message}");
                                        return false;
                                    }
                                });

                                if (correctTabPanel == null)
                                {
                                    Console.WriteLine("Nenhum tabpanel correspondente foi encontrado.");
                                    return;
                                }

                                Console.WriteLine("Tabpanel correspondente encontrado.");

                                // Processa mercados principais dentro do tabpanel correto
                                var marketContainers = correctTabPanel.QuerySelectorAllAsync("div[data-urn]").GetAwaiter().GetResult();

                                // Lista de tags cadastradas que queremos buscar
                                var tagNames = BetfairTags.TagNames;

                                if (marketContainers is { Length: > 0 })
                                {
                                    foreach (var market in marketContainers)
                                    {
                                        try
                                        {
                                            // Captura o título do mercado (nome do mercado)
                                            var marketTitleElement = market.QuerySelectorAsync("div > div > div > button[aria-expanded='true']").GetAwaiter().GetResult(); // Ajuste o seletor conforme necessário
                                            var marketTitle = marketTitleElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                            if (string.IsNullOrEmpty(marketTitle))
                                            {
                                                Console.WriteLine("Título do mercado não encontrado.");
                                                continue; // Ignora o mercado atual
                                            }

                                            // Normaliza o título do mercado
                                            string normalizedMarketTitle = _teamService.NormalizeText(marketTitle);

                                            // Verifica se o título está na lista de tags permitidas
                                            if (!tagNames.Values.Any(tagList => tagList.Any(tag => _teamService.NormalizeText(tag) == normalizedMarketTitle)))
                                            {
                                                Console.WriteLine($"Mercado ignorado: {marketTitle}");
                                                continue;
                                            }

                                            // Verifica se o mercado está fechado e clica para abrir, se necessário
                                            var collapseState = market.QuerySelectorAsync("span[class*='collapse-chevron-closed']").GetAwaiter().GetResult();
                                            if (collapseState != null)
                                            {
                                                var toggleButton = market.QuerySelectorAsync("button[aria-expanded='false']").GetAwaiter().GetResult();
                                                if (toggleButton != null)
                                                {
                                                    toggleButton.ClickAsync().GetAwaiter().GetResult();
                                                    System.Threading.Thread.Sleep(new Random().Next(400, 800));
                                                }
                                            }

                                            

                                            // Verifica se existem submercados como "Casa", "Fora", etc.
                                            var subMarketButtons = market.QuerySelectorAllAsync("button").GetAwaiter().GetResult();
                                            if (subMarketButtons != null && subMarketButtons.Length > 1)
                                            {
                                                foreach (var subMarketButton in subMarketButtons)
                                                {
                                                    var buttonText = subMarketButton.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                                                    if (buttonText == "Casa" || buttonText == "Fora" || buttonText == "Tempo regulamentar" || buttonText == "Total" || buttonText == "Ambos os times")
                                                    {
                                                        Console.WriteLine($"Clicando no submercado: {buttonText}");
                                                        subMarketButton.EvaluateFunctionAsync(@"el => el.click()").GetAwaiter().GetResult();
                                                        subMarketButton.FocusAsync().GetAwaiter().GetResult(); // Garante o foco no elemento ou aba
                                                        System.Threading.Thread.Sleep(new Random().Next(511, 820));
                                                        Console.WriteLine($"Submercado '{buttonText}' clicado.");

                                                        // Verifica se o botão de "Mostrar mais" está presente e clica
                                                        var buttons = market.QuerySelectorAllAsync("button").GetAwaiter().GetResult();
                                            if (buttons is { Length: > 0 })
                                            {
                                                foreach (var button in buttons)
                                                {
                                                    try
                                                    {
                                                        var buttonTextExpanded = button.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                                                        if (buttonTextExpanded == "Mostrar mais")
                                                        {
                                                            Console.WriteLine("Botão 'Mostrar mais' encontrado. Tentando expandir...");
                                                            bool isExpanded = false;

                                                            // Tenta clicar no botão até 3 vezes
                                                            for (int attempt = 1; attempt <= 3; attempt++)
                                                            {
                                                                button.FocusAsync().GetAwaiter().GetResult();
                                                                System.Threading.Thread.Sleep(new Random().Next(311, 554));
                                                                button.EvaluateFunctionAsync(@"el => el.click()").GetAwaiter().GetResult();
                                                                System.Threading.Thread.Sleep(new Random().Next(322, 753));

                                                                // Verifica se o texto mudou para "Mostrar menos"
                                                                var newButtonText = button.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                                                                if (newButtonText == "Mostrar menos")
                                                                {
                                                                    Console.WriteLine($"Botão 'Mostrar mais' expandido com sucesso na tentativa {attempt}.");
                                                                    isExpanded = true;
                                                                    break;
                                                                }
                                                                else
                                                                {
                                                                    Console.WriteLine($"Tentativa {attempt}: Botão não expandiu. Tentando novamente...");
                                                                }
                                                            }

                                                            if (!isExpanded)
                                                            {
                                                                Console.WriteLine("Falha ao expandir o botão 'Mostrar mais' após 3 tentativas.");
                                                            }
                                                            break;
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Console.WriteLine($"Erro ao verificar botão: {ex.Message}");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("Nenhum botão encontrado no mercado.");
                                            }

                                            if (buttonText != "Casa" && buttonText != "Fora")
                                            {
                                                buttonText = string.Empty;
                                            }

                                                        



                                                        // Processa o mercado após o clique
                                                        ProcessMarketViews(market, buttonText);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                // Processa o mercado geral
                                                Console.WriteLine("Processando mercado geral.");
                                                ProcessMarketViews(market, "");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Nenhum mercado principal encontrado.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar uma aba: {ex.Message}");
                                allTabsProcessed = false;
                            }
                        }

                        // Se conseguiu processar todas as abas visíveis, não há necessidade de continuar
                        if (allTabsProcessed && !retryRightArrow)
                        {
                            Console.WriteLine("Todas as abas visíveis foram processadas.");
                            finished = true;
                        }
                        else if (retryRightArrow)
                        {
                            // Tenta clicar no botão da direita
                            var nextButton = page.QuerySelectorAsync(rightArrowSelector).GetAwaiter().GetResult();
                            if (nextButton != null)
                            {
                                Console.WriteLine("Tentando clicar na seta para a direita...");
                                nextButton.ClickAsync().GetAwaiter().GetResult();
                                System.Threading.Thread.Sleep(500); // Aguarda a rolagem
                            }
                            else
                            {
                                Console.WriteLine("Botão da seta para a direita não encontrado. Finalizando.");
                                finished = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar abas: {ex.Message}");
                        finished = true;
                    }
                }

                Console.WriteLine("Todas as abas foram processadas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao localizar o contêiner de abas: {ex.Message}");
            }
        }

        private void ProcessMarketViews(IElementHandle market, string buttonText)
        {
            try
            {
                // Pausa para garantir que os mercados carreguem
                Thread.Sleep(new Random().Next(951, 1232));

                // Captura o título do mercado
                var titleElement = market.QuerySelectorAsync("button[aria-expanded='true']").GetAwaiter().GetResult();
                if (titleElement == null)
                {
                    Console.WriteLine("Título do mercado não encontrado.");
                    return;
                }

                var marketTitle = titleElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                // Recupera a lista de tags
                var tagNames = BetfairTags.TagNames;

                marketTitle = marketTitle + " " + buttonText;

                marketTitle = _teamService.NormalizeText(marketTitle);

                // Filtra as tags que combinam com o título do mercado e o botão (Casa/Fora) ou mercado geral
                var matchingTag = tagNames.FirstOrDefault(tag =>
                    tag.Value.Any(tagValue =>
                        _teamService.NormalizeText(tagValue) == marketTitle));

                if (matchingTag.Key == 0)
                {
                    Console.WriteLine($"Tag não encontrada: {marketTitle} {(string.IsNullOrEmpty(buttonText) ? "" : $"({buttonText})")}");
                    return;
                }

                int tagId = matchingTag.Key;

                // Processamento adicional do mercado com o `tagId` encontrado
                Console.WriteLine($"Processando mercado: {marketTitle} {(string.IsNullOrEmpty(buttonText) ? "" : $"({buttonText})")} com Tag ID: {tagId}");

                Thread.Sleep(new Random().Next(951, 1232));

                // Captura todas as linhas de apostas baseadas na estrutura correta
                var betRows = market.QuerySelectorAllAsync("div > div > div") // Captura os possíveis contêineres de linhas
                    .GetAwaiter().GetResult()
                    .Where(row =>
                    {
                        // Verifica se a linha contém exatamente:
                        // 1. Um elemento <p> representando o nome da aposta (ex: "0,5 gols").
                        // 2. Dois botões (odds) para "Mais de" e "Menos de".
                        var betNameElement = row.QuerySelectorAsync("p").GetAwaiter().GetResult();
                        var oddButtons = row.QuerySelectorAllAsync("button").GetAwaiter().GetResult();

                        // Filtra somente linhas que possuem exatamente dois botões (odds)
                        return betNameElement != null && (oddButtons?.Length == 2 || oddButtons?.Length == 1);
                    })
                    .GroupBy(row =>
                    {
                        // Identifica cada linha de aposta pela combinação do nome da aposta e os valores de odds
                        var betName = row.QuerySelectorAsync("p").GetAwaiter().GetResult()?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                        var odds = row.QuerySelectorAllAsync("button > span").GetAwaiter().GetResult()
                            ?.Select(odd => odd.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult())
                            .ToArray();

                        return new { BetName = betName, Odds = odds }; // Chave única para a linha
                    })
                    .Select(group => group.First()) // Remove duplicatas baseadas na chave única
                    .ToArray();



                if (betRows == null || betRows.Length == 0)
                {
                    Console.WriteLine("Nenhuma linha de aposta encontrada.");
                    return;
                }

                Console.WriteLine($"Linhas de apostas encontradas: {betRows.Length}");

                var currentBets = new List<BetInfo>();

                // Processa cada linha de apostas
                foreach (var betRow in betRows)
                {
                    try
                    {
                        // Captura o nome da aposta
                        var betNameElement = betRow.QuerySelectorAsync("p").GetAwaiter().GetResult();
                        var betName = betNameElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                        if (string.IsNullOrEmpty(betName))
                        {
                            Console.WriteLine("Nome da aposta não encontrado.");
                            continue;
                        }

                        // Extrai o valor numérico do nome da aposta (ex: "1.5 Cartões", "0,5 gols", "4,5 escanteios")
                        var match = BetNameNumberRegex().Match(betName);
                        if (!match.Success)
                        {
                            Console.WriteLine($"Formato de nome de aposta inesperado: {betName}");
                            continue;
                        }

                        // Converte o valor numérico para o formato com ponto como separador decimal
                        var numericBetName = match.Value.Replace(",", ".");

                        // Captura os botões de odds na linha
                        var oddButtons = betRow.QuerySelectorAllAsync("button").GetAwaiter().GetResult()
                            .Where(button =>
                            {
                                // Verifica se o botão contém uma `span` com o valor de odds
                                var spanElement = button.QuerySelectorAsync("span").GetAwaiter().GetResult();
                                if (spanElement == null) return false;

                                var oddsText = spanElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                                return !string.IsNullOrEmpty(oddsText);
                            })
                            .ToArray();

                        if (oddButtons.Length == 0)
                        {
                            Console.WriteLine($"Nenhum botão de odds válido encontrado para a aposta '{betName}'.");
                            continue;
                        }

                        if (oddButtons.Length == 2)
                        {
                            // Captura os valores dos multiplicadores
                            var moreThanMultiplierText = oddButtons[0].QuerySelectorAsync("span").GetAwaiter().GetResult()?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                            var lessThanMultiplierText = oddButtons[1].QuerySelectorAsync("span").GetAwaiter().GetResult()?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                            if (decimal.TryParse(moreThanMultiplierText?.Replace(".", ","), out var moreThanMultiplier))
                            {

                                // Adiciona a aposta "Mais de"
                                currentBets.Add(new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = marketTitle,
                                    OverUnder = "Mais de",
                                    BetAmount = decimal.Parse(numericBetName, CultureInfo.InvariantCulture),
                                    Multiplier = moreThanMultiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = gamesInfo.Site,
                                    TagId = tagId
                                });
                                Console.WriteLine($"Aposta adicionada - Mais de:'{decimal.Parse(numericBetName, CultureInfo.InvariantCulture)}' - ODD: {moreThanMultiplierText}");
                            }
                            else
                            {
                                Console.WriteLine($"Multiplicador 'Mais de' inválido para a aposta '{betName}': {moreThanMultiplierText}");
                            }

                            if (decimal.TryParse(lessThanMultiplierText?.Replace(".", ","), out var lessThanMultiplier))
                            {
                                // Adiciona a aposta "Menos de"
                                currentBets.Add(new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = marketTitle,
                                    OverUnder = "Menos de",
                                    BetAmount = decimal.Parse(numericBetName, CultureInfo.InvariantCulture),
                                    Multiplier = lessThanMultiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = gamesInfo.Site,
                                    TagId = tagId
                                });
                                Console.WriteLine($"Aposta adicionada - Menos de:'{decimal.Parse(numericBetName, CultureInfo.InvariantCulture)}' - ODD: {moreThanMultiplierText}");
                            }
                            else
                            {
                                Console.WriteLine($"Multiplicador 'Menos de' inválido para a aposta '{betName}': {lessThanMultiplierText}");
                            }

                            Console.WriteLine($"Aposta processada: {betName} - Mais de: {moreThanMultiplier}, Menos de: {lessThanMultiplier}");
                        }
                        else if (oddButtons.Length == 1)
                        {
                            var multiplierText = oddButtons[0].QuerySelectorAsync("span").GetAwaiter().GetResult()?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                            if (decimal.TryParse(multiplierText?.Replace(".", ","), out var multiplier) && decimal.Parse(numericBetName, CultureInfo.InvariantCulture) != 0)
                            {
                                currentBets.Add(new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = marketTitle,
                                    OverUnder = "Mais de", // Assume "Mais de" para casos de botão único
                                    BetAmount = decimal.Parse(numericBetName, CultureInfo.InvariantCulture),
                                    Multiplier = multiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = gamesInfo.Site,
                                    TagId = tagId
                                });
                                Console.WriteLine($"Aposta adicionada - Mais de:'{decimal.Parse(numericBetName, CultureInfo.InvariantCulture)}' - ODD: {multiplier}");
                            }
                            else
                            {
                                Console.WriteLine($"Multiplicador inválido para a aposta '{betName}': {multiplierText}");
                            }
                        }

                        

                        

                        
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar uma aposta: {ex.Message}");
                    }
                }

                // Salva as apostas no banco de dados
                if (currentBets.Count > 0)
                {
                    SaveBets(currentBets);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
            }
        }

        private void SaveBets(List<BetInfo> bets)
        {
            if (bets == null || bets.Count == 0)
                return;

            // Cria um HashSet com combinações únicas dos critérios relevantes
            var betKeys = bets
                .Select(bet => new { bet.TagName, bet.OverUnder, bet.TagId, bet.Site, bet.GamesInfo })
                .ToHashSet();

            var betInfoSet = _dbContext.BetInfo ?? throw new InvalidOperationException("DbSet<BetInfo> não configurado no contexto.");

            foreach (var betKey in betKeys)
            {
                if (betKey.GamesInfo == null || betKey.Site == null)
                {
                    Console.WriteLine("Chave de aposta inválida (GamesInfo ou Site nulo). Pulando deduplicação.");
                    continue;
                }

                var existingBets = betInfoSet.Where(b =>
                                    b.GamesInfo.GameId == betKey.GamesInfo.GameId &&
                                    b.TagName == betKey.TagName &&
                                    b.OverUnder == betKey.OverUnder &&
                                    b.TagId == betKey.TagId &&
                                    b.Site.SiteId == betKey.Site.SiteId).ToList();

                if (existingBets.Count > 0)
                {
                    // **Deletar apostas duplicadas que já estão no banco**
                    Console.WriteLine($"Aposta existente encontrada. Removendo a aposta duplicada...");
                    betInfoSet.RemoveRange(existingBets);
                    Console.WriteLine($"Removidas {existingBets.Count} apostas antigas.");
                }
            }

            // Adiciona as novas apostas
            betInfoSet.AddRange(bets);
            _dbContext.SaveChanges();

            Console.WriteLine($"Salvas {bets.Count} novas apostas.");
        }

        [GeneratedRegex(@"\w{3} \w{3} \d{1,2} \d{4} \d{2}:\d{2}:\d{2} GMT[+-]\d{4}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        private static partial Regex FullDateRegex();

        [GeneratedRegex(@"GMT[+-]\d{4}.*", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        private static partial Regex GmtSuffixRegex();

        [GeneratedRegex(@"(Hoje|Amanhã|\d{1,2} de \w{3,}),\s*\d{2}:\d{2}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        private static partial Regex LegacyDateRegex();

        [GeneratedRegex(@"\d+[.,]?\d*", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        private static partial Regex BetNameNumberRegex();
    }
}
