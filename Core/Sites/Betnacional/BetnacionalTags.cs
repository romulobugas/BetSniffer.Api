using OpenQA.Selenium;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BetSniffer.Api.Core.Sites.Betnacional
{
    public static class BetnacionalTags
    {
        // Tags fixas base usadas como template
        private static readonly Dictionary<int, List<string>> BaseTagNames = new()
        {
            { 6, new List<string> { "Total de Gols" } },
            { 20, new List<string> { "Total de Gols do Time de Casa" } },
            { 21, new List<string> { "Total de Gols do time de Fora" } },
            { 1, new List<string> { "Total de escanteios" } },
            { 8, new List<string> { "Total de escanteios time da casa" } },
            { 9, new List<string> { "Total de escanteios time de fora" } },
            { 34, new List<string> { "Total de cartões" } },
            { 3, new List<string> { "Total de chutes", "Total de finalizações" } },
            { 4, new List<string> { "Chutes a gol total" } },
            { 7, new List<string> { "Total de impedimentos" } },
            { 5, new List<string> { "Total de faltas" } },
            { 22, new List<string> { "Total de defesas do goleiro" } },
            { 35, new List<string> { "Time de casa total de cartões" } },
            { 36, new List<string> { "Time de fora total de cartões" } },
            { 12, new List<string> { "Time de casa chutes a gol" } },
            { 13, new List<string> { "Time de fora chutes a gol" } },
            { 14, new List<string> { "Time de casa total de Chutes", "Total de finalizações do time de casa" } },
            { 15, new List<string> { "Time de fora total de Chutes", "Total de finalizações do time de fora" } },
            { 18, new List<string> { "Time da casa Total de faltas" } },
            { 19, new List<string> { "Time de fora Total de faltas" } },
            { 23, new List<string> { "Time de casa total de defesas do goleiro" } },
            { 24, new List<string> { "Time de fora total de defesas do goleiro" } },
            { 25, new List<string> { "Total de cobranças de lateral" } },
            { 26, new List<string> { "Time de casa cobranças de lateral" } },
            { 27, new List<string> { "Time de fora cobranças de lateral" } },
            { 28, new List<string> { "Total de desarmes" } },
            { 29, new List<string> { "Total de desarmes do time de casa" } },
            { 30, new List<string> { "Total de desarmes do time de fora" } },
            { 31, new List<string> { "Total de tiros de meta" } },
            { 32, new List<string> { "Time de casa total de tiro de meta" } },
            { 33, new List<string> { "Time de fora total de tiro de meta" } },
            { 37, new List<string> { "Bola na trave" } },
        };

        // Isolamento por contexto de thread
        private static readonly ThreadLocal<Dictionary<int, List<string>>> ThreadTagNames =
            new(() => BaseTagNames.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value)));

        // Propriedade para acessar as tags isoladas da thread
        public static Dictionary<int, List<string>> TagNames => ThreadTagNames.Value;

        // Método para capturar o código dinâmico do elemento
        public static string CaptureElementCode(IWebElement element)
        {
            // Captura o código dinâmico a partir do atributo 'class' ou qualquer outra lógica necessária
            string classAttribute = element.GetAttribute("class");
            string elementCode = classAttribute.Split('-').Last(); // Supondo que o código seja o último segmento da classe
            return elementCode;
        }

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            // Lista de padrões de tags dinâmicas
            var dynamicTags = new Dictionary<int, List<string>>
            {
                //{ 8, new List<string> { $"{homeTeam} Total de escanteios", $"{homeTeam} Total de escanteios (alternativas)" } },
                //{ 9, new List<string> { $"{awayTeam} Total de escanteios", $"{awayTeam} Total de escanteios (alternativas)" } },
                //{ 14, new List<string> { $"{homeTeam} Total de chutes", $"{homeTeam} Total de chutes (alternativas)" } },
                //{ 15, new List<string> { $"{awayTeam} Total de chutes", $"{awayTeam} Total de chutes (alternativas)" } },
                //{ 12, new List<string> { $"{homeTeam} Chutes a gol", $"{homeTeam} Chutes a gol (alternativas)" } },
                //{ 13, new List<string> { $"{awayTeam} Chutes a gol", $"{awayTeam} Chutes a gol (alternativas)" } },
                //{ 20, new List<string> { $"{homeTeam} Total de Gols", $"{homeTeam} Total de Gols (alternativas)" } },
                //{ 21, new List<string> { $"{awayTeam} Total de Gols", $"{awayTeam} Total de Gols (alternativas)" } },
            };

            // Adicionar ao dicionário isolado de tags da thread
            var threadTags = ThreadTagNames.Value;

            foreach (var tag in dynamicTags)
            {
                if (!threadTags.TryGetValue(tag.Key, out var existingTags))
                {
                    // Adiciona a chave e a lista completa de tags
                    threadTags[tag.Key] = new List<string>(tag.Value);
                }
                else
                {
                    // Adiciona somente as tags que não existem ainda
                    foreach (var tagName in tag.Value)
                    {
                        if (!existingTags.Contains(tagName))
                        {
                            existingTags.Add(tagName);
                        }
                    }
                }
            }
        }
    }
}
