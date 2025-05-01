using System.Collections.Concurrent;
using System.Threading;

namespace BetSniffer.Api.Core.Sites.Bet365
{
    public static class Bet365Tags
    {
        // Dicionário base de tags fixas
        private static readonly Dictionary<int, List<string>> BaseTagNames = new()
        {
            { 1, new List<string> { "Total de Escanteios 🚀" } },
            { 2, new List<string> { "Total de Cartões Amarelos" } },
            { 3, new List<string> { "Total de Chutes 🚀" } },
            { 4, new List<string> { "Total de Chutes no gol 🚀" } },
            { 7, new List<string> { "Total de Impedimentos" } },
            { 5, new List<string> { "Total de Faltas" } },
            { 8, new List<string> { "Casa Total de Escanteios" } },
            { 9, new List<string> { "Visitante Total de Escanteios" } },
            { 10, new List<string> { "Casa Total de Cartões Amarelos" } },
            { 11, new List<string> { "Visitante Total de Cartões Amarelos" } },
            { 6, new List<string> { "Total de Gols 🚀", "Total de Gols (Adicional) 🚀" } },
            { 20, new List<string> { "Casa Total de Gols" } },
            { 21, new List<string> { "Visitante Total de Gols" } },
            { 25, new List<string> { "Total de Laterais" } },
            { 28, new List<string> { "Total de Desarmes" } },
            { 31, new List<string> { "Total de Tiros de Meta" } },
            { 37, new List<string> { "Bola na trave" } },
            { 40, new List<string> { "1° Tempo - Total de Gols 🚀" } },
            { 43, new List<string> { "1° Tempo - Total de Escanteios 🚀" } },
            { 44, new List<string> { "1° Tempo - Casa Total de Escanteios" } },
            { 45, new List<string> { "1° Tempo - Visitante Total de Escanteios" } },
            { 49, new List<string> { "1° Tempo - Total de Cartões Amarelos" } },
            { 52, new List<string> { "1° Tempo - Total de Cartões Vermelhos" } },
            { 67, new List<string> { "2° Tempo - Total de Gols" } }
        };

        private static readonly AsyncLocal<ConcurrentDictionary<int, List<string>>> AsyncLocalTagNames = new AsyncLocal<ConcurrentDictionary<int, List<string>>>();

        // Propriedade para acessar as tags isoladas por contexto
        public static ConcurrentDictionary<int, List<string>> TagNames
        {
            get
            {
                if (AsyncLocalTagNames.Value == null)
                    AsyncLocalTagNames.Value = new ConcurrentDictionary<int, List<string>>(BaseTagNames);

                return AsyncLocalTagNames.Value;
            }
        }

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
            {
                throw new ArgumentException("Os nomes dos times não podem ser nulos ou vazios.");
            }

            var dynamicTags = new Dictionary<int, List<string>>
            {
                { 18, new List<string> { $"{homeTeam} - Total de Faltas" } },
                { 19, new List<string> { $"{awayTeam} - Total de Faltas" } },
                { 12, new List<string> { $"{homeTeam} - Total de Chutes no gol 🚀" } },
                { 13, new List<string> { $"{awayTeam} - Total de Chutes no gol 🚀" } },
                { 14, new List<string> { $"{homeTeam} - Total de Chutes 🚀" } },
                { 15, new List<string> { $"{awayTeam} - Total de Chutes 🚀" } },
                { 16, new List<string> { $"{homeTeam} - Total de Impedimentos" } },
                { 17, new List<string> { $"{awayTeam} - Total de Impedimentos" } },
                { 26, new List<string> { $"{homeTeam} - Total de Laterais" } },
                { 27, new List<string> { $"{awayTeam} - Total de Laterais" } },
                { 29, new List<string> { $"{homeTeam} - Total de Desarmes" } },
                { 30, new List<string> { $"{awayTeam} - Total de Desarmes" } },
                { 32, new List<string> { $"{homeTeam} - Total de Tiros de Meta" } },
                { 33, new List<string> { $"{awayTeam} - Total de Tiros de Meta" } },
                { 38, new List<string> { $"{homeTeam} - Bola na trave" } },
                { 39, new List<string> { $"{awayTeam} - Bola na trave" } },
                { 50, new List<string> { $"1° Tempo - {homeTeam} Total de Cartões Amarelos" } },
                { 51, new List<string> { $"1° Tempo - {awayTeam} Total de Cartões Amarelos" } },
                { 53, new List<string> { $"{homeTeam} Total de Cartões Vermelhos" } },
                { 54, new List<string> { $"{awayTeam} Total de Cartões Vermelhos" } }
            };

            foreach (var tag in dynamicTags)
            {
                TagNames.AddOrUpdate(tag.Key, tag.Value, (key, existingValue) => tag.Value);
            }
        }
    }
}
