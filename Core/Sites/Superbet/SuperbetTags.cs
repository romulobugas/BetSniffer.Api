using OpenQA.Selenium;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BetSniffer.Api.Core.Sites.Superbet
{
    public static class SuperbetTags
    {
        // Tags fixas base usadas como template
        private static readonly Dictionary<int, List<string>> BaseTagNames = new()
        {
            { 6, new List<string> { "Total de Gols", "Total de Gols do Time" } },            
            { 1, new List<string> { "Total de Escanteios", "Total de Escanteios do Time" } },
            { 34, new List<string> { "Total de Cartões", "Total de Cartões do Time" } },
            { 2, new List<string> { "Total de Cartões Amarelos", "Time: Total de Cartões Amarelos" } },
            { 4, new List<string> { "Total de Chutes no Gol", "Total de Chutes no Gol da Equipe" } },
            { 7, new List<string> { "Total de Impedimentos", "Total de Impedimentos da Equipe" } },
            { 5, new List<string> { "Total de Faltas", "Total de Faltas da Equipe" } },
            { 25, new List<string> { "Total de Arremessos Laterais", "Total de Arremessos Laterais da Equipe" } },
            { 28, new List<string> { "Total de Desarmes", "Total de Desarmes da Equipe" } },
            { 31, new List<string> { "Total de Tiros de Meta", "Total de Tiros de Meta da Equipe" } },
            { 37, new List<string> { "Total de Chutes na Trave", "Time: Total de Acertos na Trave" } },
            { 3, new List<string> { "Finalizações Totais", "Total de Finalizações da Equipe" } },
            { 40, new List<string> { "1º Tempo - Total de Gols", "1º Tempo - Total de Gols do Time" } },
            { 43, new List<string> { "1º Tempo - Total de Escanteios", "1º tempo - Total de Escanteios do Time" } },
            { 46, new List<string> { "1º Tempo - Total de Cartões", "1º tempo - Total de Cartões do Time" } },
            { 49, new List<string> { "1º Tempo - Total de Cartões Amarelos", "1º tempo - Total de Cartões Amarelos do Time" } },
            { 52, new List<string> { "1º tempo - Total de cartões vermelhos" } },
            { 55, new List<string> { "1º Tempo - Total de Chutes no Gol", "1º Tempo - Chutes no Gol Totais da Equipe" } },
            { 58, new List<string> { "1º Tempo - Total de Finalizações", "1º Tempo - Finalizações Totais da Equipe" } },
            { 61, new List<string> { "1º Tempo - Total de Faltas", "1º Tempo - Total de Faltas da Equipe" } },
            { 64, new List<string> { "1º Tempo - Total de Impedimentos" } },
            { 65, new List<string> { "2º Tempo - Total de Gols", "2º Tempo - Total de Gols da Equipe" } },
        };

        // Isolamento por contexto de thread
        private static readonly ThreadLocal<Dictionary<int, List<string>>> ThreadTagNames =
            new(() => BaseTagNames.ToDictionary(entry => entry.Key, entry => new List<string>(entry.Value)));

        // Propriedade para acessar as tags isoladas da thread
        public static Dictionary<int, List<string>> TagNames => ThreadTagNames.Value;

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
            var dynamicTags = new Dictionary<int, List<string>>
            {
                { 20, new List<string> { $"Total de Gols do Time {homeTeam}" } },
                { 21, new List<string> { $"Total de Gols do Time {awayTeam}" } },
                { 8, new List<string> { $"Total de Escanteios do Time {homeTeam}" } },
                { 9, new List<string> { $"Total de Escanteios do Time {awayTeam}" } },
                { 35, new List<string> { $"Total de Cartões do Time {homeTeam}" } },
                { 36, new List<string> { $"Total de Cartões do Time {awayTeam}" } },
                { 10, new List<string> { $"Time: Total de Cartões Amarelos {homeTeam}" } },
                { 11, new List<string> { $"Time: Total de Cartões Amarelos {awayTeam}" } },
                { 12, new List<string> { $"Total de Chutes no Gol da Equipe {homeTeam}" } },
                { 13, new List<string> { $"Total de Chutes no Gol da Equipe {awayTeam}" } },
                { 16, new List<string> { $"Total de Impedimentos da Equipe {homeTeam}" } },
                { 17, new List<string> { $"Total de Impedimentos da Equipe {awayTeam}" } },
                { 18, new List<string> { $"Total de Faltas da Equipe {homeTeam}" } },
                { 19, new List<string> { $"Total de Faltas da Equipe {awayTeam}" } },
                { 26, new List<string> { $"Total de Arremessos Laterais da Equipe {homeTeam}" } },
                { 27, new List<string> { $"Total de Arremessos Laterais da Equipe {awayTeam}" } },
                { 29, new List<string> { $"Total de Desarmes da Equipe {homeTeam}" } },
                { 30, new List<string> { $"Total de Desarmes da Equipe {awayTeam}" } },
                { 32, new List<string> { $"Total de Tiros de Meta da Equipe {homeTeam}" } },
                { 33, new List<string> { $"Total de Tiros de Meta da Equipe {awayTeam}" } },
                { 38, new List<string> { $"Time: Total de Acertos na Trave {homeTeam}" } },
                { 39, new List<string> { $"Time: Total de Acertos na Trave {awayTeam}" } },
                { 14, new List<string> { $"Total de Finalizações da Equipe {homeTeam}" } },
                { 15, new List<string> { $"Total de Finalizações da Equipe {awayTeam}" } },
                { 41, new List<string> { $"1º Tempo - Total de Gols do Time {homeTeam}" } },
                { 42, new List<string> { $"1º Tempo - Total de Gols do Time {awayTeam}" } },
                { 44, new List<string> { $"1º tempo - Total de Escanteios do Time {homeTeam}" } },
                { 45, new List<string> { $"1º tempo - Total de Escanteios do Time {awayTeam}" } },
                { 47, new List<string> { $"1º tempo - Total de Cartões do Time {homeTeam}" } },
                { 48, new List<string> { $"1º tempo - Total de Cartões do Time {awayTeam}" } },
                { 50, new List<string> { $"1º tempo - Total de Cartões Amarelos do Time {homeTeam}" } },
                { 51, new List<string> { $"1º tempo - Total de Cartões Amarelos do Time {awayTeam}" } },
                { 53, new List<string> { $"1º tempo - Total de cartões vermelhos {homeTeam}" } },
                { 54, new List<string> { $"1º tempo - Total de cartões vermelhos {awayTeam}" } },
                { 56, new List<string> { $"1º Tempo - Chutes no Gol Totais da Equipe {homeTeam}" } },
                { 57, new List<string> { $"1º Tempo - Chutes no Gol Totais da Equipe {awayTeam}" } },
                { 59, new List<string> { $"1º Tempo - Finalizações Totais da Equipe {homeTeam}" } },
                { 60, new List<string> { $"1º Tempo - Finalizações Totais da Equipe {awayTeam}" } },
                { 62, new List<string> { $"1º Tempo - Total de Faltas da Equipe {homeTeam}" } },
                { 63, new List<string> { $"1º Tempo - Total de Faltas da Equipe {awayTeam}" } },
                { 62, new List<string> { $"1º Tempo - {homeTeam} Impedimentos" } },
                { 63, new List<string> { $"1º Tempo - {awayTeam} Impedimentos" } },
                { 66, new List<string> { $"2º Tempo - Total de Gols da Equipe {homeTeam}" } },
                { 67, new List<string> { $"2º Tempo - Total de Gols da Equipe {awayTeam}" } },
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
