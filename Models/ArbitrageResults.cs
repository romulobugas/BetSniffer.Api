namespace BetSniffer.Api.Models
{
    public class ArbitrageResults
    {
        public decimal ArbitrageLucroPercent { get; set; }
        public string TagNameX { get; set; }
        public string OverUnderX { get; set; }
        public decimal BetAmountX { get; set; }
        public decimal MultiplierX { get; set; }
        public string HomeTeam { get; set; }
        public string SiteNameX { get; set; }
        public string SiteNameY { get; set; }
        public string AwayTeam { get; set; }
        public string OverUnderY { get; set; }
        public decimal BetAmountY { get; set; }
        public decimal MultiplierY { get; set; }
        public string TagNameY { get; set; }
        public int SiteIdX { get; set; }
        public DateTime GameDateX { get; set; }
        public int SiteIdY { get; set; }
        public string LeagueX { get; set; } // Novo campo
        public string LeagueY { get; set; } // Novo campo
        public string GameX { get; set; } // Novo campo
        public string GameY { get; set; } // Novo campo
        public string URLX { get; set; } // Novo campo
        public string URLY { get; set; } // Novo campo
    }
}
