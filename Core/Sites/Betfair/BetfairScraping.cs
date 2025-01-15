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

namespace BetSniffer.Api.Core.Sites.Betfair
{
    public class BetfairScraping : IScrapingService
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

            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ??
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

            ExtractGameInfo(page);

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            BetfairTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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
                    League = leagueName,
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


            ProcessTabsAndMarketViews(page);




            Console.WriteLine("Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return new List<TagInfo>();
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
                // Aguarda um tempo aleatório para simular comportamento humano
                System.Threading.Thread.Sleep(new Random().Next(842, 1471));

                // Captura o elemento principal que contém as informações do jogo
                var gameInfoElement = page.QuerySelectorAsync("div > div > a > div > section").GetAwaiter().GetResult();

                if (gameInfoElement == null)
                {
                    Console.WriteLine("Elemento principal de informações do jogo não encontrado.");
                    return;
                }

                // Tenta capturar o nome da liga na estrutura antiga
                var leagueElement = gameInfoElement.QuerySelectorAsync("section > div > div > section > div > div:nth-child(2) > span").GetAwaiter().GetResult();

                if (leagueElement == null)
                {
                    // Se a estrutura antiga não for encontrada, tenta a nova estrutura
                    Console.WriteLine("Estrutura antiga não encontrada, tentando a nova estrutura...");
                    leagueElement = gameInfoElement.QuerySelectorAsync("div > div:nth-child(2) > span").GetAwaiter().GetResult();
                }

                if (leagueElement != null)
                {
                    // Obtém o texto do elemento
                    leagueName = leagueElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    Console.WriteLine($"Liga: {leagueName}");
                }
                else
                {
                    // Caso nenhuma estrutura seja encontrada, lança uma exceção
                    throw new Exception("Não foi possível capturar o nome da liga em nenhuma das estruturas.");
                }


                // Captura a data e hora do jogo
                var dateTimeElement = gameInfoElement.QuerySelectorAsync("section > div > div > section > section > div:nth-child(1) > div > div > time").GetAwaiter().GetResult();

                if (dateTimeElement == null)
                {
                    // Caso a estrutura antiga não funcione, tenta a nova estrutura
                    Console.WriteLine("Estrutura antiga para data e hora não encontrada, tentando a nova estrutura...");
                    dateTimeElement = gameInfoElement.QuerySelectorAsync("div > section > section > div:nth-child(2)").GetAwaiter().GetResult();
                }

                if (dateTimeElement == null)
                {
                    // Segunda tentativa com outro padrão baseado na nova estrutura
                    Console.WriteLine("Tentando capturar a data e hora em uma estrutura adicional...");
                    dateTimeElement = gameInfoElement.QuerySelectorAsync("section > div > section > div:nth-child(2)").GetAwaiter().GetResult();
                }

                if (dateTimeElement != null)
                {
                    // Obtém o texto da data e hora
                    var gameDateTimeText = dateTimeElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    gameDateTime = ParseGameDateTime(gameDateTimeText);
                    Console.WriteLine($"Horário do Jogo: {gameDateTime}");
                }
                else
                {
                    // Lança uma exceção caso nenhuma estrutura seja encontrada
                    throw new Exception("Não foi possível capturar a data e hora do jogo em nenhuma das estruturas.");
                }

                // Tentativa de captura pela estrutura antiga
                var homeTeamElement = gameInfoElement.QuerySelectorAsync("section > div > div > section > section > div:nth-child(2) > div:nth-child(1) > span > p").GetAwaiter().GetResult();
                var awayTeamElement = gameInfoElement.QuerySelectorAsync("section > div > div > section > section > div:nth-child(2) > div:nth-child(3) > span > p").GetAwaiter().GetResult();

                if (homeTeamElement == null || awayTeamElement == null)
                {
                    // Estrutura antiga não encontrada, tenta capturar pela nova estrutura
                    Console.WriteLine("Estrutura antiga para nomes dos times não encontrada, tentando a nova estrutura...");
                    var teamsElement = gameInfoElement.QuerySelectorAsync("section > div > section > div:nth-child(3)").GetAwaiter().GetResult();

                    if (teamsElement != null)
                    {
                        // Obtém o texto do elemento contendo os dois times
                        var teamsText = teamsElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                        // Divide os nomes dos times pelo separador " x "
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
                    // Captura os textos dos times a partir da estrutura antiga
                    homeTeam = homeTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    awayTeam = awayTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                }

                if (!string.IsNullOrEmpty(homeTeam) && !string.IsNullOrEmpty(awayTeam))
                {
                    Console.WriteLine($"Times: {homeTeam} vs {awayTeam}");
                }
                else
                {
                    // Lança uma exceção caso nenhum dos padrões funcione
                    throw new Exception("Não foi possível capturar os nomes dos times em nenhuma das estruturas.");
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao extrair informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao extrair informações do jogo: ", ex);
                _webScrapingService.Dispose();
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
                var parts = dateTimeText.Split(new[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);

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

                        if (tabs == null || !tabs.Any())
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

                                // Role até o final da página pressionando "PG DOWN" 15 vezes
                                for (int i = 0; i < 15; i++)
                                {
                                    page.Keyboard.PressAsync("PageDown").GetAwaiter().GetResult();
                                    System.Threading.Thread.Sleep(new Random().Next(398, 575)); // Pausa entre os comandos
                                    page.FocusAsync("body").GetAwaiter().GetResult(); // Garante o foco no corpo da página
                                }

                                System.Threading.Thread.Sleep(new Random().Next(821, 1277)); // Aguarda o carregamento

                                // Role de volta ao topo pressionando "PG UP" 15 vezes
                                for (int i = 0; i < 15; i++)
                                {
                                    page.Keyboard.PressAsync("PageUp").GetAwaiter().GetResult();
                                    System.Threading.Thread.Sleep(new Random().Next(357, 578)); // Pausa entre os comandos
                                    page.FocusAsync("body").GetAwaiter().GetResult(); // Garante o foco no corpo da página
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
                                if (allTabPanels == null || !allTabPanels.Any())
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

                                if (marketContainers != null && marketContainers.Any())
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
                                                    if (buttonText == "Casa" || buttonText == "Fora" || buttonText == "Tempo regulamentar" || buttonText == "Ambos os times" || buttonText == "Total")
                                                    {
                                                        Console.WriteLine($"Clicando no submercado: {buttonText}");
                                                        subMarketButton.EvaluateFunctionAsync(@"el => el.click()").GetAwaiter().GetResult();
                                                        subMarketButton.FocusAsync().GetAwaiter().GetResult(); // Garante o foco no elemento ou aba
                                                        System.Threading.Thread.Sleep(new Random().Next(51, 820));
                                                        Console.WriteLine($"Submercado '{buttonText}' clicado.");

                                                        // Verifica se o botão de "Mostrar mais" está presente e clica
                                                        var buttons = market.QuerySelectorAllAsync("button").GetAwaiter().GetResult();
                                                        if (buttons != null && buttons.Any())
                                                        {
                                                            foreach (var button in buttons)
                                                            {
                                                                try
                                                                {
                                                                    var buttonTextExpanded = button.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                                                                    if (buttonTextExpanded == "Mostrar mais")
                                                                    {
                                                                        System.Threading.Thread.Sleep(new Random().Next(845, 1627));
                                                                        button.EvaluateFunctionAsync(@"el => el.click()").GetAwaiter().GetResult();
                                                                        button.FocusAsync().GetAwaiter().GetResult(); // Garante o foco no elemento ou aba
                                                                        page.FocusAsync("body").GetAwaiter().GetResult(); // Garante o foco no corpo da página
                                                                        System.Threading.Thread.Sleep(new Random().Next(845, 1627));
                                                                        Console.WriteLine("Botão 'Mostrar mais' clicado com sucesso.");
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

                                                        if(buttonText != "Casa" && buttonText != "Fora")
                                                        {
                                                            buttonText = "";
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
                Thread.Sleep(new Random().Next(851, 1132));

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



                if (betRows == null || !betRows.Any())
                {
                    Console.WriteLine("Nenhuma linha de aposta encontrada.");
                    return;
                }

                Console.WriteLine($"Linhas de apostas encontradas: {betRows.Length}");

                List<BetInfo> currentBets = new List<BetInfo>();

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
                        var match = Regex.Match(betName, @"\d+[.,]?\d*");
                        if (!match.Success)
                        {
                            Console.WriteLine($"Formato de nome de aposta inesperado: {betName}");
                            continue;
                        }

                        // Converte o valor numérico para o formato com ponto como separador decimal
                        var numericBetName = match.Value.Replace(",", ".");

                        Console.WriteLine($"Valor numérico extraído: {numericBetName}");


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
                if (currentBets.Any())
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
