# Runbook — Incident Response

> Guia de resposta a incidentes mapeado por sintoma. Cada secao tem: como detectar, como diagnosticar, como mitigar, como resolver, como prevenir.

## Severidades

| Severidade | Definicao | SLA de resposta |
|---|---|---|
| **SEV1** | Sistema indisponivel ou perda de dados | 15 min |
| **SEV2** | Degradacao critica (SLO em risco) | 30 min |
| **SEV3** | Degradacao contida (single feature) | 4 h |
| **SEV4** | Cosmetico ou melhoria | 1 business day |

## Alertas Configurados (Prometheus -> Alertmanager -> Slack/Email)

| # | Alerta | Severity | Ver secao |
|---|---|---|---|
| 1 | Transactions API down | SEV1 | [INC-001](#inc-001-transactions-api-down) |
| 2 | Outbox pending > 1000 | SEV2 | [INC-002](#inc-002-outbox-nao-drena) |
| 3 | Queue depth > 5000 | SEV2 | [INC-003](#inc-003-consolidation-atrasado) |
| 4 | Consumer lag > 5 min | SEV2 | [INC-003](#inc-003-consolidation-atrasado) |
| 5 | DLQ count > 0 | SEV2 | [INC-004](#inc-004-mensagens-em-dlq) |
| 6 | p95 latency > 2x baseline | SEV3 | [INC-005](#inc-005-latencia-alta) |
| 7 | Cache miss rate > 30% | SEV3 | [INC-006](#inc-006-cache-miss-alto) |
| 8 | Postgres connection pool saturado | SEV2 | [INC-007](#inc-007-conn-pool-saturado) |

## INC-001: Transactions API down

### Detectar

- Alerta `up{service="transactions-api"} == 0`
- Merchants reportam erro 502/503 no gateway

### Diagnosticar

```bash
# Pods/containers vivos?
docker compose ps transactions-api
kubectl get pods -l app=transactions-api   # em k8s

# Logs recentes
docker compose logs --tail=200 transactions-api

# Health endpoints
curl -v http://localhost:5001/health/ready
```

### Causas comuns

| Sintoma no log | Causa provavel | Mitigacao imediata |
|---|---|---|
| `Could not connect to Npgsql` | Postgres fora | reiniciar postgres; ver INC-007 |
| `OutOfMemoryException` | memory leak / config limite | restart + rollback de ultima versao |
| `Listener failed to bind` | porta ocupada / cert invalido | validar env vars |
| Logs vazios, container reinicia | liveness probe falhando | checar `appsettings` |

### Mitigar

- Rollback para ultima versao saudavel (`docker compose up -d transactions-api:previous-tag`)
- Se falha de deploy: `kubectl rollout undo deployment/transactions-api`

### Resolver

- Root cause no log -> aplicar fix em PR
- Post-mortem com acoes preventivas

### Prevenir

- Canary deploys
- Smoke tests apos deploy
- Memory profiling em QA

## INC-002: Outbox nao drena

### Detectar

- Alerta `cashflow_outbox_pending > 1000`
- Dashboard mostra barra subindo

### Diagnosticar

```bash
docker compose exec postgres-tx psql -U cashflow -d cashflow_tx -c \
"SELECT COUNT(*), MIN(occurred_at) FROM outbox_messages WHERE published_at IS NULL;"

docker compose logs --tail=100 transactions-api | grep -i outbox
```

### Causas comuns

| Sintoma | Causa |
|---|---|
| Erro `publish timed out` nos logs | RabbitMQ fora ou lento |
| `PublisherConfirms timeout` | rede / broker sobrecarregado |
| Tabela cresce mas servico esta "OK" | publisher nao subiu (BackgroundService exception) |

### Mitigar

- Garantir RabbitMQ saudavel: `docker compose ps rabbitmq`
- Restart Transactions API: `docker compose restart transactions-api`

### Resolver

- Ver INC-007 se RabbitMQ sobrecarregado (scale cluster)
- Se exception no publisher: fix + deploy

### Prevenir

- Healthcheck dedicado do publisher (e.g., expor ultimo `publish_duration`)
- Alertas em stage anterior (`> 100` pendentes por 5 min = SEV3)

## INC-003: Consolidation atrasado (consumer lag)

### Detectar

- Alerta `cashflow_consolidation_lag_seconds > 300`
- Merchants relatam "saldo nao atualizou"

### Diagnosticar

```bash
# queue sizes
docker compose exec rabbitmq rabbitmqctl list_queues name messages consumers | grep consolidation

# service logs
docker compose logs --tail=200 consolidation-service

# consumer connections
# abrir http://localhost:15672 -> Connections
```

### Causas comuns

| Sintoma | Causa | Acao |
|---|---|---|
| `consumers = 0` | service fora | restart / scale up |
| `consumers > 0` mas backlog cresce | service lento (CPU? DB?) | scale horizontal; investigar lentidao |
| `deliver/sec = 0` com consumers > 0 | prefetch bloqueado / deadlock | restart service; ver log |

### Mitigar

```bash
# subir mais replicas do service
docker compose up -d --scale consolidation-service=4
```

### Resolver

- Identificar bottleneck (CPU, DB lock, Redis)
- Tunar prefetch count, parallelism no MassTransit
- Adicionar indices no read DB se query estiver lenta

## INC-004: Mensagens em DLQ

### Detectar

- Alerta `rabbitmq_queue_messages{queue=~".*dlq.*"} > 0`

### Diagnosticar

1. Abra RabbitMQ Mgmt -> a queue DLQ -> Get Messages (Ack Mode: Nack & requeue)
2. Ver o payload e o header `x-death` (lista de tentativas)
3. Ver logs do service proximos ao timestamp: `docker compose logs consolidation-service --since 1h`

### Causas comuns

| Erro | Causa | Acao |
|---|---|---|
| `DbUpdateException UNIQUE` | evento ja processado (race) | ignorar / ack — idempotencia ok |
| Deserializacao falhou | mudanca de schema sem versionamento | corrigir consumer ou publisher |
| Timeout em Postgres | DB congelado | ver INC-007 |
| `NullReferenceException` em handler | bug | fix + reprocessar DLQ |

### Mitigar

- Se bug conhecido + corrigido: reprocessar DLQ (move via RabbitMQ UI)
- Se payload corrompido: documentar e dropar manualmente

### Prevenir

- Testes de contrato entre publisher/consumer
- Versionamento de schema (`eventVersion`)
- Consumer sempre idempotente + validar payload antes de processar

## INC-005: Latencia alta

### Detectar

- Alerta `http_server_request_duration_seconds{quantile="0.95"} > 2x baseline`

### Diagnosticar

- Dashboard Grafana `Service Overview` -> servico impactado
- Verificar pilares: CPU/mem (Infra dash), DB (pg_stat_activity), cache hit ratio

### Causas comuns (cada uma com acao)

| Causa | Evidencia | Mitigacao |
|---|---|---|
| Carga acima do esperado | RPS subiu | scale-out, investigar abuso |
| DB lento | pg_stat_activity mostra locks/long queries | kill query, review indices |
| Redis lento | Redis SLOWLOG | verificar comandos `KEYS *` (proibidos), upgrade |
| GC pressure | CPU 100% intermitente | memory profiling, upgrade SKU |
| Cold start | primeiro request pos-deploy | warm-up probe |

## INC-006: Cache miss alto

### Detectar

- Alerta `cache_miss_rate{service="consolidation-api"} > 0.30`

### Diagnosticar

```bash
# chaves em redis para um merchant
docker compose exec redis redis-cli --scan --pattern 'balance:<merchantId>:*' | wc -l

# memoria
docker compose exec redis redis-cli INFO memory | grep used_memory_human
```

### Causas comuns

| Causa | Acao |
|---|---|
| Redis sem memoria, evictando | aumentar `maxmemory` / upgrade |
| TTL muito curto | ajustar de 60s -> 300s |
| service nao populando cache | ver logs; garantir ordem DB->Redis |
| FLUSHDB acidental | repopulacao automatica via fallback |

## INC-007: Connection pool saturado

### Detectar

- Alerta `npgsql_connection_pool_available < 2` por 2 min

### Diagnosticar

```bash
docker compose exec postgres-tx psql -U cashflow -d cashflow_tx -c \
"SELECT state, count(*) FROM pg_stat_activity WHERE datname='cashflow_tx' GROUP BY state;"
```

### Causas comuns

- Conexoes "idle in transaction" (codigo nao commita)
- Pool muito pequeno (`MaxPoolSize=50` default)
- DB lento -> conexoes ficam presas

### Mitigar

- Kill conexoes antigas: `SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE state='idle in transaction' AND state_change < NOW() - INTERVAL '5 min';`
- Aumentar pool (`MaxPoolSize=100`) em config

### Resolver

- Review codigo para `using` ausente ou transacoes longas
- Considerar PgBouncer (transaction pooling) em producao

## Template de Post-Mortem

Apos qualquer SEV1/SEV2, documentar em `docs/runbook/post-mortems/YYYY-MM-DD-summary.md`:

```markdown
# Post-Mortem YYYY-MM-DD — <titulo>

## Summary
<1 paragrafo>

## Impact
- Duracao: HH:MM a HH:MM (X min)
- Usuarios impactados: N
- SLO afetado: ___

## Timeline (UTC)
- HH:MM: detectado via ___
- HH:MM: on-call engaged
- HH:MM: mitigado
- HH:MM: resolvido

## Root Cause
<tecnica + 5 whys>

## What went well
- ___

## What went wrong
- ___

## Action items
- [ ] owner: prazo: ___ (preventivo)
- [ ] owner: prazo: ___ (detective)
- [ ] owner: prazo: ___ (corretivo)
```
