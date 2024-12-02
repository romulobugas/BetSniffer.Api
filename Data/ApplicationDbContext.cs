using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Models;

namespace BetSniffer.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<GamesInfo> GamesInfo { get; set; } // Nome correto da tabela
        public DbSet<BetInfo> BetInfo { get; set; } // Nome correto da tabela
        public DbSet<Site> Site { get; set; } // Tabela Site

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
                entity.Property(g => g.HomeTeam).HasColumnType("varchar(100)");
                entity.Property(g => g.AwayTeam).HasColumnType("varchar(100)");
                entity.Property(g => g.League).HasColumnType("varchar(100)");

                // Relação com a tabela Site
                entity.HasOne(g => g.Site) // Referência para a tabela Site
                      .WithMany(s => s.GamesInfo) // Coleção de GamesInfo na tabela Site
                      .HasForeignKey(g => g.SiteId) // Chave estrangeira SiteId
                      .OnDelete(DeleteBehavior.Cascade); // Cascata na deleção
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
                entity.Property(b => b.BetAmount).HasColumnType("decimal(10, 2)"); // Corrigido o tipo do BetAmount
                entity.Property(b => b.CaptureDate).HasColumnType("datetime");
                entity.Property(b => b.GameDate).HasColumnType("datetime");

                // Relação com a tabela Site
                entity.HasOne(b => b.Site) // Referência para a tabela Site
                      .WithMany(s => s.BetInfo) // Coleção de BetInfo na tabela Site
                      .HasForeignKey(b => b.SiteId) // Chave estrangeira SiteId
                      .OnDelete(DeleteBehavior.Cascade); // Cascata na deleção

                // Configuração da relação com GamesInfo
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

                // Relação com GamesInfo e BetInfo
                entity.HasMany(s => s.GamesInfo) // Relacionamento com GamesInfo
                      .WithOne(g => g.Site) // Relacionamento inverso
                      .HasForeignKey(g => g.SiteId); // Chave estrangeira SiteId

                entity.HasMany(s => s.BetInfo) // Relacionamento com BetInfo
                      .WithOne(b => b.Site) // Relacionamento inverso
                      .HasForeignKey(b => b.SiteId); // Chave estrangeira SiteId
            });
        }
    }
}
