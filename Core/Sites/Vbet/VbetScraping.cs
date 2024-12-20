using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using System.Text.RegularExpressions;
using Microsoft.OpenApi.Services;

namespace BetSniffer.Api.Core.Sites.Vbet
{
    public class VbetScraping : IScrapingService
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

        public VbetScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _webScrapingService = new WebScrapingServiceSelenium(); // Inicializa o serviço de scraping
            _webScrapingService.Initialize(); // Configura o WebDriver
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
            try
            {
                // Pausa aleatória para simular comportamento humano
                System.Threading.Thread.Sleep(new Random().Next(551, 1524));

                // Captura o contêiner principal fixo
                var matchInfoElement = _webScrapingService.WaitForElement("div.game-details-container-bc");
                if (matchInfoElement == null)
                    throw new Exception("O contêiner principal 'game-details-container-bc' não foi encontrado.");

                // Captura o nome da liga (gameName) pelo primeiro elemento <span> encontrado no título
                var leagueElement = _webScrapingService.FindElementWithin(matchInfoElement, ".//div[1]/div[1]/span");
                gameName = leagueElement?.Text.Trim() ?? "Liga não encontrada";

                // Captura a data e hora do jogo pelo <time>
                var dateTimeElement = _webScrapingService.FindElementWithin(matchInfoElement, ".//div[1]/div[1]/div/p/time");
                var dateTimeText = dateTimeElement?.Text.Trim();
                if (string.IsNullOrWhiteSpace(dateTimeText))
                    throw new Exception("Data e hora do jogo não foram encontradas.");

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
                        throw new Exception("A estrutura esperada para os times não corresponde.");
                    }

                    Console.WriteLine($"Time Casa: {homeTeam}, Time Visitante: {awayTeam}");
                }
                else
                {
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
                throw new Exception($"Erro ao converter a data: {dateTimeText} - {ex.Message}");
            }
        }


        private int MonthNameToNumber(string monthName)
        {
            var months = new Dictionary<string, int>
            {
                { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
                { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
                { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 },
                { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 },
                { "mai", 5 }, { "jun", 6 }, { "jul", 7 }, { "ago", 8 },
                { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
            };

            if (!months.ContainsKey(monthName))
                throw new Exception($"Nome do mês inválido: {monthName}");

            return months[monthName];
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
                        Thread.Sleep(new Random().Next(1231, 1644));
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
                // Pausa aleatória para simular comportamento humano
                Thread.Sleep(new Random().Next(521, 924));

                // Captura o título do mercado
                var marketTitleElement = _webScrapingService.FindElementWithin(marketElement, ".//p[contains(@class, '-title-')]");
                string marketTitle = marketTitleElement?.Text.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(marketTitle))
                {
                    Console.WriteLine("Título do mercado não encontrado. Pulando...");
                    return;
                }

                Console.WriteLine($"Processando mercado: {marketTitle}");

                // Lista de tags cadastradas que queremos buscar
                var tagNames = VbetTags.TagNames;

                // Verifica se a tag encontrada é válida
                if (!tagNames.Values.Any(tagList => tagList.Contains(marketTitle)))
                {
                    Console.WriteLine($"Mercado ignorado: {marketTitle}");
                    return;
                }

                // Normaliza o título do mercado
                string normalizedMarketTitle = _teamService.NormalizeText(marketTitle).Trim();

                // Encontra a tag correspondente no dicionário
                var matchingTag = tagNames.Where(x => x.Value.Contains(normalizedMarketTitle)).FirstOrDefault();


                if (matchingTag.Key == 0)
                {
                    Console.WriteLine($"Tag não encontrada para o mercado: {marketTitle}");
                    return;
                }

                int tagId = matchingTag.Key;


                // Localiza os contêineres de apostas dentro do mercado
                var betRows = _webScrapingService.FindElementsWithin(marketElement, ".//div[contains(@class, 'market-bc')]").ToList();
                if (!betRows.Any())
                {
                    Console.WriteLine($"Nenhuma linha de aposta encontrada para o mercado: {marketTitle}");
                    return;
                }

                // Determina os cabeçalhos de "Mais de" e "Menos de"
                var headers = _webScrapingService.FindElementsWithin(marketElement, ".//div[contains(@class, 'm-g-header')]").ToList();
                if (headers.Count < 2)
                {
                    Console.WriteLine("Cabeçalhos 'Mais de' e 'Menos de' não encontrados. Pulando...");
                    return;
                }

                string overHeader = headers[0].Text.Trim();
                string underHeader = headers[1].Text.Trim();

                // Lista para armazenar apostas
                var bets = new List<BetInfo>();

                // Processa as linhas de apostas
                for (int i = 0; i < betRows.Count; i++)
                {
                    try
                    {
                        // Alterna entre "Mais de" e "Menos de" com base no índice
                        string overUnder = (i % 2 == 0) ? overHeader : underHeader;

                        // Captura o valor da aposta
                        var betValueElement = _webScrapingService.FindElementWithin(betRows[i], ".//span[contains(@class, 'market-name')]",1000);
                        string betValueText = betValueElement?.Text.Trim() ?? string.Empty;

                        if (!decimal.TryParse(betValueText.Replace(".", ","), out decimal betValue))
                        {
                            Console.WriteLine($"Valor da aposta inválido: {betValueText}. Pulando...");
                            continue;
                        }

                        // Captura o multiplicador da aposta
                        var multiplierElement = _webScrapingService.FindElementWithin(betRows[i], ".//span[contains(@class, 'market-odd')]");
                        string multiplierText = multiplierElement?.Text.Trim() ?? string.Empty;

                        if (!decimal.TryParse(multiplierText.Replace(".", ","), out decimal multiplier))
                        {
                            Console.WriteLine($"Multiplicador inválido: {multiplierText}. Pulando...");
                            continue;
                        }

                        // Substituir o nome do time na tag por "Casa" ou "Visitante", respeitando a estrutura do texto
                        string adjustedTagName = marketTitle;

                        if (marketTitle.Contains(homeTeam, StringComparison.OrdinalIgnoreCase))
                        {
                            adjustedTagName = adjustedTagName.Replace(homeTeam, "Casa", StringComparison.OrdinalIgnoreCase);
                        }

                        if (marketTitle.Contains(awayTeam, StringComparison.OrdinalIgnoreCase))
                        {
                            adjustedTagName = adjustedTagName.Replace(awayTeam, "Visitante", StringComparison.OrdinalIgnoreCase);
                        }

                        // Adiciona a aposta à lista
                        bets.Add(new BetInfo
                        {
                            GamesInfo = gamesInfo,
                            TagName = adjustedTagName,
                            OverUnder = overUnder,
                            BetAmount = betValue,
                            Multiplier = multiplier,
                            GameDate = gamesInfo.GameDate,
                            CaptureDate = DateTime.Now,
                            Site = site,
                            TagId = tagId
                        });

                        Console.WriteLine($"Aposta adicionada: {overUnder} {betValue} - Mult.: {multiplier}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar linha de aposta: {ex.Message}");
                    }
                }

                SaveBets(bets);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
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
