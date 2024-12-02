using Microsoft.AspNetCore.Mvc;
using BetSniffer.Api.Core.Sites.Novibet;
using System;
using System.Text.Json;
using BetSniffer.Api.Core.Sites;
using BetSniffer.Api.Core.Services;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScrapingController : ControllerBase
    {
        private readonly NovibetScraping _scrapingService;

        // Injeção de dependência do NovibetScraping
        public ScrapingController(NovibetScraping scrapingService)
        {
            _scrapingService = scrapingService ?? throw new ArgumentNullException(nameof(scrapingService));
        }

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

                // Usa o serviço injetado para fazer o scraping
                var result = _scrapingService.ScrapeTags(url, siteName);

                // Configurar JsonSerializerOptions para permitir ciclos de referência
                var options = new JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    WriteIndented = true // Isso vai formatar a resposta para facilitar a leitura
                };

                // Serializar a resposta com o JsonSerializer com a configuração de preservação de referências
                var jsonResponse = JsonSerializer.Serialize(new { SiteName = siteName, Result = result }, options);

                return Content(jsonResponse, "application/json");
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
                var uri = new Uri(url);
                string host = uri.Host;
                string[] parts = host.Split('.');

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
