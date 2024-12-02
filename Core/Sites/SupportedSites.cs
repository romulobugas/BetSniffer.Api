namespace BetSniffer.Api.Core.Sites
{
    public static class SupportedSites
    {
        public static readonly HashSet<string> Sites = new()
        {
            "novibet",
            "bet365",
            "betano",
            "parimatch"
            // Adicione outros sites conforme necessário
        };

        public static bool IsSiteSupported(string siteName)
        {
            return Sites.Contains(siteName.ToLower());
        }
    }
}

