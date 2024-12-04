using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using Microsoft.EntityFrameworkCore.Internal;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites.Novibet;

namespace BetSniffer.Api.Core.Sites.Parimatch
{
    public class ParimatchScraping : IScrapingService
    {
        #region VariaveisGlobais

        private readonly IWebDriver _driver;

        private string gameName;

        private string gameDayText;

        private string gameHourText;

        private string homeTeam;

        private string awayTeam;

        private DateTime gameDateTime;

        private readonly ApplicationDbContext _dbContext;

        private readonly TeamService _teamService;

        #endregion

        public ParimatchScraping(IWebDriver driver, ApplicationDbContext dbContext, TeamService teamService)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
        }

        // Método para fazer o scraping e retornar as tags e apostas encontradas
        public List<TagInfo> ScrapeTagsAsync(string url, string siteName)
        {

            if (string.IsNullOrEmpty(url)) throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrEmpty(siteName)) throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            if (_driver == null)
            {
                throw new InvalidOperationException("O driver não foi inicializado corretamente.");
            }

            // Verifica se o _dbContext foi inicializado corretamente
            if (_dbContext == null)
            {
                throw new InvalidOperationException("O contexto do banco de dados não foi inicializado corretamente.");
            }

            // Verifica se o site já existe no banco
            var site = _dbContext.Site.FirstOrDefault(s => s.Name.ToLower() == siteName.ToLower());

            if (site == null)
            {
                // Caso o site não exista, cria um novo registro
                site = new Site
                {
                    Name = siteName
                };
                _dbContext.Site.Add(site);
                _dbContext.SaveChanges(); // Salva o novo site
                Console.WriteLine($"Novo site adicionado: {siteName}");
            }

            _driver.Navigate().GoToUrl(url);

            // Espera até que os elementos da página estejam carregados
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

            // Fechar o pop-up, caso ele apareça
            try
            {
                var closeButton = wait.Until(driver => driver.FindElement(By.CssSelector(".registerOrLogin_closeButton")));
                closeButton.Click();
                Console.WriteLine("Pop-up fechado com sucesso.");
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("Pop-up não encontrado.");
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera para fechar o pop-up expirou.");
            }

            // Aguarda até que o primeiro elemento esperado esteja visível
            try
            {
                wait.Until(driver => driver.FindElement(By.CssSelector("[data-id='event-markets']")));
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera excedido, o elemento não foi encontrado.");
                return new List<TagInfo>();
            }

            // Encontra todos os elementos <span> dentro da div com data-testid='event-view-header-soccer-center-container'
            var spanElements = _driver.FindElements(By.CssSelector("div[data-testid='event-view-header-soccer-center-container'] span"));

            // Verifica se existem pelo menos dois <span> dentro da div
            if (spanElements.Count >= 2)
            {
                // O segundo span contém o nome do campeonato
                gameName = spanElements[1].Text.Trim(); // Captura o texto do campeonato, por exemplo: "Brasil. Série A"
            }
            else
            {
                gameName = "Nome não encontrado"; // Caso não encontre o segundo span
            }


            // Encontra todos os contêineres de aposta
            var eventPresentationViews = _driver.FindElements(By.CssSelector("div[data-id='card-scoreboard']"));

            foreach (var eventPresentationView in eventPresentationViews) 
            {
                //Captura os times
                // Captura os elementos que possuem o atributo data-id="event-card-competitor-name"
                var teamElements = eventPresentationView.FindElements(By.XPath(".//span[@data-id='event-card-competitor-name']"));

                // Certifica-se de que existem pelo menos dois times
                if (teamElements.Count >= 2)
                {
                    homeTeam = teamElements[0].Text.Trim(); // Primeiro time
                    awayTeam = teamElements[1].Text.Trim(); // Segundo time
                }
                else
                {
                    throw new Exception("Não foi possível encontrar os dois times.");
                }

                // Captura o dia do evento
                var gameDayElement = eventPresentationView.FindElement(By.XPath(".//span[@data-testid='prematch-start-date']"));
                gameDayText = gameDayElement.Text.Trim(); // Captura o texto da data (ex: "Amanhã")

                // Captura a hora do evento
                var gameHourElement = eventPresentationView.FindElement(By.XPath(".//span[@data-testid='prematch-start-time']"));
                gameHourText = gameHourElement.Text.Trim(); // Captura o texto da hora (ex: "19:00")

                // Verifica se gameHourText contém uma hora válida
                if (gameHourText.Contains(":"))
                {
                    // Inicializa uma variável base para a data
                    DateTime baseDate;

                    // Trata os diferentes formatos de gameDayText
                    if (gameDayText.Equals("HOJE", StringComparison.OrdinalIgnoreCase))
                    {
                        // Para "HOJE", usa a data atual
                        baseDate = DateTime.Today;
                    }
                    else if (gameDayText.Equals("AMANHÃ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Para "AMANHÃ", usa o dia seguinte
                        baseDate = DateTime.Today.AddDays(1);
                    }
                    else
                    {
                        // Para datas no formato "5 de dezembro"
                        var dayMonthMatch = Regex.Match(gameDayText, @"(\d+)\s+de\s+(\w+)", RegexOptions.IgnoreCase);
                        if (dayMonthMatch.Success)
                        {
                            int day = int.Parse(dayMonthMatch.Groups[1].Value);
                            string monthName = dayMonthMatch.Groups[2].Value.ToLower();

                            // Mapeia o nome do mês para o número correspondente
                            var monthMap = new Dictionary<string, int>
                                {
                                    { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
                                    { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
                                    { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 }
                                };

                            if (!monthMap.ContainsKey(monthName))
                            {
                                throw new Exception($"Nome do mês inválido: {monthName}");
                            }

                            int month = monthMap[monthName];
                            int year = DateTime.Today.Year;

                            // Ajusta o ano se a data for do próximo mês no final do ano
                            if (month < DateTime.Today.Month)
                            {
                                year++;
                            }

                            baseDate = new DateTime(year, month, day);
                        }
                        else
                        {
                            throw new Exception($"Formato de data inválido: {gameDayText}");
                        }
                    }

                    // Combina a data base com a hora do jogo
                    gameDateTime = baseDate.Date.Add(TimeSpan.Parse(gameHourText));
                }
                else
                {
                    throw new Exception($"Formato inesperado para gameHourText: {gameHourText}");
                }

                // Exemplo de uso
                Console.WriteLine("Data e Hora do Jogo: " + gameDateTime);


            }

            // Implementação dos times usando TeamService
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            ParimatchTags.AddDynamicTags(homeTeam, awayTeam);

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _driver.FindElements(By.CssSelector("div[data-id='market-item']"));

            // Lista de tags cadastradas que queremos buscar
            var tagNames = ParimatchTags.TagNames;

            var gamesInfo = new GamesInfo
            {
                HomeTeamId = homeTeamDb,
                AwayTeamId = awayTeamDb,
                GameDate = gameDateTime,
                League = gameName,
                Site = site

            };

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
                    if (tagNames.Contains(tagName))
                    {

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
                                Thread.Sleep(3000);

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
                                    Site = site
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
                                    Site = site
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


                        // Verifica e atualiza as apostas
                        if (bets.Count > 0)
                        {
                            tagInfos.Add(new TagInfo(gamesInfo, bets));

                            // Verifica se o jogo já existe
                            var existingGame = _dbContext.GamesInfo
                                .Include(g => g.Bets) // Carrega as apostas relacionadas
                                .FirstOrDefault(g =>
                                    g.HomeTeamId == gamesInfo.HomeTeamId &&
                                    g.AwayTeamId == gamesInfo.AwayTeamId &&
                                    g.GameDate == gamesInfo.GameDate &&
                                    g.League == gamesInfo.League &&
                                    g.Site == gamesInfo.Site
                                    );

                            if (existingGame == null)
                            {
                                // Se o jogo não existir, adiciona ao banco
                                _dbContext.GamesInfo.Add(gamesInfo);
                                existingGame = gamesInfo;
                            }

                            // Verifica e atualiza as apostas
                            foreach (var bet in bets)
                            {
                                // Procura a aposta correspondente no banco
                                var existingBet = _dbContext.BetInfo.FirstOrDefault(b =>
                                    b.TagName == bet.TagName &&
                                    b.OverUnder == bet.OverUnder &&
                                    b.BetAmount == bet.BetAmount &&
                                    b.GameDate == bet.GameDate &&
                                    b.Site.SiteId == bet.Site.SiteId);

                                if (existingBet == null)
                                {
                                    // Adiciona nova aposta, pois não existe no banco
                                    _dbContext.BetInfo.Add(bet);
                                }
                                else if (existingBet.Multiplier != bet.Multiplier)
                                {
                                    // Atualiza o multiplicador da aposta existente
                                    existingBet.Multiplier = bet.Multiplier;
                                    existingBet.CaptureDate = DateTime.Now; // Atualiza a data de captura
                                    _dbContext.Entry(existingBet).State = EntityState.Modified;
                                }
                            }

                            // Salva todas as alterações no banco de uma vez
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

            return tagInfos;
        }
    }
}
