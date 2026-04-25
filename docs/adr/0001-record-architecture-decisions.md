# ADR-0001: Registrar Decisoes Arquiteturais

- **Status**: Accepted
- **Data**: 2026-04-25
- **Decisores**: Arquiteto de Solucoes

## Contexto

Decisoes arquiteturais sao tipicamente feitas e esquecidas: ninguem lembra **por que** escolhemos Postgres em vez de Cosmos, ou **por que** usamos RabbitMQ em vez de Kafka. Isso gera:

- Retrabalho (re-debater decisoes ja tomadas sem novas informacoes)
- Erosao arquitetural (decisoes originais sao violadas por desconhecimento)
- Dificuldade de onboarding de novos membros
- Perda de contexto sobre trade-offs ja considerados

## Decisao

Adotamos **Architecture Decision Records (ADRs)** no formato de Michael Nygard ([Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions.html)).

- Local: `docs/adr/NNNN-kebab-case-title.md`
- Numerados sequencialmente
- Status: `Proposed | Accepted | Deprecated | Superseded`
- Campos minimos: Status, Data, Contexto, Decisao, Consequencias
- ADRs nao sao reescritos: se a decisao muda, cria-se um novo ADR com status `Superseded by NNNN`
- Qualquer mudanca estrutural significativa deve vir acompanhada de um ADR no mesmo PR

## Consequencias

### Positivas

- Historia arquitetural auditavel
- Acelera onboarding (um leitor novo entende o "porque")
- Forca pensamento explicito sobre trade-offs antes de codificar
- Reduz re-debates

### Negativas

- Overhead de escrita (~15-30 min por ADR)
- Requer disciplina de equipe

## Referencias

- [ADR GitHub Organization](https://adr.github.io/)
- [Michael Nygard - Documenting Architecture Decisions](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions.html)