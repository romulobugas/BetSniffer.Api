using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;

namespace BetSniffer.Api.Core.Services
{
    public class TeamService
    {
        private readonly ApplicationDbContext _context;

        public TeamService(ApplicationDbContext context)
        {
            _context = context;
        }

        public int EnsureTeamExists(string teamName)
        {
            // Busca literal pelos aliases no banco de dados
            var aliases = _context.Teams
                .Select(t => new
                {
                    t.TeamId,
                    Aliases = t.Aliases
                })
                .AsEnumerable() // Processar o restante em memória
                .SelectMany(t => t.Aliases.Split(';')
                    .Select(alias => new
                    {
                        t.TeamId,
                        Alias = alias.Trim()
                    }))
                .ToList();

            // Comparação literal (case-insensitive) em memória
            var matchingAlias = aliases.FirstOrDefault(a => a.Alias.Equals(teamName, StringComparison.OrdinalIgnoreCase));

            if (matchingAlias != null)
            {
                // Time encontrado, retorna o ID
                return matchingAlias.TeamId;
            }

            // Caso não exista, cria um novo time
            var newTeam = new Team
            {
                NormalizedName = teamName, // Não normalizamos para manter o nome original
                Aliases = teamName // Inicia o alias com o próprio nome do time
            };

            _context.Teams.Add(newTeam);
            _context.SaveChanges();

            return newTeam.TeamId;
        }
    }
}
