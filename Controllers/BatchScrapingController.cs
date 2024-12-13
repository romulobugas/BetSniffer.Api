using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch;
using BetSniffer.Api.Core.Services;
using Microsoft.AspNetCore.Mvc;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using System.Diagnostics;
using BetSniffer.Api.Core.Sites.Bet365;
using BetSniffer.Api.Core.Sites.Betano;
using BetSniffer.Api.Core.Sites;

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

            var results = new List<object>();
            var errors = new List<string>();

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
                    var scrapingService = GetScrapingService(siteName);

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

        private IScrapingService GetScrapingService(string siteName)
        {
            return siteName.ToLower() switch
            {
                "novibet" => new NovibetScraping(_dbContext, _teamService, _gamesInfoRepository, _betInfoRepository),
                "parimatch" => new ParimatchScraping(_dbContext, _teamService, _gamesInfoRepository, _betInfoRepository),
                "betano" => new BetanoScraping(_dbContext, _teamService, _gamesInfoRepository, _betInfoRepository),
                _ => throw new Exception($"Serviço de scraping não encontrado para o site: {siteName}")
            };
        }

        [HttpPut("batch-update-same-games")]
        public IActionResult UpdateSameGames([FromQuery] string startDate, [FromQuery] string endDate)
        {
            try
            {
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

                DateTime startOfDay = startGameDate.Date;

                if (startGameDate.Date == DateTime.Today)
                {
                    startOfDay = DateTime.Today.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute).AddSeconds(DateTime.Now.Second);
                    startOfDay = startOfDay.AddHours(2).AddMinutes(30);
                }

                DateTime endOfDay = endGameDate.Date.AddDays(1).AddSeconds(-1);

                var games = _dbContext.GamesInfo
                    .Where(g => g.GameDate >= startOfDay && g.GameDate <= endOfDay)
                    .Join(_dbContext.GamesInfo,
                        g1 => new { g1.HomeTeamId, g1.AwayTeamId },
                        g2 => new { g2.HomeTeamId, g2.AwayTeamId },
                        (g1, g2) => new { g1, g2 })
                    .Where(x => x.g1.SiteId != x.g2.SiteId)
                    .Where(x => !string.IsNullOrEmpty(x.g1.URL) && !string.IsNullOrEmpty(x.g2.URL))
                    .Select(x => new {
                        URL1 = x.g1.URL,
                        URL2 = x.g2.URL
                    })
                    .Distinct()
                    .ToList();

                if (games.Count == 0)
                {
                    return Ok(new { message = "Nenhum jogo encontrado para o intervalo de datas informado." });
                }

                var urlsToScrape = games
                    .SelectMany(g => new[] { g.URL1, g.URL2 })
                    .Distinct()
                    .ToList();

                if (urlsToScrape.Count == 0)
                {
                    return Ok(new { message = "Nenhum jogo com URL para scraping foi encontrado." });
                }

                var result = ScrapeTagsBatch(urlsToScrape);

                return Ok(new { message = "Jogos para o intervalo de datas informado foram atualizados com sucesso." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar os jogos: {ex.Message}" });
            }
        }
    }
}
