using System;

namespace BetSniffer.Api.Models
{
    public class BetData
    {
        public string BetName { get; set; } = string.Empty; // Nome da aposta, ex: "Mais de 4,5"
        public string Multiplier { get; set; } = string.Empty; // Multiplicador, ex: "1.01"
        public string BetDescription { get; internal set; } = string.Empty;

        // Construtor
        public BetData(string betName, string multiplier)
        {
            BetName = betName ?? throw new ArgumentNullException(nameof(betName));
            Multiplier = multiplier ?? throw new ArgumentNullException(nameof(multiplier));
        }
    }
}
