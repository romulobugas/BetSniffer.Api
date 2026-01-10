# ?? Documentação BetSniffer.Api

Bem-vindo à documentação completa do projeto BetSniffer.Api! Este é um projeto de web scraping especializado em capturar informações de apostas esportivas de múltiplas casas de apostas.

---

## ?? Guias Disponíveis

### ??? [ARCHITECTURE.md](ARCHITECTURE.md)
**Visão geral da arquitetura do projeto**
- Componentes principais
- Estrutura de diretórios
- Fluxo de execução padrão
- Sistema de tags
- Entidades principais
- Boas práticas

**Ideal para**: Entender a estrutura geral do projeto

---

### ?? [PATTERNS.md](PATTERNS.md)
**Padrões de implementação de novo scraper**
- Estrutura de arquivo (template)
- Região de variáveis globais
- Método de entrada `ScrapeTags`
- Métodos auxiliares detalhados
- Construtor e DI
- Tratamento de erros
- Logging e console
- Checklist de implementação

**Ideal para**: Implementar novo scraper ou refatorar existente

---

### ?? [QUICKSTART.md](QUICKSTART.md)
**Guia passo-a-passo para criar novo scraper**
- 10 passos práticos
- Código base
- Exemplos de implementação
- Testes
- Troubleshooting rápido

**Ideal para**: Novo desenvolvedor implementando seu primeiro scraper

---

### ?? [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
**FAQ, problemas comuns e soluções**
- Perguntas frequentes
- Erros comuns e soluções
- Problemas de performance
- Testes e debugging
- Logging avançado

**Ideal para**: Resolver problemas durante desenvolvimento

---

## ?? Começar Por Onde?

### Sou novo no projeto
1. Leia: **ARCHITECTURE.md** (20 min)
2. Leia: **PATTERNS.md** (30 min)
3. Explore: `Core/Sites/Betnacional/BetnacionalScraping.cs` (código modelo)

### Preciso implementar novo scraper
1. Leia: **QUICKSTART.md** (passo-a-passo)
2. Use: **PATTERNS.md** (como referência)
3. Consulte: **TROUBLESHOOTING.md** (se tiver dúvidas)

### Preciso refatorar código existente
1. Leia: **PATTERNS.md** (padrões recomendados)
2. Compare: Com `BetnacionalScraping.cs` (modelo)
3. Use: **ARCHITECTURE.md** (para entender conceitos)

### Tenho um problema/erro
1. Consulte: **TROUBLESHOOTING.md**
2. Se necessário: Veja **PATTERNS.md** (corretos)
3. Por último: Analise **ARCHITECTURE.md** (conceitos)

---

## ?? Estrutura Rápida

```
BetSniffer.Api/
??? Core/
?   ??? Sites/           # Implementações de scraping por site
?   ?   ??? Betnacional/ ? MODELO (padrão recomendado)
?   ?   ??? Novibet/     ? Refatorado
?   ?   ??? Pixbet/      ? Refatorado
?   ?   ??? ...
?   ??? Services/        # Serviços compartilhados
?   ??? Interfaces/      # Contratos
??? Controllers/         # API endpoints
??? Data/               # Banco de dados
??? Models/             # Entidades
??? Docs/               # ?? VOCÊ ESTÁ AQUI
?   ??? ARCHITECTURE.md
?   ??? PATTERNS.md
?   ??? QUICKSTART.md
?   ??? TROUBLESHOOTING.md
?   ??? README.md (este arquivo)
??? ...
```

---

## ?? Conceitos-Chave

### IScrapingService
Interface que todas as implementações devem implementar. Define o contrato:
```csharp
public interface IScrapingService
{
    List<TagInfo> ScrapeTags(string url, string siteName);
}
```

### Ciclo de Vida de Scraping
1. **Inicialização**: Browser, página
2. **Extração**: Dados do jogo
3. **Persistência**: Game + Bets no banco
4. **Processamento**: Mercados e odds
5. **Limpeza**: Dispose de recursos

### Deduplicação
Apostas duplicadas são removidas por combinação:
- GameId
- TagId
- OverUnder
- SiteId

---

## ?? Padrão Recomendado

Sempre use `BetnacionalScraping.cs` como modelo. Ele segue:

? Estrutura clara e linear  
? Métodos bem separados  
? Validação robusta  
? Try-finally com cleanup  
? Logging detalhado  
? Tratamento de erros  
? Delays aleatórios  

---

## ??? Stack Técnico

- **Language**: C# 12
- **Framework**: .NET 8
- **Browser Automation**: PuppeteerSharp
- **Database**: Entity Framework Core
- **API**: ASP.NET Core

---

## ?? Tabela de Conteúdos

| Documento | Seções | Tempo |
|-----------|--------|-------|
| ARCHITECTURE | 13 | 20 min |
| PATTERNS | 10 | 30 min |
| QUICKSTART | 10 | 45 min |
| TROUBLESHOOTING | 8 | 15 min |

---

## ?? Quick Commands

### Ver modelo
```bash
cat Core/Sites/Betnacional/BetnacionalScraping.cs
```

### Ver tags de exemplo
```bash
cat Core/Sites/Betnacional/BetnacionalTags.cs
```

### Compilar projeto
```bash
dotnet build
```

### Rodar testes
```bash
dotnet test
```

---

## ?? Tips

1. **Sempre valide entrada** no método público
2. **Use try-finally** para limpeza de recursos
3. **Adicione delays aleatórios** para evitar detecção
4. **Normalize names** com `TeamService.NormalizeText()`
5. **Deduplique apostas** antes de salvar
6. **Documente seletores CSS** para futuras manutenções
7. **Use logging** com emojis para clareza
8. **Teste localmente** antes de fazer push

---

## ?? Referências

- **GitHub**: https://github.com/romulobugas/BetSniffer.Api
- **PuppeteerSharp**: https://www.puppeteersharp.com/
- **.NET 8**: https://docs.microsoft.com/en-us/dotnet/

---

## ? Checklist de Implementação

- [ ] Ler ARCHITECTURE.md
- [ ] Ler PATTERNS.md
- [ ] Estudar BetnacionalScraping.cs
- [ ] Seguir QUICKSTART.md
- [ ] Testar localmente
- [ ] Consultar TROUBLESHOOTING.md se necessário
- [ ] Fazer commit seguindo convenções

---

## ?? Precisa de Ajuda?

1. **Procure na FAQ**: `TROUBLESHOOTING.md`
2. **Veja um exemplo**: `Core/Sites/Betnacional/`
3. **Consulte padrões**: `PATTERNS.md`
4. **Entenda conceitos**: `ARCHITECTURE.md`

---

## ?? Changelog Recente

### Refatorações Realizadas
- ? `NovibetScraping`: Refatorado para padrão `Betnacional`
- ? `PixbetScraping`: Refatorado para padrão `Betnacional`
- ? Documentação completa criada

### Melhorias
- Estrutura mais clara e consistente
- Padrões bem definidos
- Documentação detalhada
- Exemplos práticos

---

**Última Atualização**: 2024  
**Versão**: 1.0  
**Mantainer**: Romulo Bugas

