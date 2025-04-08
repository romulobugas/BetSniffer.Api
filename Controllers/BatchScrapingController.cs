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
using BetSniffer.Api.Core.Sites.Superbet;
using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Sites.Betnacional;
using BetSniffer.Api.Core.Sites.KTO;

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
        public IActionResult ScrapeTagsBatch([FromBody] List<ScrapeRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return BadRequest(new { message = "A lista de URLs não pode estar vazia." });
            }

            // Ordenar pela data e times, valores nulos são tratados no final
            var sortedRequests = requests
                .OrderBy(req => req.GameDate ?? DateTime.MaxValue)
                .ThenBy(req => req.HomeTeam ?? int.MaxValue)
                .ThenBy(req => req.AwayTeam ?? int.MaxValue)
                .ToList();

            int maxThreads = _scrapingSettings.MaxConcurrentThreads;
            SemaphoreSlim semaphore = new SemaphoreSlim(maxThreads);

            var results = new ConcurrentBag<object>();
            var errors = new ConcurrentBag<string>();

            foreach (var request in sortedRequests)
            {
                if (_runningTasks.ContainsKey(request.URL))
                {
                    errors.Add($"URL já está em processamento: {request.URL}");
                    continue;
                }

                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var teamService = scope.ServiceProvider.GetRequiredService<TeamService>();
                        var gamesInfoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryService<GamesInfo>>();
                        var betInfoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryService<BetInfo>>();

                        try
                        {
                            string siteName = ExtractSiteName(request.URL);

                            if (!SupportedSites.IsSiteSupported(siteName))
                            {
                                errors.Add($"Site não suportado: {siteName}");
                                return;
                            }

                            var scrapingService = GetScrapingService(siteName, dbContext, teamService, gamesInfoRepo, betInfoRepo);

                            scrapingService.ScrapeTags(request.URL, siteName);

                            results.Add(new
                            {
                                Url = request.URL,
                                SiteName = siteName,
                                Result = "Sucesso",
                                GameDate = request.GameDate,
                                HomeTeam = request.HomeTeam,
                                AwayTeam = request.AwayTeam
                            });
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Erro ao processar URL '{request.URL}': {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Erro geral no processamento de URL '{request.URL}': {ex.Message}");
                    }
                    finally
                    {
                        _runningTasks.TryRemove(request.URL, out _);
                        semaphore.Release();
                    }
                });

                _runningTasks.TryAdd(request.URL, task);
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

            // Verifica se a URL pertence à Pixbet
            if (url.Contains("pixbet", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("fssb.io", StringComparison.OrdinalIgnoreCase))
            {
                return "pixbet";
            }

            string[] parts = host.Split('.');

            // Se houver mais de dois componentes e o último for um domínio de nível superior (.br, .com, etc.), pega o penúltimo
            if (parts.Length >= 3)
            {
                return parts[parts.Length - 3]; // Ex: betfast.bet.br -> "betfast"
            }
            else if (parts.Length == 2)
            {
                return parts[0]; // Ex: vbet.bet -> "vbet"
            }

            return host;
        }

        private IScrapingService GetScrapingService(string siteName, ApplicationDbContext dbContext, TeamService teamService, IRepositoryService<GamesInfo> gamesInfoRepository,IRepositoryService<BetInfo> betInfoRepository)
        {
            return siteName.ToLower() switch
            {
                "novibet" => new NovibetScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "vbet" => new VbetScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "betano" => new BetanoScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "betfast" => new BetfastScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "betfair" => new BetfairScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "superbet" => new SuperbetScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "pixbet" => new PixbetScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "betnacional" => new BetnacionalScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
                "kto" => new KTOScraping(dbContext, teamService, gamesInfoRepository, betInfoRepository),
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
                                 .OrderBy(g => g.GameDate) // Ordenar por data do jogo
                                 .ThenBy(g => g.HomeTeam) // Ordenar por time da casa
                                 .ThenBy(g => g.AwayTeam) // Ordenar por time visitante
                                 .ToList();


                // Seleciona e organiza as URLs com base nos critérios especificados
                var requests = games.SelectMany(g => g.URLs.Select(url => new ScrapeRequest
                {
                    URL = url,
                    GameDate = g.GameDate,
                    HomeTeam = g.HomeTeam,
                    AwayTeam = g.AwayTeam
                }))
                .Distinct()
                .OrderBy(req => req.GameDate) // Ordena pela data do jogo
                .ThenBy(req => req.HomeTeam)  // Ordena pelo ID do time da casa
                .ThenBy(req => req.AwayTeam)  // Ordena pelo ID do time visitante
                .ToList();


                if (!requests.Any())
                {
                    return Ok(new { message = "Nenhum jogo com URL para scraping foi encontrado." });
                }

                // Enviar as URLs e datas para o ScrapeTagsBatch
                var scrapingResult = ScrapeTagsBatch(requests) as OkObjectResult;

                return Ok(new
                {
                    message = $"Jogos para o intervalo de datas informado foram enviados para scraping. Total de URLs: {requests.Count}",
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

        [HttpGet("arbitrage-results")]
        public IActionResult GetArbitrageResults([FromQuery] int? siteIdX, [FromQuery] int? siteIdY, [FromQuery] decimal? minPercentage, [FromQuery] decimal? maxPercentage)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var query = dbContext.ArbitrageResults.AsQueryable();

                // Filtros opcionais
                if (siteIdX.HasValue)
                {
                    query = query.Where(ar => ar.SiteIdX == siteIdX.Value);
                }

                if (siteIdY.HasValue)
                {
                    query = query.Where(ar => ar.SiteIdY == siteIdY.Value);
                }

                if (minPercentage.HasValue)
                {
                    query = query.Where(ar => ar.ArbitrageLucroPercent >= minPercentage.Value);
                }

                if (maxPercentage.HasValue)
                {
                    query = query.Where(ar => ar.ArbitrageLucroPercent <= maxPercentage.Value);
                }

                // Seleciona os campos necessários (excluindo os solicitados)
                var results = query
                    .Select(ar => new
                    {
                        ar.ArbitrageLucroPercent,
                        ar.TagNameX,
                        ar.OverUnderX,
                        ar.BetAmountX,
                        ar.MultiplierX,
                        ar.HomeTeam,
                        ar.SiteNameX,
                        ar.SiteNameY,
                        ar.AwayTeam,
                        ar.OverUnderY,
                        ar.BetAmountY,
                        ar.MultiplierY,
                        ar.TagNameY,
                        ar.GameDateX,
                        ar.LeagueX,
                        ar.LeagueY,
                        ar.GameX,
                        ar.GameY,
                        ar.URLX,
                        ar.URLY
                    })
                    .OrderByDescending(ar => ar.ArbitrageLucroPercent)
                    .ToList();

                if (!results.Any())
                {
                    return NotFound(new { message = "Nenhum resultado de arbitragem encontrado." });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao buscar os resultados de arbitragem: {ex.Message}" });
            }
        }

        [HttpPost("execute-arbitrage")]
        public IActionResult ExecuteArbitrageCalculation()
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Define um timeout de 180 segundos (3 minutos)
                dbContext.Database.SetCommandTimeout(180);

                // Executa a procedure diretamente
                dbContext.Database.ExecuteSqlRaw("EXEC ExecuteArbitrageCalculation");

                // Conta os resultados da tabela ArbitrageResults
                var resultsCount = dbContext.ArbitrageResults.Count();

                // Retorna a mensagem apropriada
                if (resultsCount > 0)
                {
                    return Ok(new { message = $"{resultsCount} Apostas encontradas" });
                }
                else
                {
                    return Ok(new { message = "Nenhuma aposta encontrada" });
                }
            }
            catch (Exception ex)
            {
                // Retorna erro em caso de falha
                return StatusCode(500, new { message = $"Erro ao executar a arbitragem: {ex.Message}" });
            }
        }

        [HttpPost("scrape-leagues")]
        public IActionResult ScrapeLeaguesBatch([FromBody] List<string> leagueUrls)
        {
            if (leagueUrls == null || !leagueUrls.Any())
                return BadRequest(new { message = "A lista de ligas não pode estar vazia." });

            var results = new ConcurrentBag<object>();
            var errors = new ConcurrentBag<string>();

            int maxThreads = _scrapingSettings.MaxConcurrentThreads;
            SemaphoreSlim semaphore = new(maxThreads);

            foreach (var url in leagueUrls.Distinct())
            {
                if (_runningTasks.ContainsKey(url))
                {
                    errors.Add($"URL já está em processamento: {url}");
                    continue;
                }

                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var teamService = scope.ServiceProvider.GetRequiredService<TeamService>();
                        var gamesInfoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryService<GamesInfo>>();
                        var betInfoRepo = scope.ServiceProvider.GetRequiredService<IRepositoryService<BetInfo>>();

                        string siteName = ExtractSiteName(url);

                        if (!SupportedSites.IsSiteSupported(siteName))
                        {
                            errors.Add($"Site não suportado: {siteName}");
                            return;
                        }

                        var scraper = GetScrapingService(siteName, dbContext, teamService, gamesInfoRepo, betInfoRepo);

                        // Detecta se é uma função de leitura por liga
                        if (scraper is ILeagueScrapingService leagueScraper)
                        {
                            leagueScraper.ScrapeLeague(url, siteName);
                            results.Add(new { Url = url, SiteName = siteName, Result = "Liga processada com sucesso" });
                        }
                        else
                        {
                            errors.Add($"Scraper para {siteName} não suporta raspagem por liga.");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Erro ao processar liga '{url}': {ex.Message}");
                    }
                    finally
                    {
                        _runningTasks.TryRemove(url, out _);
                        semaphore.Release();
                    }
                });

                _runningTasks.TryAdd(url, task);
            }

            return Ok(new
            {
                message = "Processamento de ligas iniciado.",
                results,
                errors
            });
        }


    }
}
