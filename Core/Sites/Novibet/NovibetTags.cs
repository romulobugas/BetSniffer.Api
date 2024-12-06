using OpenQA.Selenium;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public static class NovibetTags
    {
        // Dicionário de tags fixas que você quer rastrear
        public static readonly Dictionary<int, string> TagNames = new()
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

            // Adicione outras tags fixas aqui conforme necessário
        };

        // Dicionário de nomes de elementos fixos que você quer rastrear
        public static readonly List<string> ElementNames = new()
        {
            ".registerOrLogin_closeButton",
            "app-event-marketview",
            // Adicione outros elementos fixos aqui conforme necessário
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
            // Lista de padrões de tags dinâmicas
            var dynamicTags = new Dictionary<int, string>
            {
                { 18, $"{homeTeam} - Total de Faltas"},
                { 19, $"{awayTeam} - Total de Faltas"},
                { 12, $"{homeTeam} - Total de Chutes no gol 🚀"},
                { 13, $"{awayTeam} - Total de Chutes no gol 🚀"},
                { 14, $"{homeTeam} - Total de Chutes 🚀"},
                { 15, $"{awayTeam} - Total de Chutes 🚀"},
                { 16, $"{homeTeam} - Total de Impedimentos"},
                { 17,  $"{awayTeam} - Total de Impedimentos" },
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
