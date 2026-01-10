using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Betano;
using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace BetSniffer.Api.Core.Sites.Vbet
{
    public class VbetScraping : IScrapingService, ILeagueScrapingService
    {
        #region VariaveisGlobais

        private readonly WebScrapingServiceSelenium _webScrapingService;
        private string gameName;
        private string gameDayText;
        private string gameHourText;
        private string homeTeam;
        private string awayTeam;
        private DateTime gameDateTime;
        private Site site;
        private GamesInfo gamesInfo;

        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private readonly ILogService _logService;

        #endregion

        public VbetScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _webScrapingService = new WebScrapingServiceSelenium(); // Inicializa o serviço de scraping
            _webScrapingService.Initialize(); // Configura o WebDriver

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

            // Verifica se o site já existe no banco
            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ?? AddNewSite(siteName);

            _webScrapingService.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(7873, 9405));

            // Aguarda o carregamento inicial da página
            _webScrapingService.WaitForElement("div.game-details-section");

            var _driver = _webScrapingService.GetWebDriver();

            CloseAgeVerificationPopup(_driver);

            // Coleta informações do jogo
            ExtractGameInfo();

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            VbetTags.AddDynamicTags(homeTeam, awayTeam);

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
                    League = gameName,
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

            // Processa todas as abas disponíveis
            ProcessTabsAndMarketViews();

            _webScrapingService.Dispose();

            return new List<TagInfo>(); // Substitua com a lógica para retornar as informações processadas
        }

        public void ScrapeLeague(string url, string siteName)
        {
            // Validações básicas
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL inválida.");
            if (string.IsNullOrWhiteSpace(siteName)) throw new ArgumentException("Nome do site inválido.");

            // Recupera ou cria o site
            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ?? AddNewSite(siteName);

            // Navega para a página da liga
            _webScrapingService.NavigateTo(url);
            System.Threading.Thread.Sleep(new Random().Next(1985, 3312));

            var driver = _webScrapingService.GetWebDriver();

            // Aguarda o carregamento inicial da página
            _webScrapingService.WaitForElement("div.game-details-section");

            CloseAgeVerificationPopup(driver);

            var leagueNameElement = driver.FindElement(By.CssSelector(".comp-title-w-bc"));
            string league = leagueNameElement?.Text.Trim() ?? "Liga não identificada";

            // Captura todos os blocos de jogos do dia
            var dayBlocks = driver.FindElements(By.CssSelector("div.competition-bc"));
            foreach (var dayBlock in dayBlocks)
            {
                try
                {
                    // Extrai a data (ex: 12.04.2025)
                    string dateRaw = dayBlock.FindElement(By.CssSelector("time.c-title-bc")).Text.Trim();
                    var gameDateOnly = DateTime.ParseExact(dateRaw, "dd.MM.yyyy", CultureInfo.InvariantCulture);

                    // Todos os jogos desse dia
                    var matches = dayBlock.FindElements(By.CssSelector("ul.multi-column-content"));
                    foreach (var match in matches)
                    {
                        try
                        {
                            var teams = match.FindElements(By.CssSelector("div.multi-column-teams p.ellipsis"));
                            if (teams.Count < 2) continue;

                            var home = teams[0].Text.Trim();
                            var away = teams[1].Text.Trim();

                            var timeRaw = match.FindElement(By.CssSelector("div.multi-column-time-icon time")).Text.Trim();
                            DateTime gameDateTime = DateTime.ParseExact($"{dateRaw} {timeRaw}", "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

                            var homeId = _teamService.EnsureTeamExists(home);
                            var awayId = _teamService.EnsureTeamExists(away);

                            // Clica no jogo para ativar e obter a URL
                            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", match);
                            match.Click();
                            Thread.Sleep(new Random().Next(1100, 1800));

                            string currentUrl = driver.Url;

                            var existing = _dbContext.GamesInfo.FirstOrDefault(g =>
                                g.HomeTeamId == homeId &&
                                g.AwayTeamId == awayId &&
                                g.GameDate == gameDateTime &&
                                g.Site.SiteId == site.SiteId);

                            if (existing != null)
                            {
                                existing.Status = 1;
                                existing.LastUpdated = DateTime.Now;
                                existing.URL = currentUrl;
                                existing.League = league;
                                Console.WriteLine($"🔄 Jogo atualizado: {home} vs {away}");
                            }
                            else
                            {
                                var game = new GamesInfo
                                {
                                    HomeTeamId = homeId,
                                    AwayTeamId = awayId,
                                    GameDate = gameDateTime,
                                    League = league,
                                    Site = site,
                                    URL = currentUrl,
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
                            _logService.LogError("Erro ao salvar jogo VBet", ex);
                        }

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar bloco de data: {ex.Message}");
                }
            }

            _webScrapingService.Dispose();
        }

        private Site AddNewSite(string siteName)
        {
            var site = new Site { Name = siteName };
            _dbContext.Site.Add(site);
            _dbContext.SaveChanges();
            Console.WriteLine($"Novo site adicionado: {siteName}");
            return site;
        }

        public void CloseAgeVerificationPopup(IWebDriver _driver)
        {
            try
            {
                // Localiza todos os elementos com a classe "popup-holder-bc"
                var popupHolders = _driver.FindElements(By.ClassName("popup-holder-bc"));

                // Filtra apenas os popups visíveis
                var visiblePopup = popupHolders.FirstOrDefault(popup =>
                    popup.Displayed && !popup.GetAttribute("class").Contains("hidden"));

                if (visiblePopup == null)
                {
                    Console.WriteLine("Nenhum popup visível encontrado.");
                    return; // Sai da função se nenhum popup visível for encontrado
                }

                // Procura o botão "Tenho 18 anos ou mais"
                var confirmButton = visiblePopup.FindElement(By.XPath(".//button[contains(text(),'Tenho 18 anos ou mais')]"));

                if (confirmButton == null)
                {
                    Console.WriteLine("Botão 'Tenho 18 anos ou mais' não encontrado no popup visível.");
                    return; // Sai da função se o botão não for encontrado
                }

                // Clica no botão "Tenho 18 anos ou mais"
                confirmButton.Click();
                Console.WriteLine("Botão 'Tenho 18 anos ou mais' clicado com sucesso.");
                Thread.Sleep(new Random().Next(1521, 1802)); // Pausa após clicar no botão
            }
            catch (NoSuchElementException ex)
            {
                Console.WriteLine($"Elemento não encontrado: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tentar fechar o popup de verificação de idade: {ex.Message}");
            }
        }

        private void ExtractGameInfo()
        {
            try
            {
                // Pausa aleatória para simular comportamento humano
                System.Threading.Thread.Sleep(new Random().Next(551, 1524));

                // Captura o contêiner principal fixo
                var matchInfoElement = _webScrapingService.WaitForElement("div.game-details-container-bc");
                if (matchInfoElement == null) 
                {
                    _webScrapingService.Dispose();
                    throw new Exception("O contêiner principal 'game-details-container-bc' não foi encontrado.");
                }                    

                // Captura o nome da liga (gameName) pelo primeiro elemento <span> encontrado no título
                var leagueElement = _webScrapingService.FindElementWithin(matchInfoElement, ".//div[1]/div[1]/span");
                gameName = leagueElement?.Text.Trim() ?? "Liga não encontrada";

                // Captura a data e hora do jogo pelo <time>
                var dateTimeElement = _webScrapingService.FindElementWithin(matchInfoElement, ".//div[1]/div[1]/div/p/time");
                var dateTimeText = dateTimeElement?.Text.Trim();
                if (string.IsNullOrWhiteSpace(dateTimeText)) 
                {
                    _webScrapingService.Dispose();
                    throw new Exception("Data e hora do jogo não foram encontradas.");
                }                    

                gameDateTime = ParseCustomDateTime(dateTimeText);
                Console.WriteLine($"Data e Hora do Jogo: {gameDateTime}");

                // Captura os times baseando-se na ordem estrutural
                var teamContainers = _webScrapingService.FindElementsWithin(matchInfoElement, ".//div//p").ToList();
                if (teamContainers != null && teamContainers.Count >= 3)
                {
                    // Verifica se o primeiro elemento corresponde à data e hora
                    if (teamContainers[0].Text.Trim() == dateTimeText)
                    {
                        homeTeam = teamContainers[1].Text.Trim();
                        awayTeam = teamContainers[2].Text.Trim();
                    }
                    else
                    {
                        _webScrapingService.Dispose();
                        throw new Exception("A estrutura esperada para os times não corresponde.");
                    }

                    Console.WriteLine($"Time Casa: {homeTeam}, Time Visitante: {awayTeam}");
                }
                else
                {
                    _webScrapingService.Dispose();
                    throw new Exception("Não foi possível encontrar os dois times.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar informações do jogo: {ex.Message}");
            }
        }

        private DateTime ParseCustomDateTime(string dateTimeText)
        {
            try
            {
                // Define o formato esperado: "21.01.2025, 14:45"
                const string format = "dd.MM.yyyy, HH:mm";

                // Tenta parsear a string usando o formato esperado
                if (DateTime.TryParseExact(dateTimeText, format,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var parsedDateTime))
                {
                    return parsedDateTime;
                }

                throw new Exception($"Formato inesperado para 'dateTimeText': {dateTimeText}");
            }
            catch (Exception ex)
            {
                _webScrapingService.Dispose();
                throw new Exception($"Erro ao converter a data: {dateTimeText} - {ex.Message}");
            }
        }

        private void ProcessTabsAndMarketViews()
        {
            try
            {
                // Localiza o contêiner principal da seção "game-details-section"
                var gameDetailsSection = _webScrapingService.WaitForElement("//div[contains(@class,'game-details-section')]", 10000);

                if (gameDetailsSection == null)
                {
                    Console.WriteLine("Contêiner 'game-details-section' não encontrado.");
                    return;
                }

                // Localiza o <div tabindex> dentro do "game-details-section"
                var tabsContainer = _webScrapingService.FindElementWithin(gameDetailsSection, ".//div[@tabindex='0']", 5000);

                if (tabsContainer == null)
                {
                    Console.WriteLine("Contêiner de abas com tabindex='0' não encontrado.");
                    return;
                }

                // Captura todos os elementos de abas dentro do tabsContainer
                var tabElements = _webScrapingService.FindElementsWithin(tabsContainer, ".//div[contains(@class, 'selected-underline')]").ToList();

                if (!tabElements.Any())
                {
                    Console.WriteLine("Nenhum botão de aba encontrado.");
                    return;
                }

                // Procura o botão com o texto "Tudo"
                IWebElement allMarketsButton = null;
                foreach (var tab in tabElements)
                {
                    var tabText = tab.Text.Trim();
                    if (tabText.Equals("Tudo", StringComparison.OrdinalIgnoreCase))
                    {
                        allMarketsButton = tab;
                        break;
                    }
                }

                if (allMarketsButton == null)
                {
                    Console.WriteLine("Botão 'Tudo' não encontrado.");
                    return;
                }

                // Tenta clicar no botão "Tudo"
                int retries = 0;
                bool clicked = false;
                while (!clicked && retries < 5)
                {
                    try
                    {
                        allMarketsButton.Click();
                        Console.WriteLine("Botão 'Tudo' clicado com sucesso.");
                        clicked = true;
                    }
                    catch (ElementClickInterceptedException)
                    {
                        retries++;
                        Console.WriteLine($"Clique interceptado no botão 'Tudo', tentando novamente ({retries}/5).");
                        Thread.Sleep(new Random().Next(531, 1254)); // Pausa antes de tentar novamente
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao tentar clicar no botão 'Tudo': {ex.Message}");
                        break;
                    }
                }

                if (!clicked)
                {
                    Console.WriteLine("Falha ao clicar no botão 'Tudo' após 5 tentativas.");
                    return;
                }

                // Aguarda que os mercados sejam carregados
                Thread.Sleep(new Random().Next(621, 1126));

                // Processa os mercados visíveis após clicar no botão "Tudo"
                ProcessMarketViews();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro geral ao processar aba 'Tudo': {ex.Message}");
            }
        }

        private void ProcessMarketViews()
        {
            // Contêiner principal onde os mercados estão localizados
            var marketContainer = _webScrapingService.WaitForElement("div.game-details-section");

            if (marketContainer == null)
            {
                Console.WriteLine("O contêiner de mercados não foi encontrado.");
                return;
            }

            // Configuração inicial para rolagem e controle de elementos processados
            int retries = 0;
            int lastElementCount = 0;
            HashSet<string> processedIndexes = new HashSet<string>();

            while (retries < 3)
            {
                try
                {
                    // Captura todos os mercados visíveis no momento
                    var marketElements = _webScrapingService.FindElementsWithin(marketContainer, ".//div[@data-index]").ToList();

                    if (marketElements.Count == 0)
                    {
                        Console.WriteLine("Nenhum mercado encontrado no contêiner atual.");
                        break;
                    }

                    // Processa os elementos de mercado não processados
                    foreach (var marketElement in marketElements)
                    {
                        var indexAttribute = marketElement.GetAttribute("data-index");

                        if (string.IsNullOrEmpty(indexAttribute) || processedIndexes.Contains(indexAttribute))
                        {
                            continue;
                        }

                        Console.WriteLine($"Processando mercado com data-index: {indexAttribute}");
                        processedIndexes.Add(indexAttribute);

                        // Aqui você pode implementar a lógica para capturar as informações do mercado
                        ProcessMarketElement(marketElement);
                    }

                    // Verifica se novos elementos foram encontrados desde a última iteração
                    if (processedIndexes.Count > lastElementCount)
                    {
                        lastElementCount = processedIndexes.Count;
                        retries = 0; // Reseta o contador de tentativas

                        // Simula a rolagem para baixo no contêiner principal
                        marketContainer.SendKeys(Keys.PageDown);
                        Thread.Sleep(new Random().Next(1031, 1244));
                    }
                    else
                    {
                        retries++;
                        Console.WriteLine($"Nenhum novo mercado encontrado. Tentativa {retries}/3 para rolar.");

                        // Realiza mais uma rolagem antes de desistir
                        marketContainer.SendKeys(Keys.PageDown);
                        Thread.Sleep(new Random().Next(1121, 1725));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar os mercados: {ex.Message}");
                    break;
                }
            }

            Console.WriteLine("Processamento de mercados concluído.");
        }

        private void ProcessMarketElement(IWebElement marketElement)
        {
            try
            {
                // Captura o título do mercado
                var marketTitleElement = _webScrapingService.FindElementWithin(marketElement, ".//p[contains(@class, '-title-')]");
                string marketTitle = marketTitleElement?.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(marketTitle) || marketTitle == "***")
                {
                    Console.WriteLine("❌ Título do mercado inválido. Pulando...");
                    return;
                }

                Console.WriteLine($"🔎 Processando mercado: {marketTitle}");

                // Verifica se o mercado é relevante
                var tagNames = VbetTags.TagNames;
                if (!tagNames.Values.Any(list => list.Contains(marketTitle)))
                {
                    Console.WriteLine($"🔕 Mercado ignorado: {marketTitle}");
                    return;
                }

                // Normaliza e busca a tag correspondente
                string normalizedMarketTitle = _teamService.NormalizeText(marketTitle).Trim();
                var matchingTag = tagNames.FirstOrDefault(x => x.Value.Contains(normalizedMarketTitle));

                if (matchingTag.Key == 0)
                {
                    Console.WriteLine($"⚠️ Tag não encontrada para o mercado: {normalizedMarketTitle}");
                    return;
                }

                int tagId = matchingTag.Key;

                // Localiza os containers de apostas
                var betRows = _webScrapingService.FindElementsWithin(marketElement, ".//div[contains(@class, 'market-bc')]").ToList();
                if (!betRows.Any())
                {
                    Console.WriteLine($"⚠️ Nenhuma linha de aposta encontrada para o mercado: {normalizedMarketTitle}");
                    return;
                }

                // Cabeçalhos de "Mais de" e "Menos de"
                var headers = _webScrapingService.FindElementsWithin(marketElement, ".//div[contains(@class, 'm-g-header')]").ToList();
                if (headers.Count < 2)
                {
                    Console.WriteLine("⚠️ Cabeçalhos 'Mais de' e 'Menos de' não encontrados. Pulando...");
                    return;
                }

                string overHeader = headers[0].Text?.Trim();
                string underHeader = headers[1].Text?.Trim();

                if (string.IsNullOrWhiteSpace(overHeader) || string.IsNullOrWhiteSpace(underHeader))
                {
                    Console.WriteLine("⚠️ Cabeçalhos vazios ou inválidos. Pulando...");
                    return;
                }

                var bets = new List<BetInfo>();

                for (int i = 0; i < betRows.Count; i++)
                {
                    try
                    {
                        string overUnder = (i % 2 == 0) ? overHeader : underHeader;

                        // Ignora se for cabeçalho genérico ou estranho
                        if (string.IsNullOrWhiteSpace(overUnder) || overUnder == "***")
                        {
                            Console.WriteLine($"⚠️ Cabeçalho inválido detectado. Índice: {i}. Pulando...");
                            continue;
                        }

                        // Captura valor da aposta
                        var betValueElement = _webScrapingService.FindElementWithin(betRows[i], ".//span[contains(@class, 'market-name')]", 1000);
                        string betValueText = betValueElement?.Text?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(betValueText) || betValueText == "***" ||
                            !decimal.TryParse(betValueText.Replace(".", ","), out decimal betValue))
                        {
                            Console.WriteLine($"⚠️ Valor de aposta inválido: '{betValueText}'. Pulando...");
                            continue;
                        }

                        // Captura multiplicador
                        var multiplierElement = _webScrapingService.FindElementWithin(betRows[i], ".//span[contains(@class, 'market-odd')]");
                        string multiplierText = multiplierElement?.Text?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(multiplierText) || multiplierText == "***" ||
                            !decimal.TryParse(multiplierText.Replace(".", ","), out decimal multiplier))
                        {
                            Console.WriteLine($"⚠️ Multiplicador inválido: '{multiplierText}'. Pulando...");
                            continue;
                        }

                        bets.Add(new BetInfo
                        {
                            GamesInfo = gamesInfo,
                            TagName = normalizedMarketTitle,
                            OverUnder = overUnder,
                            BetAmount = betValue,
                            Multiplier = multiplier,
                            GameDate = gamesInfo.GameDate,
                            CaptureDate = DateTime.Now,
                            Site = site,
                            TagId = tagId
                        });

                        Console.WriteLine($"✅ Aposta adicionada: {overUnder} {betValue} - Mult.: {multiplier}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Erro ao processar linha de aposta: {ex.Message}");
                    }
                }

                SaveBets(bets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar mercado: {ex.Message}");
                _logService.LogError("Erro ao processar opção de aposta: ", ex);
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

            foreach (var betKey in betKeys)
            {
                var existingBets = _dbContext.BetInfo.Where(b =>
                                    b.GamesInfo.GameId == betKey.GamesInfo.GameId &&
                                    b.TagName == betKey.TagName &&
                                    b.OverUnder == betKey.OverUnder &&
                                    b.TagId == betKey.TagId &&
                                    b.Site.SiteId == betKey.Site.SiteId).ToList();

                if (existingBets.Count != 0)
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
