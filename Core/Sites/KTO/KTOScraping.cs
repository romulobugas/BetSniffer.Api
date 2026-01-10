using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Betano;
using System.Globalization;
using System.Net;

namespace BetSniffer.Api.Core.Sites.KTO
{
    public class KTOScraping : IScrapingService, ILeagueScrapingService
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

        public KTOScraping(
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
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrEmpty(siteName)) throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ?? AddNewSite(siteName);

            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize();
            using var browser = _webScrapingService;

            var page = browser.NavigateTo(url);
            bool fluxoPadraoCompletado = false;

            try
            {
                var uri = new Uri(url);
                var hrefPath = uri.AbsolutePath.TrimEnd('/');
                var segments = hrefPath.Split("/", StringSplitOptions.RemoveEmptyEntries);

                if (segments.Length < 3)
                    throw new Exception("❌ URL inesperada. Não foi possível identificar o caminho da liga.");

                var esporteBase = string.Join("/", segments.Take(2));
                var startUrl = $"https://www.kto.bet.br/{esporteBase}";

                page = browser.NavigateTo(startUrl);
                Thread.Sleep(new Random().Next(9873, 10405));

                var aToZButtonHandle = page.XPathAsync("//div[contains(@class,'KambiBC-filter-menu__option') and normalize-space(text())='A-Z Futebol']")
                                           .GetAwaiter().GetResult()
                                           .FirstOrDefault();

                if (aToZButtonHandle == null)
                    throw new Exception("❌ Botão 'A-Z Futebol' não encontrado.");

                page.EvaluateFunctionAsync("el => el.click()", aToZButtonHandle).GetAwaiter().GetResult();
                Thread.Sleep(new Random().Next(4111, 6222));

                var leagueSegments = segments.Take(segments.Length - 2);
                var matchSegment = segments[^2];
                var convertedLeagueSegment = leagueSegments.Last().Replace("-", "_");

                var leagueButton = page.QuerySelectorAllAsync("a[href]").GetAwaiter().GetResult()
                    .FirstOrDefault(el => el.EvaluateFunctionAsync<string>("el => el.getAttribute('href')").Result.Contains(convertedLeagueSegment));

                if (leagueButton == null) throw new Exception($"❌ Liga com slug '{convertedLeagueSegment}' não encontrada.");

                page.EvaluateFunctionAsync("el => el.click()", leagueButton).GetAwaiter().GetResult();
                Thread.Sleep(new Random().Next(9873, 10405));

                var listReady = page.WaitForSelectorAsync("ul.KambiBC-sandwich-filter__list", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                if (listReady == null) throw new Exception("❌ Lista de jogos da liga não carregou.");

                var matchLink = page.QuerySelectorAllAsync("a.KambiBC-sandwich-filter__event-list-info").GetAwaiter().GetResult()
                    .FirstOrDefault(el => el.EvaluateFunctionAsync<string>("el => el.getAttribute('href')").Result.Contains(matchSegment));

                if (matchLink == null) throw new Exception($"❌ Jogo com slug '{matchSegment}' não encontrado.");

                page.EvaluateFunctionAsync("el => el.click()", matchLink).GetAwaiter().GetResult();

                var gameLoaded = page.WaitForSelectorAsync("div.KambiBC-event-page-component__expandable-container", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                if (gameLoaded == null) throw new Exception("❌ Página do jogo não carregou após o clique.");

                Thread.Sleep(new Random().Next(9873, 10405));

                fluxoPadraoCompletado = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Fluxo padrão falhou: {ex.Message}");
            }

            if (!fluxoPadraoCompletado)
            {
                // Se falhar no fluxo padrão, tenta acessar diretamente a URL do jogo
                Console.WriteLine("🔄 Tentando acesso direto ao link do jogo...");
                page = browser.NavigateTo(url);

                Thread.Sleep(new Random().Next(9873, 10405));

                var gameLoadedDirect = page.WaitForSelectorAsync("div.KambiBC-event-page-component__expandable-container", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                if (gameLoadedDirect == null)
                    throw new Exception("❌ Página do jogo não carregou nem mesmo acessando o link diretamente.");

            }

            ExtractGameInfo(page).GetAwaiter().GetResult();

            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            KTOTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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
                    Console.WriteLine($"Apostas associadas removidas.");
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

                Console.WriteLine($"🆕 Novo jogo adicionado: {gamesInfo.GameName}");
            }

            _dbContext.SaveChanges();
            Console.WriteLine($"✅ Jogo salvo: {gamesInfo.GameName}");

            ProcessTabsAndMarketViews(page).GetAwaiter().GetResult();

            Console.WriteLine("🏁 Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return new List<TagInfo>();
        }

        //public void ClosePopup(IPage page, string popupSelector, int timeoutMilliseconds = 10000)
        //{
        //    try
        //    {
        //        System.Threading.Thread.Sleep(new Random().Next(1855, 3626)); // Espera aleatória

        //        var element = page.WaitForSelectorAsync(popupSelector, new WaitForSelectorOptions
        //        {
        //            Timeout = timeoutMilliseconds
        //        }).GetAwaiter().GetResult();

        //        if (element != null)
        //        {
        //            element.ClickAsync().GetAwaiter().GetResult();
        //            Console.WriteLine("Pop-up fechado com sucesso.");
        //        }
        //        else
        //        {
        //            Console.WriteLine("Pop-up não encontrado.");
        //        }
        //    }
        //    catch (TimeoutException)
        //    {
        //        Console.WriteLine("Tempo de espera para fechar o pop-up expirou.");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Erro ao fechar o pop-up: {ex.Message}");
        //    }
        //}

        //public void ConfirmAgeVerification(IPage page, string ageVerificationSelector, int timeoutMilliseconds = 10000)
        //{
        //    try
        //    {
        //        System.Threading.Thread.Sleep(new Random().Next(1511, 3522)); // Espera aleatória

        //        var element = page.WaitForSelectorAsync(ageVerificationSelector, new WaitForSelectorOptions
        //        {
        //            Timeout = timeoutMilliseconds
        //        }).GetAwaiter().GetResult();

        //        if (element != null)
        //        {
        //            element.ClickAsync().GetAwaiter().GetResult();
        //            Console.WriteLine("Botão 'Sim' clicado com sucesso.");
        //        }
        //        else
        //        {
        //            Console.WriteLine("Botão 'Sim' não encontrado.");
        //        }
        //    }
        //    catch (TimeoutException)
        //    {
        //        Console.WriteLine("Tempo de espera para o botão 'Sim' expirou.");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Erro ao confirmar verificação de idade: {ex.Message}");
        //    }
        //}

        public void ScrapeLeague(string leagueUrl, string siteName)
        {
            if (string.IsNullOrEmpty(leagueUrl) || string.IsNullOrEmpty(siteName))
                throw new ArgumentException("Parâmetros inválidos.");

            var site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ?? AddNewSite(siteName);

            using var browser = new WebScrapingServicePuppeteer();
            browser.Initialize();

            bool success = false;

            try
            {
                // 🧠 Extrai os caminhos da URL
                var uri = new Uri(leagueUrl);
                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length < 3) throw new Exception("❌ URL inválida para liga.");

                var esporteBase = string.Join('/', segments.Take(2));
                var startUrl = $"https://www.kto.bet.br/{esporteBase}";

                var page = browser.NavigateTo(startUrl);
                Thread.Sleep(new Random().Next(9873, 10405));

                // 🖱️ Clica no botão "A-Z Futebol"
                var aToZButtonHandle = page.XPathAsync("//div[contains(@class,'KambiBC-filter-menu__option') and normalize-space(text())='A-Z Futebol']")
                    .GetAwaiter().GetResult().FirstOrDefault();

                if (aToZButtonHandle == null)
                    throw new Exception("❌ Botão 'A-Z Futebol' não encontrado.");

                page.EvaluateFunctionAsync("el => el.click()", aToZButtonHandle).GetAwaiter().GetResult();
                Thread.Sleep(new Random().Next(4111, 6222));

                // 🎯 Liga
                var matchSlug = segments.Last();
                var convertedLeagueSlug = matchSlug.Replace("-", "_");

                var leagueButton = page.WaitForSelectorAsync($"a[href*='{convertedLeagueSlug}']", new WaitForSelectorOptions { Timeout = 10000 })
                    .GetAwaiter().GetResult();

                if (leagueButton == null)
                    throw new Exception($"❌ Liga com href '{convertedLeagueSlug}' não encontrada.");

                page.EvaluateFunctionAsync("el => el.click()", leagueButton).GetAwaiter().GetResult();
                Thread.Sleep(new Random().Next(4111, 6222));

                // ✅ Aguarda a lista de jogos
                var listReady = page.WaitForSelectorAsync("ul.KambiBC-sandwich-filter__list", new WaitForSelectorOptions { Timeout = 10000 })
                    .GetAwaiter().GetResult();

                if (listReady == null)
                    throw new Exception("❌ Lista de jogos da liga não carregou.");

                // Se chegou até aqui, marca sucesso
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Navegação inicial falhou: {ex.Message}");
            }

            try
            {
                if (!success)
                {
                    Console.WriteLine("🔁 Tentando abrir o link da liga diretamente...");

                    var page = browser.NavigateTo(leagueUrl);
                    Thread.Sleep(new Random().Next(4877, 6222));

                    // Pequena validação para garantir que estamos na página certa
                    var confirmation = page.WaitForSelectorAsync("ul.KambiBC-sandwich-filter__list", new WaitForSelectorOptions { Timeout = 10000 })
                        .GetAwaiter().GetResult();

                    if (confirmation == null)
                        throw new Exception("❌ Página da liga direta também não carregou a lista de jogos.");
                }

                var page2 = browser.GetPage(); // Usa a mesma página que já estava aberta
                var leagueNode = page2.QuerySelectorAsync("span.KambiBC-sandwich-filter__group-header-title a:last-of-type")
                    .GetAwaiter().GetResult();
                var leagueName = leagueNode?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult()
                                 ?? "Liga Desconhecida";

                Console.WriteLine($"📌 Liga: {leagueName}");

                var matchNodes = page2.QuerySelectorAllAsync("li.KambiBC-sandwich-filter__event-list-item")
                    .GetAwaiter().GetResult();

                foreach (var node in matchNodes)
                {
                    try
                    {
                        var clockNode = node.QuerySelectorAsync("div.KambiBC-match-clock__inner span:nth-child(2)")
                            .GetAwaiter().GetResult();
                        if (clockNode != null)
                        {
                            var clockText = clockNode.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                            if (!string.IsNullOrWhiteSpace(clockText) && Regex.IsMatch(clockText, @"^\d+[:.]\d+$"))
                            {
                                Console.WriteLine("⏱️ Jogo ao vivo ignorado.");
                                continue;
                            }
                        }

                        var teamNodes = node.QuerySelectorAllAsync("div.KambiBC-event-participants__name-participant-name")
                            .GetAwaiter().GetResult();
                        if (teamNodes.Length < 2) continue;

                        var home = teamNodes[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                        var away = teamNodes[1].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                        var timeText = node.QuerySelectorAsync("span.KambiBC-event-item__start-time--time")
                            ?.GetAwaiter().GetResult()
                            ?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                        var dateText = node.QuerySelectorAsync("span.KambiBC-event-item__start-time--date")
                            ?.GetAwaiter().GetResult()
                            ?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                        if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away) ||
                            string.IsNullOrWhiteSpace(timeText) || string.IsNullOrWhiteSpace(dateText))
                            continue;

                        var baseDate = ParseKtoDate(dateText);
                        if (!TimeSpan.TryParse(timeText, out var time))
                        {
                            Console.WriteLine($"❌ Horário inválido: {timeText}");
                            continue;
                        }

                        var gameDate = baseDate.Add(time);

                        var href = node.QuerySelectorAsync("a.KambiBC-sandwich-filter__event-list-info")
                            ?.GetAwaiter().GetResult()
                            ?.EvaluateFunctionAsync<string>("el => el.getAttribute('href')").GetAwaiter().GetResult();

                        if (string.IsNullOrWhiteSpace(href)) continue;
                        var fullUrl = href.StartsWith("http") ? href : $"https://www.kto.bet.br{href}";

                        var homeId = _teamService.EnsureTeamExists(home);
                        var awayId = _teamService.EnsureTeamExists(away);

                        var existing = _dbContext.GamesInfo.FirstOrDefault(g =>
                            g.HomeTeamId == homeId &&
                            g.AwayTeamId == awayId &&
                            g.GameDate == gameDate &&
                            g.Site.SiteId == site.SiteId);

                        if (existing != null)
                        {
                            existing.Status = 1;
                            existing.LastUpdated = DateTime.Now;
                            existing.URL = fullUrl;
                            existing.League = leagueName;
                            Console.WriteLine($"🔄 Jogo atualizado: {home} vs {away}");
                        }
                        else
                        {
                            var game = new GamesInfo
                            {
                                HomeTeamId = homeId,
                                AwayTeamId = awayId,
                                GameDate = gameDate,
                                League = leagueName,
                                Site = site,
                                URL = fullUrl,
                                Status = 1,
                                LastUpdated = DateTime.Now,
                                GameName = _teamService.NormalizeText(home) + " - " + _teamService.NormalizeText(away)
                            };
                            _dbContext.GamesInfo.Add(game);
                            Console.WriteLine($"🆕 Novo jogo salvo: {home} vs {away}");
                        }

                        _dbContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Erro ao processar jogo: {ex.Message}");
                        _logService.LogError("Erro ao processar jogo da liga KTO", ex);
                    }
                }
            }
            finally
            {
                browser.Dispose();
            }
        }

        public static DateTime ParseKtoDate(string dateText)
        {
            var meses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "jan.", 1 }, { "fev.", 2 }, { "mar.", 3 }, { "abr.", 4 },
                { "mai.", 5 }, { "jun.", 6 }, { "jul.", 7 }, { "ago.", 8 },
                { "set.", 9 }, { "out.", 10 }, { "nov.", 11 }, { "dez.", 12 }
            };

            var diasSemana = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
            {
                { "dom.", DayOfWeek.Sunday },
                { "seg.", DayOfWeek.Monday },
                { "ter.", DayOfWeek.Tuesday },
                { "qua.", DayOfWeek.Wednesday },
                { "qui.", DayOfWeek.Thursday },
                { "sex.", DayOfWeek.Friday },
                { "sáb.", DayOfWeek.Saturday },
                { "sab.", DayOfWeek.Saturday } // fallback sem acento
            };

            dateText = dateText.ToLower().Trim();

            // ✅ Caso 1: Dia da semana (ex: "sex.")
            if (diasSemana.TryGetValue(dateText, out DayOfWeek diaSemana))
            {
                var hoje = DateTime.Today;
                int diasParaAdicionar = ((int)diaSemana - (int)hoje.DayOfWeek + 7) % 7;

                return hoje.AddDays(diasParaAdicionar);
            }

            // ✅ Caso 2: Formato "14 de abr."
            var partes = dateText.Replace("º", "").Split(" de ");
            if (partes.Length == 2)
            {
                if (int.TryParse(partes[0].Trim(), out int dia))
                {
                    string mesAbrev = partes[1].Trim().TrimEnd('.');
                    if (!meses.TryGetValue(mesAbrev + ".", out int mes))
                        throw new FormatException("Mês não reconhecido");

                    var hoje = DateTime.Today;
                    var data = new DateTime(hoje.Year, mes, dia);

                    // Garante que a data não está no passado (ex: 31 dez. quando hoje é jan.)
                    if (data < hoje.AddDays(-1))
                        data = data.AddYears(1);

                    return data;
                }
            }

            throw new FormatException("Formato de data inválido");
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

                // 🎯 Tenta capturar elemento raiz pela estrutura principal (preferencial)
                var rootElement = await page.QuerySelectorAsync("div.KambiBC-event-page-component__expandable-container");

                // 🔁 Fallback para estrutura alternativa se não encontrar a principal
                if (rootElement == null)
                {
                    //🕒 Aguarda um tempo antes de tentar novamente.
                    await Task.Delay(new Random().Next(2423, 4687));

                    rootElement = await page.QuerySelectorAsync("div.KambiBC-scoreboard-container-prematch_1_6");
                }

                if (rootElement == null)
                    throw new Exception("Elemento raiz do jogo não encontrado.");

                // 🕒 Data e Hora do jogo
                var timeElement = await rootElement.QuerySelectorAsync("time.KambiBC-scoreboard-align-right");
                var datetimeAttr = timeElement != null
                    ? await timeElement.EvaluateFunctionAsync<string>("el => el.getAttribute('datetime')")
                    : null;

                if (string.IsNullOrWhiteSpace(datetimeAttr) || !long.TryParse(datetimeAttr, out var timestampMs))
                    throw new Exception("Timestamp da data e hora do jogo inválido.");

                gameDateTime = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).LocalDateTime;
                Console.WriteLine($"✅ Data e Hora do jogo: {gameDateTime}");

                // 🎯 Times
                var teamLabelElements = await rootElement.QuerySelectorAllAsync("div.KambiBC-scoreboard-team-label-container label");
                if (teamLabelElements.Length != 2)
                    throw new Exception("Não foi possível capturar os nomes dos dois times.");

                homeTeam = await teamLabelElements[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                awayTeam = await teamLabelElements[1].EvaluateFunctionAsync<string>("el => el.textContent.trim()");

                if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
                    throw new Exception("Nomes dos times estão vazios.");

                Console.WriteLine($"✅ Times identificados: {homeTeam} vs {awayTeam}");

                // 🌍 Liga — Captura baseada em posição na estrutura
                var headerWrapper = await page.QuerySelectorAsync("div#KambiBC-contentWrapper__top");
                if (headerWrapper == null)
                    throw new Exception("Elemento com informações de liga não encontrado.");

                var h1Elements = await headerWrapper.QuerySelectorAllAsync("h1");
                if (h1Elements.Length == 0)
                    throw new Exception("Elemento <h1> com nome da liga não encontrado.");

                leagueName = await h1Elements[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                if (string.IsNullOrWhiteSpace(leagueName))
                    throw new Exception("Nome da liga está vazio.");

                Console.WriteLine($"✅ Liga identificada: {leagueName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao extrair informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao extrair informações do jogo da KTO", ex);
                throw; // mantém a falha propagada
            }
        }

        private async Task ProcessTabsAndMarketViews(IPage page)
        {
            var ignoredTabs = new HashSet<string> { "Criar aposta", "Buscar" };
            var processedTabs = new HashSet<string>();

            var tabSelector = "ul.KambiBC-filter-menu > li > div[data-touch-feedback='true']";
            var arrowRightSelector = "button.KambiBC-scroller__arrow-right";
            var marketContainerSelector = "section[data-testid='market-outcome-list']";

            await Task.Delay(new Random().Next(825, 1684));
            var tagNames = KTOTags.TagNames;

            try
            {
                // 1. Encontrar TODAS as seções de filtros
                var allSections = await page.QuerySelectorAllAsync("section.KambiBC-sandwich-filter");
                IElementHandle correctSection = null;

                foreach (var section in allSections)
                {
                    var hasLevel1 = await section.QuerySelectorAsync("div.KambiBC-sandwich-filter-foreground--level-1");
                    if (hasLevel1 != null)
                    {
                        correctSection = section;
                        break;
                    }
                }

                if (correctSection == null)
                {
                    Console.WriteLine("❌ Não foi encontrada a linha de abas com foreground--level-1.");
                    return;
                }

                bool finished = false;

                while (!finished)
                {
                    try
                    {
                        var tabs = await correctSection.QuerySelectorAllAsync(tabSelector);
                        if (tabs == null || tabs.Length == 0)
                        {
                            Console.WriteLine("❌ Nenhuma aba encontrada na linha correta.");
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

                                if (string.IsNullOrWhiteSpace(tabName))
                                {
                                    Console.WriteLine("⚠️ Nome da aba não pôde ser capturado.");
                                    continue;
                                }

                                if (ignoredTabs.Contains(tabName) || processedTabs.Contains(tabName))
                                {
                                    Console.WriteLine($"🔁 Ignorando aba: {tabName}");
                                    continue;
                                }

                                Console.WriteLine($"➡️ Processando aba: {tabName}");

                                int retries = 0;
                                bool clicked = false;

                                while (!clicked && retries < 3)
                                {
                                    try
                                    {
                                        await tab.EvaluateFunctionAsync("el => el.click()");
                                        await Task.Delay(new Random().Next(621, 884));
                                        clicked = true;
                                        Console.WriteLine($"✅ Aba '{tabName}' clicada com sucesso.");
                                    }
                                    catch
                                    {
                                        retries++;
                                        Console.WriteLine($"⚠️ Erro ao clicar na aba '{tabName}', tentativa {retries}.");
                                    }
                                }

                                if (!clicked)
                                {
                                    var rightArrow = await correctSection.QuerySelectorAsync(arrowRightSelector);
                                    if (rightArrow != null)
                                    {
                                        Console.WriteLine($"➡️ Tentando avançar com a seta para direita: '{tabName}'");
                                        await rightArrow.ClickAsync();
                                        await Task.Delay(new Random().Next(851, 1132));
                                        allTabsProcessed = false;
                                        continue;
                                    }
                                    else
                                    {
                                        Console.WriteLine("⚠️ Seta para direita não encontrada.");
                                        continue;
                                    }
                                }

                                // Aguarda o carregamento da estrutura principal de mercados
                                var marketsContainer = await page.WaitForSelectorAsync("div.KambiBC-betoffer-categories-view.KambiBC-bet-offer-categories", new WaitForSelectorOptions
                                {
                                    Timeout = 10000
                                });

                                if (marketsContainer == null)
                                {
                                    Console.WriteLine("⚠️ Estrutura de mercados não carregou após o clique na aba.");
                                    continue;
                                }

                                // Marca aba como processada somente após o carregamento dos mercados
                                processedTabs.Add(tabName);

                                // Processa os mercados normalmente
                                await ProcessMarketViews(page, tagNames, tabName);

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"❌ Erro ao processar aba: {ex.Message}");
                                allTabsProcessed = false;
                            }
                        }

                        finished = allTabsProcessed;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro ao processar abas secundárias: {ex.Message}");
                        finished = true;
                    }
                }

                Console.WriteLine("✅ Todas as abas secundárias foram processadas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro geral ao processar abas: {ex.Message}");
            }
        }

        private async Task ProcessMarketViews(IPage page, Dictionary<int, List<string>> tagNames, string tabName = null)
        {
            try
            {
                Console.WriteLine("Iniciando processamento dos mercados...");
                await Task.Delay(new Random().Next(851, 1132));

                var marketContainers = await page.QuerySelectorAllAsync("li.KambiBC-bet-offer-subcategory");
                if (marketContainers == null || !marketContainers.Any())
                {
                    Console.WriteLine("Nenhum mercado encontrado.");
                    return;
                }

                foreach (var marketContainer in marketContainers)
                {
                    try
                    {
                        // 👉 Captura título do mercado
                        var titleElement = await marketContainer.QuerySelectorAsync("h3.KambiBC-bet-offer-subcategory__label span");
                        if (titleElement == null) continue;

                        var marketTitleRaw = await titleElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                        var marketTitle = _teamService.NormalizeText(marketTitleRaw);

                        if (!tagNames.Values.Any(list => list.Contains(marketTitle)))
                        {
                            Console.WriteLine($"🔕 Mercado ignorado: {marketTitle}");
                            continue;
                        }

                        var matchingTag = tagNames
                            .FirstOrDefault(tag => tag.Value.Any(tagValue => _teamService.NormalizeText(tagValue) == marketTitle));

                        if (matchingTag.Key == 0)
                        {
                            Console.WriteLine($"⚠️ Tag não encontrada para: {marketTitle}");
                            continue;
                        }

                        int tagId = matchingTag.Key;

                        // 👉 Tenta expandir o mercado se necessário
                        var showListButton = await marketContainer.QuerySelectorAsync("button.KambiBC-outcomes-list__toggler-toggle-button.down");
                        if (showListButton != null)
                        {
                            await showListButton.EvaluateFunctionAsync("el => el.focus()");
                            await Task.Delay(new Random().Next(525, 825)); // ⏳ Aguarda um tempo antes do clique
                            await showListButton.EvaluateFunctionAsync("el => el.click()"); // 👆 Clica via JS

                            try
                            {
                                await marketContainer.WaitForSelectorAsync("div.KambiBC-outcomes-list--layout-grouped-betoffers-with-headers", new WaitForSelectorOptions
                                {
                                    Timeout = 5000
                                });
                            }
                            catch (WaitTaskTimeoutException)
                            {
                                Console.WriteLine("⚠️ Layout expandido não carregou após clicar em 'Mostrar lista'. Continuando mesmo assim.");
                            }
                        }


                        // 👉 Tenta encontrar estrutura expandida OU padrão
                        var outcomesList = await marketContainer.QuerySelectorAsync("div.KambiBC-outcomes-list--layout-grouped-betoffers-with-headers") ??
                                           await marketContainer.QuerySelectorAsync("div.KambiBC-outcomes-list--layout-toggler") ??
                                           await marketContainer.QuerySelectorAsync("div.KambiBC-outcomes-list--layout-grid");

                        if (outcomesList == null)
                        {
                            Console.WriteLine("❌ Nenhum layout de odds reconhecido.");
                            continue;
                        }

                        var currentBets = new List<BetInfo>();

                        // 👉 Lê odds via grid expandido (colunas)
                        var columns = await outcomesList.QuerySelectorAllAsync("div.KambiBC-outcomes-list__column");
                        foreach (var column in columns)
                        {
                            var buttons = await column.QuerySelectorAllAsync("button");
                            foreach (var button in buttons)
                            {
                                try
                                {
                                    var betTexts = await button.QuerySelectorAllAsync("div.sc-fqkvVR");
                                    var valueTexts = await button.QuerySelectorAllAsync("div.sc-dcJsrY");
                                    var multiplierSpan = await button.QuerySelectorAsync("div.sc-kAyceB");

                                    if (betTexts.Length == 0 || valueTexts.Length == 0 || multiplierSpan == null)
                                        continue;

                                    var overUnderText = await betTexts[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                                    var valueText = await valueTexts[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                                    var multiplierText = await multiplierSpan.EvaluateFunctionAsync<string>("el => el.textContent.trim()");

                                    string overUnder = overUnderText.Contains("Mais") ? "Mais de" :
                                                       overUnderText.Contains("Menos") ? "Menos de" : null;

                                    if (string.IsNullOrEmpty(overUnder) || !decimal.TryParse(multiplierText.Replace(".", ","), out var multiplier))
                                        continue;

                                    if (!decimal.TryParse(valueText.Replace(".", ","), out var betAmount))
                                    {
                                        Console.WriteLine($"Valor da linha inválido: {valueText}");
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
                                catch (Exception exButton)
                                {
                                    Console.WriteLine($"Erro ao processar botão de aposta: {exButton.Message}");
                                    _logService.LogError("Erro ao processar botão de aposta", exButton);
                                }
                            }
                        }

                        SaveBets(currentBets);
                    }
                    catch (Exception exMarket)
                    {
                        Console.WriteLine($"Erro ao processar mercado: {exMarket.Message}");
                        _logService.LogError("Erro ao processar mercado", exMarket);
                    }
                }

                Console.WriteLine("✅ Processamento dos mercados finalizado.");
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
