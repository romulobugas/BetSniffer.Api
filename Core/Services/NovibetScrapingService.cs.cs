// Core/Services/NovibetScrapingService.cs
using BetSniffer.Api.Models;
using BetSniffer.Api.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            foreach (var tagInfo in tagInfos)
            {
                var game = new GameInfo
                {
                    League = "Nome do Jogo", // Você deve definir qual é o nome do jogo
                    GameDate = DateTime.Now // Data do jogo
                };

                var betInfo = new BetInfo
                {
                    GameInfo = tagInfo.GameInfo,
                    CaptureDate = DateTime.Now, // Data de captura
                };

                //var betArbitrage = new BetArbitrage
                //{
                //    Game = "Nome do Jogo", // Ajuste conforme necessário
                //    TagName = tagInfo.TagName,
                //    BetMoreThan = "Maior que", // Preencha com os dados corretos
                //    BetMoreThanMultiplier = 1.5m, // Preencha com o valor real
                //    BetLessThan = "Menor que", // Preencha com os dados corretos
                //    BetLessThanMultiplier = 2.0m, // Preencha com o valor real
                //    CaptureDate = DateTime.Now, // Data de captura
                //    GameDate = DateTime.Now, // Data do jogo
                //    ArbitragePercentage = 10 // Exemplo de cálculo de arbitragem
                //};

                // Salva os dados no banco
                await _dbContext.GameInfos.AddAsync(game);
                await _dbContext.BetInfos.AddAsync(betInfo);
                //await _dbContext.BetArbitrages.AddAsync(betArbitrage);

                await _dbContext.SaveChangesAsync(); // Salva tudo no banco de dados
            }
        }
    }
}
