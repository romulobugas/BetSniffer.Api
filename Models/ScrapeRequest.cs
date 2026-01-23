namespace BetSniffer.Api.Models
{
    public class ScrapeRequest
    {
        public string URL { get; set; } = string.Empty;
        public DateTime? GameDate { get; set; }
        public int? HomeTeam { get; set; }
        public int? AwayTeam { get; set; }
    }


}
