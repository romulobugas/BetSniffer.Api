using BetSniffer.Api.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using OpenQA.Selenium.Interactions;
using BetSniffer.Api.Core.Sites.Superbet;
using PuppeteerSharp;

namespace BetSniffer.Api.Core.Sites.Bet365
{
    public class PixbetScraping : IScrapingService
    {
        #region VariaveisGlobais

        private readonly IWebDriver _driver;
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
        private readonly ILogService _logService;
        private WebScrapingServicePuppeteer _webScrapingService;

        #endregion

        public PixbetScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
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

            System.Threading.Thread.Sleep(new Random().Next(6873, 7405));

            string ageVerification = "div.p-dialog-mask[data-pc-section='mask']";

            // Confirmação de idade
            ConfirmAgeVerification(page, ageVerification);

            //HandleCookies(page, cookieAcceptButtonSelector);

            //string popupSelector = ".overlay.new-message.visible .popup span.close";

            //_gameService.ClosePopup(page,popupSelector);

            ExtractGameInfo(page);

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            SuperbetTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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
                    LastUpdated = DateTime.Now
                };
                _dbContext.GamesInfo.Add(gamesInfo);
            }

            _dbContext.SaveChanges();


            //ProcessTabsAndMarketViews(page);




            Console.WriteLine("Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return new List<TagInfo>();
        }

        private void ConfirmAgeVerification(IPage page, string ageVerificationSelector)
        {
            try
            {

                var popupElement = page.WaitForSelectorAsync(ageVerificationSelector, new WaitForSelectorOptions
                {
                    Timeout = 15000, // Tempo limite para encontrar o popup
                    Visible = true   // Certifica-se de que o elemento está visível
                }).GetAwaiter().GetResult();

                if (popupElement != null)
                {
                    Console.WriteLine("Popup de verificação de idade encontrado.");

                    // Dentro do popup, busca o botão "Sim" baseado no padrão
                    var confirmButton = popupElement.QuerySelectorAsync("button[aria-label='Sim']").GetAwaiter().GetResult();

                    if (confirmButton != null)
                    {
                        page.EvaluateFunctionAsync("element => element.click()", confirmButton).GetAwaiter().GetResult();
                        Console.WriteLine("Botão 'Sim' clicado com sucesso.");
                    }
                    else
                    {
                        Console.WriteLine("Botão 'Sim' não encontrado dentro do popup.");
                    }
                }
                else
                {
                    Console.WriteLine("Popup de verificação de idade não encontrado.");
                }
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Erro: Tempo limite excedido para encontrar o popup ou botão 'Sim'. Detalhes: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro inesperado ao confirmar verificação de idade: {ex.Message}");
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

        private void ExtractGameInfo(IPage page)
        {
            try
            {
                // Captura o elemento raiz que contém o Shadow DOM
                var rootElement = page.QuerySelectorAsync("div#bt-inner-page").GetAwaiter().GetResult();
                if (rootElement == null)
                {
                    throw new Exception("Elemento raiz 'div#bt-inner-page' não encontrado.");
                }

                // Acessa o Shadow DOM
                var shadowRootHandle = page.EvaluateFunctionHandleAsync(
                    @"(el) => el.shadowRoot",
                    rootElement
                ).GetAwaiter().GetResult();

                // Converte shadowRootHandle para IElementHandle
                var shadowRoot = shadowRootHandle as IElementHandle;
                if (shadowRoot == null)
                {
                    throw new Exception("Não foi possível acessar o Shadow Root como 'IElementHandle'.");
                }

                // Busca pelo elemento 'scoreBoardCard'
                var scoreBoardCardHandle = shadowRoot.QuerySelectorAsync("div[data-editor-id='scoreBoardCard']").GetAwaiter().GetResult();
                if (scoreBoardCardHandle == null)
                {
                    throw new Exception("Elemento 'scoreBoardCard' não encontrado dentro do Shadow DOM.");
                }

                // Liga: Captura o texto do elemento que possui 'scoreBoardCategory' e trata o texto para remover nação
                var leagueText = GetInnerText(scoreBoardCardHandle, "div[data-editor-id='scoreBoardCategory']")
                    .Split('\n')
                    .Last()
                    .Trim();

                // Captura os elementos que contêm as informações dos times e horários
                var matchInfoElements = scoreBoardCardHandle.QuerySelectorAllAsync("div > div").GetAwaiter().GetResult();

                if (matchInfoElements == null || matchInfoElements.Length < 3)
                {
                    throw new Exception("Estrutura de times e horários não encontrada ou incompleta.");
                }

                // Filtra o time da casa com base no padrão de conteúdo textual sem elementos gráficos
                var homeTeamElement = matchInfoElements.FirstOrDefault(el =>
                    el.EvaluateFunctionAsync<bool>("el => el.textContent.trim().length > 0 && el.querySelectorAll('img').length === 0 && el.textContent.includes('FC')").GetAwaiter().GetResult());

                // Filtra o time visitante com base no padrão de conteúdo textual sem elementos gráficos
                var awayTeamElement = matchInfoElements.LastOrDefault(el =>
                    el.EvaluateFunctionAsync<bool>("el => el.textContent.trim().length > 0 && el.querySelectorAll('img').length === 0 && el.textContent.includes('FC')").GetAwaiter().GetResult());

                if (homeTeamElement == null || awayTeamElement == null)
                {
                    throw new Exception("Não foi possível capturar os times.");
                }

                // Extrai os nomes dos times
                var homeTeam = homeTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                var awayTeam = awayTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam))
                {
                    throw new Exception("Não foi possível capturar os times.");
                }

                // Captura a data e a hora
                var gameDayText = matchInfoElements[1].QuerySelectorAsync("div[data-editor-id='prematchStartedAt'] div").GetAwaiter().GetResult()?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                var gameHourText = matchInfoElements[1].QuerySelectorAsync("div[data-editor-id='prematchStartedAt'] span").GetAwaiter().GetResult()?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                if (string.IsNullOrEmpty(gameDayText) || string.IsNullOrEmpty(gameHourText))
                {
                    throw new Exception("Não foi possível capturar a data e hora.");
                }

                // Combina a data e a hora
                var gameDateTime = ParseGameDateTime(gameDayText, gameHourText);

                // Exibe as informações no console
                Console.WriteLine($"Liga: {leagueText}");
                Console.WriteLine($"Times: {homeTeam} x {awayTeam}");
                Console.WriteLine($"Data e Hora: {gameDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao capturar informações do jogo: {ex.Message}");
            }
        }

        private string GetInnerText(IElementHandle parentHandle, string selector)
        {
            var elementHandle = parentHandle.QuerySelectorAsync(selector).GetAwaiter().GetResult();
            if (elementHandle == null)
            {
                throw new Exception($"Elemento '{selector}' não encontrado.");
            }

            // Captura o texto e remove quebras de linha e espaços extras
            var rawText = elementHandle.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
            return string.Join(" ", rawText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }








        private DateTime ParseGameDateTime(string dayText, string hourText)
        {
            if (!hourText.Contains(":"))
                throw new Exception($"Formato inesperado para gameHourText: {hourText}");

            var baseDate = dayText switch
            {
                "HOJE" => DateTime.Today,
                "AMANHÃ" => DateTime.Today.AddDays(1),
                _ => ParseCustomDate(dayText)
            };

            return baseDate.Date.Add(TimeSpan.Parse(hourText));
        }

        private DateTime ParseCustomDate(string dayText)
        {
            var match = Regex.Match(dayText, @"(\d+)\s+de\s+(\w+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new Exception($"Formato de data inválido: {dayText}");

            var day = int.Parse(match.Groups[1].Value);
            var month = MonthNameToNumber(match.Groups[2].Value.ToLower());
            var year = DateTime.Today.Year;

            if (month < DateTime.Today.Month)
                year++;

            return new DateTime(year, month, day);
        }

        private int MonthNameToNumber(string monthName)
        {
            var months = new Dictionary<string, int>
            {
                { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
                { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
                { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 }
            };

            if (!months.ContainsKey(monthName))
                throw new Exception($"Nome do mês inválido: {monthName}");

            return months[monthName];
        }

        private void ProcessTabsAndMarketViews()
        {
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            var ignoredTabs = new HashSet<string> { "Bet Builder", "Múltiplas" };

            while (true)
            {
                try
                {
                    
                    // Localiza o contêiner de abas
                    var tabsContainer = wait.Until(driver => driver.FindElement(By.CssSelector("div[data-testid='marketTabs']")));
                    var tabButtons = tabsContainer.FindElements(By.CssSelector("button[data-testid='marketTabs-button']")).ToList();

                    foreach (var tab in tabButtons)
                    {
                        try
                        {
                            // Captura o nome da aba
                            var tabName = tab.FindElement(By.CssSelector("span[data-testid='marketTabs-typography']")).Text.Trim();

                            if (ignoredTabs.Contains(tabName))
                            {
                                Console.WriteLine($"Ignorando a aba: {tabName}");
                                continue;
                            }

                            Console.WriteLine("Forçando o scroll para o topo do layout...");

                            // Rola a página até o topo absoluto usando window.scrollTo
                            var actions = new Actions(_driver);
                            actions.SendKeys(Keys.Home).Perform();  // Simula pressionar a tecla "Home"
                            Thread.Sleep(500);  // Pausa para garantir que a rolagem tenha ocorrido

                            Console.WriteLine("Scroll até o topo da página concluído.");

                            Console.WriteLine($"Processando aba: {tabName}");

                            // Tenta clicar no botão da aba, com repetição em caso de erro
                            bool clicked = false;
                            int retries = 0;
                            while (!clicked && retries < 5) // Tenta até 5 vezes
                            {
                                try
                                {
                                    tab.Click();
                                    clicked = true; // Se clicou com sucesso, sai do loop
                                }
                                catch (ElementClickInterceptedException)
                                {
                                    retries++;
                                    Console.WriteLine($"Clique interceptado na aba '{tabName}', tentando novamente ({retries}/5).");
                                    Thread.Sleep(500); // Espera antes de tentar novamente
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Erro ao tentar clicar na aba '{tabName}': {ex.Message}");
                                    break; // Sai do loop em caso de erro inesperado
                                }
                            }

                            if (!clicked)
                            {
                                Console.WriteLine($"Falha ao clicar na aba '{tabName}' após 5 tentativas.");
                                continue; // Pule para a próxima aba
                            }

                            // Aguarda que os itens da aba sejam carregados
                            wait.Until(driver => driver.FindElements(By.CssSelector("div[data-id='market-item']")).Any());

                            // Processa os mercados visíveis na aba
                            ProcessMarketViews();
                        }
                        catch (StaleElementReferenceException)
                        {
                            Console.WriteLine($"Elemento desatualizado na aba '{tab.Text}'. Recarregando...");
                            break; // Sai do loop para relocalizar as abas
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro inesperado ao processar aba '{tab.Text}': {ex.Message}");
                        }
                    }

                    break; // Sai do loop principal após processar todas as abas
                }
                catch (StaleElementReferenceException ex)
                {
                    Console.WriteLine($"Contêiner de abas desatualizado: {ex.Message}. Recarregando abas...");
                    Thread.Sleep(1000); // Pausa para estabilizar a página antes de tentar novamente
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro geral ao processar abas: {ex.Message}");
                    break;
                }
            }
        }



        private void ProcessMarketViews()
        {

            //Aguarda um tempo para carregar todos os mercados
            Thread.Sleep(5000);

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _driver.FindElements(By.CssSelector("div[data-id='market-item']"));

            // Lista de tags cadastradas que queremos buscar
            var tagNames = PixbetTags.TagNames;            

            // Lista para armazenar as apostas
            List<BetInfo> bets = new List<BetInfo>();
            List<TagInfo> tagInfos = new List<TagInfo>();

            // Verifica se o jogo já existe no banco
            var existingGame = _gamesInfoRepository.Find(g =>
                g.HomeTeamId == gamesInfo.HomeTeamId &&
                g.AwayTeamId == gamesInfo.AwayTeamId &&
                g.GameDate == gamesInfo.GameDate &&
                g.League == gamesInfo.League &&
                g.Site.SiteId == gamesInfo.Site.SiteId).FirstOrDefault();

            if (existingGame == null)
            {
                // Se o jogo não existir, adiciona ao banco
                _gamesInfoRepository.Add(gamesInfo);
                existingGame = gamesInfo;
            }

            foreach (var eventMarketView in eventMarketViews)
            {
                try
                {
                    // Verifica se o evento contém uma tag válida
                    var tagElement = eventMarketView.FindElement(By.XPath(".//div[@role='button']//span[@data-testid='modulor-typography']"));
                    string tagName = tagElement.Text.Trim();

                    // Verifica se a tag encontrada contém o nome da tag desejada
                    if (tagNames.Contains(tagName))
                    {

                        // Definindo o número máximo de tentativas
                        int maxAttempts = 3;
                        int attempt = 0;
                        bool marketWrapperFound = false;

                        // Verifica se o elemento "data-id='market-wrapper'" está presente
                        while (attempt < maxAttempts && !marketWrapperFound)
                        {
                            try
                            {
                                var marketWrapper = eventMarketView.FindElement(By.XPath(".//div[@data-id='market-wrapper']"));
                                marketWrapperFound = true;  // O elemento foi encontrado, sai do loop
                            }
                            catch (NoSuchElementException)
                            {
                                // Caso o elemento não seja encontrado, tenta clicar no botão
                                var toggleButton = eventMarketView.FindElement(By.XPath(".//div[@role='button']"));
                                toggleButton.Click();

                                // Aguardar 3 segundos
                                Thread.Sleep(3000);

                                attempt++;  // Incrementa a tentativa
                            }
                        }

                        // Se após 3 tentativas não encontrou o "market-wrapper", exibe uma mensagem
                        if (!marketWrapperFound)
                        {
                            throw new Exception("O elemento 'market-wrapper' não foi encontrado após 3 tentativas.");
                        }

                        // Captura todas as divs dentro de eventMarketView
                        var betElements = eventMarketView.FindElements(By.XPath(".//div"));

                        foreach (var betElement in betElements)
                        {
                            try
                            {
                                // Captura o valor da aposta (exemplo: "5.5")
                                var betAmountElement = betElement.FindElement(By.XPath(".//span[@data-id='modulor-typography' and not(ancestor::span[@data-id='outcome'])]"));
                                string betAmountText = betAmountElement.Text.Trim();

                                // Valida o valor da aposta
                                if (!decimal.TryParse(betAmountText.Replace(".", ","), out decimal betAmount))
                                {
                                    Console.WriteLine("Valor da aposta inválido. Pulando este elemento.");
                                    continue;
                                }

                                // Captura os multiplicadores
                                var multiplierElements = betElement.FindElements(By.XPath(".//span[contains(@style, '--text: var(--text-outcome);')]"));
                                if (multiplierElements.Count < 2)
                                {
                                    Console.WriteLine("Menos de dois multiplicadores encontrados. Pulando esta aposta.");
                                    continue; // Ignora esta iteração e vai para o próximo elemento
                                }

                                // Obtém os valores dos multiplicadores
                                string moreMultiplierText = multiplierElements[0].Text.Trim();
                                string lessMultiplierText = multiplierElements[1].Text.Trim();

                                if (!decimal.TryParse(moreMultiplierText.Replace(".", ","), out decimal moreMultiplier) ||
                                    !decimal.TryParse(lessMultiplierText.Replace(".", ","), out decimal lessMultiplier))
                                {
                                    Console.WriteLine("Multiplicadores inválidos. Pulando esta aposta.");
                                    continue;
                                }

                                // Substituir o nome do time na tag por "Casa" ou "Visitante"
                                var adjustedTagName = tagName
                                    .Replace(homeTeam, "Casa", StringComparison.OrdinalIgnoreCase)
                                    .Replace(awayTeam, "Visitante", StringComparison.OrdinalIgnoreCase);

                                // Cria a aposta "Mais de"
                                var betMore = new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = adjustedTagName,
                                    OverUnder = "Mais de", // "Mais de" para o lado "Mais"
                                    BetAmount = betAmount,
                                    Multiplier = moreMultiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = site
                                };

                                // Cria a aposta "Menos de"
                                var betLess = new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = adjustedTagName,
                                    OverUnder = "Menos de", // "Menos de" para o lado "Menos"
                                    BetAmount = betAmount,
                                    Multiplier = lessMultiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = site
                                };

                                // Adiciona as apostas à lista
                                bets.Add(betMore);
                                bets.Add(betLess);
                            }
                            catch (NoSuchElementException ex)
                            {
                                Console.WriteLine($"Elemento ausente: {ex.Message}. Pulando esta aposta.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar aposta: {ex.Message}");
                            }
                        }


                        if (bets.Count > 0)
                        {
                            tagInfos.Add(new TagInfo(gamesInfo, bets));                            

                            // Verifica e atualiza as apostas
                            foreach (var bet in bets)
                            {
                                // Substituir o nome do time na tag por "Casa" ou "Visitante"
                                var adjustedTagName = bet.TagName
                                    .Replace(homeTeam, "Casa", StringComparison.OrdinalIgnoreCase)
                                    .Replace(awayTeam, "Visitante", StringComparison.OrdinalIgnoreCase);

                                // Procura a aposta correspondente
                                var existingBet = _betInfoRepository.Find(b =>
                                    b.GamesInfo.GameId == existingGame.GameId &&
                                    b.TagName == adjustedTagName &&
                                    b.OverUnder == bet.OverUnder &&
                                    b.BetAmount == bet.BetAmount &&
                                    b.Site.SiteId == bet.Site.SiteId).FirstOrDefault();

                                if (existingBet == null)
                                {
                                    // Adiciona nova aposta
                                    bet.GamesInfo = existingGame;
                                    _betInfoRepository.Add(bet);
                                }
                                else
                                {
                                    // Atualiza a aposta existente
                                    existingBet.Multiplier = bet.Multiplier;
                                    existingBet.CaptureDate = DateTime.Now;
                                    _betInfoRepository.SaveOrUpdate(existingBet);
                                }
                            }

                            // Salva todas as alterações
                            _dbContext.SaveChanges();
                        }

                    }
                }
                catch (NoSuchElementException)
                {
                    // Se o elemento não for encontrado, apenas ignora
                    continue;
                }
                catch (WebDriverTimeoutException)
                {
                    // Se o elemento não aparecer dentro do tempo limite, ignora
                    continue;
                }
            }
        }
    }
}
