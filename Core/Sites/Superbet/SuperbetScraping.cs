using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Betano;
using Microsoft.OpenApi.Services;
using System.Reflection.Metadata;
using BetSniffer.Api.Core.Sites.Betfast;
using System.Globalization;

namespace BetSniffer.Api.Core.Sites.Superbet
{
    public class SuperbetScraping : IScrapingService
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

        public SuperbetScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
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

            string cookieAcceptButtonSelector = "#onetrust-accept-btn-handler";

            HandleCookies(page, cookieAcceptButtonSelector);

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
                    LastUpdated = DateTime.Now,
                    GameName = homeTeam + " - " + awayTeam
                };
                _dbContext.GamesInfo.Add(gamesInfo);
            }

            _dbContext.SaveChanges();


            ProcessTabsAndMarketViews(page);




            Console.WriteLine("Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return new List<TagInfo>();
        }

        public void HandleCookies(IPage page, string cookieAcceptButtonSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                // Aguarda um tempo aleatório para simular comportamento humano
                System.Threading.Thread.Sleep(new Random().Next(981, 1758));

                // Localiza o botão de aceitar cookies
                var element = page.WaitForSelectorAsync(cookieAcceptButtonSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                }).GetAwaiter().GetResult();

                if (element != null)
                {
                    // Clica no botão de aceitar cookies
                    element.ClickAsync().GetAwaiter().GetResult();
                    Console.WriteLine("Botão 'Aceitar todos os cookies' clicado com sucesso.");
                }
                else
                {
                    Console.WriteLine("Botão 'Aceitar todos os cookies' não encontrado.");
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Tempo de espera para localizar o botão de aceitar cookies expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao clicar no botão de aceitar cookies: {ex.Message}");
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
                // Simula comportamento humano com uma pausa aleatória
                System.Threading.Thread.Sleep(new Random().Next(842, 1471));

                // Captura o elemento principal que contém as informações do jogo
                var gameInfoElement = page.QuerySelectorAsync(".scoreboard-container").GetAwaiter().GetResult();

                if (gameInfoElement == null)
                {
                    Console.WriteLine("Elemento principal de informações do jogo não encontrado.");
                    return;
                }

                // Captura o nome da liga
                var leagueElement = gameInfoElement.QuerySelectorAsync(".scoreboard-tournament-name").GetAwaiter().GetResult();
                if (leagueElement != null)
                {
                    leagueName = "";
                    leagueName = leagueElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    Console.WriteLine($"Liga: {leagueName}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar o nome da liga.");
                }

                // Captura a data e hora do jogo
                var dateTimeElement = gameInfoElement.QuerySelectorAsync(".match-status span.scoreboard-match-date").GetAwaiter().GetResult();
                if (dateTimeElement != null)
                {
                    var dateTimeText = dateTimeElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    var timeText = gameInfoElement.QuerySelectorAsync(".match-status span:not(.scoreboard-match-date)").GetAwaiter().GetResult()
                        ?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                    gameDateTime = default;
                    gameDateTime = ParseGameDateTime($"{dateTimeText}, {timeText}");
                    Console.WriteLine($"Horário do Jogo: {gameDateTime}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar a data e hora do jogo.");
                }

                // Captura os nomes dos times
                var homeTeamElement = gameInfoElement.QuerySelectorAsync(".e2e-scoreboard-home-team-name").GetAwaiter().GetResult();
                var awayTeamElement = gameInfoElement.QuerySelectorAsync(".e2e-scoreboard-away-team-name").GetAwaiter().GetResult();

                if (homeTeamElement != null && awayTeamElement != null)
                {

                    homeTeam = "";
                    awayTeam = "";

                    homeTeam = homeTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    awayTeam = awayTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                    if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam))
                    {
                        _logService.Log($"Um dos times está vazio - Times: {homeTeam} vs {awayTeam} em {gameDateTime}");
                        throw new Exception($"Um dos times está vazio - Times: {homeTeam} vs {awayTeam} em {gameDateTime}");
                    }

                    Console.WriteLine($"Times: {homeTeam} vs {awayTeam}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar os nomes dos times.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao extrair informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao extrair informações do jogo: ", ex);
            }
        }

        private DateTime ParseGameDateTime(string dateTimeText)
        {
            if (string.IsNullOrWhiteSpace(dateTimeText))
                throw new ArgumentException("O parâmetro 'dateTimeText' está vazio ou nulo.");

            try
            {
                var now = DateTime.Now; // Usa o horário local

                // Normaliza entradas removendo repetições como "Amanhã, Amanhã"
                dateTimeText = dateTimeText.Replace("Amanhã, Amanhã", "Amanhã", StringComparison.OrdinalIgnoreCase)
                                           .Replace("Hoje, Hoje", "Hoje", StringComparison.OrdinalIgnoreCase)
                                           .Replace(",", "").Trim();

                // Trata "Hoje" e "Amanhã" diretamente substituindo por datas específicas
                if (dateTimeText.StartsWith("Hoje", StringComparison.OrdinalIgnoreCase))
                {
                    dateTimeText = dateTimeText.Replace("Hoje", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);
                }
                else if (dateTimeText.StartsWith("Amanhã", StringComparison.OrdinalIgnoreCase))
                {
                    dateTimeText = dateTimeText.Replace("Amanhã", now.AddDays(1).ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);
                }

                // Remove vírgulas e espaços redundantes
                dateTimeText = dateTimeText.Replace(",", "").Trim();

                // Tenta parsear o formato direto como "yyyy-MM-dd HH:mm" ou "yyyy-MM-dd H:mm"
                var formats = new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" };
                if (DateTime.TryParseExact(dateTimeText, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    return parsedDate;
                }

                // Divide o texto para formatos do tipo "Fri 10. Jan, 16:45"
                var parts = dateTimeText.Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 4)
                {
                    var monthMappings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Jan", 1 }, { "Feb", 2 }, { "Mar", 3 }, { "Apr", 4 }, { "May", 5 }, { "Jun", 6 },
                { "Jul", 7 }, { "Aug", 8 }, { "Sep", 9 }, { "Oct", 10 }, { "Nov", 11 }, { "Dec", 12 }
            };

                    // Extrai dia, mês e hora
                    if (int.TryParse(parts[1], out var day) && monthMappings.TryGetValue(parts[2], out var month))
                    {
                        var timePart = parts[^1]; // Última parte contém o horário
                        if (TimeSpan.TryParse(timePart, out var parsedTime))
                        {
                            var resultDate = new DateTime(now.Year, month, day, parsedTime.Hours, parsedTime.Minutes, 0);

                            // Ajusta para o próximo ano se a data estiver no passado
                            if (resultDate < now)
                            {
                                resultDate = resultDate.AddYears(1);
                            }

                            return resultDate;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao converter a data: {dateTimeText} - {ex.Message}");
            }

            throw new Exception($"Formato inesperado para 'dateTimeText': {dateTimeText}");
        }



        private void ProcessTabsAndMarketViews(IPage page)
        {
            try
            {
                // Localiza o contêiner principal das abas
                var marketGroupsContainer = page.QuerySelectorAllAsync("div.market-groups").GetAwaiter().GetResult().First();
                if (marketGroupsContainer == null)
                {
                    Console.WriteLine("Contêiner 'market-groups' não encontrado.");
                    return;
                }

                // Localiza o contêiner principal das abas
                var marketGroupsContainers = page.QuerySelectorAllAsync("div.market-groups").GetAwaiter().GetResult();
                if (marketGroupsContainers == null || marketGroupsContainers.Length == 0)
                {
                    Console.WriteLine("Contêiner 'market-groups' não encontrado.");
                    return;
                }

                IElementHandle allTabButton = null;

                // Itera pelos contêineres e procura a aba "Todos"
                foreach (var container in marketGroupsContainers)
                {
                    var items = container.QuerySelectorAllAsync("div.group-selector__item").GetAwaiter().GetResult();
                    foreach (var item in items)
                    {
                        var textContent = item.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                        if (textContent == "Todos")
                        {
                            allTabButton = item;
                            break;
                        }
                    }

                    if (allTabButton != null)
                    {
                        break;
                    }
                }

                if (allTabButton != null)
                {
                    page.EvaluateFunctionAsync(
                        "el => el.click()",
                        allTabButton).GetAwaiter().GetResult();
                    Console.WriteLine("Aba 'Todos' clicada.");

                    // Aguarda o carregamento do grid de eventos
                    page.WaitForSelectorAsync("div.event-grid", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                    ProcessMarketViews(marketGroupsContainer);
                }
                else
                {
                    Console.WriteLine("Aba 'Todos' não encontrada.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar abas e mercados: {ex.Message}");
                throw;
            }
        }

        private void ProcessMarketViews(IElementHandle marketGroupsContainer)
        {
            try
            {
                // Pausa para garantir que os mercados carreguem
                Thread.Sleep(new Random().Next(851, 1132));

                // Captura todos os mercados dentro do contêiner
                var marketContainers = marketGroupsContainer.QuerySelectorAllAsync("div.event-grid__expanded-market").GetAwaiter().GetResult();

                if (marketContainers == null || !marketContainers.Any())
                {
                    Console.WriteLine("Nenhum mercado encontrado dentro do contêiner fornecido.");
                    return;
                }

                Console.WriteLine($"Mercados encontrados: {marketContainers.Length}");

                // Lista de tags cadastradas
                var tagNames = SuperbetTags.TagNames;

                foreach (var market in marketContainers)
                {
                    try
                    {
                        // Centraliza o mercado na tela usando JavaScript
                        try
                        {
                            market.EvaluateFunctionAsync("el => el.scrollIntoView({ behavior: 'smooth', block: 'center' })").GetAwaiter().GetResult();

                            // Aguarda um curto intervalo para garantir que o mercado seja carregado corretamente
                            Thread.Sleep(new Random().Next(11, 144));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao centralizar o mercado: {ex.Message}");
                        }

                        // Captura o título do mercado
                        var titleElement = market.QuerySelectorAsync("div.market-header-base__name div").GetAwaiter().GetResult();
                        var marketTitle = titleElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                        if (string.IsNullOrEmpty(marketTitle))
                        {
                            Console.WriteLine("Título do mercado não encontrado.");
                            continue;
                        }

                        Console.WriteLine($"Processando mercado: {marketTitle}");

                        // Normaliza o título do mercado
                        string normalizedMarketTitle = _teamService.NormalizeText(marketTitle);

                        // Verifica se o mercado está na lista de tags permitidas
                        if (!tagNames.Values.Any(tagList => tagList.Any(tag => _teamService.NormalizeText(tag) == normalizedMarketTitle)))
                        {
                            Console.WriteLine($"Mercado ignorado: {marketTitle}");
                            continue;
                        }

                        // Verifica se o mercado está colapsado
                        var collapseIcon = market.QuerySelectorAsync("div.market-header-base__actions button i[class*='navigation-chevron-down']").GetAwaiter().GetResult();
                        if (collapseIcon != null)
                        {
                            try
                            {
                                // Realiza o clique diretamente no botão de expandir usando JavaScript
                                var expandButton = market.QuerySelectorAsync("div.market-header-base__actions").GetAwaiter().GetResult();
                                if (expandButton != null)
                                {
                                    expandButton.EvaluateFunctionAsync("el => el.click()").GetAwaiter().GetResult();
                                    Thread.Sleep(new Random().Next(433, 872)); // Pausa para permitir o carregamento
                                    Console.WriteLine("Mercado expandido com sucesso ao clicar no botão de expandir.");
                                }
                                else
                                {
                                    Console.WriteLine("Botão de expandir não encontrado.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao tentar expandir o mercado: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Mercado já está expandido.");
                        }



                        // Processa submercados (times ou categorias)
                        var subMarketButtons = market.QuerySelectorAllAsync("div.market-layout-card__team").GetAwaiter().GetResult();
                        if (subMarketButtons != null && subMarketButtons.Length > 0)
                        {
                            foreach (var subMarketButton in subMarketButtons)
                            {
                                var subMarketName = subMarketButton.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                                if (!string.IsNullOrEmpty(subMarketName))
                                {
                                    Console.WriteLine($"Processando submercado: {subMarketName}");
                                    subMarketButton.ClickAsync().GetAwaiter().GetResult();
                                    Thread.Sleep(new Random().Next(313, 618));

                                    // Processa o mercado expandido com o submercado
                                    ProcessExpandedMarket(market, $"{marketTitle} {subMarketName}");
                                }
                            }
                        }
                        else
                        {
                            // Processa o mercado diretamente se não houver submercados
                            ProcessExpandedMarket(market, marketTitle);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar os mercados: {ex.Message}");
            }
        }

        private void ProcessExpandedMarket(IElementHandle market, string marketTitle)
        {
            try
            {
                // Verifica se o mercado está registrado nas tags
                var tagNames = SuperbetTags.TagNames;
                var matchingTag = tagNames.FirstOrDefault(tag => tag.Value.Any(tagValue => _teamService.NormalizeText(tagValue) == _teamService.NormalizeText(marketTitle)));

                if (matchingTag.Key == 0)
                {
                    Console.WriteLine($"Tag não encontrada para o mercado: {marketTitle}");
                    return;
                }

                int tagId = matchingTag.Key;

                // Captura todas as linhas de apostas do mercado
                var betRows = market.QuerySelectorAllAsync("div.market-layout-card__row").GetAwaiter().GetResult();

                if (betRows == null || !betRows.Any())
                {
                    Console.WriteLine($"Nenhuma linha de aposta encontrada no mercado: {marketTitle}");
                    return;
                }

                List<BetInfo> currentBets = new List<BetInfo>();

                foreach (var betRow in betRows)
                {
                    try
                    {
                        // Captura o nome da aposta
                        var betNameElement = betRow.QuerySelectorAsync("div.market-layout-card__row-specifier div").GetAwaiter().GetResult();
                        var betName = betNameElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                        if (string.IsNullOrEmpty(betName))
                        {
                            Console.WriteLine("Nome da aposta não encontrado.");
                            continue;
                        }

                        // Captura os botões de odds
                        var oddButtons = betRow.QuerySelectorAllAsync("div.market-layout-card__odd-container button").GetAwaiter().GetResult();

                        if (oddButtons.Length == 2)
                        {
                            // "Mais de" e "Menos de"
                            ProcessBetOdds(oddButtons[0], "Mais de", betName, marketTitle, tagId, currentBets);
                            ProcessBetOdds(oddButtons[1], "Menos de", betName, marketTitle, tagId, currentBets);
                        }
                        else if (oddButtons.Length == 1)
                        {
                            // Apenas "Mais de"
                            ProcessBetOdds(oddButtons[0], "Mais de", betName, marketTitle, tagId, currentBets);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar uma linha de aposta: {ex.Message}");
                    }
                }

                // Salva as apostas no banco de dados
                if (currentBets.Any())
                {
                    SaveBets(currentBets);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mercado expandido: {ex.Message}");
            }
        }

        private void ProcessBetOdds(IElementHandle button, string overUnder, string betName, string marketTitle, int tagId, List<BetInfo> currentBets)
        {
            try
            {
                var multiplierElement = button.QuerySelectorAsync("span.odd-button__odd-value-placeholder").GetAwaiter().GetResult();
                var multiplierText = multiplierElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                if (decimal.TryParse(multiplierText?.Replace('.', ','), out var multiplier))
                {
                    currentBets.Add(new BetInfo
                    {
                        GamesInfo = gamesInfo,
                        TagName = marketTitle,
                        OverUnder = overUnder,
                        BetAmount = ParseBetAmount(betName),
                        Multiplier = multiplier,
                        GameDate = gamesInfo.GameDate,
                        CaptureDate = DateTime.Now,
                        Site = gamesInfo.Site,
                        TagId = tagId
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar botão de odds: {ex.Message}");
            }
        }

        private decimal ParseBetAmount(string betName)
        {
            // Usa Regex para encontrar valores decimais corretamente
            var match = Regex.Match(betName, @"[+-]?\d+[.,]?\d*");
            if (match.Success)
            {
                // Tenta converter o valor diretamente para decimal, garantindo o formato correto
                if (decimal.TryParse(match.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }
            }
            throw new FormatException($"Formato de nome de aposta inválido: {betName}");
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
