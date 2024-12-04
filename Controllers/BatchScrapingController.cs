using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch;
using BetSniffer.Api.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.Json;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BatchScrapingController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;

        // Injeção de dependência do IServiceProvider
        public BatchScrapingController(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        [HttpPost("scrape")]
        public IActionResult ScrapeTagsBatch([FromBody] List<string> urls)
        {
            if (urls == null || urls.Count == 0)
            {
                return BadRequest(new { message = "A lista de URLs não pode estar vazia." });
            }

            var results = new List<object>();
            var errors = new List<string>();

            foreach (var url in urls)
            {
                try
                {
                    string siteName = ExtractSiteName(url);

                    if (!SupportedSites.IsSiteSupported(siteName))
                    {
                        errors.Add($"Site não suportado: {siteName}");
                        continue;
                    }

                    Console.WriteLine($"Site detectado: {siteName}");

                    // Determine qual serviço de scraping deve ser utilizado
                    IScrapingService scrapingService = GetScrapingService(siteName);

                    // Usa o serviço de scraping correspondente
                    var result = scrapingService.ScrapeTagsAsync(url, siteName);

                    results.Add(new { Url = url, SiteName = siteName, Result = result });
                }
                catch (Exception ex)
                {
                    errors.Add($"Erro ao processar URL '{url}': {ex.Message}");
                }
            }

            // Configurar JsonSerializerOptions para permitir ciclos de referência
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                WriteIndented = true // Isso vai formatar a resposta para facilitar a leitura
            };

            // Serializar a resposta com o JsonSerializer
            var jsonResponse = JsonSerializer.Serialize(new { Results = results, Errors = errors }, options);

            return Content(jsonResponse, "application/json");
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
