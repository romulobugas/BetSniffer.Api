using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Sites.Novibet;
using BetSniffer.Api.Core.Sites.Vbet;
using BetSniffer.Api.Core.Sites.Betano;
using BetSniffer.Api.Core.Interfaces;
using BetSniffer.Api.Utils;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configuração de Log
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configurações do appsettings
builder.Services.Configure<BetSniffer.Api.Configuration.ScrapingSettings>(builder.Configuration.GetSection("ScrapingSettings"));

// Dependências
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ApplicationDbContextFactory>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped(typeof(IRepositoryService<>), typeof(RepositoryService<>));

// Registro dos serviços de Scraping concretos
builder.Services.AddScoped<BetanoScraping>();
builder.Services.AddScoped<NovibetScraping>();
builder.Services.AddScoped<VbetScraping>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BetSniffer API", Version = "v1" });
});

var app = builder.Build();

// Inicialização de Banco de Dados (Auto-Restore)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DatabaseInitializer.InitializeAsync(app.Services, builder.Configuration, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BetSniffer API v1"));
}

app.UseHttpsRedirection();

// Configuração de Arquivos Estáticos (Frontend)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "Frontend")),
    RequestPath = "",
    ServeUnknownFileTypes = true,
    DefaultContentType = "text/html"
});

app.UseRouting();
app.UseAuthorization();

// Redireciona a raiz para a página principal do Frontend
app.MapGet("/", (HttpContext context) =>
{
    context.Response.Redirect("/Pages/Index.html");
    return Task.CompletedTask;
});

app.MapControllers();

app.Run();