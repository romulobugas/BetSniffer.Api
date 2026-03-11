using System.ComponentModel.DataAnnotations;

namespace BetSniffer.Api.Models;

public class IaExtractedMarketsRequest
{
    [Required]
    public string JobId { get; set; } = string.Empty;

    [Required]
    public string SiteName { get; set; } = string.Empty;

    [Required]
    public string GameUrl { get; set; } = string.Empty;

    public string? HomeTeam { get; set; }
    public string? AwayTeam { get; set; }
    public string? GameDate { get; set; }
    public string? League { get; set; }

    [Required]
    public List<IaExtractedMarket> Markets { get; set; } = new();
}

public class IaExtractedMarket
{
    [Required]
    public string MarketName { get; set; } = string.Empty;

    [Required]
    public string SelectionName { get; set; } = string.Empty;

    [Required]
    public string Odds { get; set; } = string.Empty;
    
    public string? HandicapLine { get; set; }
}
