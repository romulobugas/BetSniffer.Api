📡 BetSniffer

Coletor multi-casas + detector de arbitragem com Selenium e PuppeteerSharp, pronto para produção.

Simulamos navegação humana (anti-bot), rodamos scrapers em paralelo e expomos um painel completo para monitoramento, arbitragem e operações manuais.
A API é ASP.NET Core, integra Selenium + PuppeteerSharp, persiste dados em SQL Server via EF Core e serve um frontend estático com Swagger público.

🔍 Visão Geral

O BetSniffer é uma plataforma completa de scraping e análise de odds para múltiplas casas de apostas.
Inclui:

Atualização de jogos

Raspagem manual

Monitoramento de links

Execução de arbitragem

Consulta de resultados

Projetado para simular navegação humana e contornar mecanismos anti-bot.

🏛️ Arquitetura
Diagrama 
flowchart LR

    FE[Frontend<br>HTML/CSS/JS] --> API
    API[API ASP.NET Core<br>/swagger + endpoints REST] --> SCRAPERS
    SCRAPERS[Selenium + PuppeteerSharp<br>Scrapers Providers] --> DB
    API --> DB

    SCRAPERS -->|Interações human-like| SITE[(Casas de Apostas)]

    DB[(SQL Server)]

🧠 Tecnologias Principais
Scraping Híbrido

Selenium / ChromeDriver (singleton)
Navegação robusta, resiliente a sites com estruturas dinâmicas.

PuppeteerSharp

Scroll suave

Cliques JS em elementos e iframes

PageDown/PageUp para carregar conteúdo

Manipulação de contextos e frames

Execução de JS "human-like"

Anti-bot & “Human-Like UX”

PuppeteerSharp injeta customizações como:

Remoção de navigator.webdriver

Ajuste de idiomas, plugins e propriedades do navigator

User-Agent dinâmico

Viewport maximizado

Zoom customizado 0.7x

Delays naturais, scroll incremental

Orquestração de Scrapers

Cada casa tem seu próprio serviço:

Betano

Betfast

Pixbet

KTO

Superbet

Betnacional

Betfair (ligas)

VBet (ligas)

Adição de nova casa = criar scraper + registrar no DI.

Dados & Database

EF Core

Timeout ampliado: 180s

Suporte a operações longas

Schema para Jogos, Markets, Odds, Logs, Links monitorados

API + UI

Swagger em /swagger

Frontend estático (Pages/Index.html)

Redirecionamento automático da raiz /

CORS liberado em modo Dev para testes locais

🔗 Endpoints Principais
Rota	Método	Finalidade
/batch-scraping	POST	Executa scraping em múltiplas casas
/games/update	POST	Atualiza jogos cadastrados
/execute-arbitrage	POST	Executa ciclo completo de arbitragem
/arbitrage-results	GET	Retorna oportunidades detectadas
/scrape-manual	POST	Raspagem manual via URL
/monitor	GET	Status dos scrapers + filas
🔁 Como funciona a Arbitragem

POST /execute-arbitrage inicia coleta e normalização.

Backend compara odds entre casas, calcula percentuais seguros.

GET /arbitrage-results traz oportunidades ativas.

Front calcula automaticamente stakes proporcionais.

👣 Interações Human-Like

Scroll suave

Cliques JS precisos

Simulação de rolagem natural

Esperas dinâmicas

Zoom customizado

User-agent variável

Navegação maximizando viewport

🧪 Execução Local
Requisitos

.NET 8+

SQL Server

ChromeDriver

Chrome/Chromium

Variáveis de ambiente
ConnectionStrings__DefaultConnection=
ScraperSettings__ChromeDriverPath=

Rodar
dotnet restore
dotnet run


Swagger: http://localhost:5241/swagger

Frontend: http://localhost:5241/

🖼️ Galeria de Telas (recomendado subir imagens)

Atualizar Jogos

Monitor

Raspagem Manual

Arbitragem

Swagger

🧭 Roadmap

Worker/Fila distribuída (Redis/Service Bus)

Alertas ao vivo (SignalR/Telegram)

Scrapers adicionais

Fingerprint rotation

Simulação real de mouse

Delays randômicos evoluídos

📥 Clone e execute
git clone https://github.com/SEU-USUARIO/BetSniffer.Api.git
cd BetSniffer.Api
dotnet run
