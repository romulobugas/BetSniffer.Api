using System;
using System.Collections.Generic;
using PuppeteerSharp;
using System.Linq;
using PuppeteerSharp.Mobile;

namespace BetSniffer.Api.Core.Services
{
    public class WebScrapingServicePuppeteer : IDisposable
    {
        public WebScrapingServicePuppeteer(bool isMobile = false)
        {
            _isMobile = isMobile;
        }

        private readonly bool _isMobile;
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
                DefaultViewport = null, // Desativa o viewport padrão do Puppeteer
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-extensions",
                    "--ignore-certificate-errors",
                    "--start-maximized", // Abre o navegador em tela cheia
                    "--disable-infobars", // Remove a barra de informações do navegador
                    "--enable-accelerated-2d-canvas",
                    "--use-gl=desktop",
                    "--force-device-scale-factor=0.7" // Define o zoom global do navegador para 70%
                }
            }).GetAwaiter().GetResult();

            // Obtém a primeira aba existente
            var pages = _browser.PagesAsync().GetAwaiter().GetResult();
            _page = pages.FirstOrDefault() ?? _browser.NewPageAsync().GetAwaiter().GetResult(); // Usa a aba existente ou cria uma nova

            if (_isMobile)
            {
                EmulateMobileAsync().GetAwaiter().GetResult();
            }

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

        private async Task EmulateMobileAsync()
        {
            Console.WriteLine("Ativando emulação mobile via CDP...");

            await _page.Client.SendAsync("Emulation.setDeviceMetricsOverride", new
            {
                width = 414,
                height = 896,
                deviceScaleFactor = 2,
                mobile = true,
                screenOrientation = new { angle = 0, type = "portraitPrimary" }
            });

            await _page.Client.SendAsync("Emulation.setUserAgentOverride", new
            {
                userAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0 Mobile/15E148 Safari/604.1"
            });

            Console.WriteLine("Emulação mobile completa aplicada.");
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

        public Page GetPage()
        {
            return (Page)_page;
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

        public IFrame GetFrameBySelector(string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                var frameElement = WaitForElement(selector, timeoutMilliseconds);
                return frameElement.ContentFrameAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao acessar o frame pelo seletor '{selector}': {ex.Message}");
                throw;
            }
        }

        public T ExecuteJavaScript<T>(string script, params object[] args)
        {
            try
            {
                return _page.EvaluateFunctionAsync<T>(script, args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao executar JavaScript: {ex.Message}");
                throw;
            }
        }

        public void ScrollToElement(IFrame frame, IElementHandle element)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            // Usa o contexto correto do frame para executar o scroll
            frame.EvaluateFunctionAsync(@"el => {
                el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
            }", element).GetAwaiter().GetResult();
        }

        public void ScrollToElement(IPage page, IElementHandle element)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            // Usa o contexto correto da página para executar o scroll
            page.EvaluateFunctionAsync(@"el => {
                el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
            }", element).GetAwaiter().GetResult();
        }

        public void ClickWithJavaScript(IFrame frame, IElementHandle element)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            try
            {
                // Usa o contexto correto do frame para executar o clique
                frame.EvaluateFunctionAsync("element => element.click()", element).GetAwaiter().GetResult();
                Console.WriteLine("Clique realizado via JavaScript no contexto do iframe.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao clicar via JavaScript no iframe: {ex.Message}");
                throw;
            }
        }

        public void ClickWithJavaScript(IPage page, IElementHandle element)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            try
            {
                // Usa o contexto correto da página para executar o clique
                page.EvaluateFunctionAsync("element => element.click()", element).GetAwaiter().GetResult();
                Console.WriteLine("Clique realizado via JavaScript no contexto da página.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao clicar via JavaScript na página: {ex.Message}");
                throw;
            }
        }

        public List<IFrame> GetAllFrames()
        {
            return _page.Frames.ToList();
        }

        public string GetFrameContent(IFrame frame)
        {
            return frame.GetContentAsync().GetAwaiter().GetResult();
        }

        public IFrame NavigateAndReturnFrame(string url, string frameSelector)
        {
            NavigateTo(url);
            return GetFrameBySelector(frameSelector);
        }

        public IFrame ClickAndWaitForNewFrame(IFrame frame, IElementHandle element, string newFrameSelector, int timeout = 20000)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            // Realiza o clique no contexto do frame
            ClickWithJavaScript(frame, element);

            System.Threading.Thread.Sleep(1000); // Aguarda um pouco antes de verificar os frames

            // Retorna o novo frame pelo seletor
            return GetFrameBySelector(newFrameSelector, timeout);
        }

        public IFrame ClickAndWaitForNewFrame(IPage page, IElementHandle element, string newFrameSelector, int timeout = 20000)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            // Realiza o clique no contexto da página
            ClickWithJavaScript(page, element);

            System.Threading.Thread.Sleep(1000); // Aguarda um pouco antes de verificar os frames

            // Retorna o novo frame pelo seletor
            return GetFrameBySelector(newFrameSelector, timeout);
        }

        public void GoBack()
        {
            try
            {
                Console.WriteLine("Voltando no histórico da página principal...");
                _page.GoBackAsync().GetAwaiter().GetResult();
                System.Threading.Thread.Sleep(2000); // Pausa para garantir o carregamento
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao voltar à página anterior: {ex.Message}");
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
