# Estimativa de Custos (TCO)

> "Estimativa de custos com infraestrutura e licencas". Este documento estima o **Total Cost of Ownership (TCO)** em 3 cenarios de carga, comparando **Azure** e **AWS**. Todos os valores sao **referenciais** (pay-as-you-go, sem reservas ou savings plans), em **USD/mes**, baseados em publicacoes oficiais dos provedores — sujeitos a variacao cambial e reajustes.

## Cenarios de Carga

| Cenario | Merchants ativos | Transacoes/dia | Leituras de saldo/dia | Pico |
|---|---|---|---|---|
| **Dev/QA** | - | baixo (tests) | baixo | - |
| **Producao Pequena** | 1.000 | 50.000 (0.6 tps medio) | 100.000 (1.2 rps medio) | 50 rps (conforme NFR) |
| **Producao Media** | 50.000 | 2.500.000 (29 tps medio) | 5.000.000 (58 rps medio) | 500 rps |

## Escolhas de Servicos Managed (Producao)

| Componente | Azure | AWS | Justificativa |
|---|---|---|---|
| Kubernetes | AKS | EKS | Orquestrador gerenciado |
| Compute (pods) | Standard_D4s_v5 | m6i.xlarge | 4vCPU/16GB, balanceado |
| Postgres | Azure DB for PostgreSQL Flexible (Burstable B2s em prod peq.; GP D4s em prod media) | RDS for PostgreSQL (db.t3.medium; db.m6g.large) | HA, backup, PITR |
| Redis | Azure Cache for Redis Standard C1 | ElastiCache Redis cache.t3.small | Small fit para saldo |
| Messaging | Azure Service Bus Standard | Amazon SQS + SNS | DLQ nativo, SLA |
| Observabilidade | Azure Monitor + App Insights | CloudWatch + X-Ray | Managed |
| IdP | Entra ID External (B2C) | Cognito | OIDC |
| CDN/WAF | Azure Front Door Standard | CloudFront + AWS WAF | Borda |
| Artifact registry | ACR Basic | ECR | imagens |
| Secrets | Key Vault | Secrets Manager | rotacao |

## Estimativa — Dev/QA (Local)

Rodando inteiramente em Docker Compose em maquina de desenvolvedor — **USD 0 de nuvem**. Custo apenas da hora-homem.

## Estimativa — Producao Pequena (~1k merchants, 50 rps pico)

### Azure

| Componente | SKU | Qtd | USD/mes (aprox) |
|---|---|---|---|
| AKS control plane | Free tier | 1 cluster | 0 |
| AKS node pool | B4ms x 2 (burstable) | 2 nos | 140 |
| Azure Postgres Flex (Tx) | Burstable B2s + 32GB | 1 | 85 |
| Azure Postgres Flex (Cons) | Burstable B2s + 64GB | 1 | 110 |
| Azure Cache for Redis Standard | C1 (1GB) | 1 | 60 |
| Azure Service Bus Standard | base + 10M ops | - | 15 |
| Azure Monitor + App Insights | 5 GB ingest | - | 50 |
| Log Analytics | 5 GB retained | - | 15 |
| Azure Front Door Standard | base + egress | - | 40 |
| ACR Basic | - | 1 | 5 |
| Key Vault | standard | 1 | 3 |
| Entra ID External (B2C) | 50.000 MAU | - | 0 (Free tier ate 50k) |
| Egress data | ~100 GB | - | 10 |
| **Subtotal** | | | **~ USD 533/mes** |

### AWS

| Componente | SKU | Qtd | USD/mes (aprox) |
|---|---|---|---|
| EKS control plane | - | 1 cluster | 73 |
| EKS node group | t3.medium x 2 | 2 nos | 120 |
| RDS PostgreSQL (Tx) | db.t3.medium + 32GB gp3 | 1 | 90 |
| RDS PostgreSQL (Cons) | db.t3.medium + 64GB gp3 | 1 | 110 |
| ElastiCache Redis | cache.t3.small | 1 | 25 |
| SQS + SNS | 10M requests | - | 10 |
| CloudWatch Logs + Metrics | 5 GB | - | 40 |
| CloudFront + WAF | base + egress | - | 30 |
| ECR | - | 1 | 2 |
| Secrets Manager | 10 secrets | - | 5 |
| Cognito | 50.000 MAU | - | 0 (Free tier ate 50k) |
| Egress data | ~100 GB | - | 9 |
| **Subtotal** | | | **~ USD 514/mes** |

### Licencas e Ferramental

| Item | Custo mensal |
|---|---|
| .NET 8 | 0 (MIT) |
| PostgreSQL | 0 (OSS) |
| Redis | 0 (OSS — versao local) |
| RabbitMQ | 0 (nao usado em prod; substituido por managed) |
| OpenTelemetry | 0 |
| Keycloak | 0 (nao usado em prod; substituido por Entra/Cognito) |
| Grafana OSS | 0 |
| GitHub (repo privado + Actions) | 4/usuario ou 0 (publico) |
| Cursor/IDE | N/A |
| **Total licencas** | **~ USD 0-20/mes** |

### **Total Producao Pequena**: ~USD 530 - 550/mes

## Estimativa — Producao Media (~50k merchants, 500 rps pico)

### Azure

| Componente | SKU | Qtd | USD/mes (aprox) |
|---|---|---|---|
| AKS node pool | D4s_v5 x 4 (com HPA ate 8) | 4 nos base | 560 |
| Azure Postgres Flex (Tx) | GP D4s_v3 + 256GB + HA zone | 1 | 700 |
| Azure Postgres Flex (Cons) | GP D4s_v3 + 512GB + read replica | 1+1 | 1.100 |
| Azure Cache for Redis Premium | P1 (6GB, zone-redundant) | 1 | 500 |
| Azure Service Bus Standard | base + 500M ops | - | 300 |
| Azure Monitor + App Insights | 100 GB ingest | - | 300 |
| Log Analytics | 100 GB retained, 90d | - | 250 |
| Azure Front Door Standard | base + 1 TB egress | - | 150 |
| ACR Standard | - | 1 | 20 |
| Key Vault | premium | 1 | 15 |
| Entra ID External (B2C) | 100.000 MAU | - | 50 (50k pagos a ~USD 0.001) |
| Egress data | ~1 TB | - | 90 |
| **Subtotal** | | | **~ USD 4.035/mes** |

### AWS

| Componente | SKU | Qtd | USD/mes (aprox) |
|---|---|---|---|
| EKS control plane | - | 1 | 73 |
| EKS node group | m6i.xlarge x 4 | 4 | 600 |
| RDS PostgreSQL (Tx) | db.m6g.large + Multi-AZ + 256GB | 1 | 600 |
| RDS PostgreSQL (Cons) | db.m6g.large + read replica + 512GB | 1+1 | 950 |
| ElastiCache Redis | cache.m6g.large + cluster mode | 1 | 300 |
| SQS + SNS | 500M requests + DLQ | - | 250 |
| CloudWatch + X-Ray | 100 GB + 10M traces | - | 400 |
| CloudFront + WAF | base + 1 TB egress | - | 120 |
| ECR | - | 1 | 10 |
| Secrets Manager | 20 secrets | - | 10 |
| Cognito | 100.000 MAU | - | ~50 |
| Egress data | ~1 TB | - | 85 |
| **Subtotal** | | | **~ USD 3.448/mes** |

### **Total Producao Media**: ~USD 3.500 - 4.100/mes

## Otimizacoes de Custo (recomendadas antes de escalar)

1. **Reserved Instances / Savings Plans**: 1 ou 3 anos de compromisso reduz ate **72%** em VM e DB (Azure Reserved / AWS SP).
2. **Autoscaling ativo**: dimensionamento por HPA baseado em RPS + schedule scaling para horarios de pouco uso (madrugada).
3. **Spot/Preemptible Nodes** para workers de consolidation (idempotentes, tolerantes a interrupcao). Economiza ate 90%.
4. **Query tuning Postgres**: menos conexoes simultaneas, PgBouncer em frente.
5. **Cache agressivo no read path**: cada cache hit economiza 1 query no DB.
6. **Dados frios para Blob/S3**: outbox > 30d, logs > 90d, lancamentos > 1 ano em cold tier.
7. **Azure Dev/Test subscription** para ambientes nao-producao (~40% discount em licencas Microsoft).

## Projecao Anual

| Cenario | Mensal | Anual (pay-as-you-go) | Anual c/ Reserved 1y (~30% discount) |
|---|---|---|---|
| Producao Pequena (Azure) | USD 530 | USD 6.360 | USD 4.450 |
| Producao Pequena (AWS) | USD 514 | USD 6.168 | USD 4.320 |
| Producao Media (Azure) | USD 4.035 | USD 48.420 | USD 33.900 |
| Producao Media (AWS) | USD 3.448 | USD 41.376 | USD 28.960 |

## Modelo de Custo por Transacao (Unit Economics)

| Cenario | Transacoes/mes | Custo mensal | Custo/transacao |
|---|---|---|---|
| Pequena | 1.5 M | USD 540 | USD 0.00036 = **R$ 0.0018** |
| Media | 75 M | USD 3.750 | USD 0.00005 = **R$ 0.00025** |

Economia de escala claramente visivel — unit cost cai 7x entre peq. e media.

## Custos Nao-Infra (a considerar no TCO real)

| Item | Ordem de grandeza mensal |
|---|---|
| Equipe eng. dedicada (2 devs + 1 SRE) | USD 15k - 40k (depende do mercado) |
| Security/compliance (audits, pen test anual) | ~USD 500 amortizado |
| Incident response (PagerDuty, etc) | USD 50 - 200 |
| Licencas de tooling (SonarCloud, Grafana Cloud, etc) | USD 100 - 500 |

Infra e **~10-20% do TCO real** em cenarios como este; a maior parte e pessoas.

## Referencias de Preco

- [Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)
- [AWS Pricing Calculator](https://calculator.aws/)
- [Azure Postgres Pricing](https://azure.microsoft.com/pricing/details/postgresql/flexible-server/)
- [AWS RDS Pricing](https://aws.amazon.com/rds/postgresql/pricing/)

**Nota**: valores foram checados em abril/2026 e representam ordem de grandeza. Sempre validar no calculator oficial para sua regiao especifica antes de orcamento formal.
