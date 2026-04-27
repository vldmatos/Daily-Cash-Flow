# Roadmap e Evolucoes Futuras

> Este documento lista o que **deixaria de entregar na V1** por tempo/escopo, e o que **faria a seguir**, ordenado por valor.

---

## V1.1 — Estabilizacao e Qualidade Operacional

Foco: tornar o sistema mais confiavel e observavel no dia a dia, sem grandes mudancas arquiteturais.

| # | Item | Valor | Esforco |
|---|---|---|---|
| 1.1 | **Dashboard de saude**: pagina interna simples mostrando status dos servicos, fila de mensagens e ultimos erros | visibilidade imediata para o time | baixo |
| 1.2 | **Alertas por e-mail ou Slack** em caso de falha critica (ex: consumer parado, DB indisponivel) | resposta rapida a incidentes | baixo |
| 1.3 | **Job de reconciliacao noturna**: comparar soma de lancamentos do dia x saldo consolidado; alertar em caso de drift | detecta issues silenciosos | medio |
| 1.4 | **Replay de eventos**: tool para reconstruir o read model do zero a partir do write DB | recuperacao de desastres e rebuild | medio |
| 1.5 | **Logs estruturados com contexto de negocio** (merchant_id, valor, operacao) para facilitar troubleshooting | debugging mais rapido | baixo |
| 1.6 | **Exportacao de relatorio em CSV/Excel**: lancamentos e saldo do periodo | demanda imediata de operadores | baixo |
| 1.7 | **Paginacao e filtros no historico de lancamentos**: por data, tipo, valor | usabilidade basica | baixo |
| 1.8 | **Secret rotation automatico** via Key Vault | seguranca | medio |

---

## V2.0 — Produto e Experiencia do Usuario

Foco: tornar o sistema mais util para quem opera o caixa no dia a dia.

### 2.1 — Categorias e Tags em Lancamentos

Permitir classificar lancamentos por categoria (ex: fornecedor, folha, imposto):

- Campo opcional de categoria no lancamento
- Filtro por categoria no relatorio
- Facilita reconciliacao contabil basica sem precisar de um contador

### 2.2 — Resumo Diario por Merchant

Envio automatico de um resumo diario (e-mail ou webhook) com:

- Total de entradas e saidas do dia
- Saldo atual vs dia anterior
- Alertas de saldo negativo ou abaixo de limite configurado

### 2.3 — Meta de Saldo Minimo

- Merchant configura um saldo minimo de seguranca
- Sistema alerta quando o saldo se aproxima do limite
- Simples de implementar, alto valor percebido pelo usuario

### 2.4 — Historico de Alteracoes (Audit Log Simples)

- Registrar quem fez o que e quando (criou, editou ou cancelou um lancamento)
- Exibir na tela de detalhes do lancamento
- Sem complexidade de event sourcing; apenas um log de auditoria em tabela separada

### 2.5 — Multi-Moeda Basico

- Permitir registrar lancamentos em moeda estrangeira com taxa de cambio no momento
- Relatorio exibe o equivalente em moeda base do merchant
- Util para merchants com operacoes internacionais

---

## V3.0 — Crescimento e Integracao

Foco: conectar o sistema com o ecossistema do merchant.

### 3.1 — Importacao de Extrato Bancario

- Upload de arquivo OFX/CSV do banco
- Parser identifica lancamentos e sugere categorizacao automatica
- Elimina entrada manual em alto volume

### 3.2 — Integracao com PIX / Open Finance

- Receber confirmacoes de PIX e registrar lancamentos automaticamente
- Reduz trabalho manual e erros de digitacao

### 3.3 — API Publica Documentada

- Endpoints REST documentados com Swagger para integracao com ERPs e sistemas externos
- Permite que merchants conectem seus proprios sistemas ao Daily Cash Flow

### 3.4 — Multiusuario por Merchant

- Suporte a multiplos usuarios com permissoes diferentes (operador, gerente, somente leitura)
- Merchant pode delegar acesso sem compartilhar senha

### 3.5 — App Mobile Simples (PWA)

- Versao progressive web app para registrar lancamentos pelo celular
- Sem necessidade de app nativo; funciona no browser mobile
- Foco em entrada rapida de dados no campo

---

## Itens Que Deixei de Fora Deliberadamente

Algumas escolhas conscientes de **nao fazer** na V1:

| Item | Motivo de nao ter incluido |
|---|---|
| Event Sourcing | Paradigma complexo; state-based + outbox atende o escopo com menos risco |
| Kafka | Overkill para o volume esperado; RabbitMQ/ASB atende com menos complexidade operacional |
| Multi-Tenancy com schema separado | Complexity vs. beneficio nao se justifica ate o produto ter trafego real |
| gRPC entre servicos | Comunicacao sincrona evitada; eventos cobrem o caso |
| Service Mesh | Valor marginal em 3 servicos; overhead operacional nao compensa ainda |
| Contabilidade fiscal (SPED, NF-e) | BC completamente diferente; requer especialidade tributaria especifica |

---

## Débitos Tecnicos Conhecidos

Conforme documentado durante a V1 (referenciados em codigo com `// TODO(v1.1)`):

- [ ] Polling outbox -> LISTEN/NOTIFY (V1.1)
- [ ] Janela de idempotencia fixa (24h) -> configuravel por merchant (V2)
- [ ] Timezone fixo UTC -> timezone por merchant (V2)
- [ ] Sem retry configuravel no consumer (V1.1)
- [ ] Logs sem PII review automatizado (V1.1)
- [ ] Realm Keycloak sem MFA (V1.1)

---

## Criterios de Priorizacao

Toda feature nova passa por 4 filtros antes de entrar no backlog formal:

1. **Valor para o usuario**: resolve uma dor real de quem opera o caixa?
2. **Esforco de implementacao**: cabe em uma sprint sem quebrar o que funciona?
3. **Risco**: impacta confiabilidade, seguranca ou dados existentes?
4. **Fit arquitetural**: cabe em um BC existente ou requer uma discussao maior?

Features que falham em 2+ filtros voltam para refinamento.
