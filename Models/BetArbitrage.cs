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
        public virtual int BetId { get; set; }

        [ForeignKey("GamesInfo")] // Chave estrangeira para GamesInfo
        public virtual int? GameId { get; set; } // Referência para GamesInfo

        public virtual string TagName { get; set; }

        public virtual string OverUnder { get; set; }

        [Column(TypeName = "decimal(10, 2)")] // Define o tipo no banco
        public virtual decimal BetAmount { get; set; }

        [Column(TypeName = "decimal(10, 2)")] // Define o tipo no banco
        public virtual decimal Multiplier { get; set; }

        public virtual DateTime CaptureDate { get; set; }

        public virtual DateTime GameDate { get; set; }

        // Propriedade de navegação para GamesInfo
        [JsonIgnore]
        public virtual GamesInfo GamesInfo { get; set; }

        // Chave estrangeira para Site
        [ForeignKey("Site")] // Relacionamento com a tabela Site
        public virtual int SiteId { get; set; }  // Chave estrangeira

        // Propriedade de navegação para Site
        public virtual Site Site { get; set; }
        public virtual int? TagId { get; set; }


    }

    public class GamesInfo
    {
        [Key]
        public int GameId { get; set; }

        // Chave estrangeira para o Site
        [ForeignKey("SiteId")]
        public virtual Site Site { get; set; }  // Marcar como virtual para Lazy Loading

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        // Chave estrangeira
        public int? SiteId { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime GameDate { get; set; }

        [StringLength(100)]
        public string League { get; set; }

        [Url]
        public string URL { get; set; }

        [Range(0, 255)]
        public byte Status { get; set; } = 0;

        [Column(TypeName = "datetime2")]
        public DateTime? LastUpdated { get; set; }

        // Propriedades de navegação
        public virtual Team HomeTeam { get; set; } // Relacionamento com o time da casa
        public virtual Team AwayTeam { get; set; } // Relacionamento com o time visitante

        // Relacionamento com BetInfo (um para muitos)
        public virtual ICollection<BetInfo> Bets { get; set; }  // Adicionando a coleção de Bets

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
