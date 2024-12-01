using BetSniffer.Api.Core.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public class NovibetScrapingService
    {
        private readonly IWebDriver _driver;

        public NovibetScrapingService()
        {
            _driver = new ChromeDriver();
        }

        // Método para fazer o scraping e retornar as tags e apostas encontradas
        public List<TagInfo> ScrapeTags(string url)
        {
            _driver.Navigate().GoToUrl(url);

            // Espera até que os elementos da página estejam carregados
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

            // Fechar o pop-up, caso ele apareça
            try
            {
                var closeButton = wait.Until(driver => driver.FindElement(By.CssSelector(".registerOrLogin_closeButton")));
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

            // Aguarda até que o primeiro elemento esperado esteja visível
            try
            {
                wait.Until(driver => driver.FindElement(By.XPath("//app-event-marketview")));
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Tempo de espera excedido, o elemento não foi encontrado.");
                _driver.Quit();
                return new List<TagInfo>();
            }

            List<TagInfo> tagInfos = new List<TagInfo>();

            // Encontra todos os contêineres de aposta
            var eventMarketViews = _driver.FindElements(By.TagName("app-event-marketview"));

            // Lista de tags cadastradas que queremos buscar
            var tagNames = new List<string> { "Total de Escanteios", "Casa Total de Escanteios" };

            foreach (var eventMarketView in eventMarketViews)
            {
                try
                {
                    // Verifica se o evento contém uma tag válida
                    var tagElement = eventMarketView.FindElement(By.XPath(".//span[contains(@class, 'eventMarketview_title')]"));

                    string tagName = tagElement.Text.Trim();

                    // Verifica se a tag encontrada contém o nome da tag desejada, ignorando diferenças como emojis
                    if (tagNames.Any(tag => tagName.Contains(tag)))
                    {
                        // Verifica se o botão "Ver Mais" (expandir aposta) está presente
                        try
                        {
                            var expandCollapseButton = eventMarketView.FindElement(By.XPath(".//sb-market-bet-expand-collapse//span[contains(text(), 'Ver Mais')]"));
                            if (expandCollapseButton != null)
                            {
                                // Clica no botão "Ver Mais" para expandir as apostas
                                expandCollapseButton.Click();

                                // Espera um tempo para garantir que as apostas foram carregadas após o clique
                                WebDriverWait waitForLoad = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
                                waitForLoad.Until(driver => driver.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_caption')]")).Count > 0);
                            }
                        }
                        catch (NoSuchElementException)
                        {
                            // Se o botão "Ver Mais" não for encontrado, segue para o próximo passo
                            // Não há necessidade de fazer nada, pois as apostas já podem estar visíveis
                        }

                        // Captura todo o HTML do app-event-marketview
                        string eventMarketViewHtml = eventMarketView.GetAttribute("outerHTML");

                        // Lista para armazenar as apostas
                        List<string> bets = new List<string>();

                        // Encontrar todas as apostas dentro do mesmo app-event-marketview
                        var betElements = eventMarketView.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_caption singleLineEllipsis')]"));

                        // Encontrar todos os multiplicadores de apostas dentro do app-event-marketview
                        var multiplierElements = eventMarketView.FindElements(By.XPath(".//span[contains(@class, 'marketBetItem_price')]"));

                        // Verifica se o número de apostas é igual ao número de multiplicadores
                        int betCount = betElements.Count;
                        int multiplierCount = multiplierElements.Count;

                        if (betCount == multiplierCount)
                        {
                            // Itera sobre as apostas e seus multiplicadores
                            for (int i = 0; i < betCount; i++)
                            {
                                string betName = betElements[i].Text.Trim();
                                string multiplier = multiplierElements[i].Text.Trim();

                                if (!string.IsNullOrEmpty(betName) && !string.IsNullOrEmpty(multiplier))
                                {
                                    // Adiciona a aposta e multiplicador no formato desejado
                                    bets.Add($"{betName}: {multiplier}");
                                }
                            }

                            // Se encontrou apostas, formata e adiciona ao retorno
                            if (bets.Count > 0)
                            {
                                string formattedBets = string.Join(" - ", bets);
                                tagInfos.Add(new TagInfo(tagName, tagElement.GetAttribute("class"), "dynamic_code", formattedBets));
                            }
                        }
                    }
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
    }
}
