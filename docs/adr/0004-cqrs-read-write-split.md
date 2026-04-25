# ADR-0004: CQRS — Separacao de Write e Read Models

- **Status**: Accepted
- **Data**: 2026-04-23
- **Decisores**: Arquiteto de Solucoes

## Contexto

Os dois requisitos funcionais principais tem perfis antagonicos:

- **Registrar lancamento**: write-heavy, pequeno volume por request, precisa de consistencia forte, integridade relacional
- **Consultar saldo diario consolidado**: read-heavy, ate 50 req/s em pico, precisa de latencia baixa (p95 < 300ms)

Usar o **mesmo modelo** para ambos gera tensoes:

- Se otimizamos para escrita (tabela normalizada de transacoes), leituras de saldo fazem `SUM(amount) GROUP BY day` — caro em 50 rps
- Se otimizamos para leitura (tabela agregada), escrita precisa atualizar dois lugares (dual-write) com race conditions
- Indices para queries de relatorio tornam inserts mais lentos

## Decisao

Aplicar **CQRS** em nivel **arquitetural** (nao confundir com CQRS de classes dentro de um servico):

- **Write model**: em `Transactions BC`, banco `cashflow_tx`, tabela `transactions` normalizada, otimizada para inserts ACID
- **Read model**: em `Consolidation BC`, banco `cashflow_cons` separado + Redis, com tabela `daily_balance` pre-agregada

Sincronizacao via eventos `TransactionCreated` / `TransactionReversed` publicados pelo write side e consumidos pelo read side (ver [ADR-0003](0003-event-driven-rabbitmq.md)).

Nao aplicamos **Event Sourcing** (ver [roadmap](../roadmap/future-work.md)) — o write model ainda armazena estado atual, nao derivado de log de eventos. Esta e uma simplificacao deliberada que pode evoluir.

## Consequencias

### Positivas

- **Cada lado otimizado independentemente**: indices, engine de DB, padrao de acesso
- **Latencia p95 < 300ms** viavel no read side (cache Redis + read replicas futuras)
- **Write side livre** para evoluir schema sem afetar leituras (tradutores traduzem eventos)
- **Scale independente**: podemos subir 10 replicas de read API e manter 2 de write API
- **Reconstrucao total do read model** possivel via replay de eventos (preparacao para Event Sourcing futuro)

### Negativas

- **Eventual consistency** (aceitavel pelo dominio, contratual)
- **Complexidade adicional**: dois modelos, dois bancos, tradutores
- **Consumer de eventos precisa ser idempotente** (garantido via `processed_events`)
- **Custo** de 2 bancos em vez de 1 (mitigavel em prod com read replicas do mesmo cluster, V1.1)

### Quando NAO e CQRS apropriado

- CRUDs simples sem assimetria read/write
- Equipe inexperiente com eventual consistency
- Dominios onde read-your-writes e obrigatorio

No nosso caso, o write side **tem** read-your-writes (`GET /transactions/{id}` vai no write store) e o read-only-consolidado **aceita** eventual consistency.

## Topologia de Bancos

| BC | Banco | Estrategia |
|---|---|---|
| Transactions | `cashflow_tx` (Postgres) | Normalizado, indices em `merchantId`, `occurredOn`. Imutavel (append-only). |
| Consolidation | `cashflow_cons` (Postgres) | Pre-agregado em `daily_balance (merchantId, date)`. UPSERT atomico. |
| Consolidation | Redis | Cache write-through `balance:{merchant}:{date}` com TTL 72h. |

## Referencias

- Martin Fowler — [CQRS](https://martinfowler.com/bliki/CQRS.html)
- Greg Young — _CQRS Documents_ (2010)
- [docs/adr/0003-event-driven-rabbitmq.md](0003-event-driven-rabbitmq.md)
