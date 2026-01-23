using System.Collections.Concurrent;

namespace BetSniffer.Api.Core.Sites.Vbet
{
    public static class VbetTags
    {
        // Dicionário base de tags fixas
        private static readonly Dictionary<int, string> BaseTagNames = new()
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
            { 40, "Total de Gols do 1º tempo" },
            { 41, "Total de Gols do Time 1 no 1ºtempo" },
            { 42, "Total de Gols do Time 2 no 1º tempo" },

        };

        // Isolamento por thread usando ThreadLocal
        private static readonly ThreadLocal<ConcurrentDictionary<int, string>?> ThreadLocalTagNames =
            new(() => CloneBaseTagNames());

        // Propriedade para acessar as tags isoladas por contexto
        public static ConcurrentDictionary<int, string> TagNames
        {
            get
            {
                var tags = ThreadLocalTagNames.Value;

                if (tags is null)
                {
                    tags = CloneBaseTagNames();
                    ThreadLocalTagNames.Value = tags;
                }

                return tags;
            }
        }

        private static ConcurrentDictionary<int, string> CloneBaseTagNames() =>
            new ConcurrentDictionary<int, string>(BaseTagNames);

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
            {
                throw new ArgumentException("Os nomes dos times não podem ser nulos ou vazios.");
            }

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

            foreach (var tag in dynamicTags)
            {
                TagNames.AddOrUpdate(tag.Key, tag.Value, (key, existingValue) => tag.Value);
            }
        }

        // Método para resetar o contexto das tags (opcional, usado em finalizações ou depurações)
        public static void ResetTags()
        {
            ThreadLocalTagNames.Value = CloneBaseTagNames();
        }
    }
}
