using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions; // Para usar expressões regulares
using BetSniffer.Api.Core.Models; // Importar a classe TagInfo

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public class NovibetScrapingService
    {
        private readonly IWebDriver _driver;

        public NovibetScrapingService()
        {
            _driver = new ChromeDriver();
        }

        // Método para fazer o scraping e retornar as tags e códigos encontrados
        public List<TagInfo> ScrapeTags(string url)
        {
            _driver.Navigate().GoToUrl(url);

            // Espera até que os elementos da página estejam carregados
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

            // Aguarda até que o primeiro elemento esperado esteja visível
            try
            {
                wait.Until(driver => driver.FindElement(By.XPath("//span[contains(text(), 'Total de Escanteios')]")));
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera excedido, o elemento não foi encontrado.");
                _driver.Quit();
                return new List<TagInfo>();
            }

            List<TagInfo> tagInfos = new List<TagInfo>();

            foreach (var tagName in NovibetTags.TagNames) // Assumindo que TagNames é uma lista de tags
            {
                try
                {
                    // Encontra o elemento com base no nome da tag
                    var element = wait.Until(driver => driver.FindElement(By.XPath($"//span[contains(text(), '{tagName}')]")));

                    // Captura o código dinâmico do elemento, ou seja, a parte após "_ngcontent-ng-"
                    string elementCode = CaptureElementCode(element);

                    // Armazena as informações da tag
                    tagInfos.Add(new TagInfo
                    (
                        tagName,                  // Nome da tag fixa
                        "_ngcontent-ng-",         // Prefixo fixo do nome do elemento
                        elementCode,              // Código dinâmico (extraído da regex)
                        element.GetAttribute("outerHTML") // HTML completo do elemento
                    ));
                }
                catch (NoSuchElementException)
                {
                    // Se o elemento não for encontrado, apenas ignora
                    continue;
                }
                catch (WebDriverTimeoutException)
                {
                    // Se o elemento não aparecer dentro do tempo limite, ignora
                    continue;
                }
            }

            _driver.Quit(); // Encerra o driver após o scraping

            return tagInfos;
        }

        // Método para capturar o código dinâmico (parte após "_ngcontent-ng-")
        private string CaptureElementCode(IWebElement element)
        {
            string outerHtml = element.GetAttribute("outerHTML");

            // Regex para encontrar o código dinâmico após "_ngcontent-ng-" (ex: c1897204168)
            var regex = new Regex(@"_ngcontent-ng-(\w+)");
            var match = regex.Match(outerHtml);

            if (match.Success)
            {
                return match.Groups[1].Value; // Retorna o código dinâmico encontrado
            }

            return string.Empty; // Caso o código não seja encontrado, retorna uma string vazia
        }
    }
}
