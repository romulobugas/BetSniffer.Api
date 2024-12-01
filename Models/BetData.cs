namespace BetSniffer.Api.Models
{
    public class BetData
    {
        public string BetName { get; set; } // Nome da aposta, ex: "Mais de 4,5"
        public string Multiplier { get; set; } // Multiplicador, ex: "1.01"
        public string BetDescription { get; internal set; }

        // Construtor
        public BetData(string betName, string multiplier)
        {
            BetName = betName;
            Multiplier = multiplier;
        }
    }
}
