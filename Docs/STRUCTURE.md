# ??? Estrutura da Documentação

## ?? Organização de Arquivos

```
BetSniffer.Api/
??? Docs/                                    # ?? DOCUMENTAÇÃO TÉCNICA
?   ??? README.md                           # ?? Ponto de entrada
?   ??? ARCHITECTURE.md                     # ??? Visão geral + conceitos
?   ??? PATTERNS.md                         # ?? Padrões de implementação
?   ??? QUICKSTART.md                       # ?? Guia prático passo-a-passo
?   ??? TROUBLESHOOTING.md                  # ?? FAQ + erros comuns
?   ??? CHANGELOG.md                        # ?? Resumo de refatorações
?   ??? STRUCTURE.md                        # ?? Este arquivo
?
??? Core/
?   ??? Sites/
?   ?   ??? Betnacional/
?   ?   ?   ??? BetnacionalScraping.cs      # ? MODELO (padrão)
?   ?   ?   ??? BetnacionalTags.cs
?   ?   ??? Novibet/
?   ?   ?   ??? NovibetScraping.cs          # ? REFATORADO
?   ?   ?   ??? NovibetTags.cs
?   ?   ??? Pixbet/
?   ?   ?   ??? PixbetScraping.cs           # ? REFATORADO
?   ?   ?   ??? PixbetTags.cs
?   ?   ??? ... (outros sites)
?   ??? Services/
?   ??? Interfaces/
?   ??? ...
?
??? Program.cs
??? BetSniffer.Api.csproj
??? ...
```

---

## ?? Mapa Mental de Navegação

```
???????????????????????????????????????????
?        ?? Comece Aqui: README.md        ?
?         "O que é e como usar?"          ?
???????????????????????????????????????????
               ?
        ???????????????
        ?             ?
        ?             ?
   ???????????   ????????????????
   ? NOVO    ?   ? JÁ CONHEÇO   ?
   ? DEVELOPER?   ? O PROJETO    ?
   ???????????   ????????????????
        ?                ?
        ?                ???? PATTERNS.md
        ?                ?    (padrões)
        ?                ?
        ?                ???? TROUBLESHOOTING.md
        ?                     (problemas)
        ?
        ???? ARCHITECTURE.md (20 min)
        ?    "Entender a estrutura"
        ?
        ???? QUICKSTART.md (45 min)
        ?    "Implementar novo scraper"
        ?
        ???? Estudar código
             BetnacionalScraping.cs
             (15 min)
```

---

## ?? Fluxo de Aprendizado Recomendado

### ?? Iniciante (Sem Conhecimento do Projeto)
```
Tempo Total: ~2 horas

1. README.md (10 min)
   ?? Objetivo: Visão geral

2. ARCHITECTURE.md (20 min)
   ?? Objetivo: Entender componentes

3. Estudar Código (15 min)
   ?? BetnacionalScraping.cs (modelo)

4. PATTERNS.md (30 min)
   ?? Objetivo: Aprender padrões

5. QUICKSTART.md (45 min)
   ?? Objetivo: Praticar com passo-a-passo

? Resultado: Pronto para implementar novo scraper
```

### ?? Intermediário (Conhece um Scraper)
```
Tempo Total: ~1 hora

1. ARCHITECTURE.md (rápida review) (5 min)

2. PATTERNS.md (primeira leitura) (30 min)

3. Comparar com código modelo (15 min)
   ?? BetnacionalScraping.cs vs Seu Código

4. QUICKSTART.md (referência) (10 min)

? Resultado: Pronto para refatorar
```

### ?? Avançado (Mantém Vários Scrapers)
```
Tempo Total: ~30 min

1. CHANGELOG.md (Mudanças recentes) (10 min)

2. PATTERNS.md (padrões atualizados) (15 min)

3. TROUBLESHOOTING.md (referência rápida) (5 min)

? Resultado: Atualizado com padrões
```

---

## ?? Estrutura de Cada Documento

### README.md
```
?? ?? Guias Disponíveis
?? ?? Começar Por Onde?
?? ?? Estrutura Rápida
?? ?? Conceitos-Chave
?? ?? Padrão Recomendado
?? ??? Stack Técnico
?? ?? Tabela de Conteúdos
?? ?? Quick Commands
?? ?? Tips
?? ?? Referências
```

### ARCHITECTURE.md
```
?? ?? Estrutura de Diretórios
?? ?? Fluxo de Execução Padrão (5 etapas)
?? ?? Padrão de Implementação (7 pontos)
?? ??? Sistema de Tags
?? ?? Fluxo de Dados (diagrama)
?? ??? Tratamento de Erros
?? ?? Entidades Principais
?? ?? Validações Importantes
?? ?? Boas Práticas (fazer/evitar)
?? ?? Performance
?? ?? Referências
```

### PATTERNS.md
```
?? 1?? Estrutura de Arquivo (Template)
?? 2?? Região de Variáveis Globais
?? 3?? Método ScrapeTags
?? 4?? Métodos Auxiliares (6 métodos)
?? 5?? Construtor
?? 6?? Tratamento de Erros
?? 7?? Delays e Aleatoriedade
?? 8?? Logging e Console
?? 9?? Checklist
?? ?? Exemplo Mínimo Funcional
```

### QUICKSTART.md
```
?? ?? Pré-requisitos
?? 1?? Criar Arquivo de Scraping
?? 2?? Criar Arquivo de Tags
?? 3?? Mapear Seletores CSS
?? 4?? Implementar ExtractGameInfo
?? 5?? Implementar SaveOrUpdateGame
?? 6?? Implementar ProcessMarkets
?? 7?? Implementar SaveBets
?? 8?? Implementar ParseGameDateTime
?? 9?? Testar
?? ?? Registrar no DI Container
?? ? Checklist Final
```

### TROUBLESHOOTING.md
```
?? 15+ Perguntas Frequentes
?? 10+ Erros Comuns com Soluções
?? Performance (3 problemas)
?? Testes (exemplos)
?? Logging Avançado
?? Debugging (técnicas)
?? Referências
?? ?? Seções por Problema
```

### CHANGELOG.md
```
?? ?? Refatorações Realizadas (2 arquivos)
?? ?? Documentação Criada (5 docs)
?? ?? Estatísticas
?? ?? Modelos de Referência
?? ?? Arquivos Modificados
?? ? Melhorias Implementadas
?? ?? Próximas Melhorias
?? ? Checklist de Validação
?? ?? Aprendizados
```

---

## ?? Casos de Uso por Documento

| Caso de Uso | Documento Principal | Secundário | Tempo |
|-------------|-------------------|-----------|-------|
| Novo no projeto | ARCHITECTURE | README | 20 min |
| Implementar novo | QUICKSTART | PATTERNS | 45 min |
| Refatorar existente | PATTERNS | ARCHITECTURE | 30 min |
| Resolver erro | TROUBLESHOOTING | PATTERNS | 10 min |
| Code review | PATTERNS | ARCHITECTURE | 15 min |
| Onboard novo dev | README ? ARCHITECTURE ? QUICKSTART | - | 90 min |
| Problema específico | TROUBLESHOOTING | PATTERNS | 5-15 min |

---

## ?? Tamanho e Escopo

### Por Documento
```
README.md          ~300 linhas     (visão geral)
ARCHITECTURE.md   ~2500 linhas    (completo)
PATTERNS.md       ~2800 linhas    (muito detalhado)
QUICKSTART.md     ~1500 linhas    (passo-a-passo)
TROUBLESHOOTING.md ~1000 linhas   (FAQ + problemas)
CHANGELOG.md       ~400 linhas    (resumo)
STRUCTURE.md       ~400 linhas    (este arquivo)
?????????????????????????????????
TOTAL             ~9000 linhas    (documentação completa)
```

### Cobertura de Tópicos
```
? Arquitetura geral          ? ARCHITECTURE
? Padrões de código          ? PATTERNS
? Guia prático               ? QUICKSTART
? Resolução de problemas     ? TROUBLESHOOTING
? Histórico de mudanças      ? CHANGELOG
? Navegação                  ? README
? Estrutura e índice         ? STRUCTURE (este)
```

---

## ?? Como Encontrar o Que Precisa

### Busca Rápida

**"Como implementar novo scraper?"**
? QUICKSTART.md

**"Qual é o padrão de SaveBets?"**
? PATTERNS.md ? Seção 4D

**"Por que meu seletor não funciona?"**
? TROUBLESHOOTING.md ? Erro "TimeoutException"

**"Qual é a estrutura do projeto?"**
? ARCHITECTURE.md ? Estrutura de Diretórios

**"O que foi refatorado?"**
? CHANGELOG.md

**"Por que delay aleatório?"**
? PATTERNS.md ? Seção 7 ou ARCHITECTURE.md ? Boas Práticas

**"Como fazer code review?"**
? PATTERNS.md ? Checklist de Implementação

**"Preciso rodar testes"**
? README.md ? Quick Commands

---

## ?? Dicas de Navegação

### Use Cmd+F (Ctrl+F) para buscar:
- "Não encontrado" ? TROUBLESHOOTING.md
- "PixbetScraping" ? CHANGELOG.md
- "SaveBets" ? PATTERNS.md
- "Fluxo de execução" ? ARCHITECTURE.md

### Bookmarks Recomendados:
```
?? ARCHITECTURE.md ? Padrão de Implementação
?? PATTERNS.md ? Método SaveBets
?? QUICKSTART.md ? Passo 6 (Processar Mercados)
?? TROUBLESHOOTING.md ? Perguntas Frequentes
```

### Atalhos Úteis:
```
?? README.md       - Comece aqui sempre
???  ARCHITECTURE.md - Quando quer entender
?? PATTERNS.md     - Quando quer implementar
?? QUICKSTART.md   - Quando está fazendo
?? TROUBLESHOOTING - Quando tem erro
```

---

## ?? Convenções de Leitura

### Símbolos Usados
```
?  Implementado / Feito
?  Não implementado / Evitar
??  Atenção / Cuidado
??  Importante / Fixar
??  Dica / Insight
??  Crítico / Não ignore
??  Negócio / Contexto
```

### Cores de Alerta
```
?? Verde   = OK / Implementado
?? Amarelo = Aviso / Cuidado
?? Vermelho= Erro / Não faça
```

### Exemplos
```
? Sempre valide entrada
? Não inicialize browser no construtor
?? Erros críticos devem relançar
?? Deduplicação é por GameId + TagId
?? Use GetAwaiter().GetResult() para sync
?? NÃO ignore erros de banco
```

---

## ?? Próximos Passos

### Se Você é Novo Dev
```
1. Leia README.md (10 min)
   ?
2. Leia ARCHITECTURE.md (20 min)
   ?
3. Estude BetnacionalScraping.cs (15 min)
   ?
4. Comece com QUICKSTART.md
```

### Se Quer Implementar
```
1. Abra QUICKSTART.md lado-a-lado
   ?
2. Siga cada passo
   ?
3. Consulte PATTERNS.md conforme necessário
   ?
4. Use TROUBLESHOOTING se tiver erro
```

### Se Quer Refatorar
```
1. Leia PATTERNS.md (seção relevante)
   ?
2. Compare com BetnacionalScraping.cs
   ?
3. Aplique mudanças
   ?
4. Valide com checklist
```

---

## ? Conclusão

Esta documentação foi projetada para ser:
- ? **Completa**: Cobre todos os aspecto
- ? **Prática**: Com exemplos reais
- ? **Fácil de Navegar**: Índices e links
- ? **Bem Organizada**: Estrutura clara
- ? **Atualizada**: Reflete código atual

**Tempo de leitura estimado por perfil:**
- ?? Novo dev: 2-3 horas
- ?? Dev intermediário: 1 hora
- ?? Dev avançado: 30 minutos

---

**Última Atualização**: 2024  
**Versão**: 1.0  
**Mantainer**: Romulo Bugas

