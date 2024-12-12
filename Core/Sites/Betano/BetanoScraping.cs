using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Sites.Betano
{
    public class BetanoScraping : IScrapingService
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

        #endregion

        public BetanoScraping(
            ApplicationDbContext dbContext,
            TeamService teamService,
            IRepositoryService<GamesInfo> gamesInfoRepository,
            IRepositoryService<BetInfo> betInfoRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _gameService = new GameService(_dbContext);
        }

        public List<TagInfo> ScrapeTagsAsync(string url, string siteName)
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

            string ageVerification = "[data-qa='age-verification-modal-ok-button']";

            // Confirmação de idade
            ConfirmAgeVerification(page, ageVerification);

            string popupSelector = "#landing-page-modal .sb-modal__close__btn";

            //// Fecha popups
            ClosePopup(page, popupSelector);

            // Espera pelo elemento de mercado
            page.WaitForSelectorAsync("div.markets", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();

            //// Coleta informações do jogo
            ExtractGameInfo(page);

            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            BetanoTags.AddDynamicTags(homeTeam, awayTeam);

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

            ProcessTabsAndMarketViews(page);

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
                System.Threading.Thread.Sleep(new Random().Next(1921, 3224));

                // Captura o texto de data e hora completo
                var dateElement = page.WaitForSelectorAsync("div.tw-font-bold", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                var gameDateTimeText = dateElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                // Captura o nome dos times
                var teamElements = page.QuerySelectorAllAsync("h1.tw-flex span.tw-font-bold").GetAwaiter().GetResult();
                if (teamElements.Length >= 2)
                {
                    homeTeam = teamElements[0].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    awayTeam = teamElements[1].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                }
                else
                {
                    throw new Exception("Não foi possível encontrar os dois times.");
                }

                // Parsea a data e hora do jogo
                gameDateTime = ParseGameDateTime(gameDateTimeText);
                Console.WriteLine($"Data e Hora do Jogo: {gameDateTime}");

                // Captura o nome da liga (4º elemento da lista breadcrumbs)
                var breadcrumbElements = page.QuerySelectorAllAsync(".breadcrumbs-container__list__item").GetAwaiter().GetResult();
                if (breadcrumbElements.Length >= 4)
                {
                    leagueName = breadcrumbElements[3].EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();
                    Console.WriteLine($"Liga: {leagueName}");
                }
                else
                {
                    throw new Exception("Não foi possível encontrar a liga nos breadcrumbs.");
                }
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Erro ao capturar elementos: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar informações do jogo: {ex.Message}");
            }

        }



        private DateTime ParseGameDateTime(string dateTimeText)
        {
            if (string.IsNullOrWhiteSpace(dateTimeText))
                throw new ArgumentException("O parâmetro 'dateTimeText' está vazio ou nulo.");

            // Define o formato esperado para o texto de data e hora
            const string format = "dddd, d MMMM yyyy HH:mm";

            // Tenta parsear a string usando o formato esperado e a cultura pt-BR
            if (DateTime.TryParseExact(dateTimeText, format,
                    System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),
                    System.Globalization.DateTimeStyles.None, out var parsedDateTime))
            {
                return parsedDateTime;
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
            var allowedTabs = new HashSet<string> { "Todos" }; // Apenas abas permitidas

            while (true)
            {
                try
                {
                    // Localiza o contêiner de abas
                    var tabsContainer = page.WaitForSelectorAsync("div[data-qa='pre-event-details-market-tabs']", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                    if (tabsContainer == null)
                    {
                        Console.WriteLine("Contêiner de abas não encontrado.");
                        break;
                    }

                    var tabButtons = tabsContainer.QuerySelectorAllAsync("div.swiper-slide").GetAwaiter().GetResult();

                    foreach (var tab in tabButtons)
                    {
                        try
                        {
                            // Captura o nome da aba
                            var tabName = tab.QuerySelectorAsync("div[data-qa]").GetAwaiter().GetResult()
                                ?.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                            if (string.IsNullOrEmpty(tabName))
                            {
                                Console.WriteLine("Não foi possível capturar o nome da aba.");
                                continue;
                            }

                            // Processa apenas as abas permitidas
                            if (!allowedTabs.Contains(tabName))
                            {
                                Console.WriteLine($"Ignorando a aba: {tabName}");
                                continue;
                            }

                            Console.WriteLine("Forçando o scroll para o topo do layout...");
                            page.EvaluateExpressionAsync("window.scrollTo(0, 0)").GetAwaiter().GetResult(); // Rola para o topo
                            System.Threading.Thread.Sleep(new Random().Next(225, 684)); // Pausa para garantir que a rolagem tenha ocorrido
                            Console.WriteLine("Scroll até o topo da página concluído.");

                            Console.WriteLine($"Processando aba: {tabName}");

                            // Tenta clicar no botão da aba, com repetição em caso de erro
                            bool clicked = false;
                            int retries = 0;
                            while (!clicked && retries < 5)
                            {
                                try
                                {
                                    tab.ClickAsync().GetAwaiter().GetResult();
                                    clicked = true; // Se clicou com sucesso, sai do loop
                                }
                                catch (PuppeteerException)
                                {
                                    retries++;
                                    Console.WriteLine($"Clique interceptado na aba '{tabName}', tentando novamente ({retries}/5).");
                                    System.Threading.Thread.Sleep(new Random().Next(421, 892)); // Espera antes de tentar novamente
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
                            var marketsLoaded = page.WaitForSelectorAsync("div.markets", new WaitForSelectorOptions { Timeout = 10000 }).GetAwaiter().GetResult();
                            if (marketsLoaded == null)
                            {
                                Console.WriteLine("Itens da aba não foram carregados.");
                                continue;
                            }

                            // Processa os mercados visíveis na aba
                            ProcessMarketViews(page);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro inesperado ao processar aba: {ex.Message}");
                        }
                    }

                    break; // Sai do loop principal após processar todas as abas
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro geral ao processar abas: {ex.Message}");
                    System.Threading.Thread.Sleep(new Random().Next(1852, 3723)); // Pausa para estabilizar a página antes de tentar novamente
                }
            }

            Console.WriteLine("Todas as abas processadas com sucesso");
        }

        private void ProcessMarketViews(IPage page)
        {
            // Aguarda um tempo para carregar todos os mercados
            System.Threading.Thread.Sleep(new Random().Next(1725, 3230));

            // Exemplo de movimentação do mouse
            page.Mouse.MoveAsync(50, 50).GetAwaiter().GetResult(); // Move o mouse para um ponto arbitrário

            // Adiciona uma pausa aleatória para simular o comportamento humano
            Random random = new Random();
            Thread.Sleep(random.Next(423, 1240)); // Pausa de 500ms a 1500ms

            // Encontra todos os contêineres de aposta dentro de <div class="markets">
            var eventMarketViews = page.QuerySelectorAllAsync("div.markets div[data-marketid]").GetAwaiter().GetResult();

            // Lista de tags cadastradas que queremos buscar
            var tagNames = BetanoTags.TagNames;

            // Lista para armazenar as apostas
            List<BetInfo> bets = new List<BetInfo>();
            List<TagInfo> tagInfos = new List<TagInfo>();

            foreach (var eventMarketView in eventMarketViews)
            {
                try
                {
                    // Verifica se o evento contém uma tag válida
                    var tagElement = eventMarketView.QuerySelectorAsync(".tw-self-center").GetAwaiter().GetResult();
                    if(tagElement == null) 
                    {
                        Console.WriteLine("O tagElement era nulo nesse trecho");
                        continue;
                    }
                    string tagName = tagElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                    // Verifica se a tag encontrada contém o nome da tag desejada
                    if (BetanoTags.TagNames.Values.Any(tagList => tagList.Contains(tagName)))
                    {
                        // Recupera o ID da tag a partir do dicionário
                        int tagId = BetanoTags.TagNames.FirstOrDefault(x => x.Value.Contains(tagName)).Key;

                        bool marketWrapperFound = false;
                        int retries = 0;

                        while (!marketWrapperFound && retries < 3)
                        {
                            try
                            {
                                var selections = eventMarketView.QuerySelectorAllAsync(".selections").GetAwaiter().GetResult();
                                if (selections.Length > 0)
                                {
                                    marketWrapperFound = true;
                                }
                                else
                                {
                                    eventMarketView.ClickAsync();
                                    Thread.Sleep(random.Next(490, 1229));
                                    retries++;
                                }
                            }
                            catch
                            {
                                eventMarketView.ClickAsync();
                                Thread.Sleep(random.Next(320, 1195));
                                retries++;
                            }
                        }

                        if (!marketWrapperFound)
                        {
                            Console.WriteLine("O elemento 'market-wrapper' não foi encontrado após 3 tentativas.");
                            continue;
                        }

                        var selectionElements = eventMarketView.QuerySelectorAllAsync(".selections__selection").GetAwaiter().GetResult();

                        foreach (var selectionElement in selectionElements)
                        {
                            try
                            {
                                var overUnderElement = selectionElement.QuerySelectorAsync(".s-name").GetAwaiter().GetResult();
                                string overUnderText = overUnderElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                var betAmountElement = selectionElement.QuerySelectorAsync(".s-name-sub").GetAwaiter().GetResult();
                                string betAmountText = betAmountElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                if (!decimal.TryParse(betAmountText.Replace(".", ","), out decimal betAmount))
                                {
                                    Console.WriteLine($"Valor da aposta inválido: {betAmountText}. Pulando este elemento.");
                                    continue;
                                }

                                var multiplierElement = selectionElement.QuerySelectorAsync(".tw-font-bold").GetAwaiter().GetResult();
                                string multiplierText = multiplierElement.EvaluateFunctionAsync<string>("el => el.textContent.trim()").GetAwaiter().GetResult();

                                if (!decimal.TryParse(multiplierText.Replace(".", ","), out decimal multiplier))
                                {
                                    Console.WriteLine($"Multiplicador inválido: {multiplierText}. Pulando este elemento.");
                                    continue;
                                }

                                var adjustedTagName = tagName
                                    .Replace(homeTeam, "Casa", StringComparison.OrdinalIgnoreCase)
                                    .Replace(awayTeam, "Visitante", StringComparison.OrdinalIgnoreCase);

                                var betInfo = new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = adjustedTagName,
                                    OverUnder = overUnderText,
                                    BetAmount = betAmount,
                                    Multiplier = multiplier,
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = site,
                                    TagId = tagId
                                };

                                bets.Add(betInfo);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar aposta: {ex.Message}");
                            }
                        }

                        if (bets.Count > 0)
                        {
                            tagInfos.Add(new TagInfo(gamesInfo, bets));
                            foreach (var bet in bets)
                            {
                                var adjustedTagName = bet.TagName
                                    .Replace(homeTeam, "Casa", StringComparison.OrdinalIgnoreCase)
                                    .Replace(awayTeam, "Visitante", StringComparison.OrdinalIgnoreCase);

                                var existingBet = _betInfoRepository.Find(b =>
                                    b.GamesInfo.GameId == gamesInfo.GameId &&
                                    b.TagName == adjustedTagName &&
                                    b.OverUnder == bet.OverUnder &&
                                    b.BetAmount == bet.BetAmount &&
                                    b.TagId == bet.TagId &&
                                    b.Site.SiteId == bet.Site.SiteId).FirstOrDefault();

                                if (existingBet == null)
                                {
                                    bet.GamesInfo = gamesInfo;
                                    _betInfoRepository.Add(bet);
                                }
                                else
                                {
                                    existingBet.Multiplier = bet.Multiplier;
                                    existingBet.CaptureDate = DateTime.Now;
                                    _betInfoRepository.SaveOrUpdate(existingBet);
                                }
                            }

                            _dbContext.SaveChanges();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar o mercado: {ex.Message}");
                }
            }
        }

    }
}
