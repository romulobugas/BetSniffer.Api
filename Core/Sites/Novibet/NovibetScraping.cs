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
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;

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

        private GamesInfo gamesInfo;

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

            // Lista de categorias processadas
            var processedCategories = new HashSet<int>();

            // Localiza o carrossel de categorias
            var categoriesCarousel = wait.Until(driver => driver.FindElement(By.CssSelector("app-event-market-categories")));            

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
                    else if (Regex.IsMatch(gameDateText, @"^\d{1,2} de [a-z]{3} \d{2}:\d{2}$")) // Exemplo: "10 de dez 14:45"
                    {
                        // Dicionário para converter os meses em português
                        Dictionary<string, int> meses = new Dictionary<string, int>
        {
            { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
            { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
        };

                        // Separar os componentes
                        string[] parts = gameDateText.Split(' ');
                        int day = int.Parse(parts[0]); // Dia
                        string monthText = parts[2];  // Nome do mês abreviado
                        string time = parts[3];       // Hora

                        if (!meses.TryGetValue(monthText, out int month))
                        {
                            throw new Exception($"Mês inválido: {monthText}");
                        }

                        // Montar o objeto DateTime
                        gameDateTime = new DateTime(DateTime.Today.Year, month, day).Add(TimeSpan.Parse(time));
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

            

            

            gamesInfo = new GamesInfo
            {
                HomeTeamId = homeTeamDb,
                AwayTeamId = awayTeamDb,
                GameDate = gameDateTime,
                League = gameName,
                Site = site

            };

            // Lista para armazenar resultados
            List<TagInfo> allTagInfos = new List<TagInfo>();

            // Captura todas as categorias disponíveis
            var categoryElements = categoriesCarousel.FindElements(By.CssSelector(".swiper-slide"));
            if (categoryElements == null || !categoryElements.Any())
                throw new Exception("Nenhuma categoria encontrada no carrossel.");

            // Configuração de número máximo de retentativas
            const int maxRetries = 5;
            const int retryDelay = 1000; // Delay em milissegundos

            // Botão de navegação para a direita
            var nextButton = _driver.FindElement(By.CssSelector(".marketCategories_arrowRight.nextBtn.u-flex.u-flexCenter"));

            int i = 0; // Índice inicial para o loop principal
            while (i < categoryElements.Count)
            {
                if (processedCategories.Contains(i))
                {
                    i++; // Pula categorias já processadas
                    continue;
                }

                bool categoryProcessed = false;
                int attempts = 0;

                while (!categoryProcessed && attempts < maxRetries)
                {
                    try
                    {
                        // Tenta clicar na categoria
                        categoryElements[i].Click();
                        processedCategories.Add(i); // Marca como processada

                        // Estratégia de retentativa para carregar o elemento necessário
                        bool elementFound = false;

                        for (int loadAttempt = 0; loadAttempt <= maxRetries; loadAttempt++)
                        {
                            try
                            {
                                // Espera que os dados carreguem
                                wait.Until(driver => driver.FindElements(By.CssSelector("app-event-marketview")).Count > 0);
                                elementFound = true;
                                break; // Sai do loop se o elemento for encontrado
                            }
                            catch (WebDriverTimeoutException)
                            {
                                if (loadAttempt < maxRetries)
                                {
                                    Console.WriteLine($"Tentativa {loadAttempt + 1} falhou. Aguardando {retryDelay / 1000} segundos...");
                                    Thread.Sleep(retryDelay); // Aguarda antes de tentar novamente
                                }
                                else
                                {
                                    Console.WriteLine("Excedido o número de tentativas para carregar 'app-event-marketview'.");
                                }
                            }
                        }

                        if (!elementFound)
                            throw new Exception("Falha ao encontrar 'app-event-marketview' após múltiplas tentativas.");

                        // Captura as tags para esta categoria
                        var tagInfos = ProcessMarketViews();
                        allTagInfos.AddRange(tagInfos); // Adiciona os resultados

                        categoryProcessed = true; // Marca a categoria como processada com sucesso
                    }
                    catch (ElementClickInterceptedException)
                    {
                        Console.WriteLine($"Categoria {i} interceptada. Tentando usar o botão 'Next' para ajustar...");
                        if (nextButton.GetAttribute("class").Contains("swiper-button-disabled"))
                        {
                            Console.WriteLine("Botão 'Next' desabilitado. Não é possível navegar mais.");
                            break; // Sai do loop de tentativas se não houver mais categorias acessíveis
                        }

                        nextButton.Click(); // Clica no botão para ajustar o carrossel
                        Thread.Sleep(retryDelay); // Aguardando para que o layout do carrossel seja ajustado
                        categoryElements = categoriesCarousel.FindElements(By.CssSelector(".swiper-slide")); // Recarrega os elementos
                    }
                    catch (ElementNotInteractableException)
                    {
                        Console.WriteLine($"Categoria {i} não interagível. Tentando usar o botão 'Next' para ajustar...");
                        if (nextButton.GetAttribute("class").Contains("swiper-button-disabled"))
                        {
                            Console.WriteLine("Botão 'Next' desabilitado. Não é possível navegar mais.");
                            break; // Sai do loop de tentativas se não houver mais categorias acessíveis
                        }

                        nextButton.Click(); // Clica no botão para ajustar o carrossel
                        Thread.Sleep(retryDelay); // Aguardando para que o layout do carrossel seja ajustado
                        categoryElements = categoriesCarousel.FindElements(By.CssSelector(".swiper-slide")); // Recarrega os elementos
                    }
                    catch (StaleElementReferenceException)
                    {
                        Console.WriteLine($"Elemento da categoria {i} ficou obsoleto. Recarregando elementos...");
                        categoryElements = categoriesCarousel.FindElements(By.CssSelector(".swiper-slide")); // Recarrega os elementos
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar categoria {i}: {ex.Message}");
                        attempts++;
                    }
                }

                if (!categoryProcessed)
                {
                    Console.WriteLine($"Não foi possível processar a categoria {i} após {maxRetries} tentativas. Passando para a próxima.");
                }

                i++; // Move para a próxima categoria
            }

            // Encerra o WebDriver
            _driver.Quit();
            return allTagInfos;




        }

        // Método para processar os "app-event-marketview" e capturar as tags
        private List<TagInfo> ProcessMarketViews()
        {
            // Lista para armazenar as apostas
            List<BetInfo> bets = new List<BetInfo>();
            List<TagInfo> tagInfos = new List<TagInfo>();

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _driver.FindElements(By.TagName("app-event-marketview"));

            foreach (var eventMarketView in eventMarketViews)
            {
                try
                {

                    // Verifica se o evento contém uma tag válida
                    var tagElement = eventMarketView.FindElement(By.XPath(".//span[contains(@class, 'eventMarketview_title')]"));

                    string tagName = tagElement.Text.Trim();

                    // Lista de tags cadastradas que queremos buscar
                    var tagNames = NovibetTags.TagNames;

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
                                string overUnder = string.Empty;
                                decimal betAmount = 0;

                                // Expressão regular para capturar "Mais de" ou "Menos de"
                                string patternName = @"^(Mais de|Menos de)";
                                Match nameMatch = Regex.Match(betName, patternName);

                                // Expressão regular para capturar o número (com ou sem vírgulas) ou o formato "+número"
                                string patternDecimal = @"(\+?\d+(?:,\d+)?)";
                                Match matchDecimal = Regex.Match(betName, patternDecimal);

                                string multiplier = multiplierElements[i].Text.Trim();

                                if (!string.IsNullOrEmpty(betName) && !string.IsNullOrEmpty(multiplier))
                                {
                                    if (nameMatch.Success)
                                    {
                                        overUnder = nameMatch.Value;
                                    }
                                    else if (betName.StartsWith("+") || betName.EndsWith("+"))
                                    {
                                        overUnder = "Mais de";
                                    }

                                    if (matchDecimal.Success)
                                    {
                                        string betAmountString = matchDecimal.Value.TrimStart('+');
                                        betAmount = decimal.Parse(betAmountString.Replace(",", "."), CultureInfo.InvariantCulture);
                                    }

                                    var betInfo = new BetInfo
                                    {
                                        GamesInfo = gamesInfo,
                                        TagName = tagName,
                                        OverUnder = overUnder,
                                        BetAmount = betAmount,
                                        Multiplier = decimal.Parse(multiplier.Replace(",", "."), CultureInfo.InvariantCulture),
                                        GameDate = gamesInfo.GameDate,
                                        CaptureDate = DateTime.Now,
                                        Site = gamesInfo.Site
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
                                        g.Site.SiteId == gamesInfo.Site.SiteId);

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
                                        existingGame.Bets.Add(bet); // Associa o BetInfo diretamente ao GamesInfo
                                    }
                                    else if (existingBet.Multiplier != bet.Multiplier)
                                    {
                                        // Atualiza o multiplicador da aposta existente
                                        existingBet.Multiplier = bet.Multiplier;
                                        existingBet.CaptureDate = DateTime.Now; // Atualiza a data de captura
                                    }
                                }

                                // Salva alterações no banco somente se houver pelo menos uma aposta válida
                                if (existingGame.Bets.Any())
                                {
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

            return tagInfos;
        }
    }
}
