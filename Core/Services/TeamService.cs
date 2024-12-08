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
        private List<AliasInfo> _aliasCache;
        private const double MATCH_THRESHOLD = 1;
        private const int MIN_NAME_LENGTH = 7; // Novo: comprimento mínimo para considerar uma correspondência

        public TeamService(ApplicationDbContext context)
        {
            _context = context;
            RefreshAliasCache();
        }

        private void RefreshAliasCache()
        {
            var teams = _context.Teams
                .AsNoTracking()
                .Select(t => new { t.TeamId, t.NormalizedName, t.Aliases })
                .ToList();

            _aliasCache = new List<AliasInfo>();

            foreach (var team in teams)
            {
                var aliases = team.Aliases.Split(';');
                foreach (var alias in aliases)
                {
                    _aliasCache.Add(new AliasInfo
                    {
                        TeamId = team.TeamId,
                        Alias = alias.Trim(),
                        NormalizedAlias = NormalizeTeamName(alias.Trim()),
                        OriginalName = team.NormalizedName
                    });
                }
                _aliasCache.Add(new AliasInfo
                {
                    TeamId = team.TeamId,
                    Alias = team.NormalizedName,
                    NormalizedAlias = NormalizeTeamName(team.NormalizedName),
                    OriginalName = team.NormalizedName
                });
            }
        }

        public int EnsureTeamExists(string teamName, DateTime gameDate, string rivalTeamName)
        {
            var matchingTeam = FindTeamByGameAndRival(teamName, gameDate, rivalTeamName);

            if (matchingTeam != null)
            {
                AddAliasIfNotExists(matchingTeam.TeamId, teamName);
                return matchingTeam.TeamId;
            }

            var matchResult = FindBestMatch(teamName);

            if (matchResult.TeamId.HasValue)
            {
                if (matchResult.Score >= MATCH_THRESHOLD)
                {
                    AddAliasIfNotExists(matchResult.TeamId.Value, teamName);
                    return matchResult.TeamId.Value;
                }
                else
                {
                    Console.WriteLine($"Correspondência ambígua encontrada para '{teamName}'. Score: {matchResult.Score}");
                }
            }

            var newTeam = new Team
            {
                NormalizedName = NormalizeTeamName(teamName),
                Aliases = teamName
            };

            _context.Teams.Add(newTeam);
            _context.SaveChanges();

            _aliasCache.Add(new AliasInfo { TeamId = newTeam.TeamId, Alias = teamName, NormalizedAlias = NormalizeTeamName(teamName), OriginalName = teamName });

            return newTeam.TeamId;
        }

        private Team FindTeamByGameAndRival(string teamName, DateTime gameDate, string rivalTeamName)
        {
            var possibleTeamIds = _aliasCache
                .Where(a => IsGoodMatch(a.Alias, teamName))
                .Select(a => a.TeamId)
                .Distinct()
                .ToList();

            var possibleRivalIds = _aliasCache
                .Where(a => IsGoodMatch(a.Alias, rivalTeamName))
                .Select(a => a.TeamId)
                .Distinct()
                .ToList();

            var game = _context.GamesInfo
                .Include(g => g.HomeTeam)
                .Include(g => g.AwayTeam)
                .Where(g => g.GameDate.Date == gameDate.Date)
                .FirstOrDefault(g =>
                    (possibleTeamIds.Contains(g.HomeTeam.TeamId) && possibleRivalIds.Contains(g.AwayTeam.TeamId)) ||
                    (possibleTeamIds.Contains(g.AwayTeam.TeamId) && possibleRivalIds.Contains(g.HomeTeam.TeamId)));

            if (game != null)
            {
                return possibleTeamIds.Contains(game.HomeTeam.TeamId) ? game.HomeTeam : game.AwayTeam;
            }

            return null;
        }

        private (int? TeamId, double Score) FindBestMatch(string teamName)
        {
            var bestMatch = _aliasCache
                .Select(a => new
                {
                    a.TeamId,
                    Score = CalculateSimilarity(a.Alias, teamName)
                })
                .Where(m => m.Score >= MATCH_THRESHOLD) // Novo: filtra apenas correspondências acima do limiar
                .OrderByDescending(m => m.Score)
                .FirstOrDefault();

            return (bestMatch?.TeamId, bestMatch?.Score ?? 0);
        }

        private bool IsGoodMatch(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return false;

            if (s1.Length < MIN_NAME_LENGTH || s2.Length < MIN_NAME_LENGTH)
                return false;

            var similarity = CalculateSimilarity(s1, s2);
            return similarity >= MATCH_THRESHOLD;
        }

        private double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
                return 0;

            s1 = s1.ToLower();
            s2 = s2.ToLower();

            // Novo: verifica se uma string contém a outra
            if (s1.Contains(s2) || s2.Contains(s1))
                return 1.0;

            var set1 = new HashSet<char>(s1);
            var set2 = new HashSet<char>(s2);

            var intersection = set1.Intersect(set2).Count();
            var union = set1.Union(set2).Count();

            return (double)intersection / union;
        }

        private void AddAliasIfNotExists(int teamId, string alias)
        {
            if (!_aliasCache.Any(a => a.TeamId == teamId && a.Alias == alias))
            {
                var team = _context.Teams.Find(teamId);
                if (team != null)
                {
                    team.Aliases += ";" + alias;
                    _context.Teams.Update(team);
                    _context.SaveChanges();

                    _aliasCache.Add(new AliasInfo { TeamId = teamId, Alias = alias, NormalizedAlias = NormalizeTeamName(alias), OriginalName = team.NormalizedName });
                }
            }
        }

        private string NormalizeTeamName(string teamName)
        {
            return teamName
                .ToLower()
                .Replace("fc", "")
                .Replace(" ", "")
                .Replace(".", "")
                .Trim();
        }
    }

    public class AliasInfo
    {
        public int TeamId { get; set; }
        public string Alias { get; set; }
        public string NormalizedAlias { get; set; }
        public string OriginalName { get; set; }
    }
}

