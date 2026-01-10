using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BetSniffer.Api.Core.Interfaces;

namespace BetSniffer.Api.Core.Services
{
    /// <summary>
    /// Serviço centralizado para salvamento de apostas.
    /// Consolida lógica duplicada de todos os scrapers de apostas.
    /// </summary>
    public class BetSavingService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogService _logService;

        public BetSavingService(ApplicationDbContext dbContext, ILogService logService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Salva apostas de forma assincronizada.
        /// Remove apostas duplicadas existentes e insere as novas.
        /// </summary>
        /// <param name="bets">Lista de apostas a salvar</param>
        /// <returns>Número de apostas salvas</returns>
        public async Task<int> SaveBetsAsync(List<BetInfo> bets)
        {
            return await Task.Run(() => SaveBets(bets));
        }

        /// <summary>
        /// Salva apostas sincronizadamente (lógica real).
        /// </summary>
        /// <param name="bets">Lista de apostas a salvar</param>
        /// <returns>Número de apostas salvas</returns>
        public int SaveBets(List<BetInfo> bets)
        {
            try
            {
                if (bets == null || !bets.Any())
                {
                    Console.WriteLine("?? Nenhuma aposta para salvar.");
                    return 0;
                }

                // Cria um HashSet com combinações únicas dos critérios relevantes
                var betKeys = bets
                    .Select(bet => new
                    {
                        bet.TagName,
                        bet.OverUnder,
                        bet.TagId,
                        SiteId = bet.Site?.SiteId,
                        GameId = bet.GamesInfo?.GameId
                    })
                    .Distinct()
                    .ToList();

                int betsRemoved = 0;

                // Remove apostas duplicadas existentes
                foreach (var betKey in betKeys)
                {
                    if (betKey.GameId == null || betKey.SiteId == null)
                        continue;

                    var existingBets = _dbContext.BetInfo.Where(b =>
                        b.GameId == betKey.GameId &&
                        b.TagName == betKey.TagName &&
                        b.OverUnder == betKey.OverUnder &&
                        b.TagId == betKey.TagId &&
                        b.SiteId == betKey.SiteId).ToList();

                    if (existingBets.Count > 0)
                    {
                        Console.WriteLine($"??? Aposta existente encontrada. Removendo {existingBets.Count} apostas antigas...");
                        _dbContext.BetInfo.RemoveRange(existingBets);
                        betsRemoved += existingBets.Count;
                    }
                }

                // Adiciona as novas apostas
                _dbContext.BetInfo.AddRange(bets);
                _dbContext.SaveChanges();

                Console.WriteLine($"? Salvas {bets.Count} novas apostas. (Removidas: {betsRemoved})");
                return bets.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao salvar apostas: {ex.Message}");
                _logService.LogError("Erro ao salvar apostas", ex);
                throw;
            }
        }

        /// <summary>
        /// Valida se as apostas contêm todas as informações requerida.
        /// </summary>
        /// <param name="bets">Lista de apostas a validar</param>
        /// <returns>True se válidas, false caso contrário</returns>
        public bool ValidateBets(List<BetInfo> bets)
        {
            if (bets == null || !bets.Any())
                return false;

            foreach (var bet in bets)
            {
                if (string.IsNullOrWhiteSpace(bet.TagName))
                {
                    Console.WriteLine("?? Aposta sem TagName.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(bet.OverUnder))
                {
                    Console.WriteLine("?? Aposta sem OverUnder.");
                    return false;
                }

                if (bet.BetAmount <= 0)
                {
                    Console.WriteLine($"?? Aposta com valor inválido: {bet.BetAmount}");
                    return false;
                }

                if (bet.Multiplier <= 0)
                {
                    Console.WriteLine($"?? Aposta com multiplicador inválido: {bet.Multiplier}");
                    return false;
                }

                if (bet.GamesInfo == null || bet.GamesInfo.GameId == 0)
                {
                    Console.WriteLine("?? Aposta sem jogo associado.");
                    return false;
                }

                if (bet.Site == null || bet.Site.SiteId == 0)
                {
                    Console.WriteLine("?? Aposta sem site associado.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Remove todas as apostas de um jogo específico.
        /// Útil para limpeza antes de novo scraping.
        /// </summary>
        /// <param name="gameId">ID do jogo</param>
        /// <returns>Número de apostas removidas</returns>
        public async Task<int> RemoveGameBetsAsync(int gameId)
        {
            try
            {
                var existingBets = _dbContext.BetInfo.Where(b => b.GameId == gameId).ToList();

                if (!existingBets.Any())
                {
                    Console.WriteLine($"?? Nenhuma aposta encontrada para o jogo {gameId}.");
                    return 0;
                }

                _dbContext.BetInfo.RemoveRange(existingBets);
                await _dbContext.SaveChangesAsync();

                Console.WriteLine($"? Removidas {existingBets.Count} apostas do jogo {gameId}.");
                return existingBets.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao remover apostas do jogo: {ex.Message}");
                _logService.LogError($"Erro ao remover apostas do jogo {gameId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Remove todas as apostas de um site específico.
        /// </summary>
        /// <param name="siteId">ID do site</param>
        /// <returns>Número de apostas removidas</returns>
        public async Task<int> RemoveSiteBetsAsync(int siteId)
        {
            try
            {
                var existingBets = _dbContext.BetInfo.Where(b => b.SiteId == siteId).ToList();

                if (!existingBets.Any())
                {
                    Console.WriteLine($"?? Nenhuma aposta encontrada para o site {siteId}.");
                    return 0;
                }

                _dbContext.BetInfo.RemoveRange(existingBets);
                await _dbContext.SaveChangesAsync();

                Console.WriteLine($"? Removidas {existingBets.Count} apostas do site {siteId}.");
                return existingBets.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao remover apostas do site: {ex.Message}");
                _logService.LogError($"Erro ao remover apostas do site {siteId}", ex);
                throw;
            }
        }

        /// <summary>
        /// Obtém estatísticas de apostas.
        /// </summary>
        public Dictionary<string, int> GetStatistics()
        {
            try
            {
                var stats = new Dictionary<string, int>
                {
                    { "TotalBets", _dbContext.BetInfo.Count() },
                    { "UniqueGames", _dbContext.BetInfo.Select(b => b.GameId).Distinct().Count() },
                    { "UniqueSites", _dbContext.BetInfo.Select(b => b.SiteId).Distinct().Count() },
                    { "UniqueTags", _dbContext.BetInfo.Select(b => b.TagName).Distinct().Count() }
                };

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao obter estatísticas: {ex.Message}");
                _logService.LogError("Erro ao obter estatísticas de apostas", ex);
                return new Dictionary<string, int>();
            }
        }
    }
}
