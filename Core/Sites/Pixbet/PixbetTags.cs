using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Sites.Bet365

{
    public static class PixbetTags
    {
        // Dicionário de tags fixas que você quer rastrear
        public static readonly List<string> TagNames = new()
        {
            "Escanteios. Total",
            "Cartões amarelos. Total",
            "Todos os chutes. Total",
            "Chutes no gol. Total",
            "Faltas. Total",
            "Total",
            "Impedimentos. Total",

            // Adicione outras tags fixas aqui conforme necessário
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
            string classAttribute = element.GetDomAttribute("class");
            string elementCode = classAttribute.Split('-').Last(); // Supondo que o código seja o último segmento da classe
            return elementCode;
        }

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            // Lista de padrões de tags dinâmicas
            var dynamicTags = new List<string>
            {
                $"Escanteios. {homeTeam} total",
                $"Escanteios. {awayTeam} total",
                $"Cartões amarelos. {homeTeam} total",
                $"Cartões amarelos. {awayTeam} total",
                $"Chutes no gol. {homeTeam} total",
                $"Chutes no gol. {awayTeam} total",
                $"Todos os chutes. {homeTeam} total",
                $"Todos os chutes. {awayTeam} total",
                $"Impedimentos. {homeTeam} total",
                $"Impedimentos. {awayTeam} total",
                $"Faltas. {homeTeam} total",
                $"Faltas. {awayTeam} total",
                $"{homeTeam} total",
                $"{awayTeam} total",
                $"Todos os chutes. {homeTeam} total",
                $"Todos os chutes. {awayTeam} total",
            };

            // Adicionar ao dicionário principal evitando duplicações
            foreach (var tag in dynamicTags)
            {
                if (!TagNames.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    TagNames.Add(tag);
                }
            }
        }
    }
}
