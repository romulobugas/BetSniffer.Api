using BetSniffer.Api.Models;

namespace BetSniffer.Api.Core.Interfaces
{
    public interface IScrapingService
    {
        List<TagInfo> ScrapeTags(string url, string siteName);
    }
}
