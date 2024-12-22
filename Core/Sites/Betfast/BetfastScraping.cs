using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Betano;
using Microsoft.OpenApi.Services;

namespace BetSniffer.Api.Core.Sites.Betfast
{
    public class BetfastScraping : IScrapingService
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

        public BetfastScraping(
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

            var visitedGames = new HashSet<string>(); // Armazena IDs ou texto identificador dos jogos

            // Aguarda o iframe inicial carregar
            var iframeSelector = "iframe"; // Ajuste para o seletor correto do iframe
            var iframe = _webScrapingService.GetFrameBySelector(iframeSelector);

            if (iframe == null)
            {
                throw new Exception("Iframe inicial não foi encontrado.");
            }

            string popupSelector = ".overlay.new-message.visible .popup span.close";

            _gameService.ClosePopup(page,popupSelector);

            // Captura a lista de jogos dentro do iframe
            var gameListSelector = "ul.match-list > li";
            var gameItems = iframe.QuerySelectorAllAsync(gameListSelector).GetAwaiter().GetResult();

            if (gameItems == null || gameItems.Length == 0)
            {
                throw new Exception("Nenhum jogo foi encontrado na lista.");
            }

            Console.WriteLine($"Total de jogos encontrados: {gameItems.Length}");

            for (int i = 0; i < gameItems.Length; i++)
            {
                var gameItem = gameItems[i];

                // Identificador único para evitar duplicação (Ex: combinação de time e horário)
                var gameText = gameItem.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                if (visitedGames.Contains(gameText))
                    continue;

                visitedGames.Add(gameText);

                Console.WriteLine($"Processando jogo {i + 1}/{gameItems.Length}: {gameText}");

                // Rolagem até o elemento e clique no jogo
                _webScrapingService.ScrollToElement(iframe, gameItem);
                _webScrapingService.ClickWithJavaScript(iframe, gameItem);

                // Aguarda o iframe do conteúdo do jogo ser carregado
                System.Threading.Thread.Sleep(new Random().Next(873, 1405));
                var innerIframeSelector = "iframe"; // Ajuste para o seletor correto do iframe do jogo
                var innerIframe = _webScrapingService.GetFrameBySelector(innerIframeSelector);

                if (innerIframe != null)
                {
                    Console.WriteLine("Entrando no iframe do jogo e coletando informações...");
                    ExtractGameInfo(innerIframe); // Extrai informações dentro do iframe
                }
                else
                {
                    Console.WriteLine("Erro: Iframe do jogo não encontrado.");
                }

                if (string.IsNullOrEmpty(_teamService.NormalizeText(homeTeam)) || string.IsNullOrEmpty(_teamService.NormalizeText(awayTeam))) 
                {
                    continue;
                }

                // Inicializa informações do jogo
                var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
                var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);



                BetfastTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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

                ProcessTabsAndMarketViews(iframe);

                // Retorno à tela anterior (botão de retorno dentro do iframe principal)
                _webScrapingService.GoBack();
                Console.WriteLine("Retornando à lista de jogos...");
                
                // Recarrega a lista de jogos dentro do iframe após retornar
                gameItems = iframe.QuerySelectorAllAsync(gameListSelector).GetAwaiter().GetResult();
            }

            Console.WriteLine("Processo de raspagem concluído.");
            _webScrapingService.Dispose();

            return new List<TagInfo>();
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

        private void ExtractGameInfo(IFrame frame)
        {
            try
            {
                // Pausa aleatória para simular comportamento humano
                System.Threading.Thread.Sleep(new Random().Next(1921, 3224));

                // Captura o elemento pai da estrutura
                var matchInfoElement = frame.WaitForSelectorAsync("div[sport='soccer'].match-info", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();

                if (matchInfoElement == null)
                    throw new Exception("O elemento pai da estrutura 'match-info' não foi encontrado.");

                // Captura o primeiro <p> que contém as informações
                var leagueElement = matchInfoElement.QuerySelectorAsync("p:first-child").GetAwaiter().GetResult();

                if (leagueElement != null)
                {
                    // Captura todos os spans dentro do <p>
                    var spans = leagueElement.QuerySelectorAllAsync("span").GetAwaiter().GetResult();

                    if (spans.Length > 1) // Certifique-se de que há pelo menos dois spans
                    {
                        // Captura o texto dos spans necessários e remove vírgulas no final
                        var leagueParts = spans
                            .Skip(1) // Ignorar o primeiro <span> (geralmente o ícone ou flag)
                            .Select(span => span.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult())
                            .Where(part => !string.IsNullOrWhiteSpace(part))
                            .Select(part => part.TrimEnd(',')) // Remove vírgulas no final de cada parte
                            .ToList();

                        // Junta os textos capturados em uma única string
                        string currentLeagueName = string.Join(" ", leagueParts);

                        leagueName = "";

                        leagueName = currentLeagueName;

                        Console.WriteLine($"Liga: {leagueName}");
                    }
                    else
                    {
                        throw new Exception("Não foi possível capturar as informações de liga.");
                    }
                }
                else
                {
                    throw new Exception("O elemento <p> com informações de liga não foi encontrado.");
                }



                // Captura o nome dos times no segundo <p>
                var teamsElement = matchInfoElement.QuerySelectorAsync("p:nth-child(2)").GetAwaiter().GetResult();

                if (teamsElement != null)
                {
                    var teamsText = teamsElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    var teams = teamsText.Split('-'); // Separador de times

                    homeTeam = "";
                    awayTeam = "";

                    if (teams.Length == 2)
                    {
                        homeTeam = teams[0].Trim();
                        awayTeam = teams[1].Trim();
                        Console.WriteLine($"Time Casa: {homeTeam}, Time Visitante: {awayTeam}");
                    }
                    else
                    {
                        throw new Exception("Formato inesperado ao capturar os nomes dos times.");
                    }
                }


                // Validação: interrompe o processamento se os nomes dos times estiverem vazios
                if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam))
                {
                    Console.WriteLine("Nomes dos times inválidos. Pulando para o próximo jogo.");
                    homeTeam = null; // Zera para evitar propagação errada
                    awayTeam = null; // Zera para evitar propagação errada
                    return;
                }

                Console.WriteLine($"Time Casa: {homeTeam}, Time Visitante: {awayTeam}");

                // Captura a data e hora do jogo no terceiro <p>
                var dateTimeElement = matchInfoElement.QuerySelectorAsync("p:nth-child(3)").GetAwaiter().GetResult();

                if (dateTimeElement != null)
                {
                    var gameDateTimeText = dateTimeElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                    if (!string.IsNullOrWhiteSpace(gameDateTimeText))
                    {
                        gameDateTime = ParseGameDateTime(gameDateTimeText);
                        Console.WriteLine($"Data e Hora do Jogo: {gameDateTime}");
                    }
                    else
                    {
                        throw new Exception("Não foi possível capturar a data e hora do jogo.");
                    }
                }
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Erro ao capturar elementos: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao processar informações do jogo: ", ex);
            }
        }


        private DateTime ParseGameDateTime(string dateTimeText)
        {
            if (string.IsNullOrWhiteSpace(dateTimeText))
                throw new ArgumentException("O parâmetro 'dateTimeText' está vazio ou nulo.");

            try
            {
                // Remove o dia da semana para facilitar o parsing
                string pattern = @"^[a-zA-ZÀ-ÿ\-]+, ";
                var cleanedDate = Regex.Replace(dateTimeText, pattern, "").Trim();

                // Define o formato esperado para o texto de data e hora
                const string format = "d MMMM HH:mm yyyy"; // Formato sem o dia da semana

                // Adiciona o ano atual à string de data
                var currentYear = DateTime.Now.Year;
                var fullDateTimeText = $"{cleanedDate} {currentYear}";

                // Força a cultura "pt-BR" para parsing correto do mês por extenso
                var culture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

                // Tenta parsear a string usando o formato esperado
                if (DateTime.TryParseExact(fullDateTimeText, format, culture, System.Globalization.DateTimeStyles.None, out var parsedDateTime))
                {
                    // Se a data está no passado, ajusta para o próximo ano
                    if (parsedDateTime < DateTime.Now)
                    {
                        fullDateTimeText = $"{cleanedDate} {currentYear + 1}";
                        if (DateTime.TryParseExact(fullDateTimeText, format, culture, System.Globalization.DateTimeStyles.None, out var nextYearParsed))
                        {
                            return nextYearParsed;
                        }
                    }

                    return parsedDateTime;
                }
            }
            catch (Exception ex)
            {
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

        private void ProcessTabsAndMarketViews(IFrame iframe)
        {
            var ignoredTabs = new HashSet<string> { "Criar Aposta" }; // Abas ignoradas
            var processedTabs = new HashSet<string>(); // Rastreia abas já processadas
            var marketCategoriesSelector = "div.categories-wrapper div.market-categories > ul > li";
            var rightArrowSelector = "div.mi-right-arrow";

            System.Threading.Thread.Sleep(new Random().Next(625, 1684)); // Pequena pausa inicial

            try
            {
                bool finished = false;

                while (!finished)
                {
                    try
                    {
                        // Captura as abas visíveis
                        var tabs = iframe.QuerySelectorAllAsync(marketCategoriesSelector).GetAwaiter().GetResult();

                        bool allTabsProcessed = true; // Assume que todas as abas estão processadas
                        bool retryRightArrow = false; // Tenta clicar no botão da direita se necessário

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
                                        tab.ClickAsync().GetAwaiter().GetResult();
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
                                    allTabsProcessed = false; // Não processou todas as abas
                                    continue;
                                }

                                // Marca a aba como processada
                                processedTabs.Add(tabName);

                                // Aguarda mercados carregarem
                                var marketsLoaded = iframe.WaitForSelectorAsync("div.markets", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                                if (marketsLoaded == null)
                                {
                                    Console.WriteLine("Nenhum mercado foi carregado para a aba selecionada.");
                                    continue;
                                }

                                // Processa os mercados visíveis
                                ProcessMarketViews(iframe);
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
                            var nextButton = iframe.QuerySelectorAsync(rightArrowSelector).GetAwaiter().GetResult();
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
                        finished = true; // Finaliza o processamento em caso de erro crítico
                    }
                }

                Console.WriteLine("Todas as abas foram processadas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao localizar o contêiner de abas: {ex.Message}");
            }
        }




        private void ProcessMarketViews(IFrame iframe)
        {
            // Pausa para garantir que os mercados carreguem
            Thread.Sleep(new Random().Next(851, 1132));

            // Captura os contêineres de mercados
            var marketContainers = iframe.QuerySelectorAllAsync("div.markets > div.m-col > div.market").GetAwaiter().GetResult();
            if (marketContainers == null || !marketContainers.Any())
            {
                Console.WriteLine("Nenhum mercado encontrado.");
                return;
            }

            // Lista de tags cadastradas que queremos buscar
            var tagNames = BetfastTags.TagNames;
            

            foreach (var marketContainer in marketContainers)
            {
                try
                {
                    // Captura o título do mercado (Ex: "Total de Gols")
                    var titleElement = marketContainer.QuerySelectorAsync("div.title > span").GetAwaiter().GetResult();
                    if (titleElement == null) continue;

                    var marketTitle = _teamService.NormalizeText(titleElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult());

                    // Verifica se o título está na lista de tags permitidas
                    if (!tagNames.Values.Any(tagList => tagList.Contains(marketTitle)))
                    {
                        Console.WriteLine($"Mercado ignorado: {marketTitle}");
                        continue;
                    }

                    // Recupera o ID da tag associada
                    string normalizedTagName = _teamService.NormalizeText(marketTitle);

                    var matchingTag = BetfastTags.TagNames
                        .FirstOrDefault(tag => tag.Value.Any(tagValue => _teamService.NormalizeText(tagValue) == normalizedTagName));

                    if (matchingTag.Key == 0)
                    {
                        Console.WriteLine($"Tag não encontrada: {marketTitle}");
                        continue;
                    }

                    int tagId = matchingTag.Key;


                    // Define o seletor das opções de aposta
                    const string betOptionsSelector = "div.market-odds > div.odd-rect-wide";

                    // Máximo de tentativas para expandir o mercado
                    const int maxRetries = 3;
                    int retryCount = 0;

                    // Loop de tentativas para expandir o mercado
                    while (retryCount < maxRetries)
                    {
                        // Captura as opções de aposta no marketContainer
                        var betOptions = marketContainer.QuerySelectorAllAsync(betOptionsSelector).GetAwaiter().GetResult();

                        if (betOptions != null && betOptions.Length > 0)
                        {
                            Console.WriteLine($"Mercado expandido com sucesso. Encontradas {betOptions.Length} opções de aposta.");
                            break; // Sai do loop se encontrar opções de aposta
                        }

                        // Verifica se o botão de expandir mercado existe
                        var arrowElement = marketContainer.QuerySelectorAsync("span.arrow").GetAwaiter().GetResult();
                        if (arrowElement != null)
                        {
                            Console.WriteLine($"Tentativa {retryCount + 1}: Expandindo mercado...");
                            arrowElement.ClickAsync().GetAwaiter().GetResult();
                            Thread.Sleep(new Random().Next(526, 1231)); // Pausa entre as tentativas
                        }
                        else
                        {
                            Console.WriteLine("Botão de expandir mercado não encontrado. Interrompendo tentativa de expansão.");
                            break;
                        }

                        retryCount++;
                    }

                    // Captura novamente as opções de aposta após o loop de tentativas
                    var finalBetOptions = marketContainer.QuerySelectorAllAsync(betOptionsSelector).GetAwaiter().GetResult();

                    if (finalBetOptions == null || finalBetOptions.Length == 0)
                    {
                        Console.WriteLine("Nenhuma opção de aposta encontrada após expandir o mercado. Pulando este mercado.");
                        return; // Sai da execução deste mercado
                    }

                    Console.WriteLine($"Opções de aposta encontradas: {finalBetOptions.Length}");
                    
                    List<BetInfo> currentBets = new List<BetInfo>();

                    foreach (var betOption in finalBetOptions)
                    {
                        

                        try
                        {
                            // Captura o nome da aposta (Ex: "Acima (2.5)" ou "Abaixo (2.5)")
                            var oddNameElement = betOption.QuerySelectorAsync("p.odd-name").GetAwaiter().GetResult();
                            var oddName = oddNameElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                            // Identifica se é "Mais de" ou "Menos de"
                            string overUnder = "";

                            if (oddName.StartsWith("Acima"))
                            {
                                overUnder = "Mais de";
                            }
                            else if (oddName.StartsWith("Abaixo"))
                            {
                                overUnder = "Menos de";
                            }
                            else
                            {
                                // Caso venha um valor inesperado, exibe uma mensagem e pula a iteração
                                Console.WriteLine($"Aposta ignorada: 'oddName' inesperado -> {oddName}");
                                continue; // Segue para a próxima interação
                            }


                            // Remove "Acima" ou "Abaixo" do nome
                            string betAmountText = Regex.Match(oddName, @"\((.*?)\)").Groups[1].Value;

                            if (!decimal.TryParse(betAmountText.Replace(".", ","), out decimal betAmount))
                            {
                                Console.WriteLine($"Valor de aposta inválido: {betAmountText}");
                                continue;
                            }

                            // Captura o multiplicador
                            var multiplierElement = betOption.QuerySelectorAsync("span.coef").GetAwaiter().GetResult();
                            string multiplierText = multiplierElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                            if (!decimal.TryParse(multiplierText.Replace(".", ","), out decimal multiplier))
                            {
                                Console.WriteLine($"Multiplicador inválido: {multiplierText}");
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

                            // Cria a aposta
                            currentBets.Add(new BetInfo
                            {
                                GamesInfo = gamesInfo,
                                TagName = adjustedTagName,
                                OverUnder = overUnder,
                                BetAmount = betAmount,
                                Multiplier = multiplier,
                                GameDate = gamesInfo.GameDate,
                                CaptureDate = DateTime.Now,
                                Site = gamesInfo.Site,
                                TagId = tagId
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar opção de aposta: {ex.Message}");
                            _logService.LogError("Erro ao processar opção de aposta: ", ex);
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
