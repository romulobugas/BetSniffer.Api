using Microsoft.AspNetCore.Mvc;
using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GamesUpdateController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly BatchScrapingController _batchScrapingController;

        public GamesUpdateController(
            ApplicationDbContext dbContext,
            BatchScrapingController batchScrapingController)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _batchScrapingController = batchScrapingController ?? throw new ArgumentNullException(nameof(batchScrapingController));
        }

        [HttpPost("update-odds")]
        public IActionResult UpdateOdds()
        {
            try
            {
                // Obtém a lista de URLs da tabela GamesInfo onde a data do jogo é maior que a data atual
                var urlsToUpdate = _dbContext.GamesInfo
                    .Where(g => g.GameDate > DateTime.UtcNow && !string.IsNullOrEmpty(g.URL))
                    .Select(g => g.URL)
                    .Distinct() // Evita duplicatas
                    .ToList();

                if (urlsToUpdate.Count == 0)
                {
                    return Ok(new { message = "Nenhum jogo com odds para atualizar foi encontrado." });
                }

                // Envia a lista de URLs para o controlador existente para atualização
                var result = _batchScrapingController.ScrapeTagsBatch(urlsToUpdate);

                return result;
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar as odds: {ex.Message}" });
            }
        }
    }
}
