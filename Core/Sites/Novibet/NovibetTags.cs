using OpenQA.Selenium;
using System.Collections.Concurrent;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public static class NovibetTags
    {
        // Dicionário base de tags fixas
        private static readonly Dictionary<int, string> BaseTagNames = new()
        {
            { 1, "Total de Escanteios 🚀" },
            { 2, "Total de Cartões Amarelos"},
            { 3, "Total de Chutes 🚀"},
            { 4, "Total de Chutes no gol 🚀"},
            { 7, "Total de Impedimentos"},
            { 5, "Total de Faltas"},
            { 8, "Casa Total de Escanteios"},
            { 9, "Visitante Total de Escanteios"},
            { 10, "Casa Total de Cartões Amarelos"},
            { 11, "Visitante Total de Cartões Amarelos"},
            { 6, "Total de Gols 🚀"},
            { 20, "Casa Total de Gols"},
            { 21, "Visitante Total de Gols" },
            { 25, "Total de Laterais" },
            { 28, "Total de Desarmes" },
            { 31, "Total de Tiros de Meta" },
            { 37, "Bola na trave" },
        };

        // Isolamento por thread usando ThreadLocal
        private static readonly ThreadLocal<ConcurrentDictionary<int, string>> ThreadLocalTagNames =
            new(() => new ConcurrentDictionary<int, string>(BaseTagNames));

        // Propriedade para acessar as tags isoladas por contexto
        public static ConcurrentDictionary<int, string> TagNames => ThreadLocalTagNames.Value;

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
            {
                throw new ArgumentException("Os nomes dos times não podem ser nulos ou vazios.");
            }

            var dynamicTags = new Dictionary<int, string>
            {
                { 18, $"{homeTeam} - Total de Faltas"},
                { 19, $"{awayTeam} - Total de Faltas"},
                { 12, $"{homeTeam} - Total de Chutes no gol 🚀"},
                { 13, $"{awayTeam} - Total de Chutes no gol 🚀"},
                { 14, $"{homeTeam} - Total de Chutes 🚀"},
                { 15, $"{awayTeam} - Total de Chutes 🚀"},
                { 16, $"{homeTeam} - Total de Impedimentos"},
                { 17, $"{awayTeam} - Total de Impedimentos" },
                { 26, $"{homeTeam} - Total de Laterais" },
                { 27, $"{awayTeam} - Total de Laterais" },
                { 29, $"{homeTeam} - Total de Desarmes" },
                { 30, $"{awayTeam} - Total de Desarmes" },
                { 32, $"{homeTeam} - Total de Tiros de Meta" },
                { 33, $"{awayTeam} - Total de Tiros de Meta" },
                { 38, $"{homeTeam} - Bola na trave" },
                { 39, $"{awayTeam} - Bola na trave" },
            };

            foreach (var tag in dynamicTags)
            {
                TagNames.AddOrUpdate(tag.Key, tag.Value, (key, existingValue) => tag.Value);
            }
        }

        // Método para resetar o contexto das tags (opcional, usado em finalizações ou depurações)
        public static void ResetTags()
        {
            ThreadLocalTagNames.Value = new ConcurrentDictionary<int, string>(BaseTagNames);
        }
    }
}
