namespace BetSniffer.Api.Core.Interfaces
{
    public interface ILeagueScrapingService
    {
        void ScrapeLeague(string url, string siteName);
    }
}
