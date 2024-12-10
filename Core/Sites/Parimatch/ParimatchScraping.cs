using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using OpenQA.Selenium.Interactions;

namespace BetSniffer.Api.Core.Sites.Parimatch
{
    public class Parimatchcraping : IScrapingService
    {
        #region VariaveisGlobais

        private readonly IWebDriver _driver;
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

        public Parimatchcraping(
            IWebDriver driver,
            ApplicationDbContext dbContext,
            TeamService teamService,
            IRepositoryService<GamesInfo> gamesInfoRepository,
            IRepositoryService<BetInfo> betInfoRepository)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
        }

        public List<TagInfo> ScrapeTagsAsync(string url, string siteName)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrEmpty(siteName))
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            // Verifica se o site já existe no banco
            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ??
                       AddNewSite(siteName);

            _driver.Navigate().GoToUrl(url);

            // Aguarda o carregamento inicial da página
            WebDriverWait wait = new(_driver, TimeSpan.FromSeconds(10));
            wait.Until(driver => driver.FindElement(By.CssSelector("[data-id='event-markets']")));

            // Coleta informações do jogo
            ExtractGameInfo();

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            ParimatchTags.AddDynamicTags(homeTeam, awayTeam);

            // Verifica se o jogo já existe no banco
            var existingGame = _dbContext.GamesInfo
                .FirstOrDefault(g =>
                    g.HomeTeamId == homeTeamDb &&
                    g.AwayTeamId == awayTeamDb &&
                    g.GameDate == gameDateTime &&
                    g.Site.SiteId == site.SiteId); // A comparação é feita usando o SiteId

            if (existingGame != null)
            {
                // Se o jogo já existe no banco, preenche o gamesInfo com os dados existentes
                gamesInfo = existingGame;
                gamesInfo.Status = 1;
                gamesInfo.LastUpdated = DateTime.Now;
            }
            else
            {
                // Se o jogo não existir no banco, cria um novo GamesInfo
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

                // Adiciona o novo jogo ao banco
                _dbContext.GamesInfo.Add(gamesInfo);
            }

            // Processa todas as abas disponíveis
            ProcessTabsAndMarketViews();

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
            System.Threading.Thread.Sleep(new Random().Next(500, 1500));

            var spanElements = _driver.FindElements(By.CssSelector("div[data-testid='event-view-header-soccer-center-container'] span"));
            gameName = spanElements.Count >= 2 ? spanElements[1].Text.Trim() : "Nome não encontrado";

            var eventPresentationView = _driver.FindElement(By.CssSelector("div[data-id='card-scoreboard']"));

            var teamElements = eventPresentationView.FindElements(By.XPath(".//span[@data-id='event-card-competitor-name']"));
            if (teamElements.Count >= 2)
            {
                homeTeam = teamElements[0].Text.Trim();
                awayTeam = teamElements[1].Text.Trim();
            }
            else
            {
                throw new Exception("Não foi possível encontrar os dois times.");
            }

            gameDayText = eventPresentationView.FindElement(By.XPath(".//span[@data-testid='prematch-start-date']")).Text.Trim();
            gameHourText = eventPresentationView.FindElement(By.XPath(".//span[@data-testid='prematch-start-time']")).Text.Trim();

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
                            System.Threading.Thread.Sleep(new Random().Next(500, 1200));  // Pausa para garantir que a rolagem tenha ocorrido

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
                                    System.Threading.Thread.Sleep(new Random().Next(500, 1300)); // Espera antes de tentar novamente
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
            Console.WriteLine("Todas as abas processadas com sucesso");

        }



        private void ProcessMarketViews()
        {

            //Aguarda um tempo para carregar todos os mercados
            System.Threading.Thread.Sleep(new Random().Next(2800, 4850));

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _driver.FindElements(By.CssSelector("div[data-id='market-item']"));

            // Lista de tags cadastradas que queremos buscar
            var tagNames = ParimatchTags.TagNames;            

            // Lista para armazenar as apostas
            List<BetInfo> bets = new List<BetInfo>();
            List<TagInfo> tagInfos = new List<TagInfo>();
                       

            foreach (var eventMarketView in eventMarketViews)
            {
                try
                {
                    // Verifica se o evento contém uma tag válida
                    var tagElement = eventMarketView.FindElement(By.XPath(".//div[@role='button']//span[@data-testid='modulor-typography']"));
                    string tagName = tagElement.Text.Trim();

                    // Verifica se a tag encontrada contém o nome da tag desejada
                    if (tagNames.Values.Contains(tagName))
                    {

                        // Recupera o ID da tag a partir do dicionário
                        int tagId = ParimatchTags.TagNames.FirstOrDefault(x => x.Value == tagName).Key;

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
                                System.Threading.Thread.Sleep(new Random().Next(1521, 3122));

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
                                    Site = site,
                                    TagId = tagId
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
                                    Site = site,
                                    TagId = tagId
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
                                    b.GamesInfo.GameId == gamesInfo.GameId &&
                                    b.TagName == adjustedTagName &&
                                    b.OverUnder == bet.OverUnder &&
                                    b.BetAmount == bet.BetAmount &&
                                    b.TagId == bet.TagId &&
                                    b.Site.SiteId == bet.Site.SiteId).FirstOrDefault();

                                if (existingBet == null)
                                {
                                    // Adiciona nova aposta
                                    bet.GamesInfo = gamesInfo;
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
