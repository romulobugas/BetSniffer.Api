
namespace BetSniffer.Api.Core.Sites.Pixbet
{
    public static class PixbetTags
    {
        // Tags fixas base usadas como template
        private static readonly Dictionary<int, List<string>> BaseTagNames = new()
        {
            { 6, new List<string> { "Total de Gols Acima/Abaixo" } },
            { 1, new List<string> { "Escanteios FT O/U" } },
            { 34, new List<string> { "Acima/Abaixo de Cartões FT" } },
            { 4, new List<string> { "Chutes no alvo Acima/Abaixo" } },
            { 7, new List<string> { "Impedimentos Acima/Abaixo no Tempo Integral" } },
            { 5, new List<string> { "Faltas Totais na Partida" } },
            //{ 25, new List<string> { "Total de Arremessos Laterais", "Total de Arremessos Laterais da Equipe" } },
            //{ 28, new List<string> { "Total de Desarmes", "Total de Desarmes da Equipe" } },
            //{ 31, new List<string> { "Total de Tiros de Meta" } },
        };

        // Isolamento por contexto de thread
        private static readonly ThreadLocal<Dictionary<int, List<string>>> ThreadTagNames =
            new(() => BaseTagNames.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value)));

        // Propriedade para acessar as tags isoladas da thread
        public static Dictionary<int, List<string>> TagNames => ThreadTagNames.Value;

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            // Lista de padrões de tags dinâmicas
            var dynamicTags = new Dictionary<int, List<string>>
            {
                { 20, new List<string> { $"{homeTeam}: Total de Gols da Equipe Acima/Abaixo" } },
                { 21, new List<string> { $"{awayTeam}: Total de Gols da Equipe Acima/Abaixo" } },
                { 8, new List<string> { $"{homeTeam}: Total Escanteios da Equipe O/U" } },
                { 9, new List<string> { $"{awayTeam}: Total Escanteios da Equipe O/U" } },
                { 35, new List<string> { $"{homeTeam}: Total de Cartões da Equipe Acima/Abaixo" } },
                { 36, new List<string> { $"{awayTeam}: Total de Cartões da Equipe Acima/Abaixo" } },
                { 12, new List<string> { $"{homeTeam}: Total de Chutes a Gol da Equipe Acima/Abaixo" } },
                { 13, new List<string> { $"{awayTeam}: Total de Chutes a Gol da Equipe Acima/Abaixo" } },
                //{ 16, new List<string> { $"Total de Impedimentos da Equipe {homeTeam}" } },
                //{ 17, new List<string> { $"Total de Impedimentos da Equipe {awayTeam}" } },
                { 18, new List<string> { $"{homeTeam}: Total de Faltas da Equipe Acima/Abaixo" } },
                { 19, new List<string> { $"{awayTeam}: Total de Faltas da Equipe Acima/Abaixo" } },
                //{ 26, new List<string> { $"Total de Arremessos Laterais da Equipe {homeTeam}" } },
                //{ 27, new List<string> { $"Total de Arremessos Laterais da Equipe {awayTeam}" } },
                //{ 29, new List<string> { $"Total de Desarmes da Equipe {homeTeam}" } },
                //{ 30, new List<string> { $"Total de Desarmes da Equipe {awayTeam}" } },
                //{ 32, new List<string> { $"Total de Tiros de Meta da Equipe {homeTeam}" } },
                //{ 33, new List<string> { $"Total de Tiros de Meta da Equipe {awayTeam}" } },
                { 14, new List<string> { $"{homeTeam}: Total de Chutes do Time Mais/Menos" } },
                { 15, new List<string> { $"{awayTeam}: Total de Chutes do Time Mais/Menos" } },
            };

            // Adicionar ao dicionário isolado de tags da thread
            var threadTags = ThreadTagNames.Value;

            foreach (var tag in dynamicTags)
            {
                if (!threadTags.TryGetValue(tag.Key, out var existingTags))
                {
                    // Adiciona a chave e a lista completa de tags
                    threadTags[tag.Key] = new List<string>(tag.Value);
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
