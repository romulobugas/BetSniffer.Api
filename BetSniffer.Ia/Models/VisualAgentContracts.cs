using System.Text.Json.Serialization;

namespace BetSniffer.Ia.Models;

public enum AgentActionType
{
    Wait,
    Click,
    DoubleClick,
    Scroll,
    Type,
    PressKey,
    ExtractMarkets,
    Finish,
    Fail
}

public sealed class VisualAgentRequest
{
    public required string JobId { get; init; }
    public required string SiteName { get; init; }
    public required string GameUrl { get; init; }
    public required string ScreenshotBase64 { get; init; }
    public required IReadOnlyCollection<string> TargetTags { get; init; }
    public required string Goal { get; init; }
    public AgentObservation? PreviousObservation { get; init; }
}

public sealed class AgentObservation
{
    public string? Notes { get; init; }
    public bool? IsPageLoaded { get; init; }
    public bool? FoundMarketContainer { get; init; }
}

public sealed class VisualAgentResponse
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentActionType Action { get; init; }

    public float Confidence { get; init; }
    public string Reason { get; init; } = string.Empty;
    public PointRatio? Target { get; init; }
    public int ScrollDelta { get; init; }
    public string? TextInput { get; init; }
    public string? Key { get; init; }
    public IReadOnlyCollection<ExtractedMarket>? Markets { get; init; }
}

public sealed class PointRatio
{
    public required float X { get; init; }
    public required float Y { get; init; }
}

public sealed class ExtractedMarket
{
    public required string MarketName { get; init; }
    public required string SelectionName { get; init; }
    public required string Odds { get; init; }
    public string? HandicapLine { get; init; }
}
