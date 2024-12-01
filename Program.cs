using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using Microsoft.Extensions.Configuration;

namespace BetSniffer.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configuração para o banco de dados SQL Server (ajuste a string de conexão conforme necessário)
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("Server=WMS;Database=BetArbitrageDB;User ID=sa;Password=mobweb@123")));

            // Adiciona o serviço de scraping
            builder.Services.AddScoped<NovibetScrapingService>();

            // Adiciona o serviço do Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Adiciona os serviços de controllers
            builder.Services.AddControllers();

            var app = builder.Build();

            // Habilita o Swagger na aplicação
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
