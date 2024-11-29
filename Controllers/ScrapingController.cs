using Microsoft.AspNetCore.Mvc;
using BetSniffer.Api.Core.Sites.Novibet;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScrapingController : ControllerBase
    {
        // Endpoint para receber o link e fazer o scraping
        [HttpPost("scrape")]
        public IActionResult ScrapeTags([FromBody] string url)
        {
            try
            {
                var scrapingService = new NovibetScrapingService();
                var result = scrapingService.ScrapeTags(url); // Passa a URL para o serviço

                return Ok(result); // Retorna os dados encontrados em JSON
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message }); // Em caso de erro
            }
        }
    }
}
