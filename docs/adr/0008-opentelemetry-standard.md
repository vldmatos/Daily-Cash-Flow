# ADR-0008: OpenTelemetry como Padrao de Observabilidade

- **Status**: Accepted
- **Data**: 2026-04-26
- **Decisores**: Arquiteto de Solucoes

## Contexto

Sistemas distribuidos sao dificeis de diagnosticar: uma request atravessa gateway, API, DB, broker, service. Sem ferramental adequado, investigar um problema e adivinhacao.

Uma boa pratica usar **"Monitoramento e Observabilidade"** isso é um diferencial para aplicações. Alem disso, NFR-006 estabelece metas mensuraveis (traceId em 100% dos saltos, dashboards RED+USE, MTTR < 30min).

Alternativas consideradas:

| Opcao | Pros | Contras |
|---|---|---|
| **OpenTelemetry (OTel)** | Padrao CNCF, vendor-neutral, exporta para qualquer backend | Ecosistema ainda maturando em .NET (v1+ GA desde 2022) |
| Application Insights (SDK proprio) | Integrado ao Azure | Lock-in; exportacao para outros backends e limitada |
| Serilog + ELK | Maduro em .NET | Foco em logs (tracing/metrics precisa de mais pecas) |
| Datadog / New Relic agents | Feature-rich out-of-the-box | Caro; lock-in; pouco control sobre dados coletados |

## Decisao

Adotar **OpenTelemetry** como o padrao unico de instrumentacao nos 3 pilares:

### Pilar 1 — Traces

- Sampling: `ParentBased(TraceIdRatioBased(0.1))` em producao (10%); `AlwaysOn` em dev/staging
- Propagacao: W3C Trace Context (header `traceparent`)
- Instrumentacao:
  - ASP.NET Core requests (auto)
  - HttpClient (auto)
  - Npgsql / EF Core (auto)
  - StackExchange.Redis (auto)
  - MassTransit (auto)
  - Outbox publisher (manual `ActivitySource`)
- Exporter: OTLP gRPC -> `otel-collector` -> Seq (local) / Azure Monitor (prod)

### Pilar 2 — Metrics

- Sistema: `Meter` (.NET nativo)
- RED por servico:
  - `http_server_request_duration_seconds` (histogram)
  - `http_server_requests_total{status}` (counter)
- Custom metrics de negocio:
  - `cashflow.transactions.created_total{merchant_id, type}`
  - `cashflow.consolidation.lag_seconds` (saldo nao atualizado vs. ultima transacao)
  - `cashflow.outbox.pending`
  - `cashflow.outbox.publish_duration_seconds`
- Exporter: OTLP -> OTel Collector -> Prometheus

### Pilar 3 — Logs

- Estruturado (JSON), sempre com `traceId` e `spanId`
- Nivel padrao: `Information`; `Debug` habilitavel via config sem redeploy
- Enrichers: `merchantId`, `correlationId`, `userId`, `environment`
- Exporter: OTLP -> OTel Collector -> Seq (com retencao 90 dias local)
- Sem PII em logs (CPF, email, amount e ok; merchantId e ok; descriptions nao sao logadas fullLine)

### Correlacao entre pilares

- `traceId` (W3C) e a chave universal de correlacao
- Seq permite pular de um log para o trace correspondente
- Grafana Explore mostra trace id nos logs com deeplink para Tempo/Seq

## Dashboards Pre-definidos

1. **Service Overview**: uptime, RPS, error rate, p50/p95/p99 por servico
2. **Transactions Detail**: inserts por minuto, outbox pending, tempo de publish
3. **Consolidation Detail**: messages consumed, consumer lag, cache hit ratio, queue depth
4. **Infrastructure**: CPU/mem por container, conn pool Postgres, RabbitMQ queue depth, Redis ops/sec
5. **SLO Dashboard**: SLI atual vs. SLO, error budget burn rate

## Alertas Criticos (Prometheus + Alertmanager)

| # | Alerta | Severity | Threshold |
|---|---|---|---|
| 1 | Transactions API down | critical | `up == 0` por 1 min |
| 2 | Outbox pending > 1000 | high | por 5 min |
| 3 | Queue depth > 5000 | high | por 5 min |
| 4 | Consumer lag > 5 min | high | por 5 min |
| 5 | DLQ count > 0 | high | qualquer momento |
| 6 | p95 latency > 2x baseline | medium | por 10 min |

## Consequencias

### Positivas

- Vendor-neutral: migrar de Seq para Datadog ou Azure Monitor = trocar apenas o `Exporter` no collector
- Custo: inteiramente open-source (exceto em managed services)
- Rico em dados: traces, metrics, logs correlacionados via `traceId`
- Padrao da industria (CNCF graduated)

### Negativas

- Maior overhead de CPU/memoria (~5%) — aceitavel
- Curva de aprendizado para equipe
- OTel Collector e mais um componente a operar (mas muito estavel)

## Referencias

- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)
- [SRE Book - SLOs](https://sre.google/sre-book/service-level-objectives/)
