# ?? Troubleshooting e FAQ

## Perguntas Frequentes

### P: Por que o browser é inicializado dentro do método e não no construtor?
**R**: Para evitar consumo desnecessário de recursos. O browser é pesado e deve existir apenas durante a execução do scraping.

---

### P: Por que usar `GetAwaiter().GetResult()` ao invés de async/await?
**R**: O método principal `ScrapeTags` é síncrono por contrato. Usar await em todo o método mudaria a interface. Alternativa: manter padrão síncrono consistente ou fazer o método principal async.

---

### P: Como lidar com pop-ups inesperados?
**R**: Adicionar try-catch em torno de `WaitForSelectorAsync` com timeout curto. Se não encontrar, continuar (não é crítico).

```csharp
try
{
    var popup = await page.WaitForSelectorAsync(".popup-selector", 
        new WaitForSelectorOptions { Timeout = 3000 });
    if (popup != null)
        await popup.ClickAsync();
}
catch (TimeoutException)
{
    Console.WriteLine("Pop-up não encontrado (não crítico).");
}
```

---

### P: Por que os delays são aleatórios?
**R**: Para simular comportamento humano e evitar detecção/bloqueio por padrões de requisição.

---

### P: Como debugar quando um seletor não é encontrado?
**R**: 
1. Adicionar screenshot: `await page.ScreenshotAsync("debug.png");`
2. Aumentar timeout
3. Usar F12 no navegador real para confirmar seletor
4. Adicionar delay antes de procurar

---

### P: O que fazer se o site tiver estrutura dinâmica (JavaScript)?
**R**: Puppeteer renderiza JavaScript, mas pode ser necessário:
- Aumentar delays
- Usar `WaitForNavigationAsync` após cliques
- Usar `WaitForFunctionAsync` para condições customizadas

```csharp
await page.WaitForFunctionAsync("() => document.querySelectorAll('.market').length > 0");
```

---

### P: Como distinguir entre "Mais de" e "Menos de"?
**R**: Verificar o texto da aposta:
- Se contém "Mais de", "+", "Over": `overUnder = "Mais de"`
- Se contém "Menos de", "-", "Under": `overUnder = "Menos de"`

```csharp
string overUnder = null;
if (betName.Contains("mais de") || betName.EndsWith("+"))
    overUnder = "Mais de";
else if (betName.Contains("menos de") || betName.EndsWith("-"))
    overUnder = "Menos de";
```

---

### P: Por que remover apostas antes de adicionar novas?
**R**: Para garantir dados atualizados. Se as odds mudaram, a aposta antiga fica obsoleta.

---

### P: Como lidar com caracteres especiais em nomes de times?
**R**: `TeamService.NormalizeText()` cuida disso automaticamente.

---

### P: O que fazer se o site mudar a estrutura?
**R**: Atualizar os seletores e revalidar. Se mudar muito, pode ser necessário reescrever grandes partes.

---

## Erros Comuns

### Erro: `TimeoutException: Waiting for selector failed`
**Causa**: Elemento não encontrado no tempo limite  
**Solução**:
- Aumentar timeout: `Timeout = 20000` (20s)
- Adicionar delay antes: `await Task.Delay(2000);`
- Validar seletor com F12

---

### Erro: `Browsing context was closed`
**Causa**: Browser/page fechado prematuramente  
**Solução**:
- Verificar se `_webScrapingService.Dispose()` é chamado cedo
- Usar `finally` para garantir limpeza

---

### Erro: `No host found`
**Causa**: URL inválida ou site down  
**Solução**:
- Validar URL
- Verificar internet
- Usar VPN se site está bloqueado

---

### Erro: `Element is not attached to the DOM`
**Causa**: Elemento foi removido da página (ex: após clique)  
**Solução**:
- Requeryar o elemento após ação
- Usar `WaitForSelectorAsync` novamente
- Adicionar delay

---

### Erro: `Expected navigation to "url" but the page has not navigated after 30000ms`
**Causa**: Clique não navegou para nova página  
**Solução**:
- Não usar `WaitForNavigationAsync` em cliques que não navegam
- Usar `WaitForSelectorAsync` para novos conteúdos ao invés

```csharp
// ? Errado
await Task.WhenAll(
    page.WaitForNavigationAsync(),
    tab.ClickAsync()
);

// ? Correto (se não há navegação)
await tab.ClickAsync();
await Task.Delay(500);
```

---

### Erro: `Connection Closed`
**Causa**: Browser crashou  
**Solução**:
- Aumentar delays
- Verificar recursos de sistema
- Limpar resíduos de processos Chrome

```powershell
# Windows
taskkill /IM chrome.exe /F

# Linux
pkill -f chrome
```

---

## Problemas de Performance

### Problema: Scraping muito lento
**Soluções**:
1. Reduzir delays (com cuidado)
2. Usar processamento paralelo no controller
3. Otimizar queries de banco de dados

---

### Problema: Muita memória consumida
**Soluções**:
1. Verificar se `Dispose()` é chamado
2. Usar `using var browser` corretamente
3. Limpar listas grandes após uso

---

### Problema: CPU alta
**Soluções**:
1. Aumentar delays entre operações
2. Reduzir número de abas simultâneas
3. Verificar loop infinito

---

## Testes

### Teste Manual
```csharp
// Program.cs ou teste unitário
var scraper = new BetnacionalScraping(dbContext, teamService, 
    gamesInfoRepo, betInfoRepo);
var result = scraper.ScrapeTags("https://...", "Betnacional");
Console.WriteLine($"Resultado: {result.Count} tags");
```

### Teste de Seletor
```csharp
var page = await browser.NewPageAsync();
await page.GoToAsync("https://example.com");
var element = await page.QuerySelectorAsync("selector");
Console.WriteLine(element != null ? "? Encontrado" : "? Não encontrado");
```

### Teste de Parsing
```csharp
var dateTime = ParseGameDateTime("Hoje às 14:00");
Console.WriteLine(dateTime);  // Deve ser hoje às 14:00
```

---

## Logging Avançado

### Adicionar Screenshot em Erro
```csharp
catch (Exception ex)
{
    await page.ScreenshotAsync($"error-{DateTime.Now:yyyyMMdd-HHmmss}.png");
    Console.WriteLine($"Screenshot salvo: error-{DateTime.Now:yyyyMMdd-HHmmss}.png");
    throw;
}
```

### Adicionar Trace de HTML
```csharp
var html = await page.GetContentAsync();
File.WriteAllText("debug.html", html);
Console.WriteLine("HTML salvo: debug.html");
```

### Log Detalhado
```csharp
Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Iniciando extração...");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Página carregada");
```

---

## Debugging

### Modo Headless Desativado
```csharp
var options = new LaunchOptions { Headless = false };
var browser = await Puppeteer.LaunchAsync(options);
```

### Slowmo (câmera lenta)
```csharp
var options = new LaunchOptions { SlowMo = 100 };  // 100ms entre ações
```

---

## Referências

- **Puppeteer Sharp Docs**: https://www.puppeteersharp.com/
- **Padrões**: `Docs/PATTERNS.md`
- **Arquitetura**: `Docs/ARCHITECTURE.md`

