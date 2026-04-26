# C4 - Nivel 3: Componentes

> O diagrama de componentes abre um container especifico em seus componentes internos (classes/modulos). Mostramos os dois containers mais relevantes: **Transactions API** e **Consolidation Service**.

## Transactions API — Componentes

```mermaid
flowchart TB
    ext[Incoming HTTP<br/>via Gateway]

    subgraph txApi [Transactions API Container]
        direction TB
        endpoint["TransactionsEndpoints<br/><i>[Minimal API]</i><br/>POST / GET / reverse"]
        idMw["IdempotencyMiddleware<br/><i>[Middleware]</i>"]
        authMw["JwtAuthMiddleware<br/><i>[Middleware]</i>"]
        mediator["IMediator<br/><i>[MediatR]</i>"]

        subgraph application [Application Layer]
            cmd[CreateTransactionHandler]
            revCmd[ReverseTransactionHandler]
            qry[GetTransactionHandler]
            validator[FluentValidation Validators]
        end

        subgraph domain [Domain Layer]
            agg["Transaction<br/><i>[Aggregate Root]</i>"]
            vo["Amount, Currency, TxType<br/><i>[Value Objects]</i>"]
            events[TransactionCreated,<br/>TransactionReversed<br/>&lpar;Domain Events&rpar;]
            spec[TransactionSpecifications]
        end

        subgraph infra [Infrastructure Layer]
            repo["TransactionRepository<br/><i>[EF Core]</i>"]
            outbox["OutboxWriter<br/><i>[EF Core]</i>"]
            publisher["OutboxPublisher<br/><i>[BackgroundService]</i>"]
            idemStore["IdempotencyStore<br/><i>[EF Core]</i>"]
        end

        db[(Postgres)]
        broker{{RabbitMQ}}
    end

    ext --> authMw --> idMw --> endpoint
    endpoint --> mediator
    mediator --> cmd
    mediator --> revCmd
    mediator --> qry
    cmd --> validator
    revCmd --> validator
    cmd --> agg
    revCmd --> agg
    agg --> events
    cmd --> repo
    cmd --> outbox
    repo --> db
    outbox --> db
    idemStore --> db
    publisher --> db
    publisher --> broker
    idMw --> idemStore
```

### Responsabilidades

| Componente | Responsabilidade |
|---|---|
| `TransactionsEndpoints` | Define rotas, mapeia DTOs, traduz excecoes para `ProblemDetails` |
| `JwtAuthMiddleware` | ASP.NET Core nativo + `[Authorize]` |
| `IdempotencyMiddleware` | Consulta `idempotency_keys`; se hit, retorna resposta armazenada |
| `CreateTransactionHandler` | Orquestra: valida, cria aggregate, persiste + outbox em transacao |
| `Transaction` (Aggregate) | Invariantes de dominio; metodos `Create()`, `Reverse()` |
| `Amount`, `Currency`, `TxType` | Value Objects imutaveis |
| `TransactionRepository` | Abstracao sobre EF Core `DbContext` |
| `OutboxWriter` | Escreve evento serializado na tabela `outbox_messages` na **mesma transacao** do aggregate |
| `OutboxPublisher` | `BackgroundService` que ponta a tabela outbox (polling a cada 500ms ou listen/notify) e publica no RabbitMQ com `publish confirms`, marcando como processado |
| `IdempotencyStore` | Repositorio de `idempotency_keys` com TTL 24h |

### Sequencia: criar lancamento

```mermaid
sequenceDiagram
    participant C as Client
    participant G as Gateway
    participant API as Transactions API
    participant DB as Postgres
    participant P as OutboxPublisher
    participant MQ as RabbitMQ

    C->>G: POST /transactions (JWT, Idempotency-Key)
    G->>API: forward + claims
    API->>DB: BEGIN
    API->>DB: SELECT idempotency_keys WHERE key=...
    alt key existe
        DB-->>API: response cached
        API-->>C: 200 OK (resposta original)
    else key nao existe
        API->>API: Transaction.Create(...)
        API->>DB: INSERT transactions
        API->>DB: INSERT outbox_messages
        API->>DB: INSERT idempotency_keys(response)
        API->>DB: COMMIT
        API-->>C: 201 Created
        Note over P,MQ: assincrono
        P->>DB: SELECT * FROM outbox WHERE published_at IS NULL
        P->>MQ: publish(TransactionCreated)
        MQ-->>P: ack
        P->>DB: UPDATE published_at
    end
```

## Consolidation Service — Componentes

```mermaid
flowchart TB
    broker{{RabbitMQ}}

    subgraph Service [Consolidation Service Container]
        direction TB

        subgraph mt [MassTransit]
            consumerCreated[TransactionCreatedConsumer]
            consumerReversed[TransactionReversedConsumer]
            retry[RetryPolicy<br/>exp 3x]
            dlq[DeadLetterQueue]
        end

        subgraph application [Application]
            orch[UpdateDailyBalanceHandler]
            idem[IdempotencyGuard]
        end

        subgraph domain [Domain]
            rm["DailyBalance<br/><i>[Read Model]</i>"]
        end

        subgraph infra [Infrastructure]
            repoRm["DailyBalanceRepository<br/><i>[Dapper]</i>"]
            redis["DailyBalanceCache<br/><i>[StackExchange.Redis]</i>"]
            processedRepo["ProcessedEventsStore<br/><i>[Dapper]</i>"]
        end

        consDb[(Postgres<br/>Read DB)]
        cache[(Redis)]
    end

    broker --> consumerCreated
    broker --> consumerReversed
    consumerCreated --> retry --> orch
    consumerReversed --> retry --> orch
    retry -. max retry .-> dlq
    orch --> idem
    idem --> processedRepo
    orch --> rm
    orch --> repoRm
    orch --> redis
    repoRm --> consDb
    processedRepo --> consDb
    redis --> cache
```

### Responsabilidades

| Componente | Responsabilidade |
|---|---|
| `TransactionCreatedConsumer` | Deserializa envelope, converte em comando interno |
| `RetryPolicy` | MassTransit: `UseMessageRetry(r => r.Exponential(...))` |
| `DeadLetterQueue` | Queue `*.dlq` + alert em Grafana |
| `UpdateDailyBalanceHandler` | Calcula delta, aplica em Postgres + Redis |
| `IdempotencyGuard` | Se `event_id` ja em `processed_events`, skip |
| `DailyBalance` (Read Model) | Value record com totais |
| `DailyBalanceRepository` | `UPSERT` em `daily_balance` (Postgres) |
| `DailyBalanceCache` | `HINCRBY balance:{m}:{d} total_credits <delta>` |

### Sequencia: consumir TransactionCreated

```mermaid
sequenceDiagram
    participant MQ as RabbitMQ
    participant W as Service
    participant G as IdempotencyGuard
    participant DB as Postgres Read
    participant R as Redis

    MQ->>W: deliver(TransactionCreated)
    W->>G: processed?
    G->>DB: SELECT event_id FROM processed_events
    alt ja processado
        G-->>W: sim
        W->>MQ: ack (skip)
    else novo
        W->>DB: BEGIN
        W->>DB: UPSERT daily_balance (atomic delta)
        W->>DB: INSERT processed_events(event_id)
        W->>DB: COMMIT
        W->>R: HINCRBY balance:m:d ...
        W->>MQ: ack
    end
```

## Consolidation API — Componentes

```mermaid
flowchart TB
    ext[Incoming HTTP]

    subgraph api [Consolidation API]
        endpoint[BalanceEndpoints]
        handler[GetDailyBalanceHandler]
        cacheSvc[CachedBalanceService]
        repo["BalanceRepository<br/><i>[Dapper]</i>"]
    end

    cache[(Redis)]
    db[(Postgres Read)]

    ext --> endpoint --> handler --> cacheSvc
    cacheSvc -- hit --> cache
    cacheSvc -- miss --> repo --> db
    repo -. populate .-> cache
```

**Politica cache-aside**:

1. GET na chave `balance:{m}:{d}`
2. Se hit: retornar
3. Se miss: consultar DB, popular cache com TTL 60s, retornar

## Principios de Design Aplicados

- **Dependency Inversion**: dominio nao conhece infra; interfaces em `Domain`, implementacoes em `Infrastructure`
- **Hexagonal Architecture** (Ports & Adapters): APIs sao adapters do dominio
- **Single Responsibility**: handlers fazem uma coisa (um command -> um handler)
- **Explicit Architecture**: dependencia de camadas visivel no grafo: API -> Application -> Domain <- Infrastructure
- **Command/Query Separation**: comandos nao retornam dados de dominio (apenas `Id` gerado); queries nao mutam
