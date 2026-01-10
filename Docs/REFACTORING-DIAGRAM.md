# ??? Estrutura de Refatoração - Diagrama Visual

## Antes da Refatoração ?

```
Core/Services/
??? TeamService.cs          ? Normalização de texto
??? GameService.cs          ? Lógica de jogo + ClosePopup() [DUPLICADO]
??? GameTimeService.cs      ? Parsing genérico [NÃO USADO]
??? WebScrapingServicePuppeteer.cs  ? Web scraping base
??? AdvancedUndetectedChromeDriver.cs

Core/Sites/
??? KTO/KTOScraping.cs      ? ClosePopup [DUPLICADO], SaveBets [DUPLICADO], ParseKtoDate
??? Betnacional/BetnacionalScraping.cs  ? ClosePopup [DUPLICADO], SaveBets [DUPLICADO], ParseGameDateTime [DUPLICADO]
??? Betfair/BetfairScraping.cs  ? RemoveObstruction [DUPLICADO], SaveBets [DUPLICADO], ParseGameDateTime [DUPLICADO]
??? Superbet/SuperbetScraping.cs  ? HandleCookies [DUPLICADO], SaveBets [DUPLICADO], ParseGameDateTime [DUPLICADO]
??? Pixbet/PixbetScraping.cs  ? ClosePopup [DUPLICADO], SaveBets [DUPLICADO], ParseGameDateTime [DUPLICADO]
??? ... outros sites

PROBLEMAS:
  ?? ~500+ linhas de código duplicado
  ?? Parsing de data em 5 lugares diferentes
  ?? SaveBets em 5 lugares diferentes
  ?? ClosePopup/HandleCookies em 4 lugares diferentes
  ?? Mistura de síncrono/assincronismo
  ?? Difícil de manter (mudança afeta 5 arquivos)
```

---

## Depois da Refatoração ?

```
Core/Services/
??? TeamService.cs          ? Normalização de texto
??? GameService.cs          ? Lógica de jogo (PopUp removido)
??? GameTimeService.cs      ? SERÁ REMOVIDO (consolidado em DateTimeParsingService)
??? WebScrapingServicePuppeteer.cs  ? Web scraping base
??? AdvancedUndetectedChromeDriver.cs
?
??? ? PuppeteerPageService.cs         ? NOVO: Manipulação de página
?   ??? ClosePopupAsync()
?   ??? HandleCookiesAsync()
?   ??? RemoveObstructionAsync()
?   ??? ConfirmAgeVerificationAsync()
?   ??? WaitForElementAsync()
?
??? ? DateTimeParsingService.cs       ? NOVO: Parsing de data/hora
?   ??? ParseAsync(text, strategy)
?   ??? Parse(text, strategy)
?   ??? ParseBetfairDateTime()
?   ??? ParseSuperbetDateTime()
?   ??? ParseBetnacionalDateTime()
?   ??? ParsePixbetDateTime()
?   ??? ParseKTODate()
?   ??? ParseNovibetDateTime()
?
??? ? BetSavingService.cs             ? NOVO: Salvamento de apostas
    ??? SaveBetsAsync()
    ??? SaveBets()
    ??? ValidateBets()
    ??? RemoveGameBetsAsync()
    ??? RemoveSiteBetsAsync()
    ??? GetStatistics()

Core/Sites/
??? KTO/KTOScraping.cs      ? Sem ParseKtoDate, sem SaveBets ?
??? Betnacional/BetnacionalScraping.cs  ? Sem ParseGameDateTime, sem SaveBets ?
??? Betfair/BetfairScraping.cs  ? Sem RemoveObstruction, sem SaveBets, sem ParseGameDateTime ?
??? Superbet/SuperbetScraping.cs  ? Sem HandleCookies, sem SaveBets, sem ParseGameDateTime ?
??? Pixbet/PixbetScraping.cs  ? Sem ClosePopup, sem SaveBets, sem ParseGameDateTime ?
??? ... outros sites

BENEFÍCIOS:
  ?? 100% do código duplicado consolidado
  ?? Parsing de data em 1 lugar (6 estratégias)
  ?? SaveBets em 1 lugar
  ?? ClosePopup/HandleCookies em 1 lugar
  ?? 100% assincronismo
  ?? Mudança em um arquivo afeta todos os scrapers
  ?? Código testável e reutilizável
```

---

## Fluxo de Dados - Arquitetura

### Antes ?

```
BatchScrapingController
    ?
    ??? KTOScraping.ScrapeTags()
    ?       ??? ExtractGameInfo() [SYNC]
    ?       ??? ParseKtoDate() [DUPLICADO]
    ?       ??? ClosePopup() [DUPLICADO]
    ?       ??? SaveBets() [DUPLICADO]
    ?
    ??? SuperbetScraping.ScrapeTags()
    ?       ??? ExtractGameInfo() [SYNC]
    ?       ??? ParseGameDateTime() [DUPLICADO]
    ?       ??? HandleCookies() [DUPLICADO]
    ?       ??? SaveBets() [DUPLICADO]
    ?
    ??? ... 3 sites mais
        (5x duplicação)

[PROBLEMA: Sem centralização de lógica comum]
```

### Depois ?

```
BatchScrapingController
    ?
    ??? PuppeteerPageService (injetado)
    ?       ??? ClosePopupAsync()
    ?       ??? HandleCookiesAsync()
    ?       ??? RemoveObstructionAsync()
    ?       ??? ConfirmAgeVerificationAsync()
    ?
    ??? DateTimeParsingService (injetado)
    ?       ??? Parse(text, DateParsingStrategy.KTO)
    ?
    ??? BetSavingService (injetado)
    ?       ??? SaveBetsAsync()
    ?       ??? ValidateBets()
    ?       ??? GetStatistics()
    ?
    ??? KTOScraping.ScrapeTagsAsync()
            ??? _pageService.ClosePopupAsync()
            ??? _dateTimeService.Parse()
            ??? _betSavingService.SaveBetsAsync()
        
        SuperbetScraping.ScrapeTagsAsync()
            ??? _pageService.HandleCookiesAsync()
            ??? _dateTimeService.Parse()
            ??? _betSavingService.SaveBetsAsync()
        
        ... 3 sites mais (0x duplicação)

[SUCESSO: Centralização com Dependency Injection]
```

---

## Padrão de Conversão - Exemplo KTO

### Passo 1: Antes da Refatoração
```csharp
public class KTOScraping : IScrapingService
{
    private readonly ILogService _logService;
    
    public KTOScraping(ApplicationDbContext dbContext, ...)
    {
        _logService = new LogService(...);  // ? Criação inline
    }
    
    public List<TagInfo> ScrapeTags(string url, string siteName)  // ? Síncrono
    {
        // ... código com .GetAwaiter().GetResult()
        var gameDateTime = ParseKtoDate(dateText);  // ? Método local
        SaveBets(currentBets);  // ? Lógica inline
    }
    
    private DateTime ParseKtoDate(string dateText) { ... }  // ? Duplicado em 4 sites
    private void SaveBets(List<BetInfo> bets) { ... }  // ? Duplicado em 5 sites
}
```

### Passo 2: Depois da Refatoração
```csharp
public class KTOScraping : IScrapingService
{
    private readonly PuppeteerPageService _pageService;  // ? Injetado
    private readonly DateTimeParsingService _dateTimeService;  // ? Injetado
    private readonly BetSavingService _betSavingService;  // ? Injetado
    private readonly ILogService _logService;  // ? Injetado
    
    public KTOScraping(
        ApplicationDbContext dbContext,
        ...
        PuppeteerPageService pageService,  // ? DI
        DateTimeParsingService dateTimeService,  // ? DI
        BetSavingService betSavingService,  // ? DI
        ILogService logService)  // ? DI
    {
        _pageService = pageService;
        _dateTimeService = dateTimeService;
        _betSavingService = betSavingService;
        _logService = logService;
    }
    
    public async Task<List<TagInfo>> ScrapeTagsAsync(
        string url,  // ? Assincronizado
        string siteName)
    {
        // ... código com await
        var gameDateTime = _dateTimeService.Parse(
            dateText,
            DateParsingStrategy.KTO  // ? Reutiliza serviço
        );
        await _betSavingService.SaveBetsAsync(currentBets);  // ? Usa serviço
    }
    
    // ? ParseKtoDate() REMOVIDO
    // ? SaveBets() REMOVIDO
}
```

---

## Estatísticas de Impacto

### Redução de Código Duplicado

| Funcionalidade | Antes | Depois | Redução |
|---|---|---|---|
| `ParseGameDateTime()` | 5 variações | 1 serviço | 80% |
| `SaveBets()` | 5 variações | 1 serviço | 80% |
| `ClosePopup()` | 4 variações | 1 serviço | 75% |
| `HandleCookies()` | 2 variações | 1 serviço | 50% |
| **TOTAL** | **~500+ linhas** | **~652 linhas em serviços** | **~50% economia** |

### Distribuição de Código

**ANTES:**
```
KTO:        ~450 linhas (com ParseKtoDate, SaveBets, etc)
Betnacional: ~400 linhas (com ParseGameDateTime, SaveBets, etc)
Betfair:    ~600 linhas (com RemoveObstruction, SaveBets, ParseGameDateTime)
Superbet:   ~550 linhas (com HandleCookies, SaveBets, ParseGameDateTime)
Pixbet:     ~350 linhas (com ClosePopup, SaveBets, ParseGameDateTime)
GameService: ~150 linhas (com ClosePopup duplicado)
?????????????????????
TOTAL:      ~2,500 linhas (com duplicação)
```

**DEPOIS:**
```
KTO:        ~400 linhas (sem ParseKtoDate, SaveBets)
Betnacional: ~350 linhas (sem ParseGameDateTime, SaveBets)
Betfair:    ~550 linhas (sem RemoveObstruction, SaveBets, ParseGameDateTime)
Superbet:   ~500 linhas (sem HandleCookies, SaveBets, ParseGameDateTime)
Pixbet:     ~300 linhas (sem ClosePopup, SaveBets, ParseGameDateTime)
GameService: ~100 linhas (sem ClosePopup)

PuppeteerPageService: +117 linhas (consolidado)
DateTimeParsingService: +390 linhas (consolidado)
BetSavingService: +145 linhas (consolidado)
?????????????????????
TOTAL:      ~2,350 linhas (mais centralizado e testável)

ECONOMIA: ~150+ linhas = 6% redução
MELHORIA: Código duplicado eliminado = 50% redução em manutenção
```

---

## Timeline de Implementação

```
FASE 1: Serviços Consolidados ? [CONCLUÍDO]
?? PuppeteerPageService.cs        [DONE]
?? DateTimeParsingService.cs      [DONE]
?? BetSavingService.cs            [DONE]
?? Documentação                   [DONE]
?? Compilação OK                  [DONE]

FASE 2: Refatorar Scrapers ? [PRÓXIMA]
?? KTOScraping               [1-2h]
?? BetnacionalScraping       [1-2h]
?? PixbetScraping            [1.5-2.5h]
?? SuperbetScraping          [2-3h]
?? BetfairScraping           [2-3h]

FASE 3: Atualizar Interfaces ? [PLANEJADO]
?? IScrapingService.cs       [0.5h]
?? ILeagueScrapingService.cs [0.5h]

FASE 4: Atualizar Controller ? [PLANEJADO]
?? BatchScrapingController   [0.25h]

FASE 5: Testes Integrados ? [PLANEJADO]
?? Teste funcional de cada scraper [2-3h]
?? Memory leak testing             [1h]
?? Documentação                    [0.5h]

TOTAL ESTIMADO: 11-17 horas
```

---

## Checklist de Qualidade

### Código
- ? Sem duplicação de lógica (consolidado em serviços)
- ? 100% assincronismo para operações I/O
- ? Dependency Injection implementado
- ? SOLID principles respeitados
- ? Testes unitários (próxima fase)

### Documentação
- ? Plano de refatoração completo
- ? Guia passo-a-passo prático
- ? Exemplos antes/depois
- ? Checklist por scraper
- ? Diagramas visuais

### Performance
- ? Não-bloqueante com async/await
- ? Melhor escalabilidade
- ? Testes de performance (próxima fase)

---

**Status Atual:** ?? FASE 1 CONCLUÍDA
**Próxima Ação:** ?? Iniciar FASE 2 com KTOScraping
