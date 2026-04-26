# ADR-0007: Redis para Cache de Saldo Diario

- **Status**: Accepted
- **Data**: 2026-04-26
- **Decisores**: Arquiteto de Solucoes

## Contexto

O NFR-002 exige **50 req/s com p95 < 300ms** na consulta de saldo diario. O read model em Postgres (`daily_balance`) atende esse SLO em condicoes normais, mas:

- Em picos combinados com outras queries, pode haver contencao
- Um cache reduz carga no read DB, liberando-o para escritas do Service
- Simplifica futura separacao em read replica

## Decisao

Usar **Redis 7** como **cache write-through** e **leitura primaria** do saldo diario.

### Chaves e Estruturas

```
KEY: balance:{merchantId}:{yyyy-MM-dd}   (Hash)
FIELDS:
    total_credits  -> NUMERIC as string
    total_debits   -> NUMERIC as string
    balance        -> NUMERIC as string (calc field)
    transaction_count -> int
    last_updated_at  -> ISO8601
TTL: 259200 (72h)
```

Motivos:

- `HSET` atomico
- `HINCRBY` permite delta atomico (evita read-modify-write race)
- TTL de 72h alinha com janela operacional de saldos "quentes"; dias mais antigos raramente sao consultados e podem ser reconstruidos do read DB

### Politica de cache

- **Write** (pelo Service): apos UPSERT em `daily_balance`, fazer `HINCRBY` no Redis (write-behind **nao** — write-through para evitar divergencia)
- **Read** (pela API):
  1. `HGETALL balance:{m}:{d}`
  2. Se hit: retorna
  3. Se miss: `SELECT` no read DB, `HSET` no Redis com TTL 60s curto (fallback stale-safe), retorna

### Por que Redis e nao outras opcoes?

| Opcao | Pro | Con |
|---|---|---|
| **Redis** | O(1) reads/writes, HINCRBY atomico, maturidade | Single-threaded p/ writes (nao e problema no nosso volume) |
| Memcached | Simples, rapido | Sem estruturas ricas (so string); sem persistencia |
| In-memory local (MemoryCache) | Zero latencia | Nao compartilhado entre replicas, cache sempre frio |
| CDN/edge cache | N/A | Dados por merchant, nao cacheaveis publicamente |

### Persistencia

- AOF (`appendfsync everysec`) **ligado** — seguranca extra, mas **Redis nao e fonte da verdade**; Postgres e. Em caso de crash total do Redis, cache se repopula em minutos.
- RDB snapshot a cada 5 min (backup secundario)

## Consequencias

### Positivas

- Latencia p95 < 50ms na leitura de saldo (cache hit case)
- Descarrega o read DB em 80%+ dos GETs
- `HINCRBY` elimina race conditions multi-Service
- Redis tem ecosistema de operacao maduro (Redis Enterprise, AWS ElastiCache, Azure Cache for Redis)

### Negativas

- **Invalidacao**: se UPSERT no DB e `HINCRBY` no Redis forem executados fora de transacao, podem divergir. Mitigacao:
  - Service sempre escreve DB **primeiro**; depois Redis
  - Se Redis falhar, proxima leitura fara cache miss e reconstruira do DB (correcao eventual)
  - Monitoring: alerta se `cache miss rate > 30%` por 5 min
- **Falha do Redis** degrada para leitura direta no DB — SLO de latencia pode nao ser atingido em pico
- **Consistencia em reversals**: atualizar os dois lados (credits/debits) em um unico `HMSET` ou com `MULTI/EXEC`

### Alternativa considerada: Caching em nivel de HTTP (response cache)

- Pros: simples, libera nao so DB mas APIs tambem
- Contras: invalidacao por merchant/dia e nao-trivial; nao protege o Service
- **Nao escolhida** mas compativel — pode ser adicionada em camada de gateway no futuro

## Referencias

- [Redis Hash commands](https://redis.io/commands/?group=hash)
- [StackExchange.Redis best practices](https://stackexchange.github.io/StackExchange.Redis/)
