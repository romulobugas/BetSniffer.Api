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

            site = _dbContext.Site.FirstOrDefault(s => string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase))
                ?? AddNewSite(siteName);

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

                var options = page.QuerySelectorAllAsync("div.KambiBC-filter-menu__option").GetAwaiter().GetResult();
                var aToZButtonHandle = options.FirstOrDefault(el =>
                {
                    var text = el.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    return string.Equals(text, "A-Z Futebol", StringComparison.OrdinalIgnoreCase);
                }) ?? throw new Exception("❌ Botão 'A-Z Futebol' não encontrado.");

                page.EvaluateFunctionAsync("el => el.click()", aToZButtonHandle).GetAwaiter().GetResult();
                Thread.Sleep(new Random().Next(4111, 6222));

                var leagueSegments = segments.Take(segments.Length - 2);
                var matchSegment = segments[^2];
                var convertedLeagueSegment = leagueSegments.Last().Replace("-", "_");

                var leagueButton = page.QuerySelectorAllAsync("a[href]").GetAwaiter().GetResult()
                    .FirstOrDefault(el => el.EvaluateFunctionAsync<string>("el => el.getAttribute('href')").Result.Contains(convertedLeagueSegment))
                    ?? throw new Exception($"❌ Liga com slug '{convertedLeagueSegment}' não encontrada.");

                page.EvaluateFunctionAsync("el => el.click()", leagueButton).GetAwaiter().GetResult();
                Thread.Sleep(new Random().Next(9873, 10405));

                var listReady = page.WaitForSelectorAsync("ul.KambiBC-sandwich-filter__list", new WaitForSelectorOptions { Timeout = 10000 })
                    .GetAwaiter().GetResult()
                    ?? throw new Exception("❌ Lista de jogos da liga não carregou.");

                var matchLink = page.QuerySelectorAllAsync("a.KambiBC-sandwich-filter__event-list-info").GetAwaiter().GetResult()
                    .FirstOrDefault(el => el.EvaluateFunctionAsync<string>("el => el.getAttribute('href')").Result.Contains(matchSegment))
                    ?? throw new Exception($"❌ Jogo com slug '{matchSegment}' não encontrado.");

                page.EvaluateFunctionAsync("el => el.click()", matchLink).GetAwaiter().GetResult();

                var gameLoaded = page.WaitForSelectorAsync("div.KambiBC-event-page-component__expandable-container", new WaitForSelectorOptions { Timeout = 10000 })
                    .GetAwaiter().GetResult()
                    ?? throw new Exception("❌ Página do jogo não carregou após o clique.");

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

                var gameLoadedDirect = page.WaitForSelectorAsync("div.KambiBC-event-page-component__expandable-container", new WaitForSelectorOptions { Timeout = 10000 })
                    .GetAwaiter().GetResult()
                    ?? throw new Exception("❌ Página do jogo não carregou nem mesmo acessando o link diretamente.");

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
                if (existingBets.Count > 0)
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

            return [];
        }
        public void ScrapeLeague(string leagueUrl, string siteName)
        {
            ScrapeTags(leagueUrl, siteName);
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
                var headerWrapper = await page.QuerySelectorAsync("div#KambiBC-contentWrapper__top")
                    ?? throw new Exception("Elemento com informações de liga não encontrado.");

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
            // section[...)] usado diretamente abaixo

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
                                await ProcessMarketViews(page, tagNames);

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

        private async Task ProcessMarketViews(IPage page, Dictionary<int, List<string>> tagNames)
        {
            try
            {
                Console.WriteLine("Iniciando processamento dos mercados...");
                await Task.Delay(new Random().Next(851, 1132));

                var marketContainers = await page.QuerySelectorAllAsync("li.KambiBC-bet-offer-subcategory");
                if (marketContainers == null || marketContainers.Length == 0)
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
                        var outcomesList = await marketContainer.QuerySelectorAsync("div.KambiBC-outcomes-list--layout-grouped-betoffers-with-headers")
                            ?? await marketContainer.QuerySelectorAsync("div.KambiBC-outcomes-list--layout-toggler")
                            ?? await marketContainer.QuerySelectorAsync("div.KambiBC-outcomes-list--layout-grid");

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
            if (bets == null || bets.Count == 0)
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
