using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch;
using BetSniffer.Api.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Core.Sites;
using BetSniffer.Api.Data;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BatchScrapingController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ApplicationDbContext _dbContext;
        private readonly TeamService _teamService;

        public BatchScrapingController(IServiceProvider serviceProvider, ApplicationDbContext dbContext, TeamService teamService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
        }

        [HttpPost("scrape")]
        public IActionResult ScrapeTagsBatch([FromBody] List<string> urls)
        {
            if (urls == null || urls.Count == 0)
            {
                return BadRequest(new { message = "A lista de URLs não pode estar vazia." });
            }

            var options = new ChromeOptions();
            
            options.AddArgument("--no-sandbox");

            using var driver = new ChromeDriver(options); // WebDriver compartilhado

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

                    // Inicializa a classe de scraping correspondente com o WebDriver compartilhado
                    var scrapingService = GetScrapingService(siteName, driver);

                    // Usa o serviço de scraping sem "await", pois não é assíncrono
                    var result = scrapingService.ScrapeTagsAsync(url, siteName); // Removido "await"

                    // Adiciona o resultado do scraping
                    results.Add(new { Url = url, SiteName = siteName, Result = "Sucesso" });
                }
                catch (Exception ex)
                {
                    errors.Add($"Erro ao processar URL '{url}': {ex.Message}");
                }
            }

            var jsonResponse = new
            {
                Results = results,
                Errors = errors
            };

            return Ok(jsonResponse);
        }

        private string ExtractSiteName(string url)
        {
            var uri = new Uri(url);
            string host = uri.Host;
            string[] parts = host.Split('.');
            return parts.Length >= 3 ? parts[1] : parts[0];
        }

        private IScrapingService GetScrapingService(string siteName, IWebDriver driver)
        {
            return siteName.ToLower() switch
            {
                "novibet" => new NovibetScraping(driver, _dbContext, _teamService),
                "parimatch" => new ParimatchScraping(driver, _dbContext, _teamService),
                _ => throw new Exception($"Serviço de scraping não encontrado para o site: {siteName}")
            };
        }
    }
}
