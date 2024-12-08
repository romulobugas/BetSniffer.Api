using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch;
using BetSniffer.Api.Core.Services;
using Microsoft.AspNetCore.Mvc;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System.Runtime.InteropServices;
using BetSniffer.Api.Core.Sites;
using System.Diagnostics;
using BetSniffer.Api.Core.Sites.Bet365;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BatchScrapingController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;
        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;

        public BatchScrapingController(
            ApplicationDbContext dbContext,
            TeamService teamService,
            IRepositoryService<GamesInfo> gamesInfoRepository,
            IRepositoryService<BetInfo> betInfoRepository)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
        }

        [HttpPost("scrape")]
        public IActionResult ScrapeTagsBatch([FromBody] List<string> urls)
        {
            if (urls == null || urls.Count == 0)
            {
                return BadRequest(new { message = "A lista de URLs não pode estar vazia." });
            }

            var options = new ChromeOptions();
            options.AddArgument("--no-sandbox");
            options.AddArgument("--force-device-scale-factor=0.1");
            options.AddArgument("--start-maximized");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.5735.198 Safari/537.36");
            options.AddArgument("--profile-directory=Default");



            var results = new List<object>();
            var errors = new List<string>();

            using (var driver = new ChromeDriver(options))
            {
                // Garante que o navegador esteja em evidência
                BringChromeToFront(driver);

                foreach (var url in urls)
                {
                    try
                    {
                        string siteName = ExtractSiteName(url);

                        if (!SupportedSites.IsSiteSupported(siteName))
                        {
                            errors.Add($"Site não suportado: {siteName}");
                            continue;
                        }

                        // Obtem o serviço de scraping
                        var scrapingService = GetScrapingService(siteName, driver);

                        // Executa o scraping
                        scrapingService.ScrapeTagsAsync(url, siteName);

                        // Adiciona o resultado à lista de sucessos
                        results.Add(new { Url = url, SiteName = siteName, Result = "Sucesso" });
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Erro ao processar URL '{url}': {ex.Message}");
                    }
                }
            }

            var jsonResponse = new
            {
                Results = results,
                Errors = errors
            };

            return Ok(jsonResponse);
        }

        private string ExtractSiteName(string url)
        {
            var uri = new Uri(url);
            string host = uri.Host;
            string[] parts = host.Split('.');
            return parts.Length >= 3 ? parts[1] : parts[0];
        }

        private IScrapingService GetScrapingService(string siteName, IWebDriver driver)
        {
            return siteName.ToLower() switch
            {
                "novibet" => new NovibetScraping(driver, _dbContext, _teamService, _gamesInfoRepository, _betInfoRepository),
                "parimatch" => new Parimatchcraping(driver, _dbContext, _teamService, _gamesInfoRepository, _betInfoRepository),
                //"bet365" => new Bet365Scraping(driver, _dbContext, _teamService, _gamesInfoRepository, _betInfoRepository),
                _ => throw new Exception($"Serviço de scraping não encontrado para o site: {siteName}")
            };
        }

        /// <summary>
        /// Traz a janela do navegador Chrome para o primeiro plano (ativa a janela).
        /// </summary>
        /// <param name="driver">Instância do WebDriver.</param>
        private void BringChromeToFront(IWebDriver driver)
        {
            try
            {
                // Obtenha todos os processos do Chrome em execução
                var processes = Process.GetProcessesByName("chrome");

                foreach (var process in processes)
                {
                    // Identifica o processo associado ao driver (caso existam vários)
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        // Traz o processo para o primeiro plano
                        SetForegroundWindow(process.MainWindowHandle);
                        return; // Encerra após trazer a primeira janela ativa para frente
                    }
                }

                Console.WriteLine("Não foi possível localizar uma janela ativa do Chrome.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao trazer o navegador para frente: {ex.Message}");
            }
        }

        // Método para atualização dos jogos com o mesmo GameDate, HomeTeam e AwayTeam
        [HttpPut("batch-update-same-games")]
        public IActionResult UpdateSameGames([FromQuery] string startDate, [FromQuery] string endDate)
        {
            try
            {
                // Converter as strings das datas para DateTime
                DateTime startGameDate;
                DateTime endGameDate;

                if (!DateTime.TryParseExact(startDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out startGameDate))
                {
                    return BadRequest(new { message = "Data inicial inválida. O formato correto é dd/MM/yyyy." });
                }

                if (!DateTime.TryParseExact(endDate, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out endGameDate))
                {
                    return BadRequest(new { message = "Data final inválida. O formato correto é dd/MM/yyyy." });
                }

                // Adiciona 2 horas e 30 minutos à data inicial (caso a data inicial seja a atual)
                DateTime startOfDay = startGameDate.Date;

                // Verifica se a data inicial é a data de hoje e ajusta o horário para a hora atual + 2:30h
                if (startGameDate.Date == DateTime.Today)
                {
                    startOfDay = DateTime.Today.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute).AddSeconds(DateTime.Now.Second);
                    startOfDay = startOfDay.AddHours(2).AddMinutes(30); // Adiciona 2 horas e 30 minutos ao horário atual
                }

                // Define o final do dia informado para a data final (23:59:59)
                DateTime endOfDay = endGameDate.Date.AddDays(1).AddSeconds(-1); // 23:59:59 do último dia informado

                // Obtém os jogos dentro do intervalo de tempo com os critérios de casamentos e URLs
                var games = _dbContext.GamesInfo
                    .Where(g => g.GameDate >= startOfDay && g.GameDate <= endOfDay)
                    .Join(_dbContext.GamesInfo,
                        g1 => new { g1.HomeTeamId, g1.AwayTeamId },
                        g2 => new { g2.HomeTeamId, g2.AwayTeamId },
                        (g1, g2) => new { g1, g2 })
                    .Where(x => x.g1.SiteId != x.g2.SiteId) // Apenas jogos casados com SiteId diferente
                    .Where(x => !string.IsNullOrEmpty(x.g1.URL) && !string.IsNullOrEmpty(x.g2.URL)) // Apenas jogos com URL em ambos os sites
                    .Select(x => new {
                        URL1 = x.g1.URL,  // Renomeia a propriedade para evitar conflito
                        URL2 = x.g2.URL   // Renomeia a propriedade para evitar conflito
                    })
                    .Distinct() // Evita URLs duplicadas
                    .ToList(); // Trazer para o lado do cliente para manipulação

                if (games.Count == 0)
                {
                    return Ok(new { message = "Nenhum jogo encontrado para o intervalo de datas informado." });
                }

                // Obter a lista de URLs dos jogos encontrados
                var urlsToScrape = games
                    .SelectMany(g => new[] { g.URL1, g.URL2 }) // Extrai as URLs de ambos os sites
                    .Distinct() // Evita URLs duplicadas
                    .ToList();

                if (urlsToScrape.Count == 0)
                {
                    return Ok(new { message = "Nenhum jogo com URL para scraping foi encontrado." });
                }

                // Envia a lista de URLs para o método ScrapeTagsBatch
                var result = ScrapeTagsBatch(urlsToScrape);

                return Ok(new { message = "Jogos para o intervalo de datas informado foram atualizados com sucesso." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar os jogos: {ex.Message}" });
            }
        }

        // Função nativa do Windows para trazer a janela para o primeiro plano
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

    }
}
