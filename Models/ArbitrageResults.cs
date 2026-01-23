namespace BetSniffer.Api.Models
{
    public class ArbitrageResults
    {
        public decimal ArbitrageLucroPercent { get; set; }
        public string TagNameX { get; set; } = string.Empty;
        public string OverUnderX { get; set; } = string.Empty;
        public decimal BetAmountX { get; set; }
        public decimal MultiplierX { get; set; }
        public string HomeTeam { get; set; } = string.Empty;
        public string SiteNameX { get; set; } = string.Empty;
        public string SiteNameY { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string OverUnderY { get; set; } = string.Empty;
        public decimal BetAmountY { get; set; }
        public decimal MultiplierY { get; set; }
        public string TagNameY { get; set; } = string.Empty;
        public int SiteIdX { get; set; }
        public DateTime GameDateX { get; set; }
        public int SiteIdY { get; set; }
        public string LeagueX { get; set; } = string.Empty; // Novo campo
        public string LeagueY { get; set; } = string.Empty; // Novo campo
        public string GameX { get; set; } = string.Empty; // Novo campo
        public string GameY { get; set; } = string.Empty; // Novo campo
        public string URLX { get; set; } = string.Empty; // Novo campo
        public string URLY { get; set; } = string.Empty; // Novo campo
    }
}
