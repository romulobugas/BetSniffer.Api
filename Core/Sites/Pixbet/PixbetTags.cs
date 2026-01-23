
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
            { 3, new List<string> { "Total Match Shots O/U" } },
            { 40, new List<string> { "Primeiro Tempo Total de Gols Acima/Abaixo" } },
            { 43, new List<string> { "Escanteio 1ª Tempo O/U" } },
            { 46, new List<string> { "Cartões 1º Tempo Acima/Abaixo" } },
        };

        // Isolamento por contexto de thread
        private static readonly ThreadLocal<Dictionary<int, List<string>>?> ThreadTagNames =
            new(CloneBaseTagNames);

        // Propriedade para acessar as tags isoladas da thread
        public static Dictionary<int, List<string>> TagNames
        {
            get
            {
                var tags = ThreadTagNames.Value;

                if (tags is null)
                {
                    tags = CloneBaseTagNames();
                    ThreadTagNames.Value = tags;
                }

                return tags;
            }
        }

        private static Dictionary<int, List<string>> CloneBaseTagNames() =>
            BaseTagNames.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value));

        // Método para adicionar tags dinâmicas com nomes de times
        public static void AddDynamicTags(string homeTeam, string awayTeam)
        {
            homeTeam ??= string.Empty;
            awayTeam ??= string.Empty;

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
                { 16, new List<string> { $"{homeTeam}: Total de impedimentos Acima/Abaixo" } },
                { 17, new List<string> { $"{awayTeam}: Total de impedimentos Acima/Abaixo" } },
                { 18, new List<string> { $"{homeTeam}: Total de Faltas da Equipe Acima/Abaixo" } },
                { 19, new List<string> { $"{awayTeam}: Total de Faltas da Equipe Acima/Abaixo" } },
                { 14, new List<string> { $"{homeTeam}: Total de Chutes do Time Mais/Menos" } },
                { 15, new List<string> { $"{awayTeam}: Total de Chutes do Time Mais/Menos" } },
                { 41, new List<string> { $"{homeTeam}: Total de Gols da Equipe no 1º Tempo Acima/Abaixo" } },
                { 42, new List<string> { $"{awayTeam}: Total de Gols da Equipe no 1º Tempo Acima/Abaixo" } },
                { 44, new List<string> { $"{homeTeam}: Total de Escanteios da Equipe no 1º Tempo Acima/Abaixo" } },
                { 45, new List<string> { $"{awayTeam}: Total de Escanteios da Equipe no 1º Tempo Acima/Abaixo" } },
            };

            // Adicionar ao dicionário isolado de tags da thread
            var threadTags = TagNames;

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
