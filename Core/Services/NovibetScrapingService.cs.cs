using BetSniffer.Api.Models;
using BetSniffer.Api.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BetSniffer.Api.Core.Services
{
    public class NovibetScrapingService
    {
        private readonly ApplicationDbContext _dbContext;

        public NovibetScrapingService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SaveBetData(List<TagInfo> tagInfos)
        {
            var gamesToAdd = new List<GamesInfo>();
            var betsToAdd = new List<BetInfo>();

            foreach (var tagInfo in tagInfos)
            {
                // Verifica se o jogo já existe no banco para evitar duplicatas
                var existingGame = await _dbContext.GamesInfo
                    .FirstOrDefaultAsync(g =>
                        g.HomeTeam == tagInfo.GameInfo.HomeTeam &&
                        g.AwayTeam == tagInfo.GameInfo.AwayTeam &&
                        g.GameDate == tagInfo.GameInfo.GameDate);

                GamesInfo game;

                if (existingGame == null)
                {
                    game = new GamesInfo
                    {
                        HomeTeam = tagInfo.GameInfo.HomeTeam,
                        AwayTeam = tagInfo.GameInfo.AwayTeam,
                        GameDate = tagInfo.GameInfo.GameDate,
                        League = tagInfo.GameInfo.League
                    };

                    // Adiciona o novo jogo à lista de jogos a serem inseridos
                    gamesToAdd.Add(game);
                }
                else
                {
                    game = existingGame;
                }

                // Adiciona as apostas associadas ao jogo
                foreach (var bet in tagInfo.BetInfo)
                {
                    var betInfo = new BetInfo
                    {
                        GamesInfo = game, // Relaciona a aposta com o jogo existente
                        TagName = bet.TagName,
                        OverUnder = bet.OverUnder,
                        Multiplier = bet.Multiplier,
                        CaptureDate = DateTime.Now,
                        BetAmount = bet.BetAmount
                    };

                    // Adiciona a aposta à lista de apostas a serem inseridas
                    betsToAdd.Add(betInfo);
                }
            }

            // Adiciona os jogos no banco (se houverem novos)
            if (gamesToAdd.Count > 0)
            {
                await _dbContext.GamesInfo.AddRangeAsync(gamesToAdd);
            }

            // Adiciona as apostas no banco
            if (betsToAdd.Count > 0)
            {
                await _dbContext.BetInfo.AddRangeAsync(betsToAdd);
            }

            // Salva todas as alterações de uma vez
            await _dbContext.SaveChangesAsync();
        }
    }
}
