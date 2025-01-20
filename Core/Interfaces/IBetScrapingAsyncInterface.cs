using BetSniffer.Api.Models;
using PuppeteerSharp;

namespace BetSniffer.Api.Core.Interfaces
{
    public interface IBetScrapingAsyncInterface
    {
        Task HandleCookies(IPage page, string cookieAcceptButtonSelector, int timeoutMilliseconds = 10000);
        Task HandleModal(IPage page, string modalSelector, string closeButtonSelector, int timeoutMilliseconds = 10000);
        Task<List<TagInfo>> ScrapeTags(string url, string siteName);
    }
}