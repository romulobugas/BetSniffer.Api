# BetSniffer.Ia (skeleton)

Projeto de agente visual **sem WebDriver**, pensado para rodar no Windows e reaproveitar endpoints do `BetSniffer.Api`.

## Objetivo

- Abrir múltiplas janelas de navegador via comando externo/SO.
- Capturar imagem da janela.
- Consultar modelo visual (`Qwen2.5-VL 7B`) via LM Studio.
- Receber ação em JSON (`click`, `scroll`, `extract`, etc.).
- Executar input nativo (mouse/teclado) no Windows.
- Enviar mercados extraídos para API.

## Contrato JSON da IA

Resposta esperada:

```json
{
  "action": "Click",
  "confidence": 0.94,
  "reason": "Botão 'Mais mercados' identificado",
  "target": { "x": 0.81, "y": 0.33 },
  "scrollDelta": 0,
  "textInput": null,
  "key": null,
  "markets": []
}
```

- `x` e `y` são relativos à janela (`0.0` a `1.0`).
- Quando ação for `ExtractMarkets`, preencher `markets`.

## LM Studio vs integração nativa

### Opção recomendada para começar: LM Studio

- Sem acoplar engine de inferência no código C#.
- API compatível com OpenAI (`/v1/chat/completions`).
- Troca de modelo sem recompilar.

### Opção avançada: inferência nativa no código

- ONNX Runtime / TensorRT / bindings Python.
- Menor latência potencial e mais controle de fila.
- Mais complexidade operacional.

Para MVP, mantenha LM Studio e evolua para inferência nativa só após validar fluxo multi-janela.

## Endpoints reaproveitados do BetSniffer.Api

- `GET /api/BatchScraping/sites` para validar suporte por casa.
- `POST /api/ia/markets` (sugerido neste skeleton) para ingestão dos mercados extraídos.

> Observação: `POST /api/ia/markets` ainda precisa ser implementado no `BetSniffer.Api`.

## Execução

```bash
dotnet run --project BetSniffer.Ia/BetSniffer.Ia.csproj
```

Arquivos de configuração:

- `appsettings.json`
- `jobs.sample.json`
