using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Vbet;
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
using BetSniffer.Api.Core.Sites.Superbet;
using BetSniffer.Api.Core.Sites.Betnacional;
using BetSniffer.Api.Core.Sites.KTO;
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

            // Registro do DbContext com timeout de 3 minutos
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection") + ";TrustServerCertificate=True;",
                    sqlOptions => sqlOptions.CommandTimeout(180) // Timeout configurado
                ));

            // Adiciona ScrapingSettings como uma configuração injetável
            builder.Services.Configure<ScrapingSettings>(builder.Configuration.GetSection("ScrapingSettings"));


            // Registra a fábrica para uso em serviços ou controladores
            builder.Services.AddScoped<ApplicationDbContextFactory>();

            // Registro de repositórios genéricos
            builder.Services.AddScoped(typeof(IRepositoryService<>), typeof(RepositoryService<>));

            // Registro de serviços específicos
            builder.Services.AddScoped<NovibetScraping>();
            builder.Services.AddScoped<VbetScraping>();
            builder.Services.AddScoped<BetanoScraping>();
            builder.Services.AddScoped<BetfastScraping>();
            builder.Services.AddScoped<PixbetScraping>();
            builder.Services.AddScoped<SuperbetScraping>();
            builder.Services.AddScoped<BetnacionalScraping>();
            builder.Services.AddScoped<KTOScraping>();
            builder.Services.AddScoped<TeamService>();
            builder.Services.AddScoped<BatchScrapingController>();
            builder.Services.AddScoped<GamesUpdateController>();
            builder.Services.AddScoped<DeviceService>();
            builder.Services.AddScoped<DevToolsService>();


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

            // Configuração para servir arquivos estáticos do front
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                    Path.Combine(builder.Environment.ContentRootPath, "Frontend")),
                RequestPath = "",
                ServeUnknownFileTypes = true, // Permite servir qualquer tipo de arquivo estático
                DefaultContentType = "text/html"
            });

            // Configuração do Swagger
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "BetSniffer API V1");
                c.RoutePrefix = "swagger"; // Swagger acessível em /swagger
            });

            // Redirecionamento da raiz para o front
            app.MapGet("/", async context =>
            {
                context.Response.Redirect("/Pages/Index.html", permanent: false);
                await Task.CompletedTask;
            });

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
