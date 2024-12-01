// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Models;

namespace BetSniffer.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<BetArbitrage> BetArbitrages { get; set; }
        public DbSet<BetInfo> BetInfos { get; set; }
        public DbSet<GameInfo> GameInfos { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BetArbitrage>().HasKey(b => b.Id);
            modelBuilder.Entity<BetInfo>().HasKey(b => b.BetId);
            modelBuilder.Entity<GameInfo>().HasKey(g => g.GameId);

            modelBuilder.Entity<GameInfo>()
                .Property(g => g.GameId)
                .ValueGeneratedOnAdd(); // Configura o incremento automático
            modelBuilder.Entity<BetInfo>()
                .Property(b => b.BetId)
                .ValueGeneratedOnAdd(); // Configura o incremento automático
        }

    }
}
