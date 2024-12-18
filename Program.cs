using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Parimatch;
using BetSniffer.Api.Core.Sites.Bet365; // Importado Bet365
using BetSniffer.Api.Core.Interfaces;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using System.Diagnostics;
using BetSniffer.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using BetSniffer.Api.Controllers;
using BetSniffer.Api.Core.Sites.Betano;
using BetSniffer.Api.Configuration;
using BetSniffer.Api.Core.Sites.Betfast;

namespace BetSniffer.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Identificar o ambiente atual
            var environment = builder.Environment.EnvironmentName;

            // Configuração do Kestrel para HTTP/HTTPS com base no ambiente
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                if (environment != "Production")
                {
                    serverOptions.ListenAnyIP(5001, listenOptions =>
                    {
                        listenOptions.UseHttps(); // HTTPS para desenvolvimento
                    });
                }
                else
                {
                    serverOptions.ListenAnyIP(5000); // Apenas HTTP em produção
                }
            });

            // Configuração do WebDriver como Singleton
            builder.Services.AddSingleton<IWebDriver>(serviceProvider =>
            {
                var options = new ChromeOptions();
                options.AddArgument("--disable-gpu");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--headless"); // Remova esta linha para depuração visual
                return new ChromeDriver(options);
            });

            // Registro do DbContext
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=True;"
                ));

            // Adiciona ScrapingSettings como uma configuração injetável
            builder.Services.Configure<ScrapingSettings>(builder.Configuration.GetSection("ScrapingSettings"));


            // Registra a fábrica para uso em serviços ou controladores
            builder.Services.AddScoped<ApplicationDbContextFactory>();

            // Registro de repositórios genéricos
            builder.Services.AddScoped(typeof(IRepositoryService<>), typeof(RepositoryService<>));

            // Registro de serviços específicos
            builder.Services.AddScoped<NovibetScraping>();
            builder.Services.AddScoped<ParimatchScraping>();
            builder.Services.AddScoped<BetanoScraping>();
            builder.Services.AddScoped<BetfastScraping>();
            builder.Services.AddScoped<Bet365Scraping>(); // Registro explícito de Bet365Scraping
            builder.Services.AddScoped<TeamService>();
            builder.Services.AddScoped<BatchScrapingController>();
            builder.Services.AddScoped<GamesUpdateController>();


            // Registro do roteamento dinâmico para IScrapingService
            builder.Services.AddScoped<Func<string, IScrapingService>>(serviceProvider => siteName =>
            {
                var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
                var teamService = serviceProvider.GetRequiredService<TeamService>();
                var gamesInfoRepository = serviceProvider.GetRequiredService<IRepositoryService<GamesInfo>>();
                var betInfoRepository = serviceProvider.GetRequiredService<IRepositoryService<BetInfo>>();
                var driver = serviceProvider.GetRequiredService<IWebDriver>();

                // Serviço dinâmico para diferentes sites de scraping
                return siteName.ToLower() switch
                {
                    //"novibet" => new NovibetScraping(driver, dbContext, teamService, gamesInfoRepository, betInfoRepository),
                    //"parimatch" => new Parimatchcraping(driver, dbContext, teamService, gamesInfoRepository, betInfoRepository),
                    "bet365" => new Bet365Scraping(driver, dbContext, teamService, gamesInfoRepository, betInfoRepository),
                    _ => throw new ArgumentException($"Serviço de scraping para o site {siteName} não encontrado.")
                };
            });

            // Configuração de CORS (liberação total)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Registro de serviços básicos
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddControllers();

            var app = builder.Build();

            // Configuração de CORS
            app.UseCors("AllowAll");

            // Configuração de Swagger
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "BetSniffer API V1");
                c.RoutePrefix = string.Empty; // Swagger na raiz
            });

            // Abrir o navegador automaticamente em desenvolvimento
            if (environment != "Production")
            {
                OpenBrowser("https://localhost:5001");
            }

            // Redirecionamento para HTTPS
            app.UseHttpsRedirection();

            // Mapear controladores
            app.MapControllers();

            // Finalização do WebDriver
            using (var scope = app.Services.CreateScope())
            {
                var driver = scope.ServiceProvider.GetRequiredService<IWebDriver>();
                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    driver.Quit();
                    driver.Dispose();
                });
            }

            // Executar o aplicativo
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
