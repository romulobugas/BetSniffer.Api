using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using OpenQA.Selenium.Interactions;

namespace BetSniffer.Api.Core.Services
{
    

    public class AdvancedUndetectedChromeDriver : ChromeDriver
    {
        private Random _random = new Random();

        public AdvancedUndetectedChromeDriver(ChromeOptions options) : base(CreateAdvancedUndetectedChromeOptions(options))
        {
            this.ExecuteCdpCommand("Page.addScriptToEvaluateOnNewDocument", new Dictionary<string, object>
            {
                ["source"] = @"
                (() => {
                    window.navigator.chrome = {
                        runtime: {},
                        // Add other properties here.
                    };
                    Object.defineProperty(navigator, 'plugins', {
                        get: () => [1, 2, 3, 4, 5],
                    });
                    Object.defineProperty(navigator, 'languages', {
                        get: () => ['en-US', 'en'],
                    });
                    const originalQuery = window.navigator.permissions.query;
                    window.navigator.permissions.query = (parameters) => (
                        parameters.name === 'notifications' ?
                            Promise.resolve({ state: Notification.permission }) :
                            originalQuery(parameters)
                    );
                    // Overwrite the `webdriver` property
                    Object.defineProperty(navigator, 'webdriver', {
                        get: () => undefined,
                    });
                })();
            "
            });
        }

        private static ChromeOptions CreateAdvancedUndetectedChromeOptions(ChromeOptions options)
        {
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddAdditionalOption("useAutomationExtension", false);
            options.AddExcludedArgument("enable-automation");

            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-infobars");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--start-maximized");

            // Randomize window size
            int width = new Random().Next(1024, 1920);
            int height = new Random().Next(768, 1080);
            options.AddArgument($"--window-size={width},{height}");

            // Randomize user agent
            var userAgents = new[]
            {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/114.0"
        };
            options.AddArgument($"user-agent={userAgents[new Random().Next(userAgents.Length)]}");

            return options;
        }

        public new void Navigate()
        {
            base.Navigate();
            SimulateHumanBehavior();
        }

        private void SimulateHumanBehavior()
        {
            // Simulate random scrolling
            IJavaScriptExecutor js = this;
            int scrollAmount = _random.Next(100, 300);
            js.ExecuteScript($"window.scrollBy(0, {scrollAmount});");

            // Simulate random mouse movements
            Actions actions = new Actions(this);
            for (int i = 0; i < 5; i++)
            {
                int x = _random.Next(0, 500);
                int y = _random.Next(0, 500);
                actions.MoveByOffset(x, y).Perform();
                System.Threading.Thread.Sleep(_random.Next(100, 500));
            }

            // Add random delay
            System.Threading.Thread.Sleep(_random.Next(1000, 3000));
        }
    }


}
