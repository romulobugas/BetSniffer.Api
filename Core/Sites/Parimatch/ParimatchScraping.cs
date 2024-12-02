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

        #endregion

        public ParimatchScraping(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            // Inicializa o driver aqui no construtor
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--disable-gpu");  // Desabilita a aceleração de GPU
            //options.AddArgument("--headless");     // Rodar em modo headless (sem interface gráfica)
            options.AddArgument("--no-sandbox");   // Desativa o sandbox (pode ajudar em servidores)
            options.AddArgument("--disable-software-rasterizer"); // Desativa o rasterizador de software

            _driver = new ChromeDriver(options);  // Inicializa o driver aqui
        }

        public ParimatchScraping()
        {
            // Inicializa o driver aqui no construtor
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--disable-gpu");  // Desabilita a aceleração de GPU
            //options.AddArgument("--headless");     // Rodar em modo headless (sem interface gráfica)
            options.AddArgument("--no-sandbox");   // Desativa o sandbox (pode ajudar em servidores)
            options.AddArgument("--disable-software-rasterizer"); // Desativa o rasterizador de software

            _driver = new ChromeDriver(options);  // Inicializa o driver aqui
        }

        // Método para fazer o scraping e retornar as tags e apostas encontradas
        public List<TagInfo> ScrapeTags(string url, string siteName)
        {

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
                _driver.Quit();
                return new List<TagInfo>();
            }

            // Captura o nome do jogo (GameName) do evento
            var gameNameElement = _driver.FindElement(By.CssSelector("span[data-testid='modulor-typography'][data-id='modulor-typography'].modulor_typography__tag__1_53_1.caption-1-regular.modulor_navigation-bar__description__1_53_1"));
            gameName = gameNameElement.Text.Trim(); // Captura o texto do evento, por exemplo: "Itália. Série A"

            // Encontra todos os contêineres de aposta
            var eventPresentationViews = _driver.FindElements(By.CssSelector("div[data-id='card-scoreboard']"));

            foreach (var eventPresentationView in eventPresentationViews) 
            {
                //Captura os times
                var teamElements = eventPresentationView.FindElements(By.XPath(".//span[contains(@class, 'modulor_typography__tag__1_53_1 caption-1-regular EC_Fw')]"));

                homeTeam = teamElements[0].Text;
                awayTeam = teamElements[1].Text;

                // Captura a hora/Data do evento
                var gameDayElement = eventPresentationView.FindElement(By.XPath(".//span[contains(@class, 'modulor_typography__tag__1_53_1 caption-2-medium-caps EC_GX')]"));
                gameDayText = gameDayElement.Text.Trim(); // Captura o texto da hora ou data

                // Captura a hora/Data do evento
                var gameHourElement = eventPresentationView.FindElement(By.XPath(".//span[contains(@class, 'modulor_typography__tag__1_53_1 title-1-semibold EC_GY')]"));
                gameHourText = gameHourElement.Text.Trim(); // Captura o texto da hora ou data

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

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _driver.FindElements(By.CssSelector("div[data-id='market-item']"));

            // Lista de tags cadastradas que queremos buscar
            var tagNames = ParimatchTags.TagNames;

            var gamesInfo = new GamesInfo
            {
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
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
                    var tagElement = eventMarketView.FindElement(By.XPath(".//span[contains(@class, 'modulor_typography__tag__1_53_1 body-regular EC_FW')]"));
                    string tagName = tagElement.Text.Trim();

                    // Verifica se a tag encontrada contém o nome da tag desejada
                    if (tagNames.Contains(tagName))
                    {
                        // Captura todos os elementos "EC_Go" que representam as apostas com seus multiplicadores
                        var betElements = eventMarketView.FindElements(By.XPath(".//div[contains(@class, 'EC_Go')]"));

                        foreach (var betElement in betElements)
                        {
                            // Captura o valor da aposta (exemplo: "4.5")
                            var betAmountElement = betElement.FindElement(By.XPath(".//span[contains(@class, 'modulor_typography__tag__1_53_1 body-rounded-medium EC_Gx')]"));
                            string betAmount = betAmountElement.Text.Trim();

                            // Captura os multiplicadores "Mais" e "Menos"
                            var moreMultiplierElement = betElement.FindElements(By.XPath(".//span[contains(@style, '--text: var(--text-outcome);')]"))[0]; // O primeiro é o "Mais"
                            var lessMultiplierElement = betElement.FindElements(By.XPath(".//span[contains(@style, '--text: var(--text-outcome);')]"))[1]; // O segundo é o "Menos"

                            // Obtém os valores dos multiplicadores
                            string moreMultiplier = moreMultiplierElement.Text.Trim();
                            string lessMultiplier = lessMultiplierElement.Text.Trim();

                            // Verifica se os multiplicadores são válidos
                            if (!string.IsNullOrEmpty(moreMultiplier) && !string.IsNullOrEmpty(lessMultiplier))
                            {
                                // Aposta "Mais de"
                                var betMore = new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = tagName,
                                    OverUnder = "Mais de",  // "Mais de" para o lado "Mais"
                                    BetAmount = decimal.Parse(betAmount.Replace(".", ",")), // Valor da aposta (ex: 4.5)
                                    Multiplier = decimal.Parse(moreMultiplier.Replace(".", ",")),
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = site
                                };

                                // Aposta "Menos de"
                                var betLess = new BetInfo
                                {
                                    GamesInfo = gamesInfo,
                                    TagName = tagName,
                                    OverUnder = "Menos de",  // "Menos de" para o lado "Menos"
                                    BetAmount = decimal.Parse(betAmount.Replace(".", ",")), // Valor da aposta (ex: 4.5)
                                    Multiplier = decimal.Parse(lessMultiplier.Replace(".", ",")),
                                    GameDate = gamesInfo.GameDate,
                                    CaptureDate = DateTime.Now,
                                    Site = site
                                };

                                // Adiciona as apostas à lista
                                bets.Add(betMore);
                                bets.Add(betLess);
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
                                    g.HomeTeam == gamesInfo.HomeTeam &&
                                    g.AwayTeam == gamesInfo.AwayTeam &&
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
                                }

                                // Salva alterações no banco
                                _dbContext.SaveChanges();
                            }
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

            _driver.Quit(); // Encerra o driver após o scraping

            return tagInfos;
        }
    }
}
