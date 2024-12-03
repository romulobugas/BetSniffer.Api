using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Sites.Novibet
{
    public static class NovibetTags
    {
        // Dicionário de tags fixas que você quer rastrear
        public static readonly List<string> TagNames = new()
        {
            "Total de Escanteios 🚀",
            "Total de Cartões Amarelos",
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
            string classAttribute = element.GetAttribute("class");
            string elementCode = classAttribute.Split('-').Last(); // Supondo que o código seja o último segmento da classe
            return elementCode;
        }
    }
}
