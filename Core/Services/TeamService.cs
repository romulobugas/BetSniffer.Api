using BetSniffer.Api.Data;
using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BetSniffer.Api.Core.Services
{
    public class TeamService
    {
        private readonly ApplicationDbContext _context;

        public TeamService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Verifica se um time já existe no banco. Caso contrário, cadastra um novo.
        /// Caso o time já exista, verifica se o alias existe. Se não, adiciona.
        /// </summary>
        /// <param name="teamName">Nome do time a ser verificado.</param>
        /// <returns>O ID do time encontrado ou cadastrado.</returns>
        public int EnsureTeamExists(string teamName)
        {
            // Normaliza o nome do time para consistência, mas vamos garantir que
            // o nome do time exato seja utilizado na verificação e no cadastro
            var normalizedTeamName = NormalizeTeamName(teamName);

            // Procura o time no banco pelos aliases ou pelo nome normalizado
            var team = _context.Teams
                .FirstOrDefault(t => t.NormalizedName == normalizedTeamName
                    || (t.Aliases != null && t.Aliases.Contains(teamName)));

            if (team != null)
            {
                // Time encontrado, agora verifica se o alias já está presente
                AddAliasIfNotExists(team, teamName);
                return team.TeamId; // Retorna o ID do time encontrado
            }

            // Se o time não for encontrado, cria um novo time
            team = new Team
            {
                NormalizedName = normalizedTeamName,
                Aliases = teamName // Armazena o alias diretamente como string
            };

            _context.Teams.Add(team);
            _context.SaveChanges();

            return team.TeamId; // Retorna o ID do novo time
        }

        /// <summary>
        /// Adiciona um alias ao time, se não existir.
        /// </summary>
        /// <param name="team">O time ao qual o alias será adicionado.</param>
        /// <param name="alias">Alias a ser adicionado.</param>
        /// <returns>Task</returns>
        private void AddAliasIfNotExists(Team team, string alias)
        {
            if (team.Aliases == null)
            {
                team.Aliases = alias; // Se o campo for null, inicializa com o alias
            }
            else if (!team.Aliases.Contains(alias))
            {
                team.Aliases += ";" + alias; // Adiciona o novo alias, separando por vírgula
            }

            _context.Teams.Update(team);
            _context.SaveChanges();
        }

        /// <summary>
        /// Normaliza o nome do time para evitar duplicatas.
        /// </summary>
        /// <param name="teamName">Nome do time a ser normalizado.</param>
        /// <returns>Nome normalizado.</returns>
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
}
