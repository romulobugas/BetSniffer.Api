using BetSniffer.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Data;

namespace BetSniffer.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class IaController : ControllerBase
{
    private readonly ILogger<IaController> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly BetSniffer.Api.Core.Services.TeamService _teamService;

    public IaController(ILogger<IaController> logger, ApplicationDbContext dbContext, BetSniffer.Api.Core.Services.TeamService teamService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _teamService = teamService;
    }

    [HttpGet("activities")]
    public async Task<IActionResult> GetActivities()
    {
        var activities = await _dbContext.IaActivities
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .ToListAsync();
        return Ok(activities);
    }

    [HttpPost("markets")]
    public async Task<IActionResult> ReceiveMarkets([FromBody] IaExtractedMarketsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("[IA] Recebido {MarketCount} mercados para o job {JobId} no site {SiteName}. Jogo: {Home} vs {Away}", 
            request.Markets.Count, request.JobId, request.SiteName, request.HomeTeam ?? "?", request.AwayTeam ?? "?");

        var homeId = !string.IsNullOrEmpty(request.HomeTeam) ? await _teamService.EnsureTeamExistsAsync(request.HomeTeam) : 0;
        var awayId = !string.IsNullOrEmpty(request.AwayTeam) ? await _teamService.EnsureTeamExistsAsync(request.AwayTeam) : 0;
        
        DateTime gameDateParsed = DateTime.Now;
        if (!string.IsNullOrEmpty(request.GameDate))
        {
            gameDateParsed = ParseFriendlyDate(request.GameDate);
        }

        var site = await _dbContext.Site.FirstOrDefaultAsync(s => s.Name.ToLower() == request.SiteName.ToLower());
        if (site == null)
        {
            site = new Site { Name = request.SiteName };
            _dbContext.Site.Add(site);
            await _dbContext.SaveChangesAsync();
        }

        // Busca o jogo seguindo o padrão do SuperbetScraping: Times + Data + Site
        var game = await _dbContext.GamesInfo
            .Include(g => g.Site)
            .FirstOrDefaultAsync(g => 
                ((g.HomeTeamId == homeId && g.AwayTeamId == awayId) || g.URL == request.GameUrl) &&
                g.SiteId == site.SiteId);

        // Se o jogo não existe, cria ele agora (Identidade do Jogo)
        if (game == null && homeId > 0 && awayId > 0)
        {
            _logger.LogInformation("[IA] Jogo novo detectado. Criando Identidade para: {Home} vs {Away}", request.HomeTeam, request.AwayTeam);
            
            game = new GamesInfo
            {
                HomeTeamId = homeId,
                AwayTeamId = awayId,
                GameDate = gameDateParsed,
                League = request.League ?? "Extraído via IA",
                SiteId = site.SiteId,
                URL = request.GameUrl,
                Status = 1,
                LastUpdated = DateTime.Now,
                GameName = $"{request.HomeTeam} - {request.AwayTeam}"
            };

            _dbContext.GamesInfo.Add(game);
            await _dbContext.SaveChangesAsync();
        }

        if (game == null)
        {
            _logger.LogWarning("[IA] Jogo não encontrado e metadados insuficientes para criação. URL: {Url}", request.GameUrl);
            return NotFound("Jogo não encontrado e sem metadados.");
        }

        // Atualiza status da atividade
        var activity = await _dbContext.IaActivities.FirstOrDefaultAsync(a => a.JobId == request.JobId);
        if (activity != null)
        {
            activity.Status = "Concluído";
            activity.HomeTeam = request.HomeTeam;
            activity.AwayTeam = request.AwayTeam;
            
            if (!string.IsNullOrEmpty(request.GameDate))
            {
                activity.GameDate = ParseFriendlyDate(request.GameDate);
            }
            
            activity.League = request.League;
            activity.LastAction = $"Extraídos {request.Markets.Count} mercados";
            _dbContext.Update(activity);
        }

        // Limpa apostas anteriores da IA para este jogo no banco para evitar duplicatas se necessário
        // Ou deixa o SaveBets lidar (vamos seguir o padrão do SuperbetScraping de limpar antes de inserir novas do mesmo lote)
        
        var newBets = new List<BetInfo>();

        foreach (var market in request.Markets)
        {
            int tagId = ResolveTagId(request.SiteName, market.MarketName);
            
            // Tenta processar Odds como JSON (caso seja array) ou string simples
            try 
            {
                var oddsJson = market.Odds;
                if (oddsJson.StartsWith("[") || oddsJson.StartsWith("{"))
                {
                    // É um JSON complexo (provavelmente array de linhas Over/Under)
                    using var doc = System.Text.Json.JsonDocument.Parse(oddsJson);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            string value = item.TryGetProperty("value", out var v) ? v.GetString() : "0";
                            string price = item.TryGetProperty("price", out var p) ? p.GetString() : "0";

                            newBets.Add(new BetInfo {
                                GameId = game.GameId,
                                SiteId = game.SiteId ?? 0,
                                TagName = market.MarketName,
                                OverUnder = market.SelectionName,
                                BetAmount = decimal.Parse((value ?? "0").Replace(".", ",")),
                                Multiplier = decimal.Parse((price ?? "0").Replace(".", ",")),
                                CaptureDate = DateTime.Now,
                                GameDate = game.GameDate,
                                TagId = tagId
                            });
                        }
                    }
                }
                else 
                {
                    // Odds simples
                    newBets.Add(new BetInfo {
                        GameId = game.GameId,
                        SiteId = game.SiteId ?? 0,
                        TagName = market.MarketName,
                        OverUnder = market.SelectionName,
                        BetAmount = decimal.Parse((market.HandicapLine ?? "0").Replace(".", ",")),
                        Multiplier = decimal.Parse(oddsJson.Replace(".", ",")),
                        CaptureDate = DateTime.Now,
                        GameDate = game.GameDate,
                        TagId = tagId
                    });
                }
            } 
            catch (Exception ex)
            {
                _logger.LogWarning("[IA] Erro ao parsear odds/handicap para {Market}: {Error}", market.MarketName, ex.Message);
            }
        }

        if (newBets.Any())
        {
            // Remove apostas antigas desse jogo/site para atualizar com as novas
            var oldBets = _dbContext.BetInfo.Where(b => b.GameId == game.GameId && b.SiteId == game.SiteId);
            _dbContext.BetInfo.RemoveRange(oldBets);
            
            _dbContext.BetInfo.AddRange(newBets);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("[IA] Salvas {Count} novas apostas via IA para o jogo ID {Id}", newBets.Count, game.GameId);
        }

        return Ok(new { message = $"Sucesso: {newBets.Count} apostas persistidas no banco." });
    }

    private DateTime ParseFriendlyDate(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText)) return DateTime.Now;

        var now = DateTime.Now;
        var text = dateText.ToLower();

        if (text.Contains("hoje") || text.Contains("today")) return now;
        if (text.Contains("amanhã") || text.Contains("tomorrow")) return now.AddDays(1);

        // Tenta formatos comuns: "11 Mar 19:00", "11/03/2026", etc.
        if (DateTime.TryParse(dateText, out var d)) return d;

        // Fallback básico para evitar falha
        return now;
    }

    private int ResolveTagId(string siteName, string marketName)
    {
        // Mapeamento simplificado baseado nas tags comuns (pode ser expandido ou injetado)
        var normalized = marketName.ToLower();
        if (normalized.Contains("gols")) return 6;
        if (normalized.Contains("escanteios") || normalized.Contains("corners")) return 1;
        if (normalized.Contains("cartões") || normalized.Contains("cards")) return 34;
        if (normalized.Contains("chutes") || normalized.Contains("shots")) return 4;
        if (normalized.Contains("impedimentos")) return 7;
        if (normalized.Contains("faltas")) return 5;
        
        return 0; // Desconhecido
    }

    [HttpPost("enqueue")]
    public async Task<IActionResult> EnqueueJobs([FromBody] List<IaEnqueueRequest> requests)
    {
        if (requests == null || requests.Count == 0)
        {
            return BadRequest("Nenhum link fornecido.");
        }

        try
        {
            // O caminho absoluto do projeto IA
            string iaProjectPath = @"f:\repository\BetSniffer.Ia";
            string jobsFilePath = Path.Combine(iaProjectPath, "jobs.json");

            var newJobs = requests.Select(r => {
                var jobId = $"ia-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4]}";
                var site = ExtractSiteName(r.Url);
                
                // Registra atividade pendente
                _dbContext.IaActivities.Add(new IaActivity {
                    JobId = jobId,
                    SiteName = site,
                    GameUrl = r.Url,
                    Status = "Pendente",
                    Timestamp = DateTime.Now
                });

                return new {
                    jobId = jobId,
                    siteName = site,
                    gameUrl = r.Url,
                    tagsToTrack = new[] { "Total de Gols Mais/Menos", "Escanteios Mais/Menos" }
                };
            }).ToList();

            await _dbContext.SaveChangesAsync();

            string json = System.Text.Json.JsonSerializer.Serialize(newJobs, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(jobsFilePath, json);

            _logger.LogInformation("[IA] {Count} novos jobs enfileirados em {Path}", newJobs.Count, jobsFilePath);

            return Ok(new { message = $"{newJobs.Count} links enviados para IA.", path = jobsFilePath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enfileirar jobs para IA.");
            return StatusCode(500, $"Erro interno: {ex.Message}");
        }
    }

    private static string ExtractSiteName(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "desconhecido";
        
        var normalizedUrl = url.ToLower();
        if (normalizedUrl.Contains("betano")) return "betano";
        if (normalizedUrl.Contains("betfair")) return "betfair";
        if (normalizedUrl.Contains("betnacional")) return "betnacional";
        if (normalizedUrl.Contains("kto")) return "kto";
        if (normalizedUrl.Contains("pixbet")) return "pixbet";
        if (normalizedUrl.Contains("novibet")) return "novibet";
        if (normalizedUrl.Contains("superbet")) return "superbet";
        if (normalizedUrl.Contains("bet365")) return "bet365";
        if (normalizedUrl.Contains("vbet")) return "vbet";

        try 
        {
            var uri = new Uri(url);
            var hostParts = uri.Host.Split('.');
            if (hostParts.Length >= 2)
            {
                // Tenta pegar o nome principal (ex: superbet.bet.br -> superbet)
                return hostParts.FirstOrDefault(p => p != "www" && p != "com" && p != "br" && p != "net") ?? "desconhecido";
            }
        }
        catch { /* Ignora erro de URI malformada */ }

        return "desconhecido";
    }

    [HttpPost("batch-enqueue")]
    public async Task<IActionResult> BatchEnqueue([FromBody] BatchEnqueueRequest request)
    {
        if (request == null || request.SiteNames == null || !request.SiteNames.Any())
            return BadRequest("Solicitação inválida.");

        try 
        {
            var startTime = DateTime.Parse(request.StartDate);
            var endTime = DateTime.Parse(request.EndDate).AddDays(1).AddSeconds(-1);

            // Busca os jogos no banco de dados para os sites e período selecionados
            var games = await _dbContext.GamesInfo
                .Include(g => g.Site)
                .Include(g => g.HomeTeam)
                .Include(g => g.AwayTeam)
                .Where(g => request.SiteNames.Contains(g.Site.Name) && 
                            g.GameDate >= startTime && 
                            g.GameDate <= endTime)
                .ToListAsync();

            if (!games.Any())
                return Ok(new { message = "Nenhum jogo encontrado para o período e sites selecionados." });

            string iaProjectPath = @"f:\repository\BetSniffer.Ia";
            string jobsFilePath = Path.Combine(iaProjectPath, "jobs.json");
            
            List<ScrapingJobType> jobs = new();

            int addedCount = 0;
            foreach (var game in games)
            {
                var jobId = $"ia-batch-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..4]}";
                
                _dbContext.IaActivities.Add(new IaActivity {
                    JobId = jobId,
                    SiteName = game.Site.Name.ToLower(),
                    GameUrl = game.URL,
                    HomeTeam = game.HomeTeam?.NormalizedName ?? "?",
                    AwayTeam = game.AwayTeam?.NormalizedName ?? "?",
                    GameDate = game.GameDate,
                    Status = "Pendente",
                    Timestamp = DateTime.Now
                });

                jobs.Add(new ScrapingJobType { 
                    jobId = jobId,
                    siteName = game.Site.Name.ToLower(),
                    gameUrl = game.URL,
                    homeTeam = game.HomeTeam?.NormalizedName ?? "?",
                    awayTeam = game.AwayTeam?.NormalizedName ?? "?",
                    tagsToTrack = new[] { "Total de Gols Mais/Menos", "Escanteios Mais/Menos" }
                });
                addedCount++;
            }

            await _dbContext.SaveChangesAsync();
            await System.IO.File.WriteAllTextAsync(jobsFilePath, System.Text.Json.JsonSerializer.Serialize(jobs, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            return Ok(new { message = $"{addedCount} jogos adicionados à fila da IA." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no BatchEnqueue.");
            return StatusCode(500, ex.Message);
        }
    }
}

public class IaEnqueueRequest
{
    public string Url { get; set; } = string.Empty;
}

public class BatchEnqueueRequest
{
    public List<string> SiteNames { get; set; } = new();
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

public class ScrapingJobType
{
    public string jobId { get; set; } = string.Empty;
    public string siteName { get; set; } = string.Empty;
    public string gameUrl { get; set; } = string.Empty;
    public string homeTeam { get; set; } = "?";
    public string awayTeam { get; set; } = "?";
    public string[] tagsToTrack { get; set; } = Array.Empty<string>();
}
