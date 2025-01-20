using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BetSniffer.Api.Core.Sites.Betboom
{
    public class BetboomScraping : IBetScrapingAsyncInterface
    {
        #region VariaveisGlobais

        private string? homeTeam;
        private string? awayTeam;
        private string? leagueName;
        private DateTime? gameDateTime;
        private Site? site;
        private GamesInfo? gamesInfo;
        private readonly GameService? _gameService;

        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly IRepositoryService<GamesInfo>? _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo>? _betInfoRepository;
        private WebScrapingServicePuppeteer? _webScrapingService;
        private ApplicationDbContext dbContext;
        private TeamService teamService;
        private readonly ILogService _logService;

        #endregion

        public BetboomScraping(ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository, IRepositoryService<BetInfo> betInfoRepository)
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

        public BetboomScraping(ApplicationDbContext dbContext, TeamService teamService)
        {
            this.dbContext = dbContext;
            this.teamService = teamService;
        }

        public async Task<List<TagInfo>> ScrapeTags(string url, string siteName)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            }

            if (string.IsNullOrEmpty(siteName))
            {
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));
            }

            site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower()) ?? await AddNewSite(siteName);

            _webScrapingService = new WebScrapingServicePuppeteer();
            _webScrapingService.Initialize();
            using var browser = _webScrapingService;
            var page = browser.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(7873, 10405));

            string cookieAcceptButtonSelector = "#onetrust-accept-btn-handler";

            await HandleCookies(page, cookieAcceptButtonSelector);

            await ExtractGameInfo(page);

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            BetboomTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

            var existingGame = _dbContext.GamesInfo
                .FirstOrDefault(g => g.HomeTeamId == homeTeamDb &&
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
                    GameDate = gameDateTime ?? DateTime.Now,
                    League = leagueName ?? string.Empty,
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
            await _dbContext.SaveChangesAsync();
            Console.WriteLine("Jogo salvo com sucesso.");

            await ProcessTabsAndMarketViews(page);

            Console.WriteLine("Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return new List<TagInfo>();
        }

        public async Task HandleCookies(IPage page, string cookieAcceptButtonSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                // Aguarda um tempo aleatório para simular comportamento humano
                System.Threading.Thread.Sleep(new Random().Next(981, 1758));

                // Localiza o botão de aceitar cookies
                var element = await page.WaitForSelectorAsync(cookieAcceptButtonSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                });

                if (element != null)
                {
                    // Clica no botão de aceitar cookies
                    await element.ClickAsync();
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

        public async Task HandleModal(IPage page, string modalSelector, string closeButtonSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                // Aguarda o modal aparecer na página
                var modal = await page.WaitForSelectorAsync(modalSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds,
                    Visible = true
                });

                if (modal != null)
                {
                    Console.WriteLine("Modal encontrado. Verificando o botão de fechamento...");

                    // Localiza o botão de fechamento dentro do modal
                    var closeButton = await modal.QuerySelectorAsync(closeButtonSelector);

                    if (closeButton != null)
                    {
                        // Clica no botão de fechar o modal
                        await closeButton.ClickAsync();
                        Console.WriteLine("Modal fechado com sucesso.");
                    }
                    else
                    {
                        Console.WriteLine("Botão de fechar não encontrado dentro do modal.");
                    }
                }
                else
                {
                    Console.WriteLine("Modal não encontrado na página.");
                }
            }
            catch (TimeoutException)
            {
                Console.WriteLine("Tempo de espera para localizar o modal expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao lidar com o modal: {ex.Message}");
            }
        }

        private async Task<Site> AddNewSite(string siteName)
        {
            try
            {
                var site = new Site { Name = siteName };
                _dbContext.Site.Add(site);
                if (await _dbContext.SaveChangesAsync() > 0)
                {
                    Console.WriteLine($"Novo site adicionado: {siteName}");
                    return site;
                };

                return null;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private async Task ExtractGameInfo(IPage page)
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
                var leagueElement = await gameInfoElement.QuerySelectorAsync(".scoreboard-tournament-name");
                if (leagueElement != null)
                {
                    leagueName = "";
                    leagueName = await leagueElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    Console.WriteLine($"Liga: {leagueName}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar o nome da liga.");
                }

                // Captura a data e hora do jogo
                var dateTimeElement = await gameInfoElement.QuerySelectorAsync(".match-status span.scoreboard-match-date");
                if (dateTimeElement != null)
                {
                    var dateTimeText = await dateTimeElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    var timeText = await gameInfoElement
                        .QuerySelectorAsync(".match-status span:not(.scoreboard-match-date)")
                        .EvaluateFunctionAsync<string>("el => el.textContent.trim()");

                    gameDateTime = default;
                    gameDateTime = ParseGameDateTime($"{dateTimeText}, {timeText}");
                    Console.WriteLine($"Horário do Jogo: {gameDateTime}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar a data e hora do jogo.");
                }

                // Captura os nomes dos times
                var homeTeamElement = await gameInfoElement.QuerySelectorAsync(".e2e-scoreboard-home-team-name");
                var awayTeamElement = await gameInfoElement.QuerySelectorAsync(".e2e-scoreboard-away-team-name");

                if (homeTeamElement != null && awayTeamElement != null)
                {

                    homeTeam = "";
                    awayTeam = "";

                    homeTeam = await homeTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                    awayTeam = await awayTeamElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()");

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
            {
                throw new ArgumentException("O parâmetro 'dateTimeText' está vazio ou nulo.");
            }

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

        private async Task ProcessTabsAndMarketViews(IPage page)
        {
            try
            {
                // Localiza o contêiner principal das abas
                var marketGroupsContainer = await page.QuerySelectorAllAsync("div.market-groups");
                if (marketGroupsContainer == null)
                {
                    Console.WriteLine("Contêiner 'market-groups' não encontrado.");
                    return;
                }

                // Localiza o contêiner principal das abas
                var marketGroupsContainers = await page.QuerySelectorAllAsync("div.market-groups");
                if (marketGroupsContainers == null || marketGroupsContainers.Length == 0)
                {
                    Console.WriteLine("Contêiner 'market-groups' não encontrado.");
                    return;
                }

                IElementHandle allTabButton = null;

                // Itera pelos contêineres e procura a aba "Todos"
                foreach (var container in marketGroupsContainers)
                {
                    var items = await container.QuerySelectorAllAsync("div.group-selector__item");
                    foreach (var item in items)
                    {
                        var textContent = await item.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
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
                    await page.EvaluateFunctionAsync("el => el.click()", allTabButton);
                    Console.WriteLine("Aba 'Todos' clicada.");

                    // Aguarda o carregamento do grid de eventos
                    await page.WaitForSelectorAsync("div.event-grid", new WaitForSelectorOptions { Timeout = 10000 });
                    await ProcessMarketViews(marketGroupsContainer.FirstOrDefault());
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

        #region ProccessMarketViews
        private async Task ProcessMarketViews(IElementHandle marketGroupsContainer)
        {
            try
            {
                // Pausa para garantir que os mercados carreguem
                Thread.Sleep(new Random().Next(851, 1132));

                // Captura todos os mercados dentro do contêiner
                var marketContainers = await marketGroupsContainer.QuerySelectorAllAsync("div.event-grid__expanded-market");

                if (marketContainers == null || !marketContainers.Any())
                {
                    Console.WriteLine("Nenhum mercado encontrado dentro do contêiner fornecido.");
                    return;
                }

                Console.WriteLine($"Mercados encontrados: {marketContainers.Length}");

                // Lista de tags cadastradas
                var tagNames = BetboomTags.TagNames;

                foreach (var market in marketContainers)
                {
                    await ProccessBetMarkets(tagNames, market);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar os mercados: {ex.Message}");
            }
        }

        private async Task ProccessBetMarkets(Dictionary<int, List<string>> tagNames, IElementHandle? market)
        {
            try
            {
                // Centraliza o mercado na tela usando JavaScript
                await CenterBetMarketOnScreen(market);

                // Captura o título do mercado
                var titleElement = await market.QuerySelectorAsync("div.market-header-base__name div");
                var marketTitle = await titleElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()");

                if (string.IsNullOrEmpty(marketTitle))
                {
                    Console.WriteLine("Título do mercado não encontrado.");
                    return;
                }

                Console.WriteLine($"Processando mercado: {marketTitle}");

                // Normaliza o título do mercado
                string normalizedMarketTitle = _teamService.NormalizeText(marketTitle);

                // Verifica se o mercado está na lista de tags permitidas
                if (!tagNames.Values.Any(tagList => tagList.Any(tag => _teamService.NormalizeText(tag) == normalizedMarketTitle)))
                {
                    Console.WriteLine($"Mercado ignorado: {marketTitle}");
                    return;
                }

                // Verifica se o mercado está colapsado
                var collapseIcon = await market.QuerySelectorAsync("div.market-header-base__actions button i[class*='navigation-chevron-down']");
                if (collapseIcon != null)
                {
                    await ExpandButtonClick(market);
                }
                else
                {
                    Console.WriteLine("Mercado já está expandido.");
                }

                // Verifica se o botão "MOSTRAR MAIS" está presente e clica
                var showMoreButton = await market.QuerySelectorAsync("button.show-more-market-lines-toggle");
                if (showMoreButton != null)
                {
                    await CenterShowMoreButtonOnScreen(showMoreButton);
                }
                else
                {
                    Console.WriteLine("Botão 'MOSTRAR MAIS' não encontrado.");
                }

                // Processa submercados (times ou categorias)
                var subMarketButtons = await market.QuerySelectorAllAsync("div.market-layout-card__team");
                if (subMarketButtons != null && subMarketButtons.Length > 0)
                {
                    foreach (var subMarketButton in subMarketButtons)
                    {
                        await SubMarketProccess(market, marketTitle, subMarketButton);
                    }
                }
                else
                {
                    // Processa o mercado diretamente se não houver submercados
                    await ProcessExpandedMarket(market, marketTitle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
            }
        }

        private async Task SubMarketProccess(IElementHandle? market, string marketTitle, IElementHandle? subMarketButton)
        {
            try
            {
                // Captura o nome do submercado
                var subMarketName = await subMarketButton.EvaluateFunctionAsync<string>("el => el.textContent.trim()");
                if (!string.IsNullOrEmpty(subMarketName))
                {
                    Console.WriteLine($"Processando submercado: {subMarketName}");

                    // Realiza até 3 tentativas para clicar no submercado
                    bool isClicked = false;
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        isClicked = await ClickOnSubMarket(subMarketButton, subMarketName, isClicked, attempt);
                    }

                    // Se o botão não for clicado após 3 tentativas, registra o erro
                    if (!isClicked)
                    {
                        Console.WriteLine($"Falha ao clicar no submercado '{subMarketName}' após 3 tentativas. Ignorando este submercado.");
                        return;
                    }

                    // Processa o mercado expandido com o submercado
                    await ProcessExpandedMarket(market, $"{marketTitle} {subMarketName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar submercado: {ex.Message}");
            }
        }

        private async Task<bool> ClickOnSubMarket(IElementHandle? subMarketButton, string subMarketName, bool isClicked, int attempt)
        {
            try
            {
                // Centraliza o botão do submercado na tela antes do clique
                await subMarketButton.EvaluateFunctionAsync("el => el.scrollIntoView({ behavior: 'smooth', block: 'center' })");

                // Aguarda um curto intervalo para garantir que o elemento esteja em foco
                Thread.Sleep(new Random().Next(51, 158));

                // Clica no botão do submercado
                subMarketButton.EvaluateFunctionAsync("el => el.click()").GetAwaiter().GetResult();

                // Aguarda um curto intervalo para verificar se o botão foi clicado
                Thread.Sleep(new Random().Next(311, 722));

                // Verifica se o botão foi marcado como clicado
                var isSelected = await subMarketButton.EvaluateFunctionAsync<bool>("el => el.hasAttribute('data-is-team-selected')");
                if (isSelected)
                {
                    Console.WriteLine($"Submercado '{subMarketName}' clicado com sucesso na tentativa {attempt}.");
                    isClicked = true;
                    await Task.CompletedTask;
                }
                else
                {
                    Console.WriteLine($"Submercado '{subMarketName}' não foi clicado na tentativa {attempt}. Tentando novamente...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tentar clicar no submercado '{subMarketName}' na tentativa {attempt}: {ex.Message}");
            }

            return isClicked;
        }

        private async Task CenterShowMoreButtonOnScreen(IElementHandle showMoreButton)
        {
            try
            {
                // Centraliza o botão "MOSTRAR MAIS" na tela antes do clique
                await showMoreButton.EvaluateFunctionAsync("el => el.scrollIntoView({ behavior: 'smooth', block: 'center' })");

                // Aguarda um curto intervalo para garantir que o botão esteja visível e clicável
                Thread.Sleep(new Random().Next(151, 322));

                // Clica no botão para expandir mais mercados
                await showMoreButton.EvaluateFunctionAsync("el => el.click()");

                // Aguarda o carregamento das linhas adicionais
                Thread.Sleep(new Random().Next(500, 1000));

                Console.WriteLine("Botão 'MOSTRAR MAIS' clicado e linhas adicionais carregadas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao clicar no botão 'MOSTRAR MAIS': {ex.Message}");
            }
        }

        private async Task ExpandButtonClick(IElementHandle? market)
        {
            try
            {
                // Realiza o clique diretamente no botão de expandir usando JavaScript
                var expandButton = await market!.QuerySelectorAsync("div.market-header-base__actions");
                if (expandButton != null)
                {
                    await expandButton.EvaluateFunctionAsync("el => el.click()");
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

        private async Task CenterBetMarketOnScreen(IElementHandle? market)
        {
            try
            {
                await market.EvaluateFunctionAsync("el => el.scrollIntoView({ behavior: 'smooth', block: 'center' })");

                // Aguarda um curto intervalo para garantir que o mercado seja carregado corretamente
                Thread.Sleep(new Random().Next(11, 144));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao centralizar o mercado: {ex.Message}");
            }
        }

        private async Task ProcessExpandedMarket(IElementHandle market, string marketTitle)
        {
            try
            {
                // Verifica se o mercado está registrado nas tags
                var tagNames = BetboomTags.TagNames;
                var matchingTag = tagNames.FirstOrDefault(tag => tag.Value.Any(tagValue => _teamService.NormalizeText(tagValue) == _teamService.NormalizeText(marketTitle)));

                if (matchingTag.Key == 0)
                {
                    Console.WriteLine($"Tag não encontrada para o mercado: {marketTitle}");
                    return;
                }

                int tagId = matchingTag.Key;

                // Captura todas as linhas de apostas do mercado
                var betRows = await market.QuerySelectorAllAsync("div.market-layout-card__row");

                if (betRows == null || !betRows.Any())
                {
                    Console.WriteLine($"Nenhuma linha de aposta encontrada no mercado: {marketTitle}");
                    return;
                }

                List<BetInfo> currentBets = new List<BetInfo>();

                foreach (var betRow in betRows)
                {
                    await GetBetMarketName(marketTitle, tagId, currentBets, betRow);
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

        private async Task GetBetMarketName(string marketTitle, int tagId, List<BetInfo> currentBets, IElementHandle? betRow)
        {
            try
            {
                // Captura o nome da aposta
                var betNameElement = await betRow.QuerySelectorAsync("div.market-layout-card__row-specifier div");
                var betName = await betNameElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()");

                if (string.IsNullOrEmpty(betName))
                {
                    Console.WriteLine("Nome da aposta não encontrado.");
                    return;
                }

                // Captura os botões de odds
                var oddButtons = await betRow.QuerySelectorAllAsync("div.market-layout-card__odd-container button");

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
        #endregion

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

            foreach (var betKey in betKeys)
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
