using BetSniffer.Api.Models;
using PuppeteerSharp;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Betano;
using Microsoft.OpenApi.Services;

namespace BetSniffer.Api.Core.Sites.Betnacional
{
    public class BetnacionalScraping : IScrapingService
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

        public BetnacionalScraping(
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

            ExtractGameInfo(page);

            var visitedGames = new HashSet<string>(); // Armazena IDs ou texto identificador dos jogos


            //string popupSelector = ".overlay.new-message.visible .popup span.close";

            //_gameService.ClosePopup(page,popupSelector);

            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            BetnacionalTags.AddDynamicTags(_teamService.NormalizeText(homeTeam), _teamService.NormalizeText(awayTeam));

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

            //ProcessTabsAndMarketViews(iframe);


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

        private void ExtractGameInfo(IPage page)
        {
            try
            {
                Thread.Sleep(new Random().Next(1423, 2687));

                // Captura o container principal com informações gerais
                var headerElement = page.WaitForSelectorAsync("div.bg-header", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                if (headerElement == null)
                    throw new Exception("Elemento com informações gerais do jogo não encontrado.");

                // 🎯 Times
                var matchNameElement = headerElement.QuerySelectorAsync("span.text-sm.md\\:text-lg.text-text-light-primary.font-bold").GetAwaiter().GetResult();
                var matchName = matchNameElement?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(matchName) || !matchName.Contains("x"))
                    throw new Exception($"Formato de nome de jogo inválido: {matchName}");

                var teams = matchName.Split("x");
                homeTeam = teams[0].Trim();
                awayTeam = teams[1].Trim();
                Console.WriteLine($"Times: {homeTeam} vs {awayTeam}");

                // 🌍 Liga (último breadcrumb)
                var breadcrumbElements = headerElement.QuerySelectorAllAsync("div.inline a").GetAwaiter().GetResult();
                if (breadcrumbElements.Length >= 3)
                {
                    string esporte = breadcrumbElements[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    string categoria = breadcrumbElements[1].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    leagueName = breadcrumbElements[2].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    Console.WriteLine($"Esporte: {esporte}, Categoria: {categoria}, Liga: {leagueName}");
                }
                else
                {
                    throw new Exception("Não foi possível capturar os breadcrumbs da liga.");
                }

                // 🕒 Data e Hora
                var dateElement = page.WaitForSelectorAsync("div.bg-odds-subheader .text-base.text-text-light-tertiary", new WaitForSelectorOptions { Timeout = 8000 }).GetAwaiter().GetResult();
                if (dateElement == null)
                    throw new Exception("Elemento com data e hora do jogo não encontrado.");

                var dateTimeText = dateElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                gameDateTime = ParseGameDateTime(dateTimeText);
                Console.WriteLine($"Data e Hora do Jogo: {gameDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao extrair informações do jogo: {ex.Message}");
                _logService.LogError("Erro ao extrair informações do jogo: ", ex);
            }
        }

        private DateTime ParseGameDateTime(string text)
        {
            var now = DateTime.Now;

            if (text.StartsWith("Hoje"))
            {
                var hour = text.Replace("Hoje às", "").Trim();
                return DateTime.ParseExact($"{now:dd/MM/yyyy} {hour}", "dd/MM/yyyy HH:mm", null);
            }
            else if (text.StartsWith("Amanhã"))
            {
                var hour = text.Replace("Amanhã às", "").Trim();
                return DateTime.ParseExact($"{now.AddDays(1):dd/MM/yyyy} {hour}", "dd/MM/yyyy HH:mm", null);
            }

            throw new Exception($"Formato inesperado para data/hora: {text}");
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
            var tagNames = BetnacionalTags.TagNames;
            

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

                    var matchingTag = BetnacionalTags.TagNames
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

                            if (oddName.StartsWith("Acima") || oddName.StartsWith("Mais de"))
                            {
                                overUnder = "Mais de";
                            }
                            else if (oddName.StartsWith("Abaixo") || oddName.StartsWith("Menos de"))
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

                            // Cria a aposta
                            currentBets.Add(new BetInfo
                            {
                                GamesInfo = gamesInfo,
                                TagName = marketTitle,
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
