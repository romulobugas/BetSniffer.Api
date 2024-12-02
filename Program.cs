using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch; // Adicione outras implementações de scraping
using BetSniffer.Api.Core.Interfaces; // Onde está definida a interface IScrapingService

namespace BetSniffer.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Registra o DbContext para o banco de dados
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=True;"));

            // Registra serviços de scraping usando a interface IScrapingService
            builder.Services.AddScoped<NovibetScraping>(); // Registro específico para Novibet
            builder.Services.AddScoped<ParimatchScraping>(); // Registro específico para Parimatch

            // Registrar o serviço de roteamento dinâmico para IScrapingService
            builder.Services.AddScoped<Func<string, IScrapingService>>(serviceProvider => siteName =>
            {
                return siteName.ToLower() switch
                {
                    "novibet" => serviceProvider.GetRequiredService<NovibetScraping>(),
                    "parimatch" => serviceProvider.GetRequiredService<ParimatchScraping>(),
                    _ => throw new ArgumentException($"Serviço de scraping para o site {siteName} não encontrado.")
                };
            });

            // Registra outros serviços
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddControllers();

            var app = builder.Build();

            // Configura o Swagger
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.MapControllers();

            app.Run();
        }
    }
}
