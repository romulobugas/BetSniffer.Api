using System;
using System.ComponentModel.DataAnnotations;

namespace BetSniffer.Api.Models
{
    public class IaActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string JobId { get; set; } = string.Empty;

        [Required]
        public string SiteName { get; set; } = string.Empty;

        [Required]
        public string GameUrl { get; set; } = string.Empty;

        public string? HomeTeam { get; set; }
        public string? AwayTeam { get; set; }
        public DateTime? GameDate { get; set; }
        public string? League { get; set; }

        [Required]
        public string Status { get; set; } = "Pendente"; // Pendente, Em Progresso, Concluído, Erro

        public string? LastAction { get; set; }
        public string? ErrorMessage { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
