namespace BetSniffer.Api.Models
{
    public class TagInfo
    {        
        // Propriedades necessárias
        public GamesInfo GameInfo { get; internal set; }
        public List<BetInfo> BetInfo { get; internal set; }  // Alterado de BetInfo para List<BetInfo>

        // Construtor que aceita um único BetInfo
        public TagInfo(GamesInfo gameInfo, BetInfo betInfo)
        {
            GameInfo = gameInfo;
            BetInfo = betInfo != null ? new List<BetInfo> { betInfo } : new List<BetInfo>(); // Garante que a lista nunca será nula
        }

        // Construtor que aceita uma lista de BetInfo
        public TagInfo(GamesInfo gameInfo, List<BetInfo> bets)
        {
            GameInfo = gameInfo;
            BetInfo = bets != null && bets.Count > 0 ? bets : new List<BetInfo>();  // Garante que a lista nunca será nula e contém pelo menos uma aposta
        }
    }
}
