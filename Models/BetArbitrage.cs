// Models/BetArbitrage.cs
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BetSniffer.Api.Models
{
    public class BetArbitrage
    {
        public int Id { get; set; }
        public string Game { get; set; }
        public string TagName { get; set; }
        public string BetMoreThan { get; set; }
        public decimal BetMoreThanMultiplier { get; set; }
        public string BetLessThan { get; set; }
        public decimal BetLessThanMultiplier { get; set; }
        public DateTime CaptureDate { get; set; }
        public DateTime GameDate { get; set; }
        public decimal ArbitragePercentage { get; set; }
    }

    public class BetInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Configura o incremento automático
        public int BetId { get; set; }
        public GameInfo GameInfo { get; set; }
        public string TagName { get; set; }
        public string OverUnder { get; set; }
        public decimal BetAmount { get; set; }
        public decimal Multiplier { get; set; }
        public DateTime CaptureDate { get; set; }
        public DateTime GameDate { get; set; }
    }

    public class GameInfo
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Configura o incremento automático
        public int GameId { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public DateTime GameDate { get; set; }
        public string League { get; set; }
        
    }
}
