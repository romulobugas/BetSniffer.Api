using System.Text.Json;
using BetSniffer.Ia.Configuration;
using BetSniffer.Ia.Models;
using BetSniffer.Ia.Orchestration;
using BetSniffer.Ia.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables();

builder.Services.Configure<LmStudioOptions>(builder.Configuration.GetSection("LmStudio"));
builder.Services.Configure<BetSnifferApiOptions>(builder.Configuration.GetSection("BetSnifferApi"));
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));

builder.Services.AddHttpClient<IVisionAgentClient, LmStudioVisionAgentClient>();
builder.Services.AddHttpClient<BetSnifferApiClient>();
builder.Services.AddSingleton<IWindowAutomationService, WindowsInputAutomationService>();
builder.Services.AddSingleton<VisualScrapingCoordinator>();

using var host = builder.Build();

var jobsPath = Path.Combine(AppContext.BaseDirectory, "jobs.sample.json");
if (!File.Exists(jobsPath))
{
    Console.WriteLine($"Arquivo de jobs não encontrado em: {jobsPath}");
    return;
}

var jobsJson = await File.ReadAllTextAsync(jobsPath);
var jobs = JsonSerializer.Deserialize<List<ScrapingJob>>(jobsJson, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
});

if (jobs is not { Count: > 0 })
{
    Console.WriteLine("Nenhum job informado para execução.");
    return;
}

var coordinator = host.Services.GetRequiredService<VisualScrapingCoordinator>();
await coordinator.RunAsync(jobs, CancellationToken.None);
