using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Linq;

namespace BetSniffer.Api.Core.Services
{
    public class WebScrapingServiceSelenium : IDisposable
    {
        private IWebDriver _driver;

        public void Initialize()
        {
            // Caminho para o executável do Chrome instalado no sistema
            string customChromePath = @"C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"; // Atualize conforme necessário

            // Configurações do ChromeDriver
            ChromeOptions options = new ChromeOptions();
            options.BinaryLocation = customChromePath;
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-setuid-sandbox");
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--start-maximized"); // Abre o navegador em tela cheia
            options.AddArgument("--disable-infobars"); // Remove a barra de informações do navegador
            options.AddArgument("--force-device-scale-factor=0.5"); // Define o zoom para 0.5
            options.AddUserProfilePreference("profile.default_content_setting_values.automatic_downloads", 1); // Permitir downloads automáticos
            options.AddExcludedArgument("enable-automation"); // Remove o controle de automação visível
            options.AddAdditionalOption("useAutomationExtension", false); // Desabilita a extensão de automação
            //options.AddArgument("--headless"); // Desabilita a interface visual


            // Criação do driver
            _driver = new ChromeDriver(options);

            // Injeta scripts para mascarar automação
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
            ";

            try
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript(script);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao injetar scripts de mascaramento: {ex.Message}");
            }
        }

        public void NavigateTo(string url)
        {
            try
            {
                _driver.Navigate().GoToUrl(url);
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
                wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao navegar para URL '{url}': {ex.Message}");
                throw;
            }
        }

        public string GetPageSource()
        {
            try
            {
                return _driver.PageSource;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter o conteúdo da página: {ex.Message}");
                throw;
            }
        }

        public IWebElement WaitForElement(string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMilliseconds));
                return wait.Until(d =>
                {
                    if (selector.StartsWith("//") || selector.StartsWith(".//"))
                    {
                        // Trata como XPath
                        return d.FindElement(By.XPath(selector));
                    }
                    else
                    {
                        // Trata como CSS Selector
                        return d.FindElement(By.CssSelector(selector));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao esperar pelo elemento '{selector}': {ex.Message}");
                throw;
            }
        }


        public IReadOnlyCollection<IWebElement> WaitForElements(string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMilliseconds));
                return wait.Until(d =>
                {
                    if (selector.StartsWith("//") || selector.StartsWith(".//"))
                    {
                        // Trata como XPath
                        return d.FindElements(By.XPath(selector));
                    }
                    else
                    {
                        // Trata como CSS Selector
                        return d.FindElements(By.CssSelector(selector));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao esperar pelos elementos '{selector}': {ex.Message}");
                throw;
            }
        }

        public IReadOnlyCollection<IWebElement> FindElementsWithin(IWebElement parentElement, string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMilliseconds));
                return wait.Until(d =>
                {
                    if (selector.StartsWith("//") || selector.StartsWith(".//"))
                    {
                        // Trata como XPath no contexto do elemento pai
                        return parentElement.FindElements(By.XPath(selector));
                    }
                    else
                    {
                        // Trata como CSS Selector no contexto do elemento pai
                        return parentElement.FindElements(By.CssSelector(selector));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar elementos dentro do contêiner com seletor '{selector}': {ex.Message}");
                throw;
            }
        }

        public IWebElement TryWaitForElement(string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMilliseconds));
                return wait.Until(d =>
                {
                    if (selector.StartsWith("//") || selector.StartsWith(".//"))
                    {
                        // Trata como XPath
                        return d.FindElement(By.XPath(selector));
                    }
                    else
                    {
                        // Trata como CSS Selector
                        return d.FindElement(By.CssSelector(selector));
                    }
                });
            }
            catch (WebDriverTimeoutException)
            {
                // Retorna null se o elemento não for encontrado dentro do tempo limite
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao esperar pelo elemento '{selector}': {ex.Message}");
                throw;
            }
        }

        public IWebElement FindElementWithin(IWebElement container, string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMilliseconds));
                return wait.Until(_ =>
                {
                    if (selector.StartsWith("//") || selector.StartsWith(".//"))
                    {
                        // Trata como XPath
                        return container.FindElement(By.XPath(selector));
                    }
                    else
                    {
                        // Trata como CSS Selector
                        return container.FindElement(By.CssSelector(selector));
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar elemento '{selector}' dentro do contêiner: {ex.Message}");
                return null; // Retorna null se o elemento não for encontrado dentro do tempo
            }
        }

        public IWebElement TryFindElementWithin(IWebElement container, string selector, int timeoutMilliseconds = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMilliseconds));
                return wait.Until(_ =>
                {
                    if (selector.StartsWith("//") || selector.StartsWith(".//"))
                    {
                        // Trata como XPath
                        return container.FindElement(By.XPath(selector));
                    }
                    else
                    {
                        // Trata como CSS Selector
                        return container.FindElement(By.CssSelector(selector));
                    }
                });
            }
            catch (WebDriverTimeoutException)
            {
                // Retorna null se o elemento não for encontrado dentro do tempo limite
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar elemento '{selector}' dentro do contêiner: {ex.Message}");
                throw;
            }
        }


        public IWebDriver GetWebDriver()
        {
            return _driver;
        }


        public void Dispose()
        {
            try
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao finalizar o WebDriver: {ex.Message}");
            }
        }
    }
}