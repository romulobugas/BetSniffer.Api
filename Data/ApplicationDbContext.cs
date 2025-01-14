using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Models;

namespace BetSniffer.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<GamesInfo> GamesInfo { get; set; }
        public DbSet<BetInfo> BetInfo { get; set; }
        public DbSet<Site> Site { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<BetArbitrage> BetArbitrage { get; set; }
        public DbSet<ArbitrageResults> ArbitrageResults { get; set; } // Adicionada nova DbSet


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
                entity.HasKey(g => g.GameId);
                entity.Property(g => g.GameDate).HasColumnType("datetime2");
                entity.Property(g => g.URL).HasMaxLength(500).IsRequired(false);
                entity.Property(g => g.Status).HasDefaultValue(0);
                entity.Property(g => g.LastUpdated).HasColumnType("datetime2").IsRequired(false);
                entity.Property(g => g.GameName).HasMaxLength(255).IsRequired(false); // Configuração do GameName

                // Relacionamento com Site
                entity.HasOne(g => g.Site)
                      .WithMany(s => s.GamesInfo)
                      .HasForeignKey(g => g.SiteId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com Team (HomeTeam)
                entity.HasOne(g => g.HomeTeam)
                      .WithMany(t => t.HomeGames)
                      .HasForeignKey(g => g.HomeTeamId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relacionamento com Team (AwayTeam)
                entity.HasOne(g => g.AwayTeam)
                      .WithMany(t => t.AwayGames)
                      .HasForeignKey(g => g.AwayTeamId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configuração da tabela BetInfo
            modelBuilder.Entity<BetInfo>(entity =>
            {
                entity.HasKey(b => b.BetId);
                entity.Property(b => b.BetId)
                      .ValueGeneratedOnAdd(); // Incremento automático
                entity.Property(b => b.TagName).HasMaxLength(100);
                entity.Property(b => b.OverUnder).HasMaxLength(10);
                entity.Property(b => b.BetAmount).HasColumnType("decimal(10, 2)");
                entity.Property(b => b.Multiplier).HasColumnType("decimal(10, 2)");
                entity.Property(b => b.CaptureDate).HasColumnType("datetime");
                entity.Property(b => b.GameDate).HasColumnType("datetime");

                // Relacionamento com GamesInfo
                entity.HasOne(b => b.GamesInfo)
                      .WithMany(g => g.Bets)  // Relacionamento um para muitos
                      .HasForeignKey(b => b.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Relacionamento com Site
                entity.HasOne(b => b.Site)
                      .WithMany(s => s.BetInfo)
                      .HasForeignKey(b => b.SiteId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuração da tabela Site
            modelBuilder.Entity<Site>(entity =>
            {
                entity.HasKey(s => s.SiteId);
                entity.Property(s => s.Name).HasMaxLength(100);
            });

            // Configuração da tabela Team
            modelBuilder.Entity<Team>(entity =>
            {
                entity.HasKey(t => t.TeamId);
                entity.Property(t => t.NormalizedName).HasMaxLength(100).IsRequired();
                entity.Property(t => t.Aliases).HasColumnType("nvarchar(max)");

                // Relacionamento com GamesInfo
                entity.HasMany(t => t.HomeGames)
                      .WithOne(g => g.HomeTeam)
                      .HasForeignKey(g => g.HomeTeamId);

                entity.HasMany(t => t.AwayGames)
                      .WithOne(g => g.AwayTeam)
                      .HasForeignKey(g => g.AwayTeamId);
            });

            // Configuração da tabela BetArbitrage
            modelBuilder.Entity<BetArbitrage>(entity =>
            {
                entity.HasKey(b => b.Id);
                entity.Property(b => b.Game).HasMaxLength(100);
                entity.Property(b => b.TagName).HasMaxLength(100);
                entity.Property(b => b.BetMoreThan).HasMaxLength(50);
                entity.Property(b => b.BetLessThan).HasMaxLength(50);
                entity.Property(b => b.BetMoreThanMultiplier).HasColumnType("decimal(10, 2)");
                entity.Property(b => b.BetLessThanMultiplier).HasColumnType("decimal(10, 2)");
                entity.Property(b => b.CaptureDate).HasColumnType("datetime");
                entity.Property(b => b.GameDate).HasColumnType("datetime");
                entity.Property(b => b.ArbitragePercentage).HasColumnType("decimal(10, 2)");
            });

            modelBuilder.Entity<ArbitrageResults>(entity =>
            {
                entity.HasKey(a => new { a.BetIdX, a.BetIdY }); // Chave composta

                entity.Property(a => a.ValorLucro)
                      .HasColumnName("Valor_Lucro")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.ArbitrageLucroPercent)
                      .HasColumnName("Arbitrage_Lucro_Percent")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.TagNameX)
                      .HasColumnName("TagName_X")
                      .HasMaxLength(255);

                entity.Property(a => a.OverUnderX)
                      .HasColumnName("OverUnder_X")
                      .HasMaxLength(50);

                entity.Property(a => a.BetAmountX)
                      .HasColumnName("BetAmount_X")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.MultiplierX)
                      .HasColumnName("Multiplier_X")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.HomeTeam)
                      .HasColumnName("HomeTeam")
                      .HasMaxLength(255);

                entity.Property(a => a.SiteNameX)
                      .HasColumnName("SiteName_X")
                      .HasMaxLength(255);

                entity.Property(a => a.SiteNameY)
                      .HasColumnName("SiteName_Y")
                      .HasMaxLength(255);

                entity.Property(a => a.AwayTeam)
                      .HasColumnName("AwayTeam")
                      .HasMaxLength(255);

                entity.Property(a => a.OverUnderY)
                      .HasColumnName("OverUnder_Y")
                      .HasMaxLength(50);

                entity.Property(a => a.BetAmountY)
                      .HasColumnName("BetAmount_Y")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.MultiplierY)
                      .HasColumnName("Multiplier_Y")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.TagNameY)
                      .HasColumnName("TagName_Y")
                      .HasMaxLength(255);

                entity.Property(a => a.SiteIdX)
                      .HasColumnName("SiteId_X");

                entity.Property(a => a.GameDateX)
                      .HasColumnName("GameDate_X")
                      .HasColumnType("datetime");

                entity.Property(a => a.BetIdX)
                      .HasColumnName("BetId_X");

                entity.Property(a => a.BetIdY)
                      .HasColumnName("BetId_Y");

                entity.Property(a => a.SiteIdY)
                      .HasColumnName("SiteId_Y");

                entity.Property(a => a.StakeX)
                      .HasColumnName("Stake_X")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.StakeY)
                      .HasColumnName("Stake_Y")
                      .HasColumnType("decimal(18, 2)");

                entity.Property(a => a.LeagueX)
                      .HasColumnName("League_X")
                      .HasMaxLength(255);

                entity.Property(a => a.LeagueY)
                      .HasColumnName("League_Y")
                      .HasMaxLength(255);

                entity.Property(a => a.GameX)
                      .HasColumnName("Game_X")
                      .HasMaxLength(255);

                entity.Property(a => a.GameY)
                      .HasColumnName("Game_Y")
                      .HasMaxLength(255);
                entity.Property(a => a.URLX)
                      .HasColumnName("Url_X")
                      .HasMaxLength(255);

                entity.Property(a => a.URLY)
                      .HasColumnName("Url_Y")
                      .HasMaxLength(255);
            });
        }
    }
}
