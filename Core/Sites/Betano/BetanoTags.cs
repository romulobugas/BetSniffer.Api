using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Sites.Betano
{
    public static class BetanoTags
    {
        // ThreadLocal para isolar o TagNames por execução
        private static readonly ThreadLocal<Dictionary<int, List<string>>> ThreadTagNames =
            new(() => new Dictionary<int, List<string>>(BaseTagNames));

        // Dicionário base de tags fixas que você quer rastrear
        private static readonly Dictionary<int, List<string>> BaseTagNames = new()
        {
            { 6, new List<string> { "Total de Gols Mais/Menos", "Total de Gols Mais/Menos (alternativas)" } },
            { 1, new List<string> { "Escanteios Mais/Menos", "Escanteios Mais/Menos (alternativas)" } },
            { 34, new List<string> { "Total de Cartões Mais/Menos", "Total de Cartões Mais/Menos (alternativas)" } },
            { 3, new List<string> { "Total de chutes", "Total de chutes (alternativas)" } },
            { 4, new List<string> { "Chutes a gol", "Chutes a gol (alternativas)" } },
            { 7, new List<string> { "Total de Impedimentos", "Total de Impedimentos (alternativas)" } },
            { 5, new List<string> { "Total de Faltas", "Total de Faltas (alternativas)" } },
            { 25, new List<string> { "Total de laterais", "Total de laterais (alternativas)" } },
            { 28, new List<string> { "Total de Desarmes", "Total de Desarmes (alternativas)" } },
            { 37, new List<string> { "Bola na trave", "Bola na trave (alternativas)" } },
            { 40, new List<string> { "Total de gols Mais/Menos - 1° Tempo", "Total de gols Mais/Menos - 1° Tempo (alternativas)" } },
            { 43, new List<string> { "Mais/Menos 1.º Tempo Escanteios (alternativas)", "Mais/Menos 1.º Tempo Escanteios" } },
            { 46, new List<string> { "Total de Cartões (Mais/Menos) 1° Tempo", "Total de Cartões (Mais/Menos) 1° Tempo (alternativas)" } },

            { 65, new List<string> { "Total de gols Mais/Menos - 2º Tempo" } },

        };

        // Método para obter as tags isoladas por thread
        public static Dictionary<int, List<string>> GetThreadTagNames()
        {
            return ThreadTagNames.Value!;
        }

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
            // Obter o dicionário isolado da thread atual
            var tagNames = GetThreadTagNames();

            // Lista de padrões de tags dinâmicas
            var dynamicTags = new Dictionary<int, List<string>>
            {
                { 8, new List<string> { $"{homeTeam} Escanteios Mais/Menos", $"{homeTeam} Escanteios Mais/Menos (alternativas)" } },
                { 9, new List<string> { $"{awayTeam} Escanteios Mais/Menos", $"{awayTeam} Escanteios Mais/Menos (alternativas)" } },
                { 14, new List<string> { $"{homeTeam} Total de chutes", $"{homeTeam} Total de chutes (alternativas)" } },
                { 15, new List<string> { $"{awayTeam} Total de chutes", $"{awayTeam} Total de chutes (alternativas)" } },
                { 12, new List<string> { $"{homeTeam} Chutes a gol", $"{homeTeam} Chutes a gol (alternativas)" } },
                { 13, new List<string> { $"{awayTeam} Chutes a gol", $"{awayTeam} Chutes a gol (alternativas)" } },
                { 16, new List<string> { $"{homeTeam} Total de Impedimentos", $"{homeTeam} Total de Impedimentos (alternativas)" } },
                { 17, new List<string> { $"{awayTeam} Total de Impedimentos", $"{awayTeam} Total de Impedimentos (alternativas)" } },
                { 18, new List<string> { $"{homeTeam} Total de Faltas", $"{homeTeam} Total de Faltas (alternativas)" } },
                { 19, new List<string> { $"{awayTeam} Total de Faltas", $"{awayTeam} Total de Faltas (alternativas)" } },
                { 35, new List<string> { $"{homeTeam} Total de Cartões Acima/Abaixo", $"{homeTeam} Total de Cartões Acima/Abaixo (alternativas)" } },
                { 36, new List<string> { $"{awayTeam} Total de Cartões Acima/Abaixo", $"{awayTeam} Total de Cartões Acima/Abaixo (alternativas)" } },
                { 20, new List<string> { $"{homeTeam} - Total de Gols Mais/Menos", $"{homeTeam} - Total de Gols Mais/Menos (alternativas)" } },
                { 21, new List<string> { $"{awayTeam} - Total de Gols Mais/Menos", $"{awayTeam} - Total de Gols Mais/Menos (alternativas)" } },
                { 26, new List<string> { $"{homeTeam} Total de laterais", $"{homeTeam} Total de laterais (alternativas)" } },
                { 27, new List<string> { $"{awayTeam} Total de laterais", $"{awayTeam} Total de laterais (alternativas)" } },
                { 29, new List<string> { $"{homeTeam} Total de Desarmes", $"{homeTeam} Total de Desarmes (alternativas)" } },
                { 30, new List<string> { $"{awayTeam} Total de Desarmes", $"{awayTeam} Total de Desarmes (alternativas)" } },
                { 41, new List<string> { $"Total de gols Mais/Menos - 1° Tempo {homeTeam}", $"Total de gols Mais/Menos - 1° Tempo {homeTeam} (alternativas)" } },
                { 42, new List<string> { $"Total de gols Mais/Menos - 1° Tempo {awayTeam}", $"Total de gols Mais/Menos - 1° Tempo {awayTeam} (alternativas)" } },
                { 44, new List<string> { $"Primeiro Tempo {homeTeam} Escanteios Mais/Menos", $"Primeiro Tempo {homeTeam} Escanteios Mais/Menos (alternativas)" } },
                { 45, new List<string> { $"Primeiro Tempo {awayTeam} Escanteios Mais/Menos", $"Primeiro Tempo {awayTeam} Escanteios Mais/Menos (alternativas)" } },
                { 47, new List<string> { $"{homeTeam} Total de Cartões 1° Tempo", $"{homeTeam} Total de Cartões 1° Tempo (alternativas)" } },
                { 48, new List<string> { $"{awayTeam} Total de Cartões 1° Tempo", $"{awayTeam} Total de Cartões 1° Tempo (alternativas)" } },
                { 66, new List<string> { $"Segundo Tempo - Total de Gols Mais/Menos {homeTeam}", $"Segundo Tempo - Total de Gols Mais/Menos {homeTeam} (alternativas)" } },
                { 67, new List<string> { $"Segundo Tempo - Total de Gols Mais/Menos {awayTeam}", $"Segundo Tempo - Total de Gols Mais/Menos {awayTeam} (alternativas)" } },
            };

            // Adicionar ao dicionário isolado, evitando duplicações
            foreach (var tag in dynamicTags)
            {
                if (!tagNames.TryGetValue(tag.Key, out var existingTags))
                {
                    // Adiciona a chave e a lista completa de tags
                    tagNames[tag.Key] = new List<string>(tag.Value);
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
