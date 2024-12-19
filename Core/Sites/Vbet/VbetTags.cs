using OpenQA.Selenium;

namespace BetSniffer.Api.Core.Sites.Vbet

{
    public static class VbetTags
    {
        public static readonly Dictionary<int, string> TagNames = new()
        {
            { 1, "Escanteios: Total" },
            { 2, "Cartões Amarelos: Total" },
            { 3, "Chutes: Total" },
            { 4, "Chute ao Gol: Total" },
            { 5, "Faltas: Total" },
            { 6, "Total de gols" },
            { 7, "Impedimentos: Total" },
            { 22, "Defesas de goleiro: Total" },
            { 12, "Chute ao Gol: Total da equipa 1" },
            { 13, "Chute ao Gol: Total da equipa 2" },
            { 20, "Total de Gols do Time da casa" },
            { 21, "Total de Gols do Time visitante" },
            { 23, "Defesas de goleiro: Total da Equipe 1" },
            { 24, "Defesas de goleiro: Total da Equipe 2" },
            { 25, "Lateral : Total" },
            { 26, "Lateral : Equipe 1 Total" },
            { 27, "Lateral : Equipe 2 Total" },
        
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
                { 8, $"Escanteios: {homeTeam} (Total)" },
                { 9, $"Escanteios: {awayTeam} (Total)" },
                { 10, $"Cartões Amarelos: {homeTeam} (Total)" },
                { 11, $"Cartões Amarelos: {awayTeam} (Total)" },                
                { 14, $"Todos os chutes: {homeTeam} (Total)" },
                { 15, $"Todos os chutes: {awayTeam} (Total)" },
                { 16, $"Impedimento: {homeTeam} (Total)" },
                { 17, $"Impedimento: {awayTeam} (Total)" },
                { 18, $"Faltas: {homeTeam} (Total)" },
                { 19, $"Faltas: {awayTeam} (Total)" }
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
