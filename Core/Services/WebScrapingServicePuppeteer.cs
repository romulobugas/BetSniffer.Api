using System;
using System.Collections.Generic;
using PuppeteerSharp;

namespace BetSniffer.Api.Core.Services
{
    public class WebScrapingServicePuppeteer : IDisposable
    {
        private IBrowser _browser;
        private IPage _page;

        public void Initialize()
        {
            // Caminho para o executável do Chrome/Chromium instalado no sistema
            string customChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe"; // Atualize o caminho conforme necessário

            if (!System.IO.File.Exists(customChromePath))
            {
                throw new InvalidOperationException($"O navegador Chrome não foi encontrado no caminho especificado: {customChromePath}");
            }

            // Configuração do navegador
            _browser = Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false, // Permite visualizar o navegador
                ExecutablePath = customChromePath,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-extensions",
                    "--disable-gpu",
                    "--ignore-certificate-errors",
                    "--start-maximized", // Abre o navegador em tela cheia
                    "--disable-infobars" // Remove a barra de informações do navegador
                }
            }).GetAwaiter().GetResult();

            // Abre uma nova página
            _page = _browser.NewPageAsync().GetAwaiter().GetResult();

            // Obtém as dimensões completas da tela (tela disponível)
            var screenDimensions = _page.EvaluateFunctionAsync<Dictionary<string, int>>(@"
                () => {
                    return {
                        width: window.screen.width,
                        height: window.screen.height
                    };
                }
            ").GetAwaiter().GetResult();

            // Ajusta o viewport para usar a resolução máxima
            _page.SetViewportAsync(new ViewPortOptions
            {
                Width = screenDimensions["width"], // Largura máxima da tela
                Height = screenDimensions["height"], // Altura máxima da tela
                DeviceScaleFactor = 1 // Nenhuma escala aplicada
            }).GetAwaiter().GetResult();

            Console.WriteLine($"Viewport ajustado para: {screenDimensions["width"]}x{screenDimensions["height"]}");



            // Injeta scripts de mascaramento desde o início
            InjectAntiAutomationScripts();
        }

        private void InjectAntiAutomationScripts()
        {
            const string script = @"
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                Object.defineProperty(navigator, 'plugins', { 
                    get: () => [{ name: 'Chrome PDF Plugin' }, { name: 'Chrome PDF Viewer' }, { name: 'Native Client' }]
                });
                Object.defineProperty(navigator, 'languages', { get: () => ['pt-BR', 'pt', 'en-US', 'en'] });
                window.chrome = { runtime: {} };
                const originalUserAgent = navigator.userAgent;
                Object.defineProperty(navigator, 'userAgent', {
                    get: () => originalUserAgent.replace('HeadlessChrome', 'Chrome')
                });
                console.log('Scripts de mascaramento aplicados.');
            ";

            _page.EvaluateExpressionAsync(script).GetAwaiter().GetResult();
        }

        public IPage NavigateTo(string url)
        {
            try
            {
                _page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Load }, // Aguarda apenas o carregamento básico
                    Timeout = 60000 // Ajusta o timeout para 60 segundos (ou o valor que desejar)
                }).GetAwaiter().GetResult();
                return _page;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao navegar para URL '{url}': {ex.Message}");
                throw;
            }
        }

        public string GetPageSource()
        {
            return _page.GetContentAsync().GetAwaiter().GetResult();
        }

        public IElementHandle WaitForElement(string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                return _page.WaitForSelectorAsync(selector, new WaitForSelectorOptions { Timeout = timeoutMilliseconds }).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao esperar pelo elemento '{selector}': {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _page?.CloseAsync().GetAwaiter().GetResult();
            _browser?.CloseAsync().GetAwaiter().GetResult();
        }
    }
}
