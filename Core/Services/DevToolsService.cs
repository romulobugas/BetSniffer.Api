using BetSniffer.Api.Core.Interfaces;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BetSniffer.Api.Core.Services
{
    public class DevToolsService
    {
        private readonly HttpClient _httpClient;
        private readonly ClientWebSocket _webSocket;
        private Uri _devToolsUri;
        private readonly DeviceService _deviceService;

        public DevToolsService()
        {
            _httpClient = new HttpClient();
            _webSocket = new ClientWebSocket();
            _deviceService = new DeviceService();
        }

        public async Task InitializeAsync()
        {
            const int port = 9222;
            _devToolsUri = new Uri($"http://127.0.0.1:{port}");
            Console.WriteLine($"Conectando no DevTools na porta {port}...");

            int retries = 10;

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(new Uri(_devToolsUri, "json"));
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("✅ DevTools está pronto.");
                        Console.WriteLine(json);
                        return;
                    }
                    else
                    {
                        Console.WriteLine($"🔁 Tentativa {i + 1}/{retries} - Status: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro na tentativa {i + 1}/{retries}: {ex.Message}");
                }

                await Task.Delay(1000);
            }

            throw new Exception("❌ DevTools não respondeu após várias tentativas.");
        }

        public async Task<string> GetPageHtmlAsync()
        {
            if (_devToolsUri == null)
                throw new InvalidOperationException("DevTools não inicializado.");

            var response = await _httpClient.GetStringAsync(new Uri(_devToolsUri, "json"));
            var pages = JsonSerializer.Deserialize<List<DevToolsPage>>(response);

            if (pages == null || pages.Count == 0)
                throw new Exception("Nenhuma página aberta encontrada no DevTools.");

            var page = pages.FirstOrDefault(p => p.url.Contains("bet365"));

            if (page == null)
                throw new Exception("Nenhuma página da Bet365 encontrada.");

            using var webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri(page.webSocketDebuggerUrl), CancellationToken.None);

            // Habilita os domínios Runtime e DOM
            await SendMessageAsync(webSocket, new { id = 0, method = "Runtime.enable" });
            await ReceiveMessageByIdAsync(webSocket, 0);

            await SendMessageAsync(webSocket, new { id = 1, method = "DOM.enable" });
            await ReceiveMessageByIdAsync(webSocket, 1);

            // Solicita o DOM principal
            await SendMessageAsync(webSocket, new
            {
                id = 2,
                method = "DOM.getDocument",
                @params = new { depth = 1, pierce = true }
            });

            var domResponse = await ReceiveMessageByIdAsync(webSocket, 2);
            Console.WriteLine("🔍 DOM Response: ");
            Console.WriteLine(domResponse);

            using var docJson = JsonDocument.Parse(domResponse);

            if (!docJson.RootElement.TryGetProperty("result", out var resultNode) ||
                !resultNode.TryGetProperty("root", out var rootNode) ||
                !rootNode.TryGetProperty("nodeId", out var nodeIdProp))
            {
                throw new Exception("❌ Estrutura de DOM inválida. A resposta não contém 'result.root.nodeId'.");
            }

            int nodeId = nodeIdProp.GetInt32();

            // Solicita o OuterHTML
            await SendMessageAsync(webSocket, new
            {
                id = 3,
                method = "DOM.getOuterHTML",
                @params = new { nodeId }
            });

            var htmlResponse = await ReceiveMessageByIdAsync(webSocket, 3);

            using var htmlJson = JsonDocument.Parse(htmlResponse);
            string outerHtml = htmlJson.RootElement
                .GetProperty("result")
                .GetProperty("outerHTML")
                .GetString();

            return outerHtml;
        }

        public async Task SendMessageAsync(ClientWebSocket socket, object message)
        {
            string json = JsonSerializer.Serialize(message);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task<string> ReceiveMessageByIdAsync(ClientWebSocket socket, int targetId)
        {
            var buffer = new byte[16384];
            var fullMessage = new StringBuilder();

            while (true)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    fullMessage.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                var messageText = fullMessage.ToString();
                fullMessage.Clear();

                try
                {
                    using var doc = JsonDocument.Parse(messageText);
                    if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.GetInt32() == targetId)
                    {
                        Console.WriteLine($"✅ Resposta recebida para id={targetId}");
                        return messageText;
                    }
                    else
                    {
                        Console.WriteLine($"🔄 Ignorando mensagem sem id ou id diferente: {messageText}");
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"⚠️ JSON inválido ignorado: {ex.Message}");
                }
            }
        }

        public async Task<ClientWebSocket> ConnectToWebSocketAsync()
        {
            var response = await _httpClient.GetStringAsync(new Uri(_devToolsUri, "json"));
            var pages = JsonSerializer.Deserialize<List<DevToolsPage>>(response);

            var page = pages.FirstOrDefault(p => p.url.Contains("bet365"));
            if (page == null)
                throw new Exception("Nenhuma página da Bet365 encontrada.");

            var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(page.webSocketDebuggerUrl), CancellationToken.None);
            return socket;
        }

        private class DevToolsPage
        {
            public string id { get; set; }
            public string title { get; set; }
            public string url { get; set; }
            public string webSocketDebuggerUrl { get; set; }
        }
    }
}
