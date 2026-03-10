using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BetSniffer.Ia.Configuration;
using BetSniffer.Ia.Models;
using Microsoft.Extensions.Options;

namespace BetSniffer.Ia.Services;

public sealed class LmStudioVisionAgentClient : IVisionAgentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly LmStudioOptions _options;

    public LmStudioVisionAgentClient(HttpClient httpClient, IOptions<LmStudioOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<VisualAgentResponse> DecideNextActionAsync(VisualAgentRequest request, CancellationToken cancellationToken)
    {
        var systemPrompt = """
            Você é um agente visual de automação para betting.
            Responda SOMENTE JSON válido no formato:
            {
              "action": "Wait|Click|DoubleClick|Scroll|Type|PressKey|ExtractMarkets|Finish|Fail",
              "confidence": 0.0,
              "reason": "...",
              "target": { "x": 0.0, "y": 0.0 },
              "scrollDelta": 0,
              "textInput": null,
              "key": null,
              "markets": [
                { "marketName": "...", "selectionName": "...", "odds": "...", "handicapLine": null }
              ]
            }
            x e y devem ser coordenadas relativas de 0.0 a 1.0.
            Use ExtractMarkets quando identificar mercados/alvos da lista de tags fornecida.
            """;

        var userPrompt = $"""
            Job: {request.JobId}
            Site: {request.SiteName}
            URL: {request.GameUrl}
            Goal: {request.Goal}
            Tags alvo: {string.Join(" | ", request.TargetTags)}
            Observação anterior: {request.PreviousObservation?.Notes ?? "nenhuma"}
            A página carregou?: {request.PreviousObservation?.IsPageLoaded?.ToString() ?? "desconhecido"}
            """;

        var payload = new
        {
            model = _options.Model,
            temperature = 0.1,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = userPrompt },
                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{request.ScreenshotBase64}" } }
                    }
                }
            }
        };

        var req = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await _httpClient.PostAsync("/v1/chat/completions", req, cancellationToken);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("LM Studio retornou conteúdo vazio.");
        }

        return JsonSerializer.Deserialize<VisualAgentResponse>(content, JsonOptions)
               ?? throw new InvalidOperationException("Não foi possível desserializar a resposta do agente visual.");
    }
}
