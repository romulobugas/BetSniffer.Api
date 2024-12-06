using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Models;

namespace BetSniffer.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<GamesInfo> GamesInfo { get; set; } // Nome correto da tabela
        public DbSet<BetInfo> BetInfo { get; set; } // Nome correto da tabela
        public DbSet<Site> Site { get; set; } // Tabela Site
        public DbSet<Team> Teams { get; set; } // Tabela Teams

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuração da tabela GamesInfo
            modelBuilder.Entity<GamesInfo>(entity =>
            {
                entity.ToTable("GamesInfo"); // Nome correto da tabela no banco
                entity.HasKey(g => g.GameId); // Chave primária
                entity.Property(g => g.GameId).ValueGeneratedOnAdd(); // Auto-incremento
                entity.Property(g => g.League).HasColumnType("varchar(100)");

                // Substituição de HomeTeam e AwayTeam por IDs
                entity.Property(g => g.HomeTeamId).IsRequired();
                entity.Property(g => g.AwayTeamId).IsRequired();

                // Relação com a tabela Site
                entity.HasOne(g => g.Site) // Referência para a tabela Site
                      .WithMany(s => s.GamesInfo) // Coleção de GamesInfo na tabela Site
                      .HasForeignKey(g => g.SiteId) // Chave estrangeira SiteId
                      .OnDelete(DeleteBehavior.Cascade); // Cascata na deleção

                // Relação com Teams (HomeTeam)
                entity.HasOne(g => g.HomeTeam) // Relacionamento com HomeTeam
                      .WithMany(t => t.HomeGames) // Coleção de HomeGames na tabela Teams
                      .HasForeignKey(g => g.HomeTeamId); // Chave estrangeira HomeTeamId

                // Relação com Teams (AwayTeam)
                entity.HasOne(g => g.AwayTeam) // Relacionamento com AwayTeam
                      .WithMany(t => t.AwayGames) // Coleção de AwayGames na tabela Teams
                      .HasForeignKey(g => g.AwayTeamId); // Chave estrangeira AwayTeamId
                entity.Property(g => g.URL)
                      .HasColumnType("varchar(500)") // Tipo adequado no banco
                      .IsRequired(false); // Permite nulo

            });

            // Configuração da tabela BetInfo
            modelBuilder.Entity<BetInfo>(entity =>
            {
                entity.ToTable("BetInfo"); // Nome correto da tabela no banco
                entity.HasKey(b => b.BetId); // Chave primária
                entity.Property(b => b.BetId).ValueGeneratedOnAdd(); // Auto-incremento
                entity.Property(b => b.TagName).HasColumnType("varchar(100)");
                entity.Property(b => b.OverUnder).HasColumnType("varchar(10)");
                entity.Property(b => b.Multiplier).HasColumnType("decimal(10, 2)");
                entity.Property(b => b.BetAmount).HasColumnType("decimal(10, 2)");
                entity.Property(b => b.CaptureDate).HasColumnType("datetime");
                entity.Property(b => b.GameDate).HasColumnType("datetime");
                entity.Property(b => b.TagId)
                      .IsRequired(false) // Define que a coluna pode ser nula
                      .HasColumnType("int");

                // Relação com a tabela Site
                entity.HasOne(b => b.Site) // Referência para a tabela Site
                      .WithMany(s => s.BetInfo) // Coleção de BetInfo na tabela Site
                      .HasForeignKey(b => b.SiteId) // Chave estrangeira SiteId
                      .OnDelete(DeleteBehavior.Cascade); // Cascata na deleção

                // Relação com GamesInfo
                entity.HasOne(b => b.GamesInfo)
                      .WithMany(g => g.Bets) // Coleção de Bets na tabela GamesInfo
                      .HasForeignKey(b => b.GameId) // Chave estrangeira GameId
                      .OnDelete(DeleteBehavior.Cascade); // Cascata na deleção
            });

            // Configuração da tabela Site
            modelBuilder.Entity<Site>(entity =>
            {
                entity.ToTable("Site"); // Nome correto da tabela no banco
                entity.HasKey(s => s.SiteId); // Chave primária
                entity.Property(s => s.SiteId).ValueGeneratedOnAdd(); // Auto-incremento
                entity.Property(s => s.Name).HasColumnType("varchar(100)");
            });

            // Configuração da tabela Teams
            modelBuilder.Entity<Team>(entity =>
            {
                entity.ToTable("Teams");
                entity.HasKey(t => t.TeamId); // Chave primária
                entity.Property(t => t.TeamId).ValueGeneratedOnAdd(); // Auto-incremento
                entity.Property(t => t.NormalizedName).HasColumnType("varchar(100)").IsRequired(); // Nome normalizado
                entity.Property(t => t.Aliases).HasColumnType("nvarchar(max)").IsRequired(); // Mapeamento correto

                // Relacionamentos
                entity.HasMany(t => t.HomeGames) // Relacionamento HomeGames
                      .WithOne(g => g.HomeTeam) // Relacionamento inverso
                      .HasForeignKey(g => g.HomeTeamId); // Chave estrangeira HomeTeamId

                entity.HasMany(t => t.AwayGames) // Relacionamento AwayGames
                      .WithOne(g => g.AwayTeam) // Relacionamento inverso
                      .HasForeignKey(g => g.AwayTeamId); // Chave estrangeira AwayTeamId
            });
        }
    }
}
