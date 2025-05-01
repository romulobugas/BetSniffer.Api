using BetSniffer.Api.Models;
using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Core.Services;
using BetSniffer.Api.Data;
using BetSniffer.Api.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Net.WebSockets;
using System.Text.Json;

namespace BetSniffer.Api.Core.Sites.Bet365
{
    public class Bet365Scraping : IScrapingService
    {
        #region ServiçosInjetados

        private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
        private readonly IRepositoryService<BetInfo> _betInfoRepository;
        private readonly TeamService _teamService;
        private readonly ApplicationDbContext _dbContext;
        private readonly GameService _gameService;
        private readonly ILogService _logService;
        private readonly DeviceService _deviceService;
        private readonly DevToolsService _devToolsService;


        #endregion

        #region VariaveisDeEstado

        private string gameName;
        private string leagueName;
        private string gameDateText;
        private string homeTeam;
        private string awayTeam;
        private GamesInfo gamesInfo;
        private DateTime gameDateTime;

        #endregion

        public Bet365Scraping(ApplicationDbContext dbContext, 
                              IRepositoryService<GamesInfo> gamesInfoRepository, 
                              IRepositoryService<BetInfo> betInfoRepository, 
                              TeamService teamService, 
                              DeviceService deviceService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _gamesInfoRepository = gamesInfoRepository ?? throw new ArgumentNullException(nameof(gamesInfoRepository));
            _betInfoRepository = betInfoRepository ?? throw new ArgumentNullException(nameof(betInfoRepository));
            _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _gameService = new GameService(dbContext);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            _logService = new LogService(configuration);
            _devToolsService = new DevToolsService();
        }

        public List<TagInfo> ScrapeTags(string url, string siteName)
        {
            return ScrapeTagsAsync(url, siteName).GetAwaiter().GetResult();
        }

        private async Task<List<TagInfo>> ScrapeTagsAsync(string url, string siteName)
        {
            Console.WriteLine($"Iniciando scraping para {siteName} com URL: {url}");

            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
            if (string.IsNullOrWhiteSpace(siteName))
                throw new ArgumentException("Nome do site não pode ser nulo ou vazio.", nameof(siteName));

            var allTagInfos = new List<TagInfo>();

            try
            {
                await InitializeScrapingAsync(url);

                // ➡️ Aqui depois entrará: LoadEventPresentationAsync, CaptureGameInfoAsync, etc
                // Ex: await LoadEventPresentationAsync();
                // Ex: await CaptureGameInfoAsync(siteName, url);
                // Ex: await SaveOrUpdateGameAsync(siteName, url);
                // Ex: var tags = await ProcessCategoriesAndMarketsAsync();
                // allTagInfos.AddRange(tags);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro durante o scraping: {ex.Message}");
                throw;
            }

            return allTagInfos;
        }

        private async Task InitializeScrapingAsync(string url)
        {
            Console.WriteLine("Inicializando conexão ADB e navegador...");

            // Conecta ao dispositivo
            _deviceService.ConnectToDevice();

            // Abre o Chrome
            OpenChromeWithDebugging();

            await Task.Delay(5000); // Espera o Chrome carregar

            // Navega para a URL da Bet365
            await NavigateToUrlAsync(url);

            await Task.Delay(8000); // Espera a Bet365 carregar

            Console.WriteLine("Página inicializada e pronta para interação.");
        }

        private void OpenChrome()
        {
            Console.WriteLine("Abrindo o navegador Chrome...");

            // Chrome Android padrão
            _deviceService.OpenApp("com.android.chrome/com.google.android.apps.chrome.Main --es args '--remote-debugging-port=9222'");
        }

        public void OpenChromeWithDebugging()
        {
            Console.WriteLine("Fechando o Chrome...");
            _deviceService.ExecuteShellCommand("am force-stop com.android.chrome");
            Task.Delay(500).Wait();

            Console.WriteLine("Limpando o log do ADB...");
            _deviceService.ExecuteShellCommand("logcat -c");
            Task.Delay(500).Wait();

            Console.WriteLine("Abrindo Chrome com Remote Debugging...");
            _deviceService.OpenApp("com.android.chrome/com.google.android.apps.chrome.Main -a android.intent.action.MAIN -c android.intent.category.LAUNCHER --es args '--remote-debugging-port=9222'");
            Task.Delay(3000).Wait();

            Console.WriteLine("Criando o túnel ADB da porta 9222...");
            _deviceService.CreateAdbForward(9222, "localabstract:chrome_devtools_remote");
            Task.Delay(1000).Wait();
        }

        private async Task NavigateToUrlAsync(string url)
        {
            Console.WriteLine($"Navegando para a URL (colando manualmente): {url}");

            _deviceService.Tap(300, 100); // Clica na barra de endereço

            await Task.Delay(1000);

            _deviceService.InputText(url);

            await Task.Delay(1000);

            _deviceService.ExecuteShellCommand("input keyevent 66"); // Pressiona ENTER

            await Task.Delay(8000); // Aguarda carregar

            await _devToolsService.InitializeAsync();

            // Agora, conecta no DevTools para capturar o DOM real
            using var webSocket = await _devToolsService.ConnectToWebSocketAsync();
            await _devToolsService.SendMessageAsync(webSocket, new { id = 0, method = "Runtime.enable" });
            await _devToolsService.ReceiveMessageByIdAsync(webSocket, 0);
            await _devToolsService.SendMessageAsync(webSocket, new { id = 1, method = "DOM.enable" });
            await _devToolsService.ReceiveMessageByIdAsync(webSocket, 1);

            // Depois use o webSocket no restante
            await ClickTabsAndRerenderAsync();

        }


        private (string esporte, string liga, string homeTeam, string awayTeam) ExtractGameInfoFromHtml(string htmlContent)
        {
            string esporte = "", liga = "", homeTeam = "", awayTeam = "";

            try
            {
                // 🏆 1. Buscar o trecho "Futebol - UEFA Champions League"
                var breadcrumbMatch = Regex.Match(htmlContent, @"<div class=""sph-Breadcrumb ""[^>]*>([^<]+)</div>");
                if (breadcrumbMatch.Success)
                {
                    var breadcrumbText = breadcrumbMatch.Groups[1].Value.Trim();
                    var parts = breadcrumbText.Split(" - ");
                    if (parts.Length == 2)
                    {
                        esporte = parts[0].Trim();
                        liga = parts[1].Trim();
                    }
                }

                // ⚽ 2. Buscar os dois times em ordem
                var teamMatches = Regex.Matches(htmlContent, @"<div class=""sph-FixturePodHeader_TeamName "">([^<]+)</div>");
                if (teamMatches.Count >= 2)
                {
                    homeTeam = teamMatches[0].Groups[1].Value.Trim();
                    awayTeam = teamMatches[1].Groups[1].Value.Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao extrair informações do HTML: {ex.Message}");
            }

            return (esporte, liga, homeTeam, awayTeam);
        }

        // Este método deve ficar no Bet365Scraping.cs, não no DevToolsService.cs
        public async Task ClickTabsAndRerenderAsync()
        {
            await _devToolsService.InitializeAsync();
            using var initialSocket = await _devToolsService.ConnectToWebSocketAsync();

            // Ativa o DOM no socket inicial
            await _devToolsService.SendMessageAsync(initialSocket, new { id = 1, method = "DOM.enable" });
            await _devToolsService.ReceiveMessageByIdAsync(initialSocket, 1);

            await _devToolsService.SendMessageAsync(initialSocket, new { id = 2, method = "DOM.getDocument", @params = new { depth = 2, pierce = true } });
            var docResult = await _devToolsService.ReceiveMessageByIdAsync(initialSocket, 2);

            // ✅ Novo trecho
            using var docJson = JsonDocument.Parse(docResult);
            int rootId = docJson.RootElement.GetProperty("result").GetProperty("root").GetProperty("nodeId").GetInt32();

            await _devToolsService.SendMessageAsync(initialSocket, new
            {
                id = 3,
                method = "DOM.querySelectorAll",
                @params = new { nodeId = rootId, selector = "div.sph-MarketGroupNavBarButton_Content" }
            });
            var tabsResult = await _devToolsService.ReceiveMessageByIdAsync(initialSocket, 3);

            using var tabsJson = JsonDocument.Parse(tabsResult);
            var nodeIds = tabsJson.RootElement.GetProperty("result").GetProperty("nodeIds").EnumerateArray().Select(n => n.GetInt32()).ToList();

            Console.WriteLine($"🔢 Total de abas: {nodeIds.Count}");

            int counter = 1;

            foreach (var nodeId in nodeIds)
            {
                Console.WriteLine($"\n🖱️ Clicando na aba #{counter} (nodeId={nodeId})...");

                using var socketForClick = await _devToolsService.ConnectToWebSocketAsync();
                await _devToolsService.SendMessageAsync(socketForClick, new { id = 10, method = "Runtime.enable" });
                await _devToolsService.ReceiveMessageByIdAsync(socketForClick, 10);
                await _devToolsService.SendMessageAsync(socketForClick, new { id = 11, method = "DOM.enable" });
                await _devToolsService.ReceiveMessageByIdAsync(socketForClick, 11);

                await _devToolsService.SendMessageAsync(socketForClick, new { id = 12, method = "DOM.focus", @params = new { nodeId } });
                await _devToolsService.ReceiveMessageByIdAsync(socketForClick, 12);

                await Task.Delay(150); // delay leve antes do clique

                await _devToolsService.SendMessageAsync(socketForClick, new
                {
                    id = 13,
                    method = "Runtime.evaluate",
                    @params = new
                    {
                        expression = $"document.querySelectorAll('div.sph-MarketGroupNavBarButton_Content')[{counter - 1}].click()",
                        includeCommandLineAPI = true
                    }
                });
                await _devToolsService.ReceiveMessageByIdAsync(socketForClick, 13);

                // Fecha o socket após clique — ele vai se invalidar
                socketForClick.Dispose();

                Console.WriteLine("⏳ Aguardando carregamento após clique...");
                await Task.Delay(3000);

                // Nova conexão para o novo estado DOM
                using var socketAfterClick = await _devToolsService.ConnectToWebSocketAsync();
                await _devToolsService.SendMessageAsync(socketAfterClick, new { id = 14, method = "Runtime.enable" });
                await _devToolsService.ReceiveMessageByIdAsync(socketAfterClick, 14);
                await _devToolsService.SendMessageAsync(socketAfterClick, new { id = 15, method = "DOM.enable" });
                await _devToolsService.ReceiveMessageByIdAsync(socketAfterClick, 15);

                await _devToolsService.SendMessageAsync(socketAfterClick, new { id = 16, method = "DOM.getDocument", @params = new { depth = 1, pierce = true } });
                var newDomResponse = await _devToolsService.ReceiveMessageByIdAsync(socketAfterClick, 16);

                await _devToolsService.SendMessageAsync(socketAfterClick, new
                {
                    id = 17,
                    method = "DOM.getOuterHTML",
                    @params = new { nodeId = rootId }
                });
                var htmlResult = await _devToolsService.ReceiveMessageByIdAsync(socketAfterClick, 17);
                using var htmlJson = JsonDocument.Parse(htmlResult);
                string html = htmlJson.RootElement.GetProperty("result").GetProperty("outerHTML").GetString();

                var (esporte, liga, home, away) = ExtractGameInfoFromHtml(html);
                Console.WriteLine($"📌 Aba #{counter} → Esporte: {esporte}, Liga: {liga}, {home} vs {away}");

                counter++;
            }

        }

    }
}
