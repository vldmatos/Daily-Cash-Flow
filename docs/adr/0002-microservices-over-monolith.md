# ADR-0002: Microservicos sobre Monolito

- **Status**: Accepted
- **Data**: 2026-04-25
- **Decisores**: Arquiteto de Solucoes

## Contexto

O desafio exige que o **servico de lancamentos permaneca disponivel se o servico de consolidacao cair**. Esta e uma restricao de **isolamento de falhas** que um monolito classico (um processo, um deploy) nao consegue atender sem cair em anti-padroes (processo zombie, degradacao silenciosa).

Alternativas consideradas:

### 1. Monolito modular (Modular Monolith)

- **Pros**: simples, baixo custo operacional, transacoes locais, sem rede entre modulos
- **Contras**: uma falha em consolidacao (ex: thread starvation, memory leak, deadlock no DB do read model) **afeta o processo inteiro**. Nao atende o NFR-001 nativamente. Exigiria containment artificial (bulkhead threads, app domains) que aproxima a complexidade de um microservico sem os beneficios de deploy independente.

### 2. Microservicos (dois servicos independentes + broker)

- **Pros**: isolamento fisico real (um pod cair nao afeta o outro); escalabilidade independente (o write side escala diferente do read side); tecnologia independente (pode mudar Redis por outra cache sem tocar Tx)
- **Contras**: complexidade operacional (mais pecas), latencia de rede, eventual consistency, observabilidade precisa ser first-class

### 3. Serverless (Azure Functions / AWS Lambda)

- **Pros**: scale-to-zero, pay-per-use
- **Contras**: cold start (latencia ruim para Transactions), lock-in de provedor, limites de execucao (e.g., Lambda 15 min), dificulta outbox polling continuo, mais caro em throughput sustentado (o SLO de 50 req/s em picos constantes)

## Decisao

**Microservicos** (opcao 2) com 2 servicos de negocio:

- `Transactions` (write side, core)
- `Consolidation` (read side, supporting)

Mais servicos transversais (Gateway, Workers, stack observabilidade, IdP).

Comunicacao **exclusivamente assincrona** via broker para prover o isolamento de falhas exigido.

## Consequencias

### Positivas

- Atende NFR-001 (desacoplamento) de forma nativa
- Permite scale-out horizontal independente por carga (write vs. read)
- Facilita deploy independente (time to market por feature)
- Alinhado com DDD (ver [bounded-contexts.md](../domain/bounded-contexts.md))
- Facilita testes de carga focados (podemos estressar so o read side)

### Negativas

- **Custo operacional maior** (broker, dois bancos, mais pipelines)
- **Eventual consistency** entre Tx e Cons (aceitavel pelo dominio, explicitado em [NFR](../requirements/non-functional.md))
- **Debugging distribuido** mais dificil (mitigado com OpenTelemetry — ver [ADR-0008](0008-opentelemetry-standard.md))
- Transacao distribuida entre Tx e Cons nao existe (resolvido com Outbox — ver [ADR-0005](0005-outbox-pattern.md))

### Riscos e Mitigacoes

| Risco | Mitigacao |
|---|---|
| "Distributed monolith" (servicos acoplados sincronicamente) | Regra de codigo: Tx nao pode fazer HTTP call para Cons. Apenas publicar evento. |
| Over-segmentation (15 microservicos para um CRUD) | Limite explicito: apenas 2 servicos de dominio. Novos BCs exigem ADR. |
| Saga complexity | Evitada em V1 — operacoes transacionais limitadas a um BC. |

## Referencias

- Sam Newman — _Building Microservices_ (2nd ed)
- Martin Fowler — [MicroservicePremium](https://martinfowler.com/bliki/MicroservicePremium.html)
- [docs/domain/bounded-contexts.md](../domain/bounded-contexts.md)
