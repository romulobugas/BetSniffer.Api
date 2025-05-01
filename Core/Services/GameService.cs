using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using PuppeteerSharp;

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

        public void ConfirmAgeVerification(IWebDriver driver,string ageVerification, int timeoutSeconds = 10)
        {
            try
            {

                System.Threading.Thread.Sleep(new Random().Next(1511, 3522));

                // Cria o WebDriverWait com base no driver fornecido e o tempo de espera
                WebDriverWait wait = new(driver, TimeSpan.FromSeconds(timeoutSeconds));

                // Aguarda até que o botão "Sim" esteja visível
                var confirmButton = wait.Until(d => d.FindElement(By.CssSelector(ageVerification)));

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

        public void ConfirmAgeVerificationWithCheckboxAndButton(IWebDriver driver, string checkboxSelector, string confirmButtonSelector, int timeoutSeconds = 10)
        {
            try
            {
                // Aguarda 3 segundos para garantir o carregamento da tela (caso ela apareça)
                System.Threading.Thread.Sleep(new Random().Next(2145, 2987));

                // Cria o WebDriverWait com base no driver fornecido e o tempo de espera
                WebDriverWait wait = new(driver, TimeSpan.FromSeconds(timeoutSeconds));

                // Verifica se o checkbox da idade está presente
                var checkbox = wait.Until(d => d.FindElement(By.CssSelector(checkboxSelector)));

                // Marca o checkbox se não estiver marcado
                if (!checkbox.Selected)
                {
                    checkbox.Click();
                    Console.WriteLine("Checkbox 'Tenho mais de 18 anos' marcado com sucesso.");
                }

                // Aguarda o botão "Continuar" ficar clicável
                var confirmButton = wait.Until(d => d.FindElement(By.CssSelector(confirmButtonSelector)));
                confirmButton.Click();
                Console.WriteLine("Botão 'Continuar' clicado com sucesso.");
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera para a tela de verificação de idade expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tentar confirmar a verificação de idade: {ex.Message}");
            }
        }



        public void ClosePopup(IWebDriver driver, string popupSelector, int timeoutSeconds = 10000)
        {
            try
            {
                System.Threading.Thread.Sleep(new Random().Next(1855, 3626));

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

        public void ClosePopup(IPage page, string popupSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                // Aguarda até o botão de fechar o pop-up aparecer
                var closeButton = page.WaitForSelectorAsync(popupSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds,
                    Visible = true // Garante que o elemento está visível
                }).GetAwaiter().GetResult();

                if (closeButton != null)
                {
                    Console.WriteLine("Botão de fechar pop-up encontrado.");

                    // Verifica explicitamente se o elemento está no viewport
                    page.EvaluateFunctionAsync("element => { const rect = element.getBoundingClientRect(); return rect.top >= 0 && rect.left >= 0 && rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) && rect.right <= (window.innerWidth || document.documentElement.clientWidth); }", closeButton).GetAwaiter().GetResult();

                    try
                    {
                        // Tenta clicar no botão diretamente
                        closeButton.ClickAsync().GetAwaiter().GetResult();
                        Console.WriteLine("Pop-up fechado com sucesso.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao clicar diretamente no botão: {ex.Message}");
                        Console.WriteLine("Tentando clicar usando JavaScript...");

                        // Clique via JavaScript como fallback
                        page.EvaluateFunctionAsync("element => element.click()", closeButton).GetAwaiter().GetResult();
                        Console.WriteLine("Pop-up fechado com sucesso (método alternativo).");
                    }
                }
                else
                {
                    Console.WriteLine("Botão de fechar pop-up não encontrado.");
                }
            }
            catch (PuppeteerSharp.WaitTaskTimeoutException)
            {
                Console.WriteLine("Tempo de espera para fechar o pop-up expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro inesperado ao tentar fechar o pop-up: {ex.Message}");
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
