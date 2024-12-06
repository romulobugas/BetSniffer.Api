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
            options.AddArgument("--force-device-scale-factor=1");
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
                "bet365" => new Bet365Scraping(driver, _dbContext, _teamService, _gamesInfoRepository, _betInfoRepository),
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

        // Função nativa do Windows para trazer a janela para o primeiro plano
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

    }
}
