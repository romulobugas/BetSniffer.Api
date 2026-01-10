using PuppeteerSharp;
using System;
using System.Threading.Tasks;
using BetSniffer.Api.Core.Interfaces;

namespace BetSniffer.Api.Core.Services
{
    /// <summary>
    /// Serviço centralizado para operações comuns em páginas Puppeteer.
    /// Consolida funcionalidades duplicadas de manipulação de pop-ups, cookies e obstruções.
    /// </summary>
    public class PuppeteerPageService
    {
        private readonly ILogService _logService;

        public PuppeteerPageService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Fecha pop-ups na página de forma assincronizada.
        /// </summary>
        /// <param name="page">A página Puppeteer</param>
        /// <param name="popupSelector">Seletor CSS do pop-up</param>
        /// <param name="timeoutMilliseconds">Timeout em milissegundos</param>
        public async Task ClosePopupAsync(IPage page, string popupSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                await Task.Delay(new Random().Next(1855, 3626)); // Espera aleatória para simular humano

                var element = await page.WaitForSelectorAsync(popupSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                });

                if (element != null)
                {
                    await element.ClickAsync();
                    Console.WriteLine("? Pop-up fechado com sucesso.");
                }
                else
                {
                    Console.WriteLine("?? Pop-up não encontrado.");
                }
            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine("?? Tempo de espera para fechar o pop-up expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao fechar o pop-up: {ex.Message}");
                _logService.LogError("Erro ao fechar pop-up", ex);
            }
        }

        /// <summary>
        /// Aceita cookies na página de forma assincronizada.
        /// </summary>
        /// <param name="page">A página Puppeteer</param>
        /// <param name="cookieAcceptButtonSelector">Seletor CSS do botão de aceitar cookies</param>
        /// <param name="timeoutMilliseconds">Timeout em milissegundos</param>
        public async Task HandleCookiesAsync(IPage page, string cookieAcceptButtonSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                await Task.Delay(new Random().Next(981, 1758)); // Espera aleatória para simular humano

                var element = await page.WaitForSelectorAsync(cookieAcceptButtonSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                });

                if (element != null)
                {
                    await element.ClickAsync();
                    Console.WriteLine("? Botão de aceitar cookies clicado com sucesso.");
                }
                else
                {
                    Console.WriteLine("?? Botão de aceitar cookies não encontrado.");
                }
            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine("?? Tempo de espera para localizar cookies expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao lidar com cookies: {ex.Message}");
                _logService.LogError("Erro ao lidar com cookies", ex);
            }
        }

        /// <summary>
        /// Remove obstruções visuais (overlays, modais, etc.) da página.
        /// </summary>
        /// <param name="page">A página Puppeteer</param>
        /// <param name="obstructionSelector">Seletor CSS da obstrução</param>
        /// <param name="timeoutMilliseconds">Timeout em milissegundos</param>
        public async Task RemoveObstructionAsync(IPage page, string obstructionSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                await Task.Delay(new Random().Next(981, 1758)); // Espera aleatória para simular humano

                var element = await page.WaitForSelectorAsync(obstructionSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                });

                if (element != null)
                {
                    // Remove o elemento alterando seu estilo
                    await page.EvaluateFunctionAsync(
                        "selector => { const el = document.querySelector(selector); if (el) el.style.display = 'none'; }",
                        obstructionSelector);
                    Console.WriteLine("? Elemento de obstrução removido com sucesso.");
                }
                else
                {
                    Console.WriteLine("?? Elemento de obstrução não encontrado.");
                }
            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine("?? Tempo de espera para localizar obstrução expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao remover obstrução: {ex.Message}");
                _logService.LogError("Erro ao remover obstrução", ex);
            }
        }

        /// <summary>
        /// Confirma verificação de idade clicando em um botão simples.
        /// </summary>
        /// <param name="page">A página Puppeteer</param>
        /// <param name="ageVerificationSelector">Seletor CSS do botão</param>
        /// <param name="timeoutMilliseconds">Timeout em milissegundos</param>
        public async Task ConfirmAgeVerificationAsync(IPage page, string ageVerificationSelector, int timeoutMilliseconds = 10000)
        {
            try
            {
                await Task.Delay(new Random().Next(1511, 3522)); // Espera aleatória para simular humano

                var element = await page.WaitForSelectorAsync(ageVerificationSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                });

                if (element != null)
                {
                    await element.ClickAsync();
                    Console.WriteLine("? Botão de confirmação de idade clicado com sucesso.");
                }
                else
                {
                    Console.WriteLine("?? Botão de confirmação não encontrado.");
                }
            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine("?? Tempo de espera para confirmação de idade expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao confirmar idade: {ex.Message}");
                _logService.LogError("Erro ao confirmar idade", ex);
            }
        }

        /// <summary>
        /// Confirma verificação de idade com checkbox + botão.
        /// </summary>
        /// <param name="page">A página Puppeteer</param>
        /// <param name="checkboxSelector">Seletor CSS do checkbox</param>
        /// <param name="confirmButtonSelector">Seletor CSS do botão de confirmação</param>
        /// <param name="timeoutMilliseconds">Timeout em milissegundos</param>
        public async Task ConfirmAgeVerificationWithCheckboxAsync(
            IPage page,
            string checkboxSelector,
            string confirmButtonSelector,
            int timeoutMilliseconds = 10000)
        {
            try
            {
                await Task.Delay(new Random().Next(2145, 2987)); // Espera aleatória para simular humano

                // Clica no checkbox
                var checkbox = await page.WaitForSelectorAsync(checkboxSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                });

                if (checkbox != null)
                {
                    await checkbox.ClickAsync();
                    Console.WriteLine("? Checkbox de idade marcado com sucesso.");
                }

                // Clica no botão de confirmação
                await Task.Delay(new Random().Next(500, 1000));
                var confirmButton = await page.WaitForSelectorAsync(confirmButtonSelector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds
                });

                if (confirmButton != null)
                {
                    await confirmButton.ClickAsync();
                    Console.WriteLine("? Botão de confirmação clicado com sucesso.");
                }
                else
                {
                    Console.WriteLine("?? Botão de confirmação não encontrado.");
                }
            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine("?? Tempo de espera para verificação de idade expirou.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao confirmar idade com checkbox: {ex.Message}");
                _logService.LogError("Erro ao confirmar idade com checkbox", ex);
            }
        }

        /// <summary>
        /// Aguarda um elemento estar visível e pronto para interação.
        /// </summary>
        /// <param name="page">A página Puppeteer</param>
        /// <param name="selector">Seletor CSS do elemento</param>
        /// <param name="timeoutMilliseconds">Timeout em milissegundos</param>
        public async Task<IElementHandle> WaitForElementAsync(IPage page, string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                return await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
                {
                    Timeout = timeoutMilliseconds,
                    Visible = true
                });
            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine($"?? Tempo de espera para elemento '{selector}' expirou.");
                return null;
            }
        }

        /// <summary>
        /// Clica em um elemento e aguarda que a navegação seja concluída.
        /// </summary>
        /// <param name="page">A página Puppeteer</param>
        /// <param name="element">O elemento a clicar</param>
        /// <param name="timeoutMilliseconds">Timeout em milissegundos</param>
        public async Task ClickAndWaitForNavigationAsync(
            IPage page,
            IElementHandle element,
            int timeoutMilliseconds = 30000)
        {
            try
            {
                await Task.WhenAll(
                    page.WaitForNavigationAsync(new NavigationOptions { Timeout = timeoutMilliseconds }),
                    element.ClickAsync()
                );
                Console.WriteLine("? Clique executado e navegação concluída.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao clicar e aguardar navegação: {ex.Message}");
                _logService.LogError("Erro ao clicar e aguardar navegação", ex);
            }
        }
    }
}
