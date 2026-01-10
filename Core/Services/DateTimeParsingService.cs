using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BetSniffer.Api.Core.Interfaces;

namespace BetSniffer.Api.Core.Services
{
    /// <summary>
    /// Serviço centralizado para parsing de datas e horas.
    /// Consolida lógica de parsing de múltiplos sites de apostas.
    /// </summary>
    public class DateTimeParsingService
    {
        private readonly ILogService _logService;

        public DateTimeParsingService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Faz parsing de data/hora de forma assincronizada (wrapper assíncrono).
        /// </summary>
        public async Task<DateTime> ParseAsync(string dateText, DateParsingStrategy strategy)
        {
            return await Task.Run(() => Parse(dateText, strategy));
        }

        /// <summary>
        /// Faz parsing de data/hora sincronizado (lógica real).
        /// </summary>
        public DateTime Parse(string dateText, DateParsingStrategy strategy)
        {
            if (string.IsNullOrWhiteSpace(dateText))
                throw new ArgumentException("Texto de data/hora não pode estar vazio.", nameof(dateText));

            try
            {
                return strategy switch
                {
                    DateParsingStrategy.Betfair => ParseBetfairDateTime(dateText),
                    DateParsingStrategy.Superbet => ParseSuperbetDateTime(dateText),
                    DateParsingStrategy.Betnacional => ParseBetnacionalDateTime(dateText),
                    DateParsingStrategy.Pixbet => ParsePixbetDateTime(dateText),
                    DateParsingStrategy.KTO => ParseKTODate(dateText),
                    DateParsingStrategy.Novibet => ParseNovibetDateTime(dateText),
                    _ => throw new ArgumentException($"Estratégia de parsing não suportada: {strategy}")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao fazer parsing de data: '{dateText}' - {ex.Message}");
                _logService.LogError($"Erro ao fazer parsing de data com estratégia {strategy}", ex);
                throw;
            }
        }

        /// <summary>
        /// Parsing para datas do Betfair.
        /// Formatos: "Thu Jan 30 2025 21:30:00 GMT-0300" ou "Hoje, 21:30"
        /// </summary>
        private DateTime ParseBetfairDateTime(string dateTimeText)
        {
            var now = DateTime.Now;
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var currentYear = DateTime.Now.Year;

            // Verifica se o formato contém "Hoje" ou "Amanhã"
            if (dateTimeText.StartsWith("Hoje", StringComparison.OrdinalIgnoreCase))
            {
                var timePart = dateTimeText.Replace("Hoje", "").Replace(",", "").Trim();

                if (DateTime.TryParseExact(timePart, "HH:mm", culture, DateTimeStyles.None, out var parsedTime))
                {
                    return DateTime.Today.AddHours(parsedTime.Hour).AddMinutes(parsedTime.Minute);
                }
            }
            else if (dateTimeText.StartsWith("Amanhã", StringComparison.OrdinalIgnoreCase))
            {
                var timePart = dateTimeText.Replace("Amanhã", "").Replace(",", "").Trim();
                if (DateTime.TryParseExact(timePart, "HH:mm", culture, DateTimeStyles.None, out var parsedTime))
                {
                    return DateTime.Today.AddDays(1).AddHours(parsedTime.Hour).AddMinutes(parsedTime.Minute);
                }
            }

            // Para formatos como "28 de dez.,17:30"
            var monthMappings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
                { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
            };

            var parts = dateTimeText.Split(new[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 3 && int.TryParse(parts[0], out var day) && monthMappings.TryGetValue(parts[2], out var month))
            {
                var timePart = parts[^1];

                if (DateTime.TryParseExact(timePart, "HH:mm", culture, DateTimeStyles.None, out var parsedTime))
                {
                    var parsedDate = new DateTime(currentYear, month, day, parsedTime.Hour, parsedTime.Minute, 0);

                    if (parsedDate < DateTime.Now)
                    {
                        parsedDate = parsedDate.AddYears(1);
                    }

                    return parsedDate;
                }
            }

            throw new FormatException($"Formato de data Betfair não reconhecido: {dateTimeText}");
        }

        /// <summary>
        /// Parsing para datas do Superbet.
        /// Formatos: "Fri 10. Jan, 16:45" ou "Hoje, 20:00"
        /// </summary>
        private DateTime ParseSuperbetDateTime(string dateTimeText)
        {
            var now = DateTime.Now;

            // Normaliza entradas
            dateTimeText = dateTimeText.Replace("Amanhã, Amanhã", "Amanhã", StringComparison.OrdinalIgnoreCase)
                                       .Replace("Hoje, Hoje", "Hoje", StringComparison.OrdinalIgnoreCase)
                                       .Replace(",", "").Trim();

            // Trata "Hoje" e "Amanhã"
            if (dateTimeText.StartsWith("Hoje", StringComparison.OrdinalIgnoreCase))
            {
                dateTimeText = dateTimeText.Replace("Hoje", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);
            }
            else if (dateTimeText.StartsWith("Amanhã", StringComparison.OrdinalIgnoreCase))
            {
                dateTimeText = dateTimeText.Replace("Amanhã", now.AddDays(1).ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);
            }

            dateTimeText = dateTimeText.Replace(",", "").Trim();

            // Tenta formatos diretos
            var formats = new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" };
            if (DateTime.TryParseExact(dateTimeText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate;
            }

            // Trata "Fri 10. Jan, 16:45"
            var parts = dateTimeText.Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 4)
            {
                var monthMappings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Jan", 1 }, { "Feb", 2 }, { "Mar", 3 }, { "Apr", 4 }, { "May", 5 }, { "Jun", 6 },
                    { "Jul", 7 }, { "Aug", 8 }, { "Sep", 9 }, { "Oct", 10 }, { "Nov", 11 }, { "Dec", 12 }
                };

                if (int.TryParse(parts[1], out var day) && monthMappings.TryGetValue(parts[2], out var month))
                {
                    var timePart = parts[^1];
                    if (TimeSpan.TryParse(timePart, out var parsedTime))
                    {
                        var resultDate = new DateTime(now.Year, month, day, parsedTime.Hours, parsedTime.Minutes, 0);

                        if (resultDate < now)
                        {
                            resultDate = resultDate.AddYears(1);
                        }

                        return resultDate;
                    }
                }
            }

            throw new FormatException($"Formato de data Superbet não reconhecido: {dateTimeText}");
        }

        /// <summary>
        /// Parsing para datas do Betnacional.
        /// Formatos: "Hoje às 20:00" ou "sábado, 29 março às 10:00"
        /// </summary>
        private DateTime ParseBetnacionalDateTime(string text)
        {
            var now = DateTime.Now;
            var culture = CultureInfo.InvariantCulture;

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Texto de data/hora vazio ou nulo.");

            text = text.Trim();

            if (text.StartsWith("Hoje"))
            {
                var hour = text.Replace("Hoje às", "").Trim();
                var dateString = $"{now:dd/MM/yyyy} {hour}";

                if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, DateTimeStyles.None, out var today))
                    return today;
            }
            else if (text.StartsWith("Amanhã"))
            {
                var hour = text.Replace("Amanhã às", "").Trim();
                var dateString = $"{now.AddDays(1):dd/MM/yyyy} {hour}";

                if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, DateTimeStyles.None, out var tomorrow))
                    return tomorrow;
            }
            else
            {
                // Exemplo: "sábado, 29 março às 10:00"
                var regex = new Regex(@"(\d{1,2})\s+([a-zç]+)\s+às\s+(\d{2}:\d{2})", RegexOptions.IgnoreCase);
                var match = regex.Match(text);

                if (match.Success)
                {
                    int day = int.Parse(match.Groups[1].Value);
                    string monthName = match.Groups[2].Value.ToLower();
                    string time = match.Groups[3].Value;

                    var monthMap = new Dictionary<string, int>
                    {
                        { "janeiro", 1 }, { "fevereiro", 2 }, { "março", 3 }, { "abril", 4 },
                        { "maio", 5 }, { "junho", 6 }, { "julho", 7 }, { "agosto", 8 },
                        { "setembro", 9 }, { "outubro", 10 }, { "novembro", 11 }, { "dezembro", 12 }
                    };

                    if (!monthMap.TryGetValue(monthName, out int month))
                        throw new Exception($"Mês inválido: {monthName}");

                    var year = now.Year;
                    var dateString = $"{day:D2}/{month:D2}/{year} {time}";

                    if (DateTime.TryParseExact(dateString, "dd/MM/yyyy HH:mm", culture, DateTimeStyles.None, out var parsedDate))
                        return parsedDate;
                }
            }

            throw new FormatException($"Formato de data Betnacional não reconhecido: {text}");
        }

        /// <summary>
        /// Parsing para datas do Pixbet.
        /// Formato: "14/01, 20:30" ou "qui., 15 de jan., 20:30"
        /// </summary>
        private DateTime ParsePixbetDateTime(string dateTimeText)
        {
            try
            {
                string cleanedDate = Regex.Replace(dateTimeText, @"^[a-zA-ZÀ-ÿ\-]+, ", "").Trim();
                var parts = cleanedDate.Split(",");

                if (parts.Length != 2)
                    throw new Exception("Formato inválido para a data e hora.");

                var datePart = parts[0].Trim();
                if (!DateTime.TryParseExact(datePart, "d/MM", null, DateTimeStyles.None, out var date))
                    throw new Exception("Data inválida.");

                var timePart = parts[1].Trim();
                if (!TimeSpan.TryParse(timePart, out var time))
                    throw new Exception("Hora inválida.");

                var currentYear = DateTime.Now.Year;
                var fullDate = new DateTime(currentYear, date.Month, date.Day, time.Hours, time.Minutes, 0);

                if (fullDate < DateTime.Now)
                    fullDate = fullDate.AddYears(1);

                return fullDate;
            }
            catch (Exception ex)
            {
                throw new FormatException($"Erro ao converter data Pixbet: {dateTimeText}", ex);
            }
        }

        /// <summary>
        /// Parsing para datas do KTO.
        /// Formatos: "sex." ou "14 de abr."
        /// </summary>
        public DateTime ParseKTODate(string dateText)
        {
            var meses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "jan.", 1 }, { "fev.", 2 }, { "mar.", 3 }, { "abr.", 4 },
                { "mai.", 5 }, { "jun.", 6 }, { "jul.", 7 }, { "ago.", 8 },
                { "set.", 9 }, { "out.", 10 }, { "nov.", 11 }, { "dez.", 12 }
            };

            var diasSemana = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
            {
                { "dom.", DayOfWeek.Sunday },
                { "seg.", DayOfWeek.Monday },
                { "ter.", DayOfWeek.Tuesday },
                { "qua.", DayOfWeek.Wednesday },
                { "qui.", DayOfWeek.Thursday },
                { "sex.", DayOfWeek.Friday },
                { "sáb.", DayOfWeek.Saturday },
                { "sab.", DayOfWeek.Saturday }
            };

            dateText = dateText.ToLower().Trim();

            // Caso 1: Dia da semana (ex: "sex.")
            if (diasSemana.TryGetValue(dateText, out DayOfWeek diaSemana))
            {
                var hoje = DateTime.Today;
                int diasParaAdicionar = ((int)diaSemana - (int)hoje.DayOfWeek + 7) % 7;
                return hoje.AddDays(diasParaAdicionar);
            }

            // Caso 2: Formato "14 de abr."
            var partes = dateText.Replace("º", "").Split(" de ");
            if (partes.Length == 2)
            {
                if (int.TryParse(partes[0].Trim(), out int dia))
                {
                    string mesAbrev = partes[1].Trim().TrimEnd('.');
                    if (!meses.TryGetValue(mesAbrev + ".", out int mes))
                        throw new FormatException("Mês não reconhecido");

                    var hoje = DateTime.Today;
                    var data = new DateTime(hoje.Year, mes, dia);

                    if (data < hoje.AddDays(-1))
                        data = data.AddYears(1);

                    return data;
                }
            }

            throw new FormatException($"Formato de data KTO não reconhecido: {dateText}");
        }

        /// <summary>
        /// Parsing para datas do Novibet.
        /// Formatos: "em 15'" ou "15:30" ou "15 de jan 15:30" ou "dom. 15:30"
        /// </summary>
        private DateTime ParseNovibetDateTime(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Texto de data/hora não pode estar vazio.");

            text = text.Trim().ToLower();

            // Caso 1: "em 15'" - evento começando em X minutos
            if (Regex.IsMatch(text, @"em (\d+)'"))
            {
                Match match = Regex.Match(text, @"em (\d+)'");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int minutesToAdd))
                    return DateTime.Now.AddMinutes(minutesToAdd);
            }

            // Caso 2: Apenas hora "15:30"
            if (Regex.IsMatch(text, @"^\d{1,2}:\d{2}$"))
            {
                var today = DateTime.Today;
                if (TimeSpan.TryParse(text, out var gameTime))
                {
                    var dateTime = today.Add(gameTime);
                    if (dateTime <= DateTime.Now)
                        dateTime = dateTime.AddDays(1);
                    return dateTime;
                }
            }

            // Caso 3: "15 de jan 15:30"
            if (Regex.IsMatch(text, @"^\d{1,2} de [a-z]{3} \d{2}:\d{2}$"))
            {
                var meses = new Dictionary<string, int>
                {
                    { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 }, { "mai", 5 }, { "jun", 6 },
                    { "jul", 7 }, { "ago", 8 }, { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 }
                };

                string[] parts = text.Split(' ');
                if (parts.Length >= 4 && int.TryParse(parts[0], out int day) && meses.TryGetValue(parts[2], out int month))
                {
                    var dateTime = new DateTime(DateTime.Today.Year, month, day)
                        .Add(TimeSpan.Parse(parts[3]));
                    if (dateTime <= DateTime.Now)
                        dateTime = dateTime.AddYears(1);
                    return dateTime;
                }
            }

            // Caso 4: "dom. 15:30"
            if (Regex.IsMatch(text, @"^[a-z]{3}\. \d{1,2}:\d{2}$"))
            {
                var diasSemana = new Dictionary<string, int>
                {
                    { "dom", 0 }, { "seg", 1 }, { "ter", 2 }, { "qua", 3 },
                    { "qui", 4 }, { "sex", 5 }, { "sáb", 6 }, { "sab", 6 }
                };

                var parts = text.Split(' ');
                var dayOfWeekAbbr = parts[0].Replace(".", "");
                if (diasSemana.TryGetValue(dayOfWeekAbbr, out int targetDayOfWeek) && TimeSpan.TryParse(parts[1], out var time))
                {
                    var today = DateTime.Today;
                    int todayDayOfWeek = (int)today.DayOfWeek;
                    int daysToAdd = (targetDayOfWeek - todayDayOfWeek + 7) % 7;

                    if (daysToAdd == 0)
                    {
                        var eventTimeToday = today.Date.Add(time);
                        if (eventTimeToday <= DateTime.Now)
                            daysToAdd = 7;
                    }

                    return today.AddDays(daysToAdd).Date.Add(time);
                }
            }

            throw new FormatException($"Formato de data Novibet não reconhecido: {text}");
        }
    }

    /// <summary>
    /// Enum que define a estratégia de parsing a ser utilizada.
    /// </summary>
    public enum DateParsingStrategy
    {
        Betfair,
        Superbet,
        Betnacional,
        Pixbet,
        KTO,
        Novibet
    }
}
