using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Sites.Parimatch

{
    public static class ParimatchTags
    {
        public static readonly Dictionary<int, string> TagNames = new()
        {
            { 1, "Escanteios. Total" },
            { 2, "Cartões amarelos. Total" },
            { 3, "Todos os chutes. Total" },
            { 4, "Chutes no gol. Total" },
            { 5, "Faltas. Total" },
            { 6, "Total" },
            { 7, "Impedimentos. Total" },
            { 22, "Defesas. Total" },
        
            // Adicione outras tags fixas com IDs aqui
        };

        // Dicionário de tags fixas que você quer rastrear
        public static readonly List<string> ElementNames = new()
        {
            ".registerOrLogin_closeButton",
            "app-event-marketview",
            // Adicione outras tags fixas aqui conforme necessário
        };

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
            // Dicionário de padrões de tags dinâmicas com IDs fixos
            var dynamicTags = new Dictionary<int, string>
            {
                { 8, $"Escanteios. {homeTeam} total" },
                { 9, $"Escanteios. {awayTeam} total" },
                { 10, $"Cartões amarelos. {homeTeam} total" },
                { 11, $"Cartões amarelos. {awayTeam} total" },
                { 12, $"Chutes no gol. {homeTeam} total" },
                { 13, $"Chutes no gol. {awayTeam} total" },
                { 14, $"Todos os chutes. {homeTeam} total" },
                { 15, $"Todos os chutes. {awayTeam} total" },
                { 16, $"Impedimentos. {homeTeam} total" },
                { 17, $"Impedimentos. {awayTeam} total" },
                { 18, $"Faltas. {homeTeam} total" },
                { 19, $"Faltas. {awayTeam} total" },
                { 20, $"{homeTeam} total" },
                { 21, $"{awayTeam} total" },
                { 23, $"Defesas. {homeTeam} total" },
                { 24, $"Defesas. {awayTeam} total" }
            };

            // Adicionar ao dicionário principal evitando duplicações
            foreach (var tag in dynamicTags)
            {
                if (!TagNames.ContainsKey(tag.Key))
                {
                    TagNames.Add(tag.Key, tag.Value);
                }
            }
        }

    }
}
