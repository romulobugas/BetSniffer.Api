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
using BetSniffer.Api.Core.Services;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public class NovibetScraping : IScrapingService
    {
        #region VariaveisGlobais

        private readonly IWebDriver _driver;

        private string gameName;

        private string gameDateText;

        private string homeTeam;

        private string awayTeam;

        private DateTime gameDateTime;

        private readonly ApplicationDbContext _dbContext;

        private readonly TeamService _teamService;

        #endregion

        public NovibetScraping(ApplicationDbContext dbContext, TeamService teamService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));  // Inicializa o TeamService corretamente
            // Inicializa o driver aqui no construtor
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("--disable-gpu");  // Desabilita a aceleração de GPU
            //options.AddArgument("--headless");     // Rodar em modo headless (sem interface gráfica)
            options.AddArgument("--no-sandbox");   // Desativa o sandbox (pode ajudar em servidores)
            options.AddArgument("--disable-software-rasterizer"); // Desativa o rasterizador de software

            _driver = new ChromeDriver(options);  // Inicializa o driver aqui
        }

        public NovibetScraping()
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
        public List<TagInfo> ScrapeTagsAsync(string url, string siteName)
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
                wait.Until(driver => driver.FindElement(By.XPath("//app-event-marketview")));
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera excedido, o elemento não foi encontrado.");
                _driver.Quit();
                return new List<TagInfo>();
            }           

            // Encontra todos os contêineres de aposta
            var eventPresentationViews = _driver.FindElements(By.TagName("app-event-presentation"));

            foreach (var eventPresentationView in eventPresentationViews) 
            {
                // Captura o nome do jogo (GameName) do evento
                var gameNameElement = eventPresentationView.FindElement(By.XPath(".//div[contains(@class, 'eventPresentation_caption')]"));
                gameName = gameNameElement.Text.Trim(); // Captura o texto do evento, por exemplo: "Brasil - Brasileirão - Série A, Rodada 36"

                //Captura os times
                var teamElements = eventPresentationView.FindElements(By.XPath(".//span[contains(@class, 'eventPresentation_text')]"));

                homeTeam = teamElements[0].Text;
                awayTeam = teamElements[1].Text;

                // Captura a hora/Data do evento
                var gameDateElement = eventPresentationView.FindElement(By.XPath(".//div[contains(@class, 'eventPresentation_time')]"));
                gameDateText = gameDateElement.Text.Trim(); // Captura o texto da hora ou data                

                if (gameDateText.Contains(":")) // Certifica-se de que há uma hora no texto
                {
                    if (gameDateText.Length <= 5) // Apenas hora (ex: "16:00")
                    {
                        // Considera o dia corrente e adiciona a hora
                        gameDateTime = DateTime.Today.Date.Add(TimeSpan.Parse(gameDateText));
                    }
                    else // Dia da semana e hora (ex: "qua 19:00")
                    {
                        string[] daysOfWeek = { "dom", "seg", "ter", "qua", "qui", "sex", "sáb" };
                        string todayDay = daysOfWeek[(int)DateTime.Today.DayOfWeek];

                        // Separa o dia da semana e a hora
                        string[] parts = gameDateText.Split(' ');
                        string dayOfWeek = parts[0];
                        string time = parts[1];

                        // Determina o índice dos dias da semana
                        int currentDayIndex = Array.IndexOf(daysOfWeek, todayDay);
                        int targetDayIndex = Array.IndexOf(daysOfWeek, dayOfWeek);

                        if (targetDayIndex == -1)
                        {
                            throw new Exception($"Dia da semana inválido: {dayOfWeek}");
                        }

                        // Ajusta para o próximo dia da semana correspondente, se necessário
                        if (targetDayIndex < currentDayIndex)
                        {
                            targetDayIndex += 7; // Ajusta para a próxima semana
                        }

                        int daysToAdd = targetDayIndex - currentDayIndex;
                        DateTime targetDate = DateTime.Today.AddDays(daysToAdd);

                        // Combina a data encontrada com a hora
                        gameDateTime = targetDate.Date.Add(TimeSpan.Parse(time));
                    }
                }
                else if (Regex.IsMatch(gameDateText, @"em (\d+)'")) // Exemplo: "em 57'"
                {
                    Match match = Regex.Match(gameDateText, @"em (\d+)'");
                    if (match.Success)
                    {
                        int minutesToAdd = int.Parse(match.Groups[1].Value);
                        gameDateTime = DateTime.Now.AddMinutes(minutesToAdd); // Adiciona os minutos ao horário atual
                    }
                    else
                    {
                        throw new Exception("Formato inesperado para gameDateText: " + gameDateText);
                    }
                }
                else
                {
                    throw new Exception("Formato inesperado para gameDateText: " + gameDateText);
                }


                // Exemplo de uso
                Console.WriteLine("Data e Hora do Jogo: " + gameDateTime);

            }


            // Implementação dos times usando TeamService
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _driver.FindElements(By.TagName("app-event-marketview"));

            // Lista de tags cadastradas que queremos buscar
            var tagNames = NovibetTags.TagNames;

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
                    var tagElement = eventMarketView.FindElement(By.XPath(".//span[contains(@class, 'eventMarketview_title')]"));

                    string tagName = tagElement.Text.Trim();

                    // Verifica se a tag encontrada contém o nome da tag desejada, ignorando diferenças como emojis
                    if (tagNames.Contains(tagName))
                    {
                        // Verifica se o botão "Ver Mais" (expandir aposta) está presente
                        try
                        {
                            var expandCollapseButton = eventMarketView.FindElement(By.XPath(".//sb-market-bet-expand-collapse//span[contains(text(), 'Ver Mais')]"));
                            if (expandCollapseButton != null)
                            {
                                // Clica no botão "Ver Mais" para expandir as apostas
                                expandCollapseButton.Click();

                                // Espera um tempo para garantir que as apostas foram carregadas após o clique
                                WebDriverWait waitForLoad = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
                                waitForLoad.Until(driver => driver.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_caption')]")).Count > 0);
                            }
                        }
                        catch (NoSuchElementException)
                        {
                            // Se o botão "Ver Mais" não for encontrado, segue para o próximo passo
                            // Não há necessidade de fazer nada, pois as apostas já podem estar visíveis
                        }



                        // Captura todo o HTML do app-event-marketview
                        string eventMarketViewHtml = eventMarketView.GetAttribute("outerHTML");

                        // Encontrar todas as apostas dentro do mesmo app-event-marketview
                        var betElements = eventMarketView.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_caption singleLineEllipsis')]"));

                        // Encontrar todos os multiplicadores de apostas dentro do app-event-marketview
                        var multiplierElements = eventMarketView.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_price')]"));

                        // Verifica se o número de apostas é igual ao número de multiplicadores
                        int betCount = betElements.Count;
                        int multiplierCount = multiplierElements.Count;

                        if (betCount == multiplierCount)
                        {
                            // Itera sobre as apostas e seus multiplicadores
                            for (int i = 0; i < betCount; i++)
                            {
                                string betName = betElements[i].Text.Trim();
                                // Expressão regular para capturar "Mais de" ou "Menos de"
                                string patternName = @"^(Mais de|Menos de)";
                                Match nameMatch = Regex.Match(betName, patternName);
                                // Expressão regular para capturar o número
                                string patternDecimal = @"(\d+,\d+|\d+)"; // Captura números com ou sem vírgulas
                                Match matchDecimal = Regex.Match(betName, patternDecimal);
                                string multiplier = multiplierElements[i].Text.Trim();

                                if (!string.IsNullOrEmpty(betName) && !string.IsNullOrEmpty(multiplier))
                                {
                                    var betInfo = new BetInfo
                                    {
                                        GamesInfo = gamesInfo,
                                        TagName = tagName,
                                        OverUnder = nameMatch.Success ? nameMatch.Value : string.Empty,
                                        BetAmount = matchDecimal.Success ? decimal.Parse(matchDecimal.Value) : 0,
                                        Multiplier = decimal.Parse(multiplier.Replace(".", ",")),
                                        GameDate = gamesInfo.GameDate,
                                        CaptureDate = DateTime.Now,
                                        Site = site

                                    };

                                    // Adiciona a aposta e multiplicador no formato desejado
                                    bets.Add(betInfo);
                                }
                            }

                            // Se encontrou apostas, formata e adiciona ao retorno
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
                                    }

                                    // Salva alterações no banco
                                    _dbContext.SaveChanges();

                                }
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
