namespace BetSniffer.Api.Core.Sites
{
    public static class SupportedSites
    {
        public static readonly HashSet<string> Sites = new()
        {
            "novibet",
            "betfast",
            "betano",
            "vbet",
            "betfair",
            "superbet",
            "pixbet",
            "betboom"
            // Adicione outros sites conforme necessário
        };

        public static bool IsSiteSupported(string siteName)
        {
            return Sites.Contains(siteName.ToLower());
        }
    }
}

