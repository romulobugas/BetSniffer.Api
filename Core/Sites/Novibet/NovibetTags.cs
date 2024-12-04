using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public static class NovibetTags
    {
        // Dicionário de tags fixas que você quer rastrear
        public static readonly List<string> TagNames = new()
        {
            "Total de Escanteios 🚀" ,
            "Total de Cartões Amarelos",
            "Total de Chutes 🚀",
            "Total de Chutes no gol 🚀",
            "Total de Impedimentos",
            "Total de Faltas",
            "Casa Total de Escanteios",
            "Visitante Total de Escanteios",
            "Casa Total de Cartões Amarelos",
            "Visitante Total de Cartões Amarelos",
            "Total de Gols 🚀",
            "Casa Total de Gols",
            "Visitante Total de Gols",

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
            var dynamicTags = new List<string>
            {
                $"{homeTeam} - Total de Faltas",
                $"{awayTeam} - Total de Faltas",
                $"{homeTeam} - Total de Chutes no gol 🚀",
                $"{awayTeam} - Total de Chutes no gol 🚀",
                $"{homeTeam} - Total de Chutes 🚀",
                $"{awayTeam} - Total de Chutes 🚀",
                $"{homeTeam} - Total de Impedimentos",
                $"{awayTeam} - Total de Impedimentos",
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
