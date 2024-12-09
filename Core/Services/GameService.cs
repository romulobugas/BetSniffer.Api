using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Services
{
    public class GameService
    {
        private readonly ApplicationDbContext _context;

        public GameService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Método para verificar se o jogo existe e, caso não, salvar no banco
        public void ProcessGame(GamesInfo gameInfo, string leagueName)
        {
            var existingGame = FindGameByExternalId(gameInfo.GameId);

            if (existingGame == null)
            {
                // Se o jogo não existe, salva o novo jogo
                _context.GamesInfo.Add(gameInfo);
                _context.SaveChanges();
                Console.WriteLine($"Novo jogo processado: {gameInfo.GameId}");
            }
            else
            {
                // Se o jogo já existe, atualiza as informações necessárias
                UpdateGame(existingGame, gameInfo);
                Console.WriteLine($"Jogo atualizado: {gameInfo.GameId}");
            }
        }

        // Método para encontrar um jogo pelo ID externo
        private GamesInfo FindGameByExternalId(int externalId)
        {
            return _context.GamesInfo
                .FirstOrDefault(g => g.GameId == externalId);
        }

        // Método para atualizar as informações de um jogo existente
        private void UpdateGame(GamesInfo existingGame, GamesInfo gameInfo)
        {
            existingGame.GameDate = gameInfo.GameDate;
            existingGame.HomeTeamId = gameInfo.HomeTeamId;
            existingGame.AwayTeamId = gameInfo.AwayTeamId;
            existingGame.Status = 0;
            existingGame.LastUpdated = DateTime.Now;
            existingGame.URL = gameInfo.URL;
            // Atualize outras propriedades conforme necessário

            _context.GamesInfo.Update(existingGame);
            _context.SaveChanges();
        }

        // Método para deletar um jogo
        public void DeleteGame(int externalId)
        {
            var game = FindGameByExternalId(externalId);

            if (game != null)
            {
                _context.GamesInfo.Remove(game);
                _context.SaveChanges();
                Console.WriteLine($"Jogo deletado: {externalId}");
            }
            else
            {
                Console.WriteLine($"Jogo não encontrado para deletar: {externalId}");
            }
        }

        public void ConfirmAgeVerification(IWebDriver driver, int timeoutSeconds = 10)
        {
            try
            {

                Thread.Sleep(3000);

                // Cria o WebDriverWait com base no driver fornecido e o tempo de espera
                WebDriverWait wait = new(driver, TimeSpan.FromSeconds(timeoutSeconds));

                // Aguarda até que o botão "Sim" esteja visível
                var confirmButton = wait.Until(d => d.FindElement(By.CssSelector("[data-qa='age-verification-modal-ok-button']")));

                // Clica no botão
                confirmButton.Click();
                Console.WriteLine("Botão 'Sim' clicado com sucesso.");
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("Botão 'Sim' não encontrado.");
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera para o botão 'Sim' expirou.");
            }
        }

        public void ClosePopup(IWebDriver driver, string popupSelector, int timeoutSeconds = 10)
        {
            try
            {
                // Cria o WebDriverWait com base no driver fornecido e o tempo de espera
                WebDriverWait wait = new(driver, TimeSpan.FromSeconds(timeoutSeconds));

                // Espera até o botão de fechar o pop-up aparecer
                var closeButton = wait.Until(d => d.FindElement(By.CssSelector(popupSelector)));
                closeButton.Click();
                Console.WriteLine("Pop-up fechado com sucesso.");
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("Pop-up não encontrado.");
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera para fechar o pop-up expirou.");
            }
        }



        // Método para verificar se um jogo já existe
        public GamesInfo GameExists(int homeTeamDb, int awayTeamDb, DateTime gameDateTime, Models.Site site, string gameLink, string leagueName)
        {

            var gamesInfo = new GamesInfo();

            // Verifica se o jogo já existe no banco
            var existingGame = _context.GamesInfo
                .FirstOrDefault(g =>
                    g.HomeTeamId == homeTeamDb &&
                    g.AwayTeamId == awayTeamDb &&
                g.GameDate == gameDateTime &&
                    g.Site.SiteId == site.SiteId);

            if (existingGame != null)
            {
                // Atualiza o jogo existente caso a URL esteja ausente
                if (string.IsNullOrEmpty(existingGame.URL))
                {
                    existingGame.URL = gameLink;
                    existingGame.LastUpdated = DateTime.Now;

                    _context.GamesInfo.Update(existingGame);
                    _context.SaveChanges();
                    Console.WriteLine("Jogo atualizado no banco de dados.");
                }
                else
                {
                    Console.WriteLine("Jogo já cadastrado e atualizado previamente.");
                }
            }
            else
            {
                // Se o jogo não existir no banco, cria um novo GamesInfo
                gamesInfo = new GamesInfo
                {
                    HomeTeamId = homeTeamDb,
                    AwayTeamId = awayTeamDb,
                    GameDate = gameDateTime,
                    League = leagueName,
                    SiteId = site.SiteId,
                    URL = gameLink,
                    Status = 0,
                    LastUpdated = DateTime.Now
                };

                _context.GamesInfo.Add(gamesInfo);
                _context.SaveChanges();
                Console.WriteLine("Jogo salvo no banco de dados.");
            }

            return existingGame != null ? existingGame : gamesInfo;
        }
    }
}
