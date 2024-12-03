using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch;
using BetSniffer.Api.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;
using BetSniffer.Api.Core.Sites;
using BetSniffer.Api.Core.Interfaces;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScrapingController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;

        // Injeção de dependência do IServiceProvider
        public ScrapingController(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

                // Determine qual serviço de scraping deve ser utilizado
                IScrapingService scrapingService = GetScrapingService(siteName);

                // Usa o serviço de scraping correspondente
                var result = scrapingService.ScrapeTagsAsync(url, siteName);

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

        // Método que retorna o serviço de scraping baseado no nome do site
        private IScrapingService GetScrapingService(string siteName)
        {
            switch (siteName)
            {
                case "novibet":
                    return _serviceProvider.GetService<NovibetScraping>(); // Usando o NovibetScraping
                case "parimatch":
                    return _serviceProvider.GetService<ParimatchScraping>(); // Usando o ParimatchScraping
                default:
                    throw new Exception($"Serviço de scraping não encontrado para o site: {siteName}");
            }
        }
    }
}
