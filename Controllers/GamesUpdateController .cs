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

        // Função de atualização das odds
        [HttpPut("update-odds")]
        public IActionResult UpdateOdds()
        {
            try
            {
                // Obtém a lista de URLs da tabela GamesInfo onde a data do jogo é maior que a data atual, o status é 0, e a URL não é nula
                var urlsToUpdate = _dbContext.GamesInfo
                    .Where(g => g.GameDate > DateTime.UtcNow && g.Status == 0 && !string.IsNullOrEmpty(g.URL))
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

        // Função para atualizar jogos com o mesmo GameDate, HomeTeam e AwayTeam, mas com SiteId diferentes
        [HttpPut("update-same-games")]
        public IActionResult UpdateSameGame()
        {
            try
            {
                // Obtém a data atual em UTC
                DateTime now = DateTime.UtcNow;

                // Define o startDate como a data atual + 2:30h
                DateTime startDate = now.AddHours(2).AddMinutes(30);

                // Define o endDate como o final do dia de hoje (23:59:59)
                DateTime endDate = now.Date.AddDays(1).AddSeconds(-1); // final do dia atual

                // Chama o método UpdateSameGames do BatchScrapingController passando startDate e endDate
                var result = _batchScrapingController.UpdateSameGames(startDate.ToString("dd/MM/yyyy HH:mm"), endDate.ToString("dd/MM/yyyy HH:mm"));

                // Verifica se o resultado não for Ok (caso algum erro ocorra dentro de UpdateSameGames)
                if (result is ObjectResult objectResult && objectResult.StatusCode == 200)
                {
                    return Ok(new { message = "Jogos atualizados com sucesso!" });
                }
                else
                {
                    // Caso o resultado seja erro
                    return StatusCode(500, new { message = "Erro ao atualizar jogos com o mesmo GameDate, HomeTeam e AwayTeam." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao atualizar jogos com o mesmo GameDate, HomeTeam e AwayTeam: {ex.Message}" });
            }
        }

    }
}
