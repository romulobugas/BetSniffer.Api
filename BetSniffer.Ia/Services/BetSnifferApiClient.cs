using System.Net.Http.Json;
using BetSniffer.Ia.Configuration;
using BetSniffer.Ia.Models;
using Microsoft.Extensions.Options;

namespace BetSniffer.Ia.Services;

public sealed class BetSnifferApiClient
{
    private readonly HttpClient _httpClient;

    public BetSnifferApiClient(HttpClient httpClient, IOptions<BetSnifferApiOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
    }

    public async Task<IReadOnlyCollection<string>> GetSupportedSitesAsync(CancellationToken cancellationToken)
    {
        var sites = await _httpClient.GetFromJsonAsync<List<string>>("api/BatchScraping/sites", cancellationToken);
        return sites ?? [];
    }

    public async Task SendExtractedMarketsAsync(ScrapingJob job, IReadOnlyCollection<ExtractedMarket> markets, CancellationToken cancellationToken)
    {
        var payload = new
        {
            job.JobId,
            job.SiteName,
            job.GameUrl,
            job.HomeTeam,
            job.AwayTeam,
            Markets = markets
        };

        // Endpoint sugerido para receber payload da IA no BetSniffer.Api.
        using var response = await _httpClient.PostAsJsonAsync("api/ia/markets", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Falha ao enviar mercados para API: {(int)response.StatusCode} {errorBody}");
        }
    }
}
