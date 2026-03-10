using BetSniffer.Ia.Configuration;
using BetSniffer.Ia.Models;
using BetSniffer.Ia.Services;
using Microsoft.Extensions.Options;

namespace BetSniffer.Ia.Orchestration;

public sealed class VisualScrapingCoordinator
{
    private readonly AgentOptions _options;
    private readonly IVisionAgentClient _visionAgent;
    private readonly IWindowAutomationService _windowAutomation;
    private readonly BetSnifferApiClient _apiClient;

    public VisualScrapingCoordinator(
        IOptions<AgentOptions> options,
        IVisionAgentClient visionAgent,
        IWindowAutomationService windowAutomation,
        BetSnifferApiClient apiClient)
    {
        _options = options.Value;
        _visionAgent = visionAgent;
        _windowAutomation = windowAutomation;
        _apiClient = apiClient;
    }

    public async Task RunAsync(IReadOnlyCollection<ScrapingJob> jobs, CancellationToken cancellationToken)
    {
        var supportedSites = await _apiClient.GetSupportedSitesAsync(cancellationToken);

        using var semaphore = new SemaphoreSlim(_options.ParallelWindows);
        var tasks = jobs.Select(async job =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!supportedSites.Contains(job.SiteName, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[SKIP] Site '{job.SiteName}' não suportado pela API.");
                    return;
                }

                await RunJobAsync(job, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task RunJobAsync(ScrapingJob job, CancellationToken cancellationToken)
    {
        var window = await _windowAutomation.OpenBrowserAsync(job.GameUrl, cancellationToken);
        var previous = new AgentObservation { Notes = "Início da navegação." };

        try
        {
            for (var step = 1; step <= _options.MaxStepsPerJob; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var screenshot = await _windowAutomation.CaptureScreenshotBase64Async(window, cancellationToken);

                var request = new VisualAgentRequest
                {
                    JobId = job.JobId,
                    SiteName = job.SiteName,
                    GameUrl = job.GameUrl,
                    ScreenshotBase64 = screenshot,
                    TargetTags = job.TagsToTrack,
                    Goal = "Expandir mercados da partida, validar tags alvo e extrair odds.",
                    PreviousObservation = previous
                };

                var response = await _visionAgent.DecideNextActionAsync(request, cancellationToken);

                if (response.Action == AgentActionType.ExtractMarkets && response.Markets is { Count: > 0 })
                {
                    await _apiClient.SendExtractedMarketsAsync(job, response.Markets, cancellationToken);
                }

                if (response.Action is AgentActionType.Finish or AgentActionType.Fail)
                {
                    Console.WriteLine($"[JOB {job.JobId}] fim por ação '{response.Action}' no passo {step}.");
                    break;
                }

                await _windowAutomation.ExecuteActionAsync(window, response, cancellationToken);
                previous = new AgentObservation
                {
                    Notes = response.Reason,
                    IsPageLoaded = response.Action != AgentActionType.Wait,
                    FoundMarketContainer = response.Markets is { Count: > 0 }
                };

                await Task.Delay(_options.StepDelayMs, cancellationToken);
            }
        }
        finally
        {
            await _windowAutomation.CloseWindowAsync(window, cancellationToken);
        }
    }
}
