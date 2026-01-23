using System;
using System.Collections.Generic;
using PuppeteerSharp;
using System.Linq;

namespace BetSniffer.Api.Core.Services
{
    public class WebScrapingServicePuppeteer : IDisposable
    {
        private IBrowser? _browser;
        private IPage? _page;

        public void Initialize()
        {
            // Caminho para o executável do Chrome/Chromium instalado no sistema
            string customChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

            if (!System.IO.File.Exists(customChromePath))
            {
                throw new InvalidOperationException($"O navegador Chrome não foi encontrado no caminho especificado: {customChromePath}");
            }

            // Configuração do navegador com argumentos que evitam problemas de referrerPolicy
            _browser = Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                ExecutablePath = customChromePath,
                DefaultViewport = null,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-blink-features=AutomationControlled",
                    "--disable-extensions",
                    "--ignore-certificate-errors",
                    "--start-maximized",
                    "--disable-infobars",
                    "--enable-accelerated-2d-canvas",
                    "--use-gl=desktop",
                    "--force-device-scale-factor=0.7",
                    "--disable-features=VizDisplayCompositor",
                    "--disable-web-resources", // Evita validações rigorosas de recursos
                    "--disable-extensions-file-access-check"
                }
            }).GetAwaiter().GetResult();

            var pages = _browser.PagesAsync().GetAwaiter().GetResult();
            _page = pages.FirstOrDefault() ?? _browser.NewPageAsync().GetAwaiter().GetResult();

            // Define o User-Agent para parecer um navegador normal
            _page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36").GetAwaiter().GetResult();

            // Injeta scripts de mascaramento
            InjectAntiAutomationScripts();
        }

        private void EnsureInitialized()
        {
            if (_browser is null || _page is null)
            {
                throw new InvalidOperationException("WebScrapingServicePuppeteer não foi inicializado. Chame Initialize() antes de usar outras operações.");
            }
        }

        private IBrowser Browser
        {
            get
            {
                EnsureInitialized();
                return _browser!;
            }
        }

        private IPage Page
        {
            get
            {
                EnsureInitialized();
                return _page!;
            }
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
                // Define referrerPolicy para evitar erros de navegação
                document.referrerPolicy = 'no-referrer-when-downgrade';
                console.log('Scripts de mascaramento aplicados.');
            ";

            Page.EvaluateExpressionAsync(script).GetAwaiter().GetResult();
        }

        public IPage NavigateTo(string url)
        {
            try
            {
                // Tenta navegação com timeout reduzido e sem esperar a rede ficar completamente ociosa
                Page.GoToAsync(url, new NavigationOptions 
                { 
                    Timeout = 30000,
                    WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }
                }).GetAwaiter().GetResult();
                return Page;
            }
            catch (PuppeteerException ex) when (ex.Message.Contains("Invalid referrerPolicy") || ex.Message.Contains("Protocol error"))
            {
                Console.WriteLine($"⚠️ Erro de navegação com WaitUntil, tentando sem aguardar rede...");
                try
                {
                    // Fallback: sem nenhuma opção de wait
                    Page.GoToAsync(url, new NavigationOptions { Timeout = 30000 }).GetAwaiter().GetResult();
                    System.Threading.Thread.Sleep(2000); // Aguarda manualmente por segurança
                    return Page;
                }
                catch (Exception retryEx)
                {
                    Console.WriteLine($"❌ Falha em ambas as tentativas: {retryEx.Message}");
                    throw;
                }
            }
            catch (PuppeteerException ex) when (ex.Message.Contains("Timeout"))
            {
                Console.WriteLine($"⚠️ Timeout na navegação, mas tentando continuar mesmo assim...");
                // Se a página carregou parcialmente, tenta continuar
                System.Threading.Thread.Sleep(2000);
                return Page;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao navegar para URL '{url}': {ex.Message}");
                throw;
            }
        }

        public string GetPageSource()
        {
            return Page.GetContentAsync().GetAwaiter().GetResult();
        }

        public Page GetPage()
        {
            return (Page)Page;
        }

        public IElementHandle WaitForElement(string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                return Page.WaitForSelectorAsync(selector, new WaitForSelectorOptions { Timeout = timeoutMilliseconds }).GetAwaiter().GetResult();
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
                var frame = frameElement.ContentFrameAsync().GetAwaiter().GetResult();
                if (frame is null)
                {
                    throw new InvalidOperationException($"Frame não encontrado para o seletor '{selector}'.");
                }

                return frame;
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
                return Page.EvaluateFunctionAsync<T>(script, args).GetAwaiter().GetResult();
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
            return Page.Frames.ToList();
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
                Page.GoBackAsync().GetAwaiter().GetResult();
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
            _page = null;
            _browser = null;
        }
    }
}
