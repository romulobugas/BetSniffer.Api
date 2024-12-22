using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using System.Globalization;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public class NovibetScraping : IScrapingService
    {
        #region VariaveisGlobais

        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private readonly TeamService _teamService;
        private string gameName;
        private string gameDateText;
        private string homeTeam;
        private string awayTeam;
        private GamesInfo gamesInfo;
        private DateTime gameDateTime;
        private readonly ApplicationDbContext _dbContext;
        private readonly GameService _gameService;
        private readonly WebScrapingServiceSelenium _webScrapingService;

        private readonly ILogService _logService;

        #endregion

        public NovibetScraping(
         ApplicationDbContext dbContext,
         TeamService teamService,
         IRepositoryService<GamesInfo> gamesInfoRepository,
         IRepositoryService<BetInfo> betInfoRepository
        )
        {
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _gameService = new GameService(_dbContext);
            _webScrapingService = new WebScrapingServiceSelenium(); // Inicializa o serviço de scraping
            _webScrapingService.Initialize(); // Configura o WebDriver

            // Inicializa o serviço de log diretamente
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _logService = new LogService(configuration);
        }

        // Método para fazer o scraping e retornar as tags e apostas encontradas
        public List<TagInfo> ScrapeTags(string url, string siteName)
        {
            Console.WriteLine($"Iniciando scraping para {siteName} com URL: {url}");

            // Validação básica
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrEmpty(siteName)) throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));


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
                site = new Site { Name = siteName };
                _dbContext.Site.Add(site);
                _dbContext.SaveChanges(); // Salva o novo site
                Console.WriteLine($"Novo site adicionado: {siteName}");
            }

            // Navega para a URL
            _webScrapingService.NavigateTo(url);

            System.Threading.Thread.Sleep(new Random().Next(2145, 3992));

            // Confirmar verificação de idade
            string novibetCheckboxSelector = "div.ageRestrictionOptions_option:nth-of-type(1)";
            string novibetConfirmButtonSelector = "nds-button.ageRestrictionModal_button button.button.large.teal";
            _gameService.ConfirmAgeVerificationWithCheckboxAndButton(_webScrapingService.GetWebDriver(), novibetCheckboxSelector, novibetConfirmButtonSelector);

            // Fechar o pop-up
            try
            {
                var closeButton = _webScrapingService.WaitForElement(".registerOrLogin_closeButton", 5000);
                System.Threading.Thread.Sleep(new Random().Next(1547, 2245));
                closeButton.Click();
                Console.WriteLine("Pop-up fechado com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tentar fechar o pop-up: {ex.Message}");
            }

            // Aguarda até que o primeiro elemento esperado esteja visível
            try
            {
                Console.WriteLine("Aguardando até 2 segundos antes de continuar...");
                System.Threading.Thread.Sleep(new Random().Next(878, 1147));
                _webScrapingService.WaitForElement("//app-event-marketview", 10000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar 'app-event-marketview': {ex.Message}");
                return new List<TagInfo>();
            }

            // Processamento dos elementos
            var processedCategories = new HashSet<int>();
            var categoriesCarousel = _webScrapingService.WaitForElement("app-event-market-categories", 10000);
            var eventPresentationViews = _webScrapingService.WaitForElements("app-event-presentation");

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

                        // Ajusta para o próximo dia se o horário já passou hoje
                        if (gameDateTime <= DateTime.Now)
                        {
                            gameDateTime = gameDateTime.AddDays(1);
                        }
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

                        // Montar o objeto DateTime no ano atual
                        gameDateTime = new DateTime(DateTime.Today.Year, month, day).Add(TimeSpan.Parse(time));

                        // Se a data estiver no passado, ajusta para o próximo ano
                        if (gameDateTime <= DateTime.Now)
                        {
                            gameDateTime = gameDateTime.AddYears(1);
                        }
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
                        if (targetDayIndex <= currentDayIndex)
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


            // Inicializa informações do jogo
            var homeTeamDb = _teamService.EnsureTeamExists(homeTeam);
            var awayTeamDb = _teamService.EnsureTeamExists(awayTeam);


            NovibetTags.AddDynamicTags(homeTeam, awayTeam);


            // Verifica se o jogo já existe no banco
            var existingGame = _dbContext.GamesInfo
                .FirstOrDefault(g =>
                    g.HomeTeamId == homeTeamDb &&
                    g.AwayTeamId == awayTeamDb &&
                    g.GameDate == gameDateTime &&
                    g.Site.SiteId == site.SiteId); // A comparação é feita usando o SiteId

            if (existingGame != null)
            {
                // Se o jogo já existe no banco, preenche o gamesInfo com os dados existentes
                gamesInfo = existingGame;
                gamesInfo.Status = 1;
                gamesInfo.LastUpdated = DateTime.Now;
            }
            else
            {
                // Se o jogo não existir no banco, cria um novo GamesInfo
                gamesInfo = new GamesInfo
                {
                    HomeTeamId = homeTeamDb,
                    AwayTeamId = awayTeamDb,
                    GameDate = gameDateTime,
                    League = gameName,
                    Site = site,
                    URL = url,
                    Status = 1,
                    LastUpdated = DateTime.Now
                };

                // Adiciona o novo jogo ao banco
                _dbContext.GamesInfo.Add(gamesInfo);
            }

            // Lista para armazenar resultados
            List<TagInfo> allTagInfos = new List<TagInfo>();

            // Captura todas as categorias disponíveis
            System.Threading.Thread.Sleep(new Random().Next(2145, 2987));

            // Captura apenas os elementos "swiper-slide" dentro do contêiner específico
            var categoryElements = _webScrapingService.FindElementsWithin(categoriesCarousel, ".swiper-slide", 5000).ToList();
            int totalCategories = categoryElements.Count; // Total inicial de categorias

            // Verifica se algum elemento foi encontrado
            if (categoryElements == null || !categoryElements.Any())
                throw new Exception("Nenhuma categoria encontrada no carrossel.");

            // Configuração de número máximo de retentativas
            const int maxRetries = 3;
            const int retryDelay = 1000; // Delay em milissegundos

            // Botão de navegação para a direita
            var nextButton = _webScrapingService.WaitForElement(".marketCategories_arrowRight.nextBtn.u-flex.u-flexCenter", 10000);

            int i = 0; // Índice inicial para o loop principal
            while (i < totalCategories)
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
                        // Verifica se o elemento da categoria está disponível antes de interagir
                        var categoryElement = _webScrapingService.FindElementsWithin(categoriesCarousel, ".swiper-slide .marketCategories_carouselItem", 5000).ElementAtOrDefault(i);
                        if (categoryElement == null)
                            throw new Exception($"Categoria {i} não encontrada no carrossel.");

                        // Tenta clicar na categoria
                        categoryElement.Click();
                        processedCategories.Add(i); // Marca como processada

                        // Estratégia de retentativa para carregar o elemento necessário
                        bool elementFound = false;

                        for (int loadAttempt = 0; loadAttempt <= maxRetries; loadAttempt++)
                        {
                            try
                            {
                                // Espera que os dados carreguem
                                var elements = _webScrapingService.WaitForElements("app-event-marketview", retryDelay * maxRetries);
                                if (elements.Count > 0)
                                {
                                    elementFound = true;
                                    break; // Sai do loop se o elemento for encontrado
                                }
                            }
                            catch (Exception ex)
                            {
                                if (loadAttempt < maxRetries)
                                {
                                    Console.WriteLine($"Tentativa {loadAttempt + 1} falhou. Aguardando {retryDelay / 1000} segundos...");
                                    Thread.Sleep(retryDelay); // Aguarda antes de tentar novamente
                                }
                                else
                                {
                                    Console.WriteLine($"Excedido o número de tentativas para carregar 'app-event-marketview': {ex.Message}");
                                }
                            }
                        }

                        if (!elementFound)
                            throw new Exception("Falha ao encontrar 'app-event-marketview' após múltiplas tentativas.");

                        Console.WriteLine($"Processando a Categoria: {categoryElement.Text} {i + 1}");

                        // Captura as tags para esta categoria
                        var tagInfos = ProcessMarketViews();
                        allTagInfos.AddRange(tagInfos); // Adiciona os resultados

                        categoryProcessed = true; // Marca a categoria como processada com sucesso
                    }
                    catch (ElementClickInterceptedException)
                    {
                        Console.WriteLine($"Categoria {i} interceptada. Tentando usar o botão 'Next' para ajustar...");
                        if (nextButton.GetDomAttribute("class").Contains("swiper-button-disabled"))
                        {
                            Console.WriteLine("Botão 'Next' desabilitado. Não é possível navegar mais.");
                            break; // Sai do loop de tentativas se não houver mais categorias acessíveis
                        }

                        nextButton.Click();
                        Thread.Sleep(retryDelay); // Aguardando para que o layout do carrossel seja ajustado
                        categoryElements = _webScrapingService.FindElementsWithin(categoriesCarousel, ".swiper-slide", 5000).ToList(); // Recarrega os elementos
                        totalCategories = categoryElements.Count; // Atualiza o total de categorias após recarregar
                    }
                    catch (ElementNotInteractableException)
                    {
                        Console.WriteLine($"Categoria {i} não interagível. Tentando usar o botão 'Next' para ajustar...");
                        if (nextButton.GetDomAttribute("class").Contains("swiper-button-disabled"))
                        {
                            Console.WriteLine("Botão 'Next' desabilitado. Não é possível navegar mais.");
                            break; // Sai do loop de tentativas se não houver mais categorias acessíveis
                        }

                        nextButton.Click();
                        Thread.Sleep(retryDelay); // Aguardando para que o layout do carrossel seja ajustado
                        categoryElements = _webScrapingService.FindElementsWithin(categoriesCarousel, ".swiper-slide", 5000).ToList(); // Recarrega os elementos
                        totalCategories = categoryElements.Count; // Atualiza o total de categorias após recarregar
                    }
                    catch (StaleElementReferenceException)
                    {
                        Console.WriteLine($"Elemento da categoria {i} ficou obsoleto. Recarregando elementos...");
                        categoryElements = _webScrapingService.FindElementsWithin(categoriesCarousel, ".swiper-slide", 5000).ToList(); // Recarrega os elementos
                        totalCategories = categoryElements.Count; // Atualiza o total de categorias após recarregar
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar categoria {i}: {ex.Message}");
                        attempts++;
                    }
                }

                // Use Page Up para voltar ao topo antes de sair do processamento da categoria
                try
                {
                    Console.WriteLine("Retornando ao topo da página...");
                    _webScrapingService.GetWebDriver().FindElement(By.TagName("body")).SendKeys(Keys.Home);
                    Thread.Sleep(500); // Aguarda um pequeno intervalo para garantir a rolagem
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao tentar voltar ao topo da página: {ex.Message}");
                }

                if (!categoryProcessed)
                {
                    Console.WriteLine($"Não foi possível processar a categoria {i} após {maxRetries} tentativas. Passando para a próxima.");
                }

                i++; // Move para a próxima categoria
            }

            _webScrapingService.Dispose();

            Console.WriteLine($"Scraping concluído para {siteName} com URL: {url}");

            return allTagInfos;

        }

        // Método para processar os "app-event-marketview" e capturar as tags
        private List<TagInfo> ProcessMarketViews()
        {
            // Lista para armazenar as apostas
            List<BetInfo> bets = new List<BetInfo>();
            List<TagInfo> tagInfos = new List<TagInfo>();

            // Encontra todos os contêineres de aposta
            System.Threading.Thread.Sleep(new Random().Next(2873, 3405));
            var eventMarketViews = _webScrapingService.WaitForElements("app-event-marketview", 5000);

            foreach (var eventMarketView in eventMarketViews)
            {
                try
                {
                    // Verifica se o evento contém uma tag válida
                    var tagElement = _webScrapingService.FindElementsWithin(eventMarketView, ".//span[contains(@class, 'eventMarketview_title')]").FirstOrDefault();
                    string tagName = tagElement.Text.Trim();

                    // Lista de tags cadastradas que queremos buscar
                    var tagNames = NovibetTags.TagNames;

                    // Verifica se a tag encontrada contém o nome da tag desejada, ignorando diferenças como emojis
                    if (tagNames.Values.Contains(tagName))
                    {

                        // Recupera o ID da tag a partir do dicionário
                        int tagId = NovibetTags.TagNames.FirstOrDefault(x => x.Value == tagName).Key;

                        // Expande as apostas, se necessário
                        try
                        {
                            var expandCollapseButton = _webScrapingService.TryWaitForElement(".//sb-market-bet-expand-collapse//span[contains(text(), 'Ver Mais')]", 2000);
                            if (expandCollapseButton != null)
                            {
                                expandCollapseButton.Click();
                                _webScrapingService.WaitForElements(".//span[contains(@class, 'marketBetItem_caption')]", 5000);
                            }
                        }
                        catch (NoSuchElementException)
                        {
                            // Se o botão "Ver Mais" não for encontrado, segue para o próximo passo
                            // Não há necessidade de fazer nada, pois as apostas já podem estar visíveis
                        }

                        // Remove emojis do texto da tag
                        string pattern = @"[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u200D\uFE0F]";
                        tagName = Regex.Replace(tagName, pattern, "").Trim();

                        // Encontra apostas e multiplicadores
                        var betElements = eventMarketView.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_caption singleLineEllipsis')]"));
                        var multiplierElements = eventMarketView.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_price')]"));

                        if (betElements.Count == multiplierElements.Count)
                        {
                            // Cria uma lista para armazenar as apostas atuais
                            var currentBets = new List<BetInfo>();

                            for (int i = 0; i < betElements.Count; i++)
                            {
                                string betName = betElements[i].Text.Trim();
                                string multiplier = multiplierElements[i].Text.Trim();

                                string overUnder = string.Empty;
                                decimal betAmount = 0;

                                if (!string.IsNullOrEmpty(betName) && !string.IsNullOrEmpty(multiplier))
                                {
                                    if (Regex.Match(betName, @"^(Mais de|Menos de)").Success)
                                    {
                                        overUnder = Regex.Match(betName, @"^(Mais de|Menos de)").Value;
                                    }
                                    else if (betName.StartsWith("+") || betName.EndsWith("+"))
                                    {
                                        overUnder = "Mais de";
                                    }

                                    if (Regex.Match(betName, @"(\+?\d+(?:,\d+)?)").Success)
                                    {
                                        betAmount = decimal.Parse(Regex.Match(betName, @"(\+?\d+(?:,\d+)?)").Value.Replace(",", "."), CultureInfo.InvariantCulture);
                                    }

                                    // Substituir o nome do time na tag por "Casa" ou "Visitante", respeitando a estrutura do texto
                                    string adjustedTagName = tagName;

                                    if (tagName.Contains(homeTeam, StringComparison.OrdinalIgnoreCase))
                                    {
                                        adjustedTagName = adjustedTagName.Replace(homeTeam, "Casa", StringComparison.OrdinalIgnoreCase);
                                    }

                                    if (tagName.Contains(awayTeam, StringComparison.OrdinalIgnoreCase))
                                    {
                                        adjustedTagName = adjustedTagName.Replace(awayTeam, "Visitante", StringComparison.OrdinalIgnoreCase);
                                    }

                                    currentBets.Add(new BetInfo
                                    {
                                        GamesInfo = gamesInfo,
                                        TagName = adjustedTagName,
                                        OverUnder = overUnder,
                                        BetAmount = betAmount,
                                        Multiplier = decimal.Parse(multiplier.Replace(",", "."), CultureInfo.InvariantCulture),
                                        GameDate = gamesInfo.GameDate,
                                        CaptureDate = DateTime.Now,
                                        Site = gamesInfo.Site,
                                        TagId = tagId
                                    });
                                }
                            }

                            // **1. Busca apostas existentes no banco com todos os critérios**
                            foreach (var bet in currentBets)
                            {
                                var existingBet = _dbContext.BetInfo.Where(b =>
                                    b.GamesInfo.GameId == gamesInfo.GameId &&
                                    b.TagName == bet.TagName &&
                                    b.OverUnder == bet.OverUnder &&
                                    //b.BetAmount == bet.BetAmount &&
                                    b.TagId == bet.TagId &&
                                    b.Site.SiteId == bet.Site.SiteId).ToList();

                                if (existingBet.Count != 0)
                                {
                                    // **Deletar apostas duplicadas que já estão no banco**
                                    Console.WriteLine($"Aposta existente encontrada. Removendo a aposta duplicada...");
                                    _dbContext.BetInfo.RemoveRange(existingBet);
                                }
                            }

                            // **2. Adicionar as novas apostas**
                            _dbContext.BetInfo.AddRange(currentBets);

                            // **3. Salvar as alterações no banco de dados**
                            if (_dbContext.ChangeTracker.HasChanges())
                            {
                                _dbContext.SaveChanges();
                                Console.WriteLine("Alterações salvas com sucesso.");
                            }
                        }
                    }
                }
                catch (NoSuchElementException)
                {
                    // Ignora erros de elementos não encontrados
                    continue;
                }
                catch (WebDriverTimeoutException)
                {
                    // Ignora erros de timeout
                    continue;
                }
            }


            return tagInfos;
        }
    }
}
