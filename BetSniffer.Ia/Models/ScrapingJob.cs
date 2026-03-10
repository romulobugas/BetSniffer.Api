namespace BetSniffer.Ia.Models;

public sealed class ScrapingJob
{
    public required string JobId { get; init; }
    public required string SiteName { get; init; }
    public required string GameUrl { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public required IReadOnlyCollection<string> TagsToTrack { get; init; }
}
