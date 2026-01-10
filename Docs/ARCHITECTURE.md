# ??? Arquitetura de Scraping - BetSniffer.Api

## Visão Geral

O BetSniffer.Api é um sistema de web scraping especializado em capturar informações de apostas esportivas de múltiplas casas de apostas. A arquitetura foi projetada para ser modular, escalável e fácil de manter.

## Componentes Principais

### 1. **Interface Core: `IScrapingService`**
```csharp
public interface IScrapingService
{
    List<TagInfo> ScrapeTags(string url, string siteName);
}
```

Contrato que todas as implementações de scraping devem seguir.

---

## ?? Estrutura de Diretórios

```
Core/
??? Sites/
?   ??? Betano/
?   ?   ??? BetanoScraping.cs       ? Implementação
?   ?   ??? BetanoTags.cs           ?? Mapeamento de mercados
?   ??? Betnacional/
?   ?   ??? BetnacionalScraping.cs  ? Implementação (MODELO)
?   ?   ??? BetnacionalTags.cs      ?? Mapeamento
?   ??? Novibet/
?   ?   ??? NovibetScraping.cs      ? Refatorado
?   ?   ??? NovibetTags.cs          ?? Mapeamento
?   ??? Pixbet/
?   ?   ??? PixbetScraping.cs       ? Refatorado
?   ?   ??? PixbetTags.cs           ?? Mapeamento
?   ??? ...
??? Services/
?   ??? WebScrapingServicePuppeteer.cs   ?? Driver browser (Puppeteer)
?   ??? WebScrapingServiceSelenium.cs    ?? Driver browser (Selenium)
?   ??? TeamService.cs                   ?? Gerenciar times
?   ??? GameService.cs                   ?? Gerenciar jogos
?   ??? ...
??? Interfaces/
    ??? IScrapingService.cs              ?? Contrato
```

---

## ?? Fluxo de Execução Padrão

### 1?? **Inicialização e Validação**
```csharp
public List<TagInfo> ScrapeTags(string url, string siteName)
{
    // ? Validar parâmetros de entrada
    if (string.IsNullOrEmpty(url))
        throw new ArgumentException("URL não pode ser nula ou vazia.", nameof(url));
    
    // ? Buscar ou criar site no banco
    site = _dbContext.Site.FirstOrDefault(...) ?? AddNewSite(siteName);
    
    // ? Inicializar browser
    _webScrapingService = new WebScrapingServicePuppeteer();
    _webScrapingService.Initialize();
}
```

### 2?? **Extração de Informações do Jogo**
```csharp
using var browser = _webScrapingService;
var page = browser.NavigateTo(url);
System.Threading.Thread.Sleep(new Random().Next(9873, 11405));

ExtractGameInfo(page).GetAwaiter().GetResult();
```

**Dados Extraídos:**
- `homeTeam`: Nome do time mandante
- `awayTeam`: Nome do time visitante
- `leagueName`: Nome da liga/torneio
- `gameDateTime`: Data e hora do jogo

### 3?? **Salvamento ou Atualização do Jogo**
```csharp
SaveOrUpdateGame(siteName, url);
```

**Lógica:**
- Se jogo existe: atualiza status e remove apostas antigas
- Se novo: cria novo registro no banco
- Sempre salva no banco com `_dbContext.SaveChanges()`

### 4?? **Processamento de Mercados e Apostas**
```csharp
ProcessTabsAndMarketViews(page).GetAwaiter().GetResult();
```

**Processamento:**
1. Localizar abas de mercados
2. Clicar em cada aba
3. Extrair mercados disponíveis
4. Para cada mercado:
   - Localizar odds
   - Extrair valores
   - Criar objetos `BetInfo`
5. Salvar apostas com deduplicação

### 5?? **Limpeza e Fechamento**
```csharp
finally
{
    _webScrapingService?.Dispose();
}
```

---

## ?? Padrão de Implementação (Modelo: BetnacionalScraping)

### ? Estrutura Recomendada

1. **Região de Variáveis Globais**
```csharp
#region VariaveisGlobais
// Dados do jogo
private string homeTeam;
private string awayTeam;
private string leagueName;
private DateTime gameDateTime;

// Entidades
private Site site;
private GamesInfo gamesInfo;

// Serviços injetados
private readonly ApplicationDbContext _dbContext;
private readonly TeamService _teamService;
private readonly IRepositoryService<GamesInfo> _gamesInfoRepository;
private readonly IRepositoryService<BetInfo> _betInfoRepository;
private readonly GameService _gameService;
private readonly ILogService _logService;
private WebScrapingServicePuppeteer _webScrapingService;
#endregion
```

2. **Construtor com Validação**
```csharp
public BetnacionalScraping(
    ApplicationDbContext dbContext,
    TeamService teamService,
    IRepositoryService<GamesInfo> gamesInfoRepository,
    IRepositoryService<BetInfo> betInfoRepository)
{
    _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
    // ... validações para todos os parâmetros
}
```

3. **Método Principal Simples**
```csharp
public List<TagInfo> ScrapeTags(string url, string siteName)
{
    // 1. Validar entrada
    // 2. Preparar browser
    // 3. Executar etapas
    // 4. Retornar resultado
}
```

4. **Métodos Auxiliares Específicos**
- `ExtractGameInfo(IPage)`: Extrai dados do jogo
- `SaveOrUpdateGame()`: Persiste jogo
- `ProcessTabsAndMarketViews(IPage)`: Processa mercados
- `ProcessMarketViews(IPage)`: Processa apostas
- `SaveBets(List<BetInfo>)`: Persiste apostas com deduplicação
- `ParseGameDateTime(string)`: Parse da data/hora
- `AddNewSite(string)`: Adiciona novo site

---

## ??? Sistema de Tags (Mapeamento de Mercados)

### Estrutura de Tags

```csharp
// Exemplo: BetnacionalTags.cs
public static class BetnacionalTags
{
    public static Dictionary<int, List<string>> TagNames { get; private set; } = new()
    {
        { 1, new List<string> { "Vencedor da Partida", "Match Winner" } },
        { 2, new List<string> { "Total de Gols", "Over/Under" } },
        { 3, new List<string> { "Ambos Marcam", "Both Teams to Score" } }
        // ...
    };

    public static void AddDynamicTags(string homeTeam, string awayTeam)
    {
        // Adiciona tags dinâmicas (ex: "Vitória de {TIME}")
    }
}
```

### Ciclo de Vida das Tags

1. **Inicialização**: Tags estáticas carregadas
2. **Dinâmicas**: Tags específicas do jogo adicionadas (times, etc)
3. **Matching**: Mercado extraído é comparado com tags
4. **Salvamento**: BetInfo associado ao TagId correto

---

## ?? Fluxo de Dados

```
URL + SiteName
    ?
[Validação]
    ?
[Inicializar Browser]
    ?
[Navegar para URL]
    ?
[Extração de Dados do Jogo] ? homeTeam, awayTeam, leagueName, gameDateTime
    ?
[Salvamento/Atualização do Jogo] ? gamesInfo (GamesInfo)
    ?
[Processamento de Mercados] 
    ?
[Processamento de Apostas]
    ?
[Deduplicação e Salvamento de Apostas] ? BetInfo[]
    ?
[Limpeza de Recursos]
    ?
List<TagInfo> (resultado)
```

---

## ??? Tratamento de Erros

### Padrão Implementado

```csharp
try
{
    // Operação crítica
    ExtractGameInfo(page).GetAwaiter().GetResult();
}
catch (Exception ex)
{
    Console.WriteLine($"? Erro ao extrair informações: {ex.Message}");
    _logService.LogError("Erro ao extrair informações", ex);
    throw;  // Re-lançar para parar processamento
}
finally
{
    _webScrapingService?.Dispose();  // Sempre limpar
}
```

### Erros Não-Críticos

```csharp
try
{
    // Operação opcional
    ConfirmAgeVerification(page);
}
catch (TimeoutException)
{
    Console.WriteLine("Nenhuma verificação de idade necessária.");
}
catch (Exception ex)
{
    Console.WriteLine($"Erro ao confirmar idade (não crítico): {ex.Message}");
}
```

---

## ?? Entidades Principais

### GamesInfo
```csharp
public class GamesInfo
{
    public int GameId { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime GameDate { get; set; }
    public string League { get; set; }
    public int? SiteId { get; set; }
    public string URL { get; set; }
    public int Status { get; set; }  // 1 = Ativo
    public DateTime LastUpdated { get; set; }
    public string GameName { get; set; }
    
    public Site Site { get; set; }
    public List<BetInfo> Bets { get; set; }
}
```

### BetInfo
```csharp
public class BetInfo
{
    public int BetId { get; set; }
    public int GameId { get; set; }
    public int SiteId { get; set; }
    public int TagId { get; set; }
    public string TagName { get; set; }
    public string OverUnder { get; set; }  // "Mais de" ou "Menos de"
    public decimal BetAmount { get; set; }
    public decimal Multiplier { get; set; }
    public DateTime GameDate { get; set; }
    public DateTime CaptureDate { get; set; }
    
    public GamesInfo GamesInfo { get; set; }
    public Site Site { get; set; }
}
```

### Site
```csharp
public class Site
{
    public int SiteId { get; set; }
    public string Name { get; set; }
    public List<GamesInfo> Games { get; set; }
    public List<BetInfo> Bets { get; set; }
}
```

---

## ?? Validações Importantes

### Entrada (URL e SiteName)
- Não podem ser nulos/vazios
- URL deve ser válida

### Extração de Dados
- Times devem existir no banco
- Data/hora deve ser parseável
- Mercados devem estar mapeados em Tags

### Salvamento
- Deduplicação de apostas por: `GameId + TagId + OverUnder + SiteId`
- Apostas antigas removidas antes de novas serem adicionadas
- Sempre usar `_dbContext.SaveChanges()` após operações

---

## ?? Boas Práticas

### ? Faça:
- Use `using` para browser/page
- Sempre valide entrada no método público
- Sempre limpe recursos no `finally`
- Use `GetAwaiter().GetResult()` para chamadas async em código síncrono
- Adicione delays aleatórios para evitar detecção
- Normalize nomes com `_teamService.NormalizeText()`

### ? Evite:
- Inicializar browser no construtor (faça no método público)
- Mix de async/await sem necessidade (use padrão síncrono)
- Deixar resources sem limpeza
- Ignorar erros de parsing
- Salvar dados duplicados

---

## ?? Performance

- **Browser Pooling**: Criar nova instância por request
- **Delays**: Aleatórios (8-11s) para simular comportamento humano
- **Deduplicação**: HashSet para comparações rápidas
- **Índices**: GameId, SiteId, GameDate em BetInfo

---

## ?? Referências

- Padrão modelo: `Core/Sites/Betnacional/BetnacionalScraping.cs`
- Interface contrato: `Core/Interfaces/IScrapingService.cs`
- Banco de dados: `Data/ApplicationDbContext.cs`
- Controllers: `Controllers/BatchScrapingController.cs`

