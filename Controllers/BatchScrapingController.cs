using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Core.Sites.Betano;
using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Vbet;
using BetSniffer.Api.Core.Sites;
using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Threading;
using BetSniffer.Api.Configuration;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using BetSniffer.Api.Core.Sites.Betfast;
using BetSniffer.Api.Core.Sites.Betfair;
using BetSniffer.Api.Core.Sites.Bet365;
using System.Linq;
using System.Globalization;
using System.Reflection;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BatchScrapingController : ControllerBase
    {
        #region Globais

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private static readonly ConcurrentBag<Thread> _scrapingThreads = new();

        private static readonly ConcurrentDictionary<string, Task> _runningTasks = new();

        private readonly ScrapingSettings _scrapingSettings;


        #endregion

        public BatchScrapingController(IServiceScopeFactory serviceScopeFactory, IOptions<ScrapingSettings> scrapingSettings)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

            _scrapingSettings = scrapingSettings.Value ?? throw new ArgumentNullException(nameof(scrapingSettings));
        }

        [HttpPost("scrape")]
        public IActionResult ScrapeTagsBatch([FromBody] List<string> urls)
        {
            if (urls == null || urls.Count == 0)
            {
                return BadRequest(new { message = "A lista de URLs não pode estar vazia." });
            }

            // Remove duplicados na mesma requisição
            urls = urls.Distinct().ToList();

            // Limite de threads definido no appsettings
            int maxThreads = _scrapingSettings.MaxConcurrentThreads;
            SemaphoreSlim semaphore = new SemaphoreSlim(maxThreads);

            var results = new ConcurrentBag<object>();
            var errors = new ConcurrentBag<string>();

            foreach (var url in urls)
            {

                if (_runningTasks.ContainsKey(url))
                {
                    errors.Add($"URL já está em processamento: {url}");
                    continue;
                }

                // Cria uma thread para cada link
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(); // Aguarda a liberação de uma vaga no limite de threads

                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var teamService = scope.ServiceProvider.GetRequiredService<TeamService>();
                        var gamesInfoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryService<GamesInfo>>();
                        var betInfoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryService<BetInfo>>();

                        try
                        {
                            string siteName = ExtractSiteName(url);

                            if (!SupportedSites.IsSiteSupported(siteName))
                            {
                                errors.Add($"Site não suportado: {siteName}");
                                return;
                            }

                            // Obtem o serviço de scraping
                            var scrapingService = GetScrapingService(siteName, dbContext, teamService, gamesInfoRepo, betInfoRepo);

                            // Executa o scraping de forma síncrona
                            scrapingService.ScrapeTags(url, siteName);

                            results.Add(new { Url = url, SiteName = siteName, Result = "Sucesso" });
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Erro ao processar URL '{url}': {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Erro geral no processamento de URL '{url}': {ex.Message}");
                    }
                    finally
                    {
                        _runningTasks.TryRemove(url, out _);
                        Console.WriteLine($"Tarefa removida para URL: {url}");
                        semaphore.Release(); // Libera uma vaga no limite de threads

                    }
                });

                _runningTasks.TryAdd(url, task); // Adiciona a tarefa ao dicionário
                Console.WriteLine($"Tarefa adicionada para URL: {url}");
            }

            return Ok(new
            {
                message = "Scraping iniciado. Verifique os logs para acompanhar o progresso.",
                results,
                errors
            });
        }



        private string ExtractSiteName(string url)
        {
            var uri = new Uri(url);
            string host = uri.Host;
            string[] parts = host.Split('.');

            // Se houver mais de dois componentes e o último for um domínio de nível superior (.br, .com, etc.), pega o penúltimo
            if (parts.Length >= 3)
            {
                // Se o domínio for algo como betfast.bet.br, retorna "betfast"
                return parts[parts.Length - 3];
            }
            else if (parts.Length == 2)
            {
                // Para domínios como vbet.bet, retorna "vbet"
                return parts[0];
            }

            return host;
        }


        private IScrapingService GetScrapingService(
            string siteName,
            ApplicationDbContext dbContext,
            TeamService teamService,
            IRepositoryService<GamesInfo> gamesInfoRepository,
            IRepositoryService<BetInfo> betInfoRepository)
        {
            return siteName.ToLower() switch
            {
                "novibet" => new NovibetScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "vbet" => new VbetScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "betano" => new BetanoScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "betfast" => new BetfastScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "betfair" => new BetfairScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                _ => throw new Exception($"Serviço de scraping não encontrado para o site: {siteName}")
            };
        }

        [HttpGet("monitor")]
        public IActionResult MonitorScraping()
        {
            var activeTasks = _runningTasks.Where(t => !t.Value.IsCompleted).Select(t => t.Key).ToList();
            return Ok(new
            {
                activeTasksCount = activeTasks.Count,
                activeTasks // Retorna as URLs em processamento
            });
        }

        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Desconhecida";
            return Ok(new { version });
        }

        [HttpPut("batch-update-same-games")]
        public IActionResult UpdateSameGames([FromQuery] string startDate, [FromQuery] string endDate, [FromQuery] string siteIds)
        {
            try
            {
                string[] validFormats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" }; // Inclua formatos adicionais, se necessário.

                if (!DateTime.TryParseExact(startDate, validFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime startGameDate))
                {
                    return BadRequest(new { message = "Data inicial inválida. O formato correto é dd/MM/yyyy." });
                }

                if (!DateTime.TryParseExact(endDate, validFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime endGameDate))
                {
                    return BadRequest(new { message = "Data final inválida. O formato correto é dd/MM/yyyy." });
                }


                DateTime startOfDay = startGameDate.Date;
                DateTime endOfDay = endGameDate.Date.AddDays(1).AddSeconds(-1);

                if (startGameDate.Date == DateTime.Today)
                {
                    startOfDay = DateTime.Today.AddHours(DateTime.Now.Hour)
                                               .AddMinutes(DateTime.Now.Minute)
                                               .AddSeconds(DateTime.Now.Second)
                                               .AddHours(1).AddMinutes(15);
                }

                // Parse siteIds as a list of integers
                List<int> siteIdList = new List<int>();
                if (!string.IsNullOrEmpty(siteIds))
                {
                    siteIdList = siteIds.Split(',')
                                        .Select(id => int.TryParse(id, out int parsedId) ? parsedId : (int?)null)
                                        .Where(id => id.HasValue)
                                        .Select(id => id.Value)
                                        .ToList();
                }

                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                

                // Filtra os jogos com base nos sites selecionados (se fornecidos)
                var query = dbContext.GamesInfo.Where(g => g.GameDate >= startOfDay && g.GameDate <= endOfDay);

                if (siteIdList != null && siteIdList.Any())
                {
                    query = query.Where(g => siteIdList.Contains(g.SiteId.Value));
                }

                var games = query.AsEnumerable()
                         .GroupBy(g => new { g.GameDate, g.HomeTeamId, g.AwayTeamId })
                         .Where(group => group.Select(g => g.SiteId).Distinct().Count() > 1)
                         .Select(group => new
                         {
                             GameDate = group.Key.GameDate,
                             HomeTeam = group.Key.HomeTeamId,
                             AwayTeam = group.Key.AwayTeamId,
                             URLs = group.Where(g => !string.IsNullOrEmpty(g.URL))
                                         .Select(g => g.URL)
                                         .Distinct()
                                         .ToList()
                         })
                         .Where(g => g.URLs.Count > 0)
                         .OrderBy(g => g.GameDate) // Ordena os jogos pela data e hora mais próximos do horário atual
                         .ToList();

                var urlsToScrape = games.SelectMany(g => g.URLs).Distinct().ToList();

                if (!urlsToScrape.Any())
                {
                    return Ok(new { message = "Nenhum jogo com URL para scraping foi encontrado." });
                }

                // Reutiliza o método ScrapeTagsBatch para enviar as URLs
                var scrapingResult = ScrapeTagsBatch(urlsToScrape) as OkObjectResult;

                return Ok(new
                {
                    message = $"Jogos para o intervalo de datas informado foram enviados para scraping. Jogos: {urlsToScrape.Count}",
                    scrapingResult?.Value
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar os jogos: {ex.Message}" });
            }
        }

        [HttpGet("sites")]
        public IActionResult GetSites()
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var sites = dbContext.Site
                    .Select(s => new
                    {
                        SiteId = s.SiteId,
                        Name = s.Name
                    })
                    .ToList();

                if (!sites.Any())
                {
                    return NotFound(new { message = "Nenhum site encontrado." });
                }

                return Ok(sites);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao buscar sites: {ex.Message}" });
            }
        }


    }
}
