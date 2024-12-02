using Microsoft.AspNetCore.Mvc;
using BetSniffer.Api.Core.Sites;
using BetSniffer.Api.Core.Sites.Novibet;
using System;
using System.Text.RegularExpressions;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScrapingController : ControllerBase
    {
        [HttpPost("scrape")]
        public IActionResult ScrapeTags([FromBody] string url)
        {
            try
            {
                string siteName = ExtractSiteName(url);

                if (!SupportedSites.IsSiteSupported(siteName))
                {
                    return BadRequest(new { message = $"Site não suportado: {siteName}" });
                }

                Console.WriteLine($"Site detectado: {siteName}");

                // Por enquanto, continuamos usando o NovibetScrapingService para todos os casos
                var scrapingService = new NovibetScrapingService();
                var result = scrapingService.ScrapeTags(url);

                return Ok(new { SiteName = siteName, Result = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        private string ExtractSiteName(string url)
        {
            try
            {
                // Cria um objeto Uri para facilitar o parsing
                var uri = new Uri(url);

                // Pega o hostname completo (ex: "br.novibet.com")
                string host = uri.Host;

                // Divide o hostname em partes
                string[] parts = host.Split('.');

                // Se tiver 3 ou mais partes (ex: "br.novibet.com"), pega a segunda parte
                // Se tiver 2 partes (ex: "novibet.com"), pega a primeira parte
                string siteName = parts.Length >= 3 ? parts[1] : parts[0];

                return siteName.ToLower();
            }
            catch
            {
                return "unknown";
            }
        }
    }
}

