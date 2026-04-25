# ADR-0005: Outbox Pattern para Publicacao Confiavel de Eventos

- **Status**: Accepted
- **Data**: 2026-04-25
- **Decisores**: Arquiteto de Solucoes

## Contexto

No handler de `CreateTransactionCommand`, precisamos de duas acoes conjuntas:

1. `INSERT` na tabela `transactions` (Postgres)
2. `PUBLISH` do evento `TransactionCreated` no RabbitMQ

Cenarios de falha se fizermos ingenuamente:

- **A**: insert sucesso, publish falha (RabbitMQ fora / timeout de rede) -> consolidacao nunca sabe -> **perda de evento**
- **B**: insert falha, publish sucesso -> consolidacao atualiza saldo sem que a transacao exista no Tx -> **fantasma / saldo inconsistente**
- **C**: insert sucesso, publish sucesso, mas erro ao persistir que foi publicado -> **duplicacao** em caso de retry

Este e o classico **Dual-Write Problem**.

## Decisao

Aplicar o **Transactional Outbox Pattern**:

1. Na mesma transacao do `INSERT transactions`, fazer `INSERT outbox_messages(event_type, payload, occurred_at, published_at=null)`
2. Um `BackgroundService` ("OutboxPublisher") faz polling da tabela `outbox_messages WHERE published_at IS NULL`, publica no RabbitMQ com **publish confirms**, e marca `published_at = now()` ao receber confirmacao

### Schema

```sql
CREATE TABLE outbox_messages (
    id              UUID PRIMARY KEY,
    aggregate_type  VARCHAR(100) NOT NULL,
    aggregate_id    UUID NOT NULL,
    event_type      VARCHAR(100) NOT NULL,
    event_version   INTEGER NOT NULL,
    payload         JSONB NOT NULL,
    occurred_at     TIMESTAMPTZ NOT NULL,
    published_at    TIMESTAMPTZ NULL,
    retry_count     INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX idx_outbox_unpublished ON outbox_messages (occurred_at) WHERE published_at IS NULL;
```

### Polling vs. LISTEN/NOTIFY

- **V1**: polling a cada 500ms (simples, previsivel)
- **V1.1 consideracao**: Postgres `LISTEN/NOTIFY` para reduzir latencia (~0ms em carga baixa) — ver [roadmap](../roadmap/future-work.md)

### Publisher confirms

MassTransit configurado com `PublisherConfirmation = true` no RabbitMQ. So marcamos `published_at` apos confirmacao do broker.

## Consequencias

### Positivas

- **Entrega at-least-once garantida**: ou a transacao inteira foi commit (com outbox) ou nenhuma das duas partes aconteceu
- **Atomicidade sem XA/2PC** (2PC e notoriamente problematico)
- **Alinhamento perfeito** com nosso modelo de consumer idempotente em Consolidation
- **Resiliente** a queda do broker (eventos ficam pendentes ate publicador conseguir enviar)

### Negativas

- **Latencia adicional** (~polling interval ms, tipicamente 250ms medio)
- **Tabela pode crescer** -> precisa de job de limpeza (archive >7 dias, delete >30 dias — ver runbook)
- **Polling custa CPU/DB** mesmo quando nao ha eventos -> mitigado com backoff
- **Ordering entre eventos** pode ser violado se multiplos publishers rodam -> mitigado com **single publisher lock** (advisory lock Postgres) ou eleicao de lider

### Comparacao com alternativas

| Opcao | Dual-write safe? | Overhead | Complexidade |
|---|---|---|---|
| Publish dentro da transacao | NAO | baixo | baixa |
| 2PC/XA | Sim | alto | alta |
| **Outbox polling** | **Sim** | **medio** | **media** |
| Change Data Capture (Debezium) | Sim | baixo | alta (infra) |
| Event sourcing | Sim | medio | alta (paradigma) |

Outbox polling e o sweet spot para nosso escopo.

## Referencias

- Chris Richardson — [Transactional Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- Microsoft — [Outbox pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/transactional-outbox-cosmos)
- [MassTransit Transactional Outbox](https://masstransit.io/documentation/patterns/transactional-outbox)
