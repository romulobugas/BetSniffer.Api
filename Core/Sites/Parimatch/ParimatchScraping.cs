using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using System.Text.RegularExpressions;

namespace BetSniffer.Api.Core.Sites.Parimatch
{
    public class ParimatchScraping : IScrapingService
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

        #endregion

        public ParimatchScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _webScrapingService = new WebScrapingServiceSelenium(); // Inicializa o serviço de scraping
            _webScrapingService.Initialize(); // Configura o WebDriver
        }

        public List<TagInfo> ScrapeTagsAsync(string url, string siteName)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrEmpty(siteName))
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            // Verifica se o site já existe no banco
            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ?? AddNewSite(siteName);

            _webScrapingService.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(6873, 7405));

            // Aguarda o carregamento inicial da página
            _webScrapingService.WaitForElement("[data-id='event-markets']", 10000);

            // Coleta informações do jogo
            ExtractGameInfo();

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            ParimatchTags.AddDynamicTags(homeTeam, awayTeam);

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
                    League = gameName,
                    Site = site,
                    URL = url,
                    Status = 1,
                    LastUpdated = DateTime.Now
                };
                _dbContext.GamesInfo.Add(gamesInfo);
            }


            // Processa todas as abas disponíveis
            ProcessTabsAndMarketViews();

            _webScrapingService.Dispose();

            return new List<TagInfo>(); // Substitua com a lógica para retornar as informações processadas
        }

        private Site AddNewSite(string siteName)
        {
            var site = new Site { Name = siteName };
            _dbContext.Site.Add(site);
            _dbContext.SaveChanges();
            Console.WriteLine($"Novo site adicionado: {siteName}");
            return site;
        }

        private void ExtractGameInfo()
        {
            // Espera aleatória para simular comportamento humano
            System.Threading.Thread.Sleep(new Random().Next(551, 1524));

            // Captura o nome do jogo
            var spanElements = _webScrapingService.WaitForElements("div[data-testid='event-view-header-soccer-center-container'] span", 5000).ToList();
            gameName = spanElements.Count >= 2 ? spanElements[1].Text.Trim() : "Nome não encontrado";

            // Localiza o contêiner de apresentação do evento
            var eventPresentationView = _webScrapingService.WaitForElement("div[data-id='card-scoreboard']", 5000);

            // Captura os times
            var teamElements = _webScrapingService.FindElementsWithin(eventPresentationView, ".//span[@data-id='event-card-competitor-name']", 5000).ToList();
            if (teamElements.Count >= 2)
            {
                homeTeam = teamElements[0].Text.Trim();
                awayTeam = teamElements[1].Text.Trim();
            }
            else
            {
                throw new Exception("Não foi possível encontrar os dois times.");
            }

            // Captura a data e hora do jogo
            gameDayText = _webScrapingService.FindElementWithin(eventPresentationView, ".//span[@data-testid='prematch-start-date']", 5000)?.Text.Trim();
            gameHourText = _webScrapingService.FindElementWithin(eventPresentationView, ".//span[@data-testid='prematch-start-time']", 5000)?.Text.Trim();

            if (string.IsNullOrEmpty(gameDayText) || string.IsNullOrEmpty(gameHourText))
                throw new Exception("Não foi possível capturar a data e/ou hora do jogo.");

            gameDateTime = ParseGameDateTime(gameDayText, gameHourText);
            Console.WriteLine($"Data e Hora do Jogo: {gameDateTime}");
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
            var ignoredTabs = new HashSet<string> { "Bet Builder", "Múltiplas" };


            //Pequena pausa para localizar o container
            Thread.Sleep(new Random().Next(621, 1126));
            // Localiza o contêiner de abas
            var tabsContainer = _webScrapingService.WaitForElement("div[data-testid='marketTabs']", 10000);

            while (true)
            {
                try
                {
                    Thread.Sleep(new Random().Next(621, 926));

                   
                    if (tabsContainer == null)
                    {
                        Console.WriteLine("Contêiner de abas não encontrado.");
                        break;
                    }

                    // Captura os botões das abas
                    var tabButtons = _webScrapingService.FindElementsWithin(tabsContainer, "button[data-testid='marketTabs-button']", 5000).ToList();

                    foreach (var tab in tabButtons)
                    {
                        try
                        {
                            // Captura o nome da aba
                            var tabNameElement = _webScrapingService.FindElementWithin(tab, "span[data-testid='marketTabs-typography']", 2000);
                            var tabName = tabNameElement?.Text.Trim();

                            if (string.IsNullOrEmpty(tabName))
                            {
                                Console.WriteLine("Não foi possível capturar o nome da aba.");
                                continue;
                            }

                            if (ignoredTabs.Contains(tabName))
                            {
                                Console.WriteLine($"Ignorando a aba: {tabName}");
                                continue;
                            }

                            Console.WriteLine("Forçando o scroll para o topo do layout...");
                            _webScrapingService.GetWebDriver().FindElement(By.TagName("body")).SendKeys(Keys.Home); // Simula pressionar a tecla "Home"
                            Thread.Sleep(new Random().Next(621, 1126)); // Pausa para garantir que a rolagem tenha ocorrido
                            Console.WriteLine("Scroll até o topo da página concluído.");

                            Console.WriteLine($"Processando aba: {tabName}");

                            // Tenta clicar no botão da aba, com repetição em caso de erro
                            bool clicked = false;
                            int retries = 0;
                            while (!clicked && retries < 5)
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
                                    Thread.Sleep(new Random().Next(500, 1300)); // Espera antes de tentar novamente
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
                            var marketItems = _webScrapingService.WaitForElements("div[data-id='market-item']", 5000);
                            if (!marketItems.Any())
                            {
                                Console.WriteLine($"Nenhum item de mercado encontrado na aba '{tabName}'.");
                                continue;
                            }

                            // Processa os mercados visíveis na aba
                            ProcessMarketViews();
                        }
                        catch (StaleElementReferenceException)
                        {
                            Console.WriteLine($"Elemento desatualizado na aba. Recarregando...");
                            break; // Sai do loop para relocalizar as abas
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro inesperado ao processar aba: {ex.Message}");
                        }
                        _webScrapingService.GetWebDriver().FindElement(By.TagName("body")).SendKeys(Keys.Home); // Simula pressionar a tecla "Home"
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

            Console.WriteLine("Todas as abas processadas com sucesso");
        }



        private void ProcessMarketViews()
        {
            // Aguarda um tempo para carregar todos os mercados
            Thread.Sleep(new Random().Next(2800, 4850));

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _webScrapingService.WaitForElements("div[data-id='market-item']", 5000);

            // Lista de tags cadastradas que queremos buscar
            var tagNames = ParimatchTags.TagNames;

            List<TagInfo> tagInfos = new List<TagInfo>();

            foreach (var eventMarketView in eventMarketViews)
            {
                try
                {
                    // Verifica se o evento contém uma tag válida
                    var tagElement = _webScrapingService.FindElementWithin(eventMarketView, ".//div[@role='button']//span[@data-testid='modulor-typography']", 2000);
                    string tagName = tagElement?.Text.Trim() ?? string.Empty;

                    // Verifica se a tag encontrada contém o nome da tag desejada
                    if (tagNames.Values.Contains(tagName))
                    {
                        // Recupera o ID da tag a partir do dicionário
                        int tagId = tagNames.FirstOrDefault(x => x.Value == tagName).Key;

                        // Verifica se o elemento "market-wrapper" está presente, com tentativa de expansão
                        bool marketWrapperFound = false;
                        for (int attempt = 0; attempt < 3; attempt++)
                        {
                            try
                            {
                                var marketWrapper = _webScrapingService.TryFindElementWithin(eventMarketView, ".//div[@data-id='market-wrapper']", 2000);
                                if (marketWrapper != null)
                                {
                                    marketWrapperFound = true;
                                    break;
                                }
                                else 
                                {
                                    // Caso o elemento não seja encontrado, tenta clicar no botão
                                    Console.WriteLine("Clicando para expandir a aba.");
                                    var toggleButton = _webScrapingService.FindElementWithin(eventMarketView, ".//div[@role='button']", 2000);
                                    toggleButton?.Click();
                                    Thread.Sleep(new Random().Next(921, 1522));
                                }
                            }
                            catch
                            {
                                // Caso o elemento não seja encontrado, tenta clicar no botão
                                var toggleButton = _webScrapingService.FindElementWithin(eventMarketView, ".//div[@role='button']", 2000);
                                toggleButton?.Click();
                                Thread.Sleep(new Random().Next(1521, 3122));
                            }
                        }

                        if (!marketWrapperFound)
                        {
                            Console.WriteLine("O elemento 'market-wrapper' não foi encontrado após 3 tentativas.");
                            continue;
                        }

                        // Captura todas as divs dentro de eventMarketView
                        var betElements = _webScrapingService.FindElementsWithin(eventMarketView, ".//div", 2000);

                        //Lista para armazenar as apostas
                        var currentBets = new List<BetInfo>();

                        // Processa e adiciona novas apostas
                        foreach (var betElement in betElements)
                        {
                            try
                            {
                                // Captura o valor da aposta
                                var betAmountElement = _webScrapingService.FindElementWithin(betElement, ".//span[@data-id='modulor-typography' and not(ancestor::span[@data-id='outcome'])]", 2000);
                                string betAmountText = betAmountElement?.Text.Trim() ?? string.Empty;

                                if (!decimal.TryParse(betAmountText.Replace(".", ","), out decimal betAmount))
                                {
                                    Console.WriteLine("Valor da aposta inválido. Pulando este elemento.");
                                    continue;
                                }

                                // Captura os multiplicadores
                                var multiplierElements = _webScrapingService.FindElementsWithin(betElement, ".//span[contains(@style, '--text: var(--text-outcome);')]", 2000);
                                if (multiplierElements.Count < 2)
                                {
                                    Console.WriteLine("Menos de dois multiplicadores encontrados. Pulando esta aposta.");
                                    continue;
                                }

                                // Obtém os valores dos multiplicadores
                                if (!decimal.TryParse(multiplierElements.ElementAtOrDefault(0)?.Text.Trim().Replace(".", ","), out decimal moreMultiplier) ||
                                    !decimal.TryParse(multiplierElements.ElementAtOrDefault(1)?.Text.Trim().Replace(".", ","), out decimal lessMultiplier))
                                {
                                    Console.WriteLine("Multiplicadores inválidos. Pulando esta aposta.");
                                    continue;
                                }

                                // Substituir o nome do time na tag por "Casa" ou "Visitante", respeitando a estrutura do texto
                                string adjustedTagName = tagName;

                                if (tagName.Contains(homeTeam, StringComparison.OrdinalIgnoreCase))
                                {
                                    adjustedTagName = adjustedTagName.Replace(homeTeam, "Casa", StringComparison.OrdinalIgnoreCase);
                                }

                                if (tagName.Contains(awayTeam, StringComparison.OrdinalIgnoreCase))
                                {
                                    adjustedTagName = adjustedTagName.Replace(awayTeam, "Visitante", StringComparison.OrdinalIgnoreCase);
                                }

                                // Cria apostas "Mais de" e "Menos de"
                                currentBets.Add(new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = adjustedTagName,
                                    OverUnder = "Mais de",
                                    BetAmount = betAmount,
                                    Multiplier = moreMultiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = site,
                                    TagId = tagId
                                });

                                currentBets.Add(new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = adjustedTagName,
                                    OverUnder = "Menos de",
                                    BetAmount = betAmount,
                                    Multiplier = lessMultiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = site,
                                    TagId = tagId
                                });
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar aposta: {ex.Message}");
                            }
                        }

                        if (currentBets.Count > 0)
                        {


                            // **1. Busca apostas existentes no banco com todos os critérios**
                            foreach (var bet in currentBets)
                            {
                                var existingBet = _dbContext.BetInfo.FirstOrDefault(b =>
                                    b.GamesInfo.GameId == gamesInfo.GameId &&
                                    b.TagName == bet.TagName &&
                                    b.OverUnder == bet.OverUnder &&
                                    b.BetAmount == bet.BetAmount &&
                                    b.TagId == bet.TagId &&
                                    b.Site.SiteId == bet.Site.SiteId);

                                if (existingBet != null)
                                {
                                    // **Deletar apostas duplicadas que já estão no banco**
                                    Console.WriteLine($"Aposta existente encontrada. Removendo a aposta duplicada...");
                                    _dbContext.BetInfo.Remove(existingBet);
                                }
                            }

                            // **2. Adicionar as novas apostas**
                            _dbContext.BetInfo.AddRange(currentBets);

                            // **3. Salvar as alterações no banco de dados**
                            if (_dbContext.ChangeTracker.HasChanges())
                            {
                                _dbContext.SaveChanges();
                                Console.WriteLine("Alterações salvas com sucesso.");
                            }
                        }



                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
                }
            }
        }

    }
}
