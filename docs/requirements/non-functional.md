# Requisitos Nao-Funcionais (NFRs) — SLOs e SLIs

> "Nao funcional" nao significa "opcional". Aqui esta a resposta direta aos NFRs do PDF, traduzida em **metricas mensuraveis** (SLI), **metas** (SLO) e **mecanismos de enforcement**.

## Origem de Negócio

> "O servico de controle de lancamento **nao deve ficar indisponivel** se o sistema de consolidado diario cair. Em dias de picos, o servico de consolidado diario recebe **50 requisicoes por segundo**, com no maximo **5% de perda** de requisicoes."

Isto se traduz em 3 NFRs primarios, e derivamos NFRs complementares.

## NFR-001 — Desacoplamento Temporal (Fault Isolation)

**Enunciado**: Transactions API deve permanecer operacional independentemente do estado do Consolidation (Service ou API).

### SLI / SLO

| SLI | SLO |
|---|---|
| `up{service="transactions-api"}` | >= 99.9% em janela de 30 dias |
| Taxa de erro 5xx em Transactions quando Consolidation esta fora | 0% |

### Enforcement (como garantimos)

1. **Comunicacao assincrona** via RabbitMQ (ver [ADR-0003](../adr/0003-event-driven-rabbitmq.md)). Transactions nao espera resposta do Consolidation.
2. **Outbox Pattern** (ver [ADR-0005](../adr/0005-outbox-pattern.md)): eventos sao persistidos na mesma transacao do write principal; um publisher em background os envia ao broker. Mesmo se o RabbitMQ estiver fora, a API responde com sucesso.
3. **Durable queue** no RabbitMQ: mensagens sobrevivem a restart do broker.
4. **Dead Letter Queue (DLQ)** para mensagens que falham repetidamente.
5. **Chaos test**: derrubar `consolidation-service` e validar que Transactions segue em 100% de sucesso. Ver [secao "Prova"](#prova-do-desacoplamento) abaixo.

### Como e medido

Dashboard Grafana `Service Availability` — painel `Transactions Availability` (query PromQL):

```promql
avg_over_time(up{service="transactions-api"}[30d]) * 100
```

## NFR-002 — Throughput e Taxa de Erro Consolidation (Pico)

**Enunciado**: Consolidation deve suportar 50 req/s com maximo de 5% de perda.

### SLI / SLO

| SLI | SLO |
|---|---|
| Throughput sustentado em Consolidation | >= 50 req/s por 5 minutos |
| Taxa de erro (5xx) em pico | <= 5% |
| Latencia p95 em pico | < 500 ms |

### Enforcement

1. **Read model pre-computado** (CQRS, ver [ADR-0004](../adr/0004-cqrs-read-write-split.md))
2. **Cache Redis** como primeiro hop (O(1), ver [ADR-0007](../adr/0007-redis-for-daily-balance.md))
3. **Auto-scale horizontal**: API stateless replicada atras do gateway (min 2, max 10)
4. **Rate limiting** global: 100 req/s por token (headroom sobre o SLO de 50) — previne DoS, nao impede uso legitimo
5. **Connection pooling** (Npgsql, Redis multiplexer compartilhado)

### Prova do SLO de Throughput

Teste de carga executado com **k6** (script em [`load-tests/k6/consolidation-50rps.js`](../../load-tests/k6/)):

```javascript
// 50 req/s por 5 min, rampa 30s
export const options = {
  scenarios: {
    peak: {
      executor: 'constant-arrival-rate',
      rate: 50, timeUnit: '1s',
      duration: '5m', preAllocatedVUs: 50,
    },
  },
  thresholds: {
    'http_req_failed': ['rate<0.05'],           // < 5% erro
    'http_req_duration{status:200}': ['p(95)<500'], // p95 < 500ms
  },
};
```

**Resultado esperado** (a ser atualizado apos execucao real com codigo implementado):

| Metrica | Valor Alvo | Valor Medido | Status |
|---|---|---|---|
| Requests sent | 15.000 (50 rps x 300s) | _a preencher_ | - |
| Success rate | >= 95% | _a preencher_ | - |
| p50 latency | < 100 ms | _a preencher_ | - |
| p95 latency | < 500 ms | _a preencher_ | - |
| p99 latency | < 1000 ms | _a preencher_ | - |
| Error rate | <= 5% | _a preencher_ | - |

Screenshots do dashboard Grafana durante o teste: `docs/requirements/artifacts/k6-grafana-*.png` (gerar apos execucao).

### Prova do Desacoplamento

Cenario de chaos engineering documentado em [`runbook/operations.md`](../runbook/operations.md#chaos-engineering):

```bash
# terminal 1: gerar carga em Transactions
k6 run load-tests/k6/transactions-load.js

# terminal 2: derrubar Consolidation
docker compose stop consolidation-service consolidation-api

# Observar:
# - Transactions continua 100% OK
# - Mensagens acumulam em RabbitMQ (queue size sobe)
# - Alert dispara em 2 min

# Restaurar:
docker compose start consolidation-service consolidation-api

# Observar:
# - service consome backlog em alguns minutos
# - Saldo Consolidation se atualiza
```

## NFR-003 — Durabilidade (Zero Perda)

**Enunciado**: Nenhum lancamento pode ser perdido, mesmo em falhas.

### SLI / SLO

| SLI | SLO |
|---|---|
| RPO (Recovery Point Objective) Write side | 0 |
| Eventos perdidos entre Tx e Consolidation | 0 |

### Enforcement

- Outbox pattern + transacao atomica (write + outbox na mesma `BEGIN/COMMIT`)
- MassTransit com `publish confirms` + queue durable + manual ack
- DLQ com alerta em caso de `rejected > 0`
- Backup diario do Postgres (`pg_dump` em volume + upload S3-compatible em producao)
- Testes de recuperacao trimestrais (chaos game day)

## NFR-004 — Latencia

| Endpoint | p50 | p95 | p99 |
|---|---|---|---|
| `POST /transactions` | < 100 ms | < 500 ms | < 1 s |
| `GET /transactions/{id}` | < 50 ms | < 200 ms | < 500 ms |
| `GET /balance/...` (cache hit) | < 20 ms | < 100 ms | < 300 ms |
| `GET /balance/...` (cache miss) | < 150 ms | < 300 ms | < 500 ms |

Medido via OpenTelemetry histogram em `http_server_request_duration_seconds`.

## NFR-005 — Escalabilidade

- APIs stateless (horizontal scale linear)
- RabbitMQ cluster (3 nos em producao) — versao local: single-node
- Postgres read replicas para Consolidation (V1.1)
- Redis cluster (V1.1) — versao local: single-node

Parametros de scale-out (compose/Kubernetes HPA):

| Servico | Min | Max | Trigger |
|---|---|---|---|
| Transactions API | 2 | 10 | CPU > 60% ou RPS > 100 |
| Consolidation API | 2 | 10 | CPU > 60% ou RPS > 40 |
| Consolidation service | 2 | 8 | queue depth > 500 |

## NFR-006 — Observabilidade

| Area | Meta |
|---|---|
| Cobertura de logs estruturados | 100% dos requests |
| Propagacao de `traceId` | 100% dos saltos (gateway -> api -> db -> broker) |
| Metricas RED (Rate, Errors, Duration) | publicadas por servico |
| Metricas USE (Utilization, Saturation, Errors) | publicadas por recurso (db, broker, redis) |
| Dashboards prontos | 4 (APIs, services, Infra, SLOs) |
| Alertas criticos | 6 (detalhes em [runbook](../runbook/incident-response.md)) |

Ver [ADR-0008](../adr/0008-opentelemetry-standard.md).

## NFR-007 — Seguranca

| Categoria | Controle |
|---|---|
| Transporte | TLS 1.2+ obrigatorio em producao |
| Autenticacao | OAuth2/OIDC via Keycloak, JWT RS256 |
| Autorizacao | Claims-based: `merchantId`, `role` |
| Rate limiting | 100 req/s por token no gateway |
| Input validation | FluentValidation em todos os commands |
| SQL Injection | EF Core parametrizado; sem string concat em SQL |
| Secrets | user-secrets local; Vault/KeyVault em producao |
| Auditoria | Lancamentos append-only; logs imutaveis (Seq retido 90d) |
| LGPD | PII minimo; sem CPF/email no lancamento |

Ver [ADR-0009](../adr/0009-jwt-oauth2-keycloak.md).

## NFR-008 — Testabilidade e Qualidade

| Metrica | Meta |
|---|---|
| Cobertura de testes (dominio) | >= 90% |
| Cobertura geral | >= 70% |
| Build time (CI) | < 5 min |
| Flaky test rate | < 1% |
| Analise estatica | `dotnet format`, analyzers .NET, SonarCloud (opcional) |

## NFR-009 — Manutenibilidade

- ADRs para toda decisao estrutural (ver [docs/adr/](../adr/))
- C4 model atualizado por PR (ver [docs/architecture/](../architecture/))
- Convencoes de commit (Conventional Commits)
- Linguagem ubiqua rigidamente aplicada (ver [ubiquitous-language.md](../domain/ubiquitous-language.md))

## Matriz de Trade-offs (NFR x Decisao)

| Decisao | Beneficia | Compromete |
|---|---|---|
| Microservicos | NFR-001, NFR-005 | NFR-009 (mais pecas moveis) |
| Async messaging | NFR-001, NFR-003 | NFR-004 (latencia do read model, eventual consistency) |
| CQRS | NFR-002, NFR-004 | NFR-009 (dois modelos) |
| Outbox | NFR-003 | NFR-004 (latencia extra de ~100ms para publicar) |
| Redis cache | NFR-002, NFR-004 | NFR-003 (se cache fora de sync — mitigado por TTL + fallback DB) |
| Keycloak | NFR-007, NFR-009 | NFR-005 (IdP e single point) — mitigado com cluster |
