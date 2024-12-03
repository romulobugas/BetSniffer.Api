using BetSniffer.Api.Models;

namespace BetSniffer.Api.Core.Interfaces
{
    public interface IScrapingService
    {
        List<TagInfo> ScrapeTagsAsync(string url, string siteName);
    }
}
