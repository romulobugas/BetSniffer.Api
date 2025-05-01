using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;

namespace BetSniffer.Api.Core.Services
{
    public class WebScrapingServiceAppium : IDisposable
    {
        private AndroidDriver _driver;

        private Process _appiumServerProcess;

        public async Task InitializeAsync()
        {
            // Primeiro: Startar o ADB manualmente
            StartAdbServer();

            var options = new AppiumOptions();
            options.PlatformName = "Android";
            options.DeviceName = "emulator-5554"; // ou ajustar dinamicamente depois
            options.BrowserName = "Chrome";

            // Localiza automaticamente o chromedriver dentro da pasta Drivers
            var chromedriverPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "chromedriver.exe");
            options.AddAdditionalAppiumOption("chromedriverExecutable", chromedriverPath);

            _driver = new AndroidDriver(new Uri("http://127.0.0.1:4723/wd/hub"), options);

            await Task.Delay(1000); // Aguarda sessão ser estabelecida
        }

        private void StartAppiumServer()
        {
            try
            {
                var appiumPath = @"C:\Users\SEU_USUARIO\AppData\Roaming\npm\appium.cmd"; // Caminho do appium.cmd
                var adbFolder = Path.Combine(AppContext.BaseDirectory, "Drivers");

                if (!System.IO.File.Exists(appiumPath))
                {
                    throw new FileNotFoundException("Appium não encontrado no caminho esperado.", appiumPath);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = appiumPath,
                    Arguments = "--address 127.0.0.1 --port 4723",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Setar as variáveis de ambiente manualmente
                startInfo.Environment["ANDROID_HOME"] = adbFolder;
                startInfo.Environment["ANDROID_SDK_ROOT"] = adbFolder;
                startInfo.Environment["PATH"] = $"{adbFolder};{Environment.GetEnvironmentVariable("PATH")}";

                _appiumServerProcess = Process.Start(startInfo);

                if (_appiumServerProcess == null)
                {
                    throw new Exception("Falha ao iniciar o Appium Server.");
                }

                Console.WriteLine("Appium Server iniciado com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao iniciar o Appium Server: {ex.Message}");
                throw;
            }
        }

        private void StartAdbServer()
        {
            try
            {
                var adbPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "adb.exe");

                if (!System.IO.File.Exists(adbPath))
                {
                    throw new FileNotFoundException("ADB não encontrado no caminho esperado.", adbPath);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "start-server",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(startInfo);
                process.WaitForExit(5000); // Espera até 5 segundos o ADB iniciar
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao iniciar ADB: {ex.Message}");
                throw;
            }
        }

        public async Task NavigateToAsync(string url)
        {
            _driver.Navigate().GoToUrl(url);
            await Task.Delay(2000); // Aguarda o carregamento inicial
        }

        public async Task<IWebElement> WaitForElementAsync(string selector, int timeoutMs = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMs));
                return wait.Until(d => d.FindElement(By.CssSelector(selector)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao esperar elemento '{selector}': {ex.Message}");
                return null;
            }
        }

        public async Task<IReadOnlyCollection<IWebElement>> WaitForElementsAsync(string selector, int timeoutMs = 10000)
        {
            try
            {
                WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromMilliseconds(timeoutMs));
                return wait.Until(d => d.FindElements(By.CssSelector(selector)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao esperar elementos '{selector}': {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetPageSourceAsync()
        {
            try
            {
                return _driver.PageSource;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao pegar fonte da página: {ex.Message}");
                return null;
            }
        }

        public async Task ScrollDownAsync()
        {
            try
            {
                var js = (IJavaScriptExecutor)_driver;
                js.ExecuteScript("window.scrollBy(0, window.innerHeight);");
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao fazer scroll: {ex.Message}");
            }
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
                Console.WriteLine($"Erro ao finalizar o Appium Driver: {ex.Message}");
            }
        }
    }
}
