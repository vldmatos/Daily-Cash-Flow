# ADR-0006: PostgreSQL como RDBMS dos dois Bounded Contexts

- **Status**: Accepted
- **Data**: 2026-04-26
- **Decisores**: Arquiteto de Solucoes

## Contexto

Precisamos de uma base de dados durable para:

1. **Write side** (Transactions): transacoes ACID, unique constraints (`idempotency_keys`), outbox na mesma transacao, append-only com auditoria
2. **Read side** (Consolidation): read model `daily_balance` (UPSERT atomico), tabela `processed_events` para idempotencia

Alternativas consideradas:

| Opcao | Fit para write side | Fit para read side | Observacao |
|---|---|---|---|
| PostgreSQL | **Excelente** | Bom | Gratis, ACID, JSONB, rico |
| SQL Server | Excelente | Bom | Licenca paga |
| MongoDB | Medio (transacoes multi-doc limitadas) | Bom | Schema flex mas overkill p/ read model tabular |
| Cosmos DB (SQL API) | Bom | Bom | Lock-in Azure, caro |
| DynamoDB | Fraco (sem JOIN, TX limitada) | Medio | Lock-in AWS |
| EventStoreDB | Fit perfeito p/ event sourcing | N/A | Paradigma diferente — V2 |

## Decisao

Usar **PostgreSQL 16** em ambos os BCs, com **bancos fisicamente separados**:

- `cashflow_tx` (instancia Postgres do write side)
- `cashflow_cons` (instancia Postgres do read side)

Separacao fisica justificada por:

- **Blast radius**: downtime ou migracao em um nao afeta o outro
- **Scale independente**: podemos por read replicas apenas em `cashflow_cons`
- **Tuning independente**: parametros de WAL/checkpoint podem ser diferentes
- **Permissoes independentes**: read DB pode ter read-only roles

### Versao

- Postgres **16** (suporte LTS ate 2028)
- Extensoes: `pg_trgm` (para busca futura em descricao), `btree_gin` (search opcional)

### Convencoes

- IDs: `UUID` (v4) para evitar hot-spot de B-tree em insercao
- Timestamps: `TIMESTAMPTZ` (sempre UTC)
- Valores monetarios: `NUMERIC(18, 4)` (nunca `FLOAT` ou `DOUBLE`)
- Nomenclatura: snake_case em DB; schema `public` para V1 (multi-schema em V2 para multi-tenancy hard)
- Migracoes: EF Core Migrations versionadas (para Tx); Dapper + scripts `V__*.sql` (para Cons)

### Indices Chave

**cashflow_tx**:

```sql
CREATE UNIQUE INDEX ix_transactions_merchant_idempotency
    ON transactions (merchant_id, idempotency_key);
CREATE INDEX ix_transactions_merchant_occurred
    ON transactions (merchant_id, occurred_on DESC);
```

**cashflow_cons**:

```sql
CREATE UNIQUE INDEX pk_daily_balance ON daily_balance (merchant_id, date);
CREATE UNIQUE INDEX pk_processed_events ON processed_events (event_id);
```

### Particionamento (V1.1)

- `daily_balance` por range em `date` (mensal)
- `processed_events` por range em `occurred_at` (mensal), com purge > 90 dias

## Consequencias

### Positivas

- Familiaridade do ecossistema .NET (Npgsql, EF Core, Dapper)
- ACID (pre-requisito para Outbox)
- Custo zero (OSS, inclusive em clouds com managed services acessiveis)
- JSONB flexivel para evoluir payload de outbox sem migrar schema
- Rico em operadores (RETURNING, UPSERT via `ON CONFLICT`)
- Ferramental maduro (pgAdmin, pg_stat_statements, pgBadger)

### Negativas

- **Scale-out** exige sharding explicito (nao-trivial) ou managed (Citus, Aurora-style) — nao atingimos esse limite na V1
- Operacao de HA (streaming replication, failover automatico) requer atencao — resolvido em prod com managed

### Quando reconsiderar

- Se cashflow_cons crescer muito e queries analiticas pressionarem OLTP -> evoluir para read replica dedicada ou lakehouse (V2)
- Se ordenacao estrita de eventos por aggregate for critica -> Event Sourcing em EventStoreDB ou Kafka com tabela append

## Referencias

- [Postgres 16 Release Notes](https://www.postgresql.org/docs/16/release-16.html)
- [EF Core with PostgreSQL](https://www.npgsql.org/efcore/)
