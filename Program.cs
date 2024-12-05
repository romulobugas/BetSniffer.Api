using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch;
using BetSniffer.Api.Core.Interfaces;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System.Diagnostics;
using BetSniffer.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BetSniffer.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configura Kestrel para escutar em HTTPS apenas
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ListenAnyIP(5001, listenOptions =>
                {
                    listenOptions.UseHttps(); // Configura HTTPS na porta 5001
                });
            });

            // Configura o WebDriver compartilhado como Singleton
            builder.Services.AddSingleton<IWebDriver>(serviceProvider =>
            {
                var options = new ChromeOptions();
                options.AddArgument("--disable-gpu");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--headless"); // Remove se quiser ver o navegador
                return new ChromeDriver(options);
            });

            // Registra o DbContext para o banco de dados
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=True;"));

            // Registra o serviço de repositório genérico
            builder.Services.AddScoped(typeof(IRepositoryService<>), typeof(RepositoryService<>));

            // Registra serviços de scraping usando a interface IScrapingService
            builder.Services.AddScoped<NovibetScraping>();
            builder.Services.AddScoped<ParimatchScraping>();
            builder.Services.AddScoped<TeamService>();

            // Registrar o serviço de roteamento dinâmico para IScrapingService
            builder.Services.AddScoped<Func<string, IScrapingService>>(serviceProvider => siteName =>
            {
                // Obtenha os serviços necessários
                var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
                var teamService = serviceProvider.GetRequiredService<TeamService>();
                var gamesInfoRepository = serviceProvider.GetRequiredService<IRepositoryService<GamesInfo>>();
                var betInfoRepository = serviceProvider.GetRequiredService<IRepositoryService<BetInfo>>();
                var siteRepository = serviceProvider.GetRequiredService<IRepositoryService<Site>>(); // Adicionado
                var driver = serviceProvider.GetRequiredService<IWebDriver>();

                // Retorne a instância do serviço de scraping dependendo do nome do site
                return siteName.ToLower() switch
                {
                    "novibet" => new NovibetScraping(driver, dbContext, teamService, gamesInfoRepository, betInfoRepository),
                    "parimatch" => new ParimatchScraping(driver, dbContext, teamService, gamesInfoRepository, betInfoRepository),
                    _ => throw new ArgumentException($"Serviço de scraping para o site {siteName} não encontrado.")
                };
            });


            // Configurar CORS para liberar tudo
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()  // Permite todas as origens
                          .AllowAnyMethod()  // Permite todos os métodos HTTP (GET, POST, etc.)
                          .AllowAnyHeader(); // Permite todos os cabeçalhos
                });
            });

            // Registra outros serviços
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddControllers();

            var app = builder.Build();

            // Ativar a política de CORS
            app.UseCors("AllowAll");

            // Configura o Swagger para estar disponível em todos os ambientes
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "BetSniffer API V1");
                c.RoutePrefix = string.Empty; // Deixa o Swagger na raiz do aplicativo
            });

            // Abrir automaticamente o navegador no HTTPS ao iniciar
            OpenBrowser("https://localhost:5001");

            app.UseHttpsRedirection();
            app.MapControllers();

            // Garante que o WebDriver seja liberado ao final
            using (var scope = app.Services.CreateScope())
            {
                var driver = scope.ServiceProvider.GetRequiredService<IWebDriver>();
                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    driver.Quit();
                    driver.Dispose();
                });
            }

            app.Run();
        }

        /// <summary>
        /// Abre o navegador na URL especificada.
        /// </summary>
        /// <param name="url">URL a ser aberta no navegador.</param>
        private static void OpenBrowser(string url)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tentar abrir o navegador: {ex.Message}");
            }
        }
    }
}

