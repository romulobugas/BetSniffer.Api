using BetSniffer.Api.Models;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Pixbet;
using PuppeteerSharp;
using System.Globalization;
using Microsoft.OpenApi.Services;

namespace BetSniffer.Api.Core.Sites.Bet365
{
    public class PixbetScraping : IScrapingService
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

            // Confirmação de idade
            //ConfirmAgeVerification(page, ageVerification); //Comentado pois na implementação atual não aparece o pop-up da idade.

            ExtractGameInfo(page);

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            PixbetTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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

        private void ProcessTabsAndMarketViews(IPage page)
        {
            try
            {
                // Localiza o contêiner principal das abas
                var marketTabsContainer = page.QuerySelectorAsync("div.eventpage_fe_Layout_marketTabsWrapper").GetAwaiter().GetResult();
                if (marketTabsContainer == null)
                {
                    Console.WriteLine("Contêiner de abas de mercados não encontrado.");
                    return;
                }

                // Localiza o botão "Todos os mercados"
                var allMarketsButton = marketTabsContainer.QuerySelectorAllAsync("li.components-fe_Carousel_item span").GetAwaiter().GetResult()
                    .FirstOrDefault(tab => tab.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult() == "Todos os mercados");

                if (allMarketsButton == null)
                {
                    Console.WriteLine("Botão 'Todos os mercados' não encontrado.");
                    return;
                }

                // Realiza o clique no botão "Todos os mercados" usando JavaScript
                allMarketsButton.EvaluateFunctionAsync("el => el.click()").GetAwaiter().GetResult();
                Console.WriteLine("Botão 'Todos os mercados' clicado com sucesso.");

                // Aguarda o carregamento do grid de eventos
                System.Threading.Thread.Sleep(new Random().Next(873, 1405));                
                Console.WriteLine("Grid de eventos carregado.");

                // Chama o método para processar as visualizações de mercado
                ProcessMarketViews(page);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar abas e mercados: {ex.Message}");
                throw;
            }
        }

        private void ProcessMarketViews(IPage page)
        {
            try
            {
                // Localiza todas as divs com mercados já expandidos
                var expandedMarketDivs = page.QuerySelectorAllAsync("div.eventpage_fe_Markets_expanded").GetAwaiter().GetResult();
                if (expandedMarketDivs == null || expandedMarketDivs.Length == 0)
                {
                    Console.WriteLine("Nenhum mercado expandido encontrado.");
                    return;
                }

                foreach (var marketDiv in expandedMarketDivs)
                {
                    try
                    {
                        // Centraliza o mercado na tela usando JavaScript
                        try
                        {
                            marketDiv.EvaluateFunctionAsync("el => el.scrollIntoView({ behavior: 'smooth', block: 'center' })").GetAwaiter().GetResult();

                            // Aguarda um curto intervalo para garantir que o mercado seja carregado corretamente
                            Thread.Sleep(new Random().Next(411, 622));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao centralizar o mercado: {ex.Message}");
                        }

                        // Captura o nome do mercado
                        var marketNameElement = marketDiv.QuerySelectorAsync("h3.eventpage_fe_Markets_marketName").GetAwaiter().GetResult();
                        var marketNameRaw = marketNameElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                        var marketName = marketNameRaw?.Split('|')[0].Trim();

                        if (string.IsNullOrEmpty(marketName))
                        {
                            Console.WriteLine("Nome do mercado não encontrado.");
                            continue;
                        }

                        Console.WriteLine($"Processando mercado: {marketName}");

                        // Verifica se o mercado está registrado nas tags do Pixbet
                        var tagNames = PixbetTags.TagNames;
                        var matchingTag = tagNames.FirstOrDefault(tag => tag.Value.Any(tagValue => _teamService.NormalizeText(tagValue) == _teamService.NormalizeText(marketName)));

                        if (matchingTag.Key == 0)
                        {
                            Console.WriteLine($"Tag não encontrada para o mercado: {marketName}");
                            continue;
                        }

                        int tagId = matchingTag.Key;

                        

                        // Captura as seleções dentro do mercado expandido
                        var selectionRows = marketDiv.QuerySelectorAllAsync("button.eventpage_fe_HandicapSelection_line, button.eventpage_fe_OverUnderSelection_line").GetAwaiter().GetResult();
                        if (selectionRows == null || selectionRows.Length == 0)
                        {
                            Console.WriteLine($"Nenhuma seleção encontrada no mercado: {marketName}");
                            continue;
                        }

                        List<BetInfo> currentBets = new List<BetInfo>();

                        foreach (var selection in selectionRows)
                        {
                            try
                            {
                                // Captura o nome ou significado (Mais de / Menos de ou nome do time)
                                var meaningElement = selection.QuerySelectorAsync("div.eventpage_fe_HandicapSelection_name, span[title]").GetAwaiter().GetResult();
                                var meaning = meaningElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                // Captura os pontos (Ex: 0.5, -0.25, 0)
                                var pointsSpan = selection.QuerySelectorAsync("span.eventpage_fe_HandicapSelection_points, span.eventpage_fe_OverUnderSelection_points").GetAwaiter().GetResult();
                                var points = pointsSpan?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                // Captura as odds
                                var oddsSpan = selection.QuerySelectorAsync("span.eventpage_fe_HandicapSelection_odds, span.eventpage_fe_OverUnderSelection_odds").GetAwaiter().GetResult();
                                var odds = oddsSpan?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                if (!string.IsNullOrEmpty(meaning) && !string.IsNullOrEmpty(points) && !string.IsNullOrEmpty(odds))
                                {
                                    Console.WriteLine($"Seleção: {meaning} {points} | Odds: {odds}");

                                    // Adiciona as odds processadas à lista de apostas
                                    ProcessBetOdds(odds, meaning, points, marketName, tagId, currentBets);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar uma seleção: {ex.Message}");
                            }
                        }

                        // Salva as apostas após processar todos os mercados
                        SaveBets(currentBets);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar mercado: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar mercados: {ex.Message}");
            }
        }

        private void ProcessBetOdds(string odds, string overUnder, string betName, string marketTitle, int tagId, List<BetInfo> currentBets)
        {
            try
            {
                if (decimal.TryParse(odds?.Replace('.', ','), out var multiplier))
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
                throw;
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
                try
                {
                    var existingBets = _dbContext.BetInfo.Where(b =>
                                        b.GamesInfo.GameId == betKey.GamesInfo.GameId &&
                                        b.TagName == betKey.TagName &&
                                        b.OverUnder == betKey.OverUnder &&
                                        b.TagId == betKey.TagId &&
                                        b.Site.SiteId == betKey.Site.SiteId).ToList();

                    if (existingBets.Count > 0)
                    {
                        Console.WriteLine($"Aposta existente encontrada. Removendo duplicadas...");
                        _dbContext.BetInfo.RemoveRange(existingBets);
                        Console.WriteLine($"Removidas {existingBets.Count} apostas duplicadas.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao verificar ou remover apostas existentes: {ex.Message}");
                }
            }

            try
            {
                // Adiciona as novas apostas
                _dbContext.BetInfo.AddRange(bets);
                _dbContext.SaveChanges();

                Console.WriteLine($"Salvas {bets.Count} novas apostas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar novas apostas: {ex.Message}");
                throw;
            }
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

        // Atualize o método ExtractGameInfo
        private void ExtractGameInfo(IPage page)
        {
            try
            {
                // Captura o container principal do jogo
                var gameContainer = page.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_gameContainer").GetAwaiter().GetResult();
                if (gameContainer == null)
                {
                    throw new Exception("Container do jogo não encontrado.");
                }

                // Captura o nome do time da casa
                var homeTeamElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_homeTeam > span").GetAwaiter().GetResult();
                homeTeam = homeTeamElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                // Captura o nome do time visitante
                var awayTeamElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_awayTeam > span").GetAwaiter().GetResult();
                awayTeam = awayTeamElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                // Captura a data e horário do jogo
                var gameDateElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_startDate").GetAwaiter().GetResult();
                var gameDateText = gameDateElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                // Captura o nome da liga
                var leagueElement = gameContainer.QuerySelectorAsync("div.eventpage_fe_UpcomingScoreboard_leagueName > span").GetAwaiter().GetResult();
                leagueName = leagueElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                // Parsea a data e hora do jogo
                if (!string.IsNullOrEmpty(gameDateText))
                {
                    gameDateTime = ParseGameDateTime(gameDateText);
                }

                Console.WriteLine($"Liga: {leagueName}");
                Console.WriteLine($"Times: {homeTeam} vs {awayTeam}");
                Console.WriteLine($"Data e Hora: {gameDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao capturar informações do jogo: {ex.Message}");
            }
        }

        private DateTime ParseGameDateTime(string dateTimeText)
        {
            if (string.IsNullOrWhiteSpace(dateTimeText))
                throw new ArgumentException("O parâmetro 'dateTimeText' está vazio ou nulo.");

            try
            {
                // Remove o dia da semana e espaços desnecessários
                string cleanedDate = Regex.Replace(dateTimeText, @"^[a-zA-ZÀ-ÿ\-]+, ", "").Trim();

                // Divide a data e hora para facilitar o processamento
                var parts = cleanedDate.Split(",");
                if (parts.Length != 2)
                    throw new Exception("Formato inválido para a data e hora.");

                // Processa a parte da data (d/MM)
                var datePart = parts[0].Trim();
                if (!DateTime.TryParseExact(datePart, "d/MM", null, System.Globalization.DateTimeStyles.None, out var date))
                    throw new Exception("Data inválida.");

                // Processa a parte da hora (HH:mm)
                var timePart = parts[1].Trim();
                if (!TimeSpan.TryParse(timePart, out var time))
                    throw new Exception("Hora inválida.");

                // Combina a data e a hora e ajusta o ano, se necessário
                var currentYear = DateTime.Now.Year;
                var fullDate = new DateTime(currentYear, date.Month, date.Day, time.Hours, time.Minutes, 0);

                if (fullDate < DateTime.Now)
                    fullDate = fullDate.AddYears(1);

                return fullDate;
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao converter a data: {dateTimeText} - {ex.Message}");
            }
        }        
    }
}
