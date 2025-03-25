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
            { 6, new List<string> { "Total" } },
            { 1, new List<string> { "Total De Escanteios" } },
            { 34, new List<string> { "Cartão - Total De Cartões" } },

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
                { 20, new List<string> { $"{homeTeam} - Total" } },
                { 21, new List<string> { $"{awayTeam} - Total" } },
                // { 8, new List<string> { $"Total de Escanteios do Time {homeTeam}" } },
                // { 9, new List<string> { $"Total de Escanteios do Time {awayTeam}" } },
                // Outros comentários para referência...
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
