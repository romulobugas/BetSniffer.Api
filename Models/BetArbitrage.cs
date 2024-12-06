using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        public int BetId { get; set; }

        [ForeignKey("GamesInfo")] // Chave estrangeira para GamesInfo
        public int? GameId { get; set; } // Referência para GamesInfo

        public string TagName { get; set; }

        public string OverUnder { get; set; }

        [Column(TypeName = "decimal(10, 2)")] // Define o tipo no banco
        public decimal BetAmount { get; set; }

        [Column(TypeName = "decimal(10, 2)")] // Define o tipo no banco
        public decimal Multiplier { get; set; }

        public DateTime CaptureDate { get; set; }

        public DateTime GameDate { get; set; }

        // Propriedade de navegação para GamesInfo
        [JsonIgnore]
        public GamesInfo GamesInfo { get; set; }

        // Chave estrangeira para Site
        [ForeignKey("Site")] // Relacionamento com a tabela Site
        public int SiteId { get; set; }  // Chave estrangeira

        // Propriedade de navegação para Site
        public Site Site { get; set; }
    }

    public class GamesInfo
    {
        public int GameId { get; set; }

        // Relacionamentos com Times
        [ForeignKey("HomeTeam")]
        public int HomeTeamId { get; set; }
        public Team HomeTeam { get; set; }

        [ForeignKey("AwayTeam")]
        public int AwayTeamId { get; set; }
        public Team AwayTeam { get; set; }

        public DateTime GameDate { get; set; }

        public string League { get; set; }

        // Chave estrangeira para Site
        [ForeignKey("Site")]
        public int SiteId { get; set; } // Chave estrangeira para o Site

        // Propriedade de navegação para Site
        public Site Site { get; set; }
        public string URL { get; set; }

        // Propriedade de navegação para Bets
        public ICollection<BetInfo> Bets { get; set; } = new List<BetInfo>();
    }

    public class Site
    {
        public int SiteId { get; set; }

        public string Name { get; set; }

        // Navegação de volta para as entidades GamesInfo e BetInfo
        public ICollection<GamesInfo> GamesInfo { get; set; } = new List<GamesInfo>();
        public ICollection<BetInfo> BetInfo { get; set; } = new List<BetInfo>();
    }

    public class Team
    {
        public int TeamId { get; set; }
        public string NormalizedName { get; set; } // Nome padronizado
        public string Aliases { get; set; } // Lista de nomes alternativos (JSON)

        // Relacionamento com GamesInfo
        public ICollection<GamesInfo> HomeGames { get; set; }
        public ICollection<GamesInfo> AwayGames { get; set; }
    }

}
