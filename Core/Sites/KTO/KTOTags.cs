using OpenQA.Selenium;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BetSniffer.Api.Core.Sites.KTO
{
    public static class KTOTags
    {
        // Tags fixas base usadas como template
        private static readonly Dictionary<int, List<string>> BaseTagNames = new()
        {
            { 6, new List<string> { "Total de gols" } },
            { 1, new List<string> { "Total de escanteios" } },
            { 34, new List<string> { "Total de Cartões" } },
            { 40, new List<string> { "Total de gols - 1º Tempo" } },
            { 43, new List<string> { "Total de Escanteios - 1º Tempo" } },
            { 67, new List<string> { "Total de gols - 2º Tempo" } },
            { 4, new List<string> { "Total de chutes a gol (Decidido utilizando dados da Opta)" } },
            { 3, new List<string> { "Total de chutes (Decidido através dos dados de Opta)" } },
            { 5, new List<string> { "Total de faltas concedidas (Decidido através dos dados de Opta)" } },
            { 7, new List<string> { "Total de impedimentos (Decidido através dos dados de Opta)" } },
        };

        // Isolamento por contexto assíncrono (em vez de por thread)
        private static readonly AsyncLocal<Dictionary<int, List<string>>> ContextTags = new();

        // Acesso à instância isolada atual
        public static Dictionary<int, List<string>> TagNames
        {
            get
            {
                if (ContextTags.Value == null)
                {
                    ContextTags.Value = BaseTagNames.ToDictionary(kvp => kvp.Key, kvp => new List<string>(kvp.Value));
                }
                return ContextTags.Value;
            }
        }

        // Método para capturar código do elemento (caso precise)
        public static string CaptureElementCode(IWebElement element)
        {
            string classAttribute = element?.GetAttribute("class") ?? "";
            return classAttribute.Split('-').LastOrDefault() ?? "";
        }

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
                return;

            var dynamicTags = new Dictionary<int, List<string>>
            {
                { 20, new List<string> { $"Total de Gols do {homeTeam}" } },
                { 21, new List<string> { $"Total de Gols do {awayTeam}" } },
                { 41, new List<string> { $"Total de Gols do {homeTeam} - 1º Tempo" } },
                { 42, new List<string> { $"Total de Gols do {awayTeam} - 1º Tempo" } },
                { 8, new List<string> { $"Total de Escanteio por {homeTeam}" } },
                { 9, new List<string> { $"Total de Escanteio por {awayTeam}" } },
                { 44, new List<string> { $"Total de escanteios por {homeTeam} - 1º Tempo" } },
                { 45, new List<string> { $"Total de escanteios por {awayTeam} - 1º Tempo" } },
                { 35, new List<string> { $"Total de cartões - {homeTeam}" } },
                { 36, new List<string> { $"Total de cartões - {awayTeam}" } },
                { 12, new List<string> { $"Total de chutes a gol por {homeTeam} (Decidido utilizando dados da Opta)" } },
                { 13, new List<string> { $"Total de chutes a gol por {awayTeam} (Decidido utilizando dados da Opta)" } },
                { 14, new List<string> { $"Total de chutes por {homeTeam} (Decidido utilizando dados da Opta)" } },
                { 15, new List<string> { $"Total de chutes por {awayTeam} (Decidido utilizando dados da Opta)" } },
                { 18, new List<string> { $"Total de faltas concedidas por {homeTeam} (Decidido utilizando dados da Opta)" } },
                { 19, new List<string> { $"Total de faltas concedidas por {awayTeam} (Decidido utilizando dados da Opta)" } },
                { 16, new List<string> { $"Total de Impedimentos por {homeTeam} (Decidido através dos dados de Opta)" } },
                { 17, new List<string> { $"Total de Impedimentos por {awayTeam} (Decidido através dos dados de Opta)" } },
            };

            var tags = TagNames;

            foreach (var (tagId, tagList) in dynamicTags)
            {
                if (!tags.TryGetValue(tagId, out var existingList))
                {
                    tags[tagId] = new List<string>(tagList);
                }
                else
                {
                    foreach (var tag in tagList)
                    {
                        if (!existingList.Contains(tag))
                        {
                            existingList.Add(tag);
                        }
                    }
                }
            }
        }
    }
}
