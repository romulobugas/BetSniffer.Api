using System.Text.RegularExpressions;


namespace BetSniffer.Api.Core.Services
{
    /// <summary>
    /// Serviço para processar informações de jogos, incluindo data e hora.
    /// </summary>
    public class GameTimeService
    {
        /// <summary>
        /// Processa a string de entrada e converte em um objeto DateTime.
        /// </summary>
        /// <param name="input">Texto contendo informações de data e hora do jogo.</param>
        /// <returns>Um objeto DateTime representando a data e hora do jogo.</returns>
        public DateTime ExtractGameInfo(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("A entrada não pode ser vazia.", nameof(input));

            // Remove espaços extras
            input = input.Trim();

            // Caso: "Hoje, HH:mm" ou "Amanhã, HH:mm"
            if (Regex.IsMatch(input, @"^(hoje|amanhã),\s*\d{1,2}:\d{2}$", RegexOptions.IgnoreCase))
            {
                var parts = input.Split(',', StringSplitOptions.TrimEntries);
                var dayText = parts[0].Trim();
                var timeText = parts[1].Trim();
                var baseDate = dayText.Equals("hoje", StringComparison.OrdinalIgnoreCase)
                    ? DateTime.Today
                    : DateTime.Today.AddDays(1);

                return baseDate.Add(TimeSpan.Parse(timeText));
            }

            // Caso: "1º tempo XX'" ou "2º tempo XX'"
            if (Regex.IsMatch(input, @"^(1º|2º)\s+tempo\s+\d{1,2}'$", RegexOptions.IgnoreCase))
            {
                // Para esses casos, podemos retornar o horário atual
                return DateTime.Now;
            }

            // Caso: "DD de mês, HH:mm"
            if (Regex.IsMatch(input, @"^\d{1,2}\s+de\s+\w+,\s*\d{1,2}:\d{2}$", RegexOptions.IgnoreCase))
            {
                var parts = input.Split(',', StringSplitOptions.TrimEntries);
                var dateText = parts[0].Trim();
                var timeText = parts[1].Trim();

                var baseDate = ParseCustomDate(dateText);
                return baseDate.Add(TimeSpan.Parse(timeText));
            }

            throw new FormatException($"Formato inválido para a entrada: {input}");
        }

        /// <summary>
        /// Converte uma data no formato "DD de mês" para um objeto DateTime.
        /// </summary>
        private DateTime ParseCustomDate(string customDateText)
        {
            var match = Regex.Match(customDateText, @"(\d{1,2})\s+de\s+(\w+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new FormatException($"Formato de data inválido: {customDateText}");

            var day = int.Parse(match.Groups[1].Value);
            var month = MonthNameToNumber(match.Groups[2].Value.ToLower());
            var year = DateTime.Today.Year;

            // Ajusta o ano para o próximo se a data já passou
            if (month < DateTime.Today.Month || (month == DateTime.Today.Month && day < DateTime.Today.Day))
                year++;

            return new DateTime(year, month, day);
        }

        /// <summary>
        /// Converte o nome do mês para um número correspondente.
        /// </summary>
        private int MonthNameToNumber(string monthName)
        {
            var months = new Dictionary<string, int>
            {
                { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
                { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
                { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 }
            };

            if (!months.TryGetValue(monthName, out var month))
                throw new Exception($"Nome do mês inválido: {monthName}");

            return month;
        }
    }
}
