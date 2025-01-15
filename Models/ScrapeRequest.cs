namespace BetSniffer.Api.Models
{
    public class ScrapeRequest
    {
        public string URL { get; set; }
        public DateTime? GameDate { get; set; }
        public int? HomeTeam { get; set; }
        public int? AwayTeam { get; set; }
    }


}
