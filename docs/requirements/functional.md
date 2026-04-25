# Requisitos Funcionais

## RF-001 — Registrar Lancamento

**Como** comerciante, **quero** registrar um lancamento de debito ou credito, **para** manter meu fluxo de caixa atualizado.

### Criterios de Aceitacao

- [x] Dado um token JWT valido, quando envio `POST /transactions` com `{type, amount, occurredOn}`, entao recebo `201 Created` com o `id` gerado.
- [x] Dado um `amount <= 0`, quando envio a requisicao, entao recebo `400 Bad Request` com detalhes do erro (RFC 7807).
- [x] Dado um `type` diferente de `Credit|Debit`, entao recebo `400`.
- [x] Dado um `occurredOn` mais de 5 minutos no futuro, entao recebo `400`.
- [x] Dada uma `Idempotency-Key` repetida nas ultimas 24h, entao recebo a resposta original (`200 OK`), sem duplicar.
- [x] Sem token JWT, recebo `401 Unauthorized`.
- [x] Com token de outro `merchantId` no path/body, recebo `403 Forbidden`.

### Contrato

```
POST /transactions
Headers:
  Authorization: Bearer <jwt>
  Idempotency-Key: <uuid>
Body:
  {
    "merchantId": "guid",
    "type": "Credit" | "Debit",
    "amount": decimal > 0,
    "currency": "BRL",           // opcional, default BRL
    "occurredOn": "ISO8601",
    "description": "string?<=140"
  }
Responses:
  201 Created { id, status, createdAt }
  400 Bad Request (ProblemDetails)
  401 | 403
```

## RF-002 — Estornar Lancamento

**Como** comerciante, **quero** estornar um lancamento, **para** corrigir erros operacionais sem mutilar a auditoria.

### Criterios de Aceitacao

- [x] Cria um novo lancamento com tipo oposto e mesmo valor, vinculado ao original.
- [x] O lancamento original **nao e alterado** (imutabilidade).
- [x] Nao e possivel estornar um lancamento ja estornado (`409 Conflict`).
- [x] O saldo do **dia do lancamento original** e que e afetado (nao o dia do estorno).

### Contrato

```
POST /transactions/{id}/reverse
Headers: Authorization, Idempotency-Key
Body: { "reason": "string" }
Responses:
  201 Created { reversalId, originalId }
  404 Not Found | 409 Conflict
```

## RF-003 — Consultar Lancamento

**Como** comerciante, **quero** consultar um lancamento especifico.

- [x] `GET /transactions/{id}` retorna `200` com os dados, ou `404` se nao existir.
- [x] Apenas o merchant dono pode consultar (autorizacao por `merchantId` no claim).

## RF-004 — Consultar Saldo Diario Consolidado

**Como** comerciante, **quero** consultar o saldo consolidado de um dia, **para** acompanhar meu fluxo de caixa.

### Criterios de Aceitacao

- [x] `GET /balance/{merchantId}?date=yyyy-MM-dd` retorna `200` com totais.
- [x] Se o dia nao tem lancamentos, retorna `200` com todos os valores zerados.
- [x] Latencia p95 < 300ms (via cache Redis).
- [x] Aceita dia futuro (retorna zeros) e dia passado.
- [x] Reflete lancamentos ja processados (eventual consistency, tipicamente <= 30s apos criacao).

### Contrato

```
GET /balance/{merchantId}?date=2026-04-23
Response 200:
  {
    "merchantId": "guid",
    "date": "2026-04-23",
    "totalCredits": 12450.00,
    "totalDebits": 3120.50,
    "balance": 9329.50,
    "transactionCount": 87,
    "computedAt": "ISO8601"
  }
```

## RF-005 — Consultar Saldo em Intervalo (serie temporal)

- [x] `GET /balance/{merchantId}/range?from=...&to=...` retorna array de DailyBalance.
- [x] Intervalo maximo de 90 dias (prevenir abuso).

## RF-006 — Health Checks

- [x] `GET /health/live` — processo vivo (sem dependencias).
- [x] `GET /health/ready` — pronto para receber trafego (checa DB, RabbitMQ, Redis).
- [x] Formato padrao Kubernetes (`status: Healthy|Degraded|Unhealthy`).

## RF-007 — OpenAPI/Swagger

- [x] Cada API expoe `/swagger` com contratos versionados.
- [x] Exemplos de request/response documentados.

## Fora de Escopo V1

- Multi-moeda e conversao cambial
- Categorizacao de lancamentos (tags, contas contabeis)
- Listagem paginada com filtros complexos (por data/tipo/valor/descricao)
- Export CSV/PDF
- Fechamento contabil formal
- Alertas por limite de saldo
- Webhook para notificar terceiros
- Multi-tenancy hierarquico (grupo economico > varios merchants)

Evolucoes previstas em [roadmap/future-work.md](../roadmap/future-work.md).
