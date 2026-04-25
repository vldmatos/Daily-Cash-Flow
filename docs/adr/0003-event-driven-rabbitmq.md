# ADR-0003: Comunicacao Event-Driven com RabbitMQ

- **Status**: Accepted
- **Data**: 2026-04-25
- **Decisores**: Arquiteto de Solucoes

## Contexto

Escolhido microservicos (ver [ADR-0002](0002-microservices-over-monolith.md)), precisamos de um mecanismo de comunicacao entre Transactions e Consolidation que:

1. Garanta **desacoplamento temporal** (Tx nao depende de Cons estar no ar)
2. Garanta **entrega durable** (mensagens sobrevivem a crash do consumer)
3. Suporte o throughput de pico (50 req/s) com folga
4. Seja **portavel** (podemos trocar por cloud-managed em producao sem reescrever codigo)

Opcoes avaliadas:

### 1. RabbitMQ

- Pros: maturidade, AMQP 0.9.1, custo zero (OSS), DLQ, rotas flexiveis (topic, fanout), fast enough para o SLO
- Contras: throughput limitado (~50k msg/s/node) comparado a Kafka; operacao de cluster tem suas peculiaridades

### 2. Apache Kafka

- Pros: throughput gigante, retention nativo (replay)
- Contras: overkill para o SLO; complexidade operacional (Zookeeper/KRaft, particoes, consumer groups); modelo pub/sub e diferente (ack via offset)

### 3. Redis Streams

- Pros: ja temos Redis
- Contras: menos features (sem DLQ nativo, retention manual); risco de misturar papeis (cache + broker); menos maturidade em cenarios de producao

### 4. Azure Service Bus / AWS SQS+SNS (managed)

- Pros: gerenciado, SLA do cloud provider, DLQ nativo
- Contras: lock-in em cloud; dificulta dev local; latencia em dev

## Decisao

Usar **RabbitMQ** via **MassTransit** localmente e em staging. Em producao, trocar por **Azure Service Bus** (mesmo `MassTransit`, bastam ~5 linhas de config).

### Topologia

- Exchange: `cashflow.events` (type: `topic`)
- Routing keys: `transaction.created`, `transaction.reversed`
- Queues:
  - `consolidation.transaction.created` (bind `transaction.created`)
  - `consolidation.transaction.reversed` (bind `transaction.reversed`)
- DLQ: `consolidation.*.dlq`
- Durable: `true`; auto-delete: `false`; ack mode: `manual`

### Padroes aplicados

- **Outbox** no publisher (ver [ADR-0005](0005-outbox-pattern.md))
- **Idempotent consumer** no subscriber (tabela `processed_events`)
- **Retry exponencial** no consumer (3 tentativas: 2s, 8s, 32s) -> DLQ
- **Publish confirms** no publisher (garantia de entrega ao broker)

## Consequencias

### Positivas

- Desacoplamento temporal atendendo NFR-001
- DLQ nativo com dashboard integrado
- Portabilidade via MassTransit (RabbitMQ -> Azure Service Bus quase transparente)
- Throughput do RabbitMQ (50k msg/s) e ordens de grandeza acima do SLO (50 req/s)

### Negativas

- Operar RabbitMQ em producao tem curva de aprendizado (memoria, shovel, federation) — resolvido com managed service (ASB) em prod
- Erlang VM: perfil de uso de memoria as vezes confuso
- Ordering garantida apenas por partition key — no nosso caso, usamos `merchantId` como routing para evitar problemas de ordering cross-merchant. Porem, ordering **dentro** do service para o mesmo `merchantId` ainda exige cuidado (ver runbook).

### Trade-offs explicitos

- **Eventual consistency**: consolidacao pode estar ate 30s atras da realidade. Dominio aceita.
- **At-least-once**: pode haver reprocessamento; consumer **deve** ser idempotente (garantido por `processed_events` unique constraint).

## Referencias

- [MassTransit docs](https://masstransit.io/)
- [RabbitMQ Reliability Guide](https://www.rabbitmq.com/reliability.html)
- [docs/adr/0005-outbox-pattern.md](0005-outbox-pattern.md)
