using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

namespace BetSniffer.Api.Core.Services
{
    public class TeamService
    {
        private readonly ApplicationDbContext _context;

        public TeamService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Método auxiliar para normalizar o texto
        public string NormalizeText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var normalized = input
                .Replace('\u00A0', ' ') // Substitui "espaço não separável" por espaço comum
                .Replace('\u200B', ' ') // Remove "zero-width space"
                .Replace('\u200C', ' ') // Remove "zero-width non-joiner"
                .Replace('\u200D', ' ') // Remove "zero-width joiner"
                .Normalize(NormalizationForm.FormC) // Normaliza a composição unicode
                .Trim();

            return Regex.Replace(normalized, @"\s+", " "); // Substitui múltiplos espaços por um único
        }

        public int EnsureTeamExists(string teamName)
        {
            // Normaliza o nome do time recebido
            var normalizedTeamName = NormalizeText(teamName);

            // Busca os aliases no banco de dados e os normaliza
            var aliases = _context.Teams
                .Select(t => new
                {
                    t.TeamId,
                    Aliases = t.Aliases
                })
                .AsEnumerable() // Processar em memória a normalização
                .SelectMany(t => t.Aliases.Split(';')
                    .Select(alias => new
                    {
                        t.TeamId,
                        Alias = NormalizeText(alias.Trim())
                    }))
                .ToList();

            // Comparação normalizada (case-insensitive)
            var matchingAlias = aliases.FirstOrDefault(a => a.Alias.Equals(normalizedTeamName, StringComparison.OrdinalIgnoreCase));

            if (matchingAlias != null)
            {
                // Time encontrado, retorna o ID
                return matchingAlias.TeamId;
            }

            // Caso não exista, cria um novo time com o nome normalizado
            var newTeam = new Team
            {
                NormalizedName = normalizedTeamName,
                Aliases = normalizedTeamName // Inicia os aliases com o nome normalizado
            };

            _context.Teams.Add(newTeam);
            _context.SaveChanges();

            return newTeam.TeamId;
        }

        // ✅ NOVO - Assíncrono
        public async Task<int> EnsureTeamExistsAsync(string teamName)
        {
            var normalizedTeamName = NormalizeText(teamName);

            var teams = await _context.Teams
                .Select(t => new { t.TeamId, t.Aliases })
                .ToListAsync();

            var aliases = teams
                .SelectMany(t => t.Aliases.Split(';')
                    .Select(alias => new { t.TeamId, Alias = NormalizeText(alias.Trim()) }))
                .ToList();

            var matchingAlias = aliases.FirstOrDefault(a => a.Alias.Equals(normalizedTeamName, StringComparison.OrdinalIgnoreCase));

            if (matchingAlias != null)
            {
                return matchingAlias.TeamId;
            }

            var newTeam = new Team
            {
                NormalizedName = normalizedTeamName,
                Aliases = normalizedTeamName
            };

            await _context.Teams.AddAsync(newTeam);
            await _context.SaveChangesAsync();

            return newTeam.TeamId;
        }
    }
}
