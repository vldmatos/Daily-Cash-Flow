# Linguagem Ubiqua (Ubiquitous Language)

> Vocabulario compartilhado entre negocio e tecnologia. Deve ser usado em codigo, documentos, reunioes e APIs. Traducoes devem ser evitadas — quando houver divergencia entre PT/EN, prevalece o termo em **negrito**.

## Termos de Dominio

| Termo (PT) | Termo (EN) | Definicao |
|---|---|---|
| Comerciante | **Merchant** | Pessoa ou empresa que registra movimentacoes financeiras. Unidade de isolamento tenant do sistema. |
| Lancamento | **Transaction** | Registro atomico de uma movimentacao (debito ou credito) em uma conta de um merchant. Imutavel apos criado. |
| Debito | **Debit** | Tipo de lancamento que diminui o saldo. Representa saida de valor (ex: pagamento a fornecedor, taxa). |
| Credito | **Credit** | Tipo de lancamento que aumenta o saldo. Representa entrada de valor (ex: venda, estorno recebido). |
| Estorno | **Reversal** | Lancamento tecnicamente novo (tipo oposto, com vinculo ao original) que anula o efeito de um lancamento anterior. Nao apaga o original (imutabilidade). |
| Valor | **Amount** | Quantia monetaria estritamente positiva (>0). Moeda implicita (ver "Moeda"). |
| Moeda | **Currency** | Unidade monetaria do lancamento. Padrao: **BRL**. Fora de escopo nesta versao. |
| Data do Lancamento | **OccurredOn** | Data/hora em que a movimentacao ocorreu do ponto de vista do negocio (nao da insercao no sistema). UTC. |
| Descricao | **Description** | Texto livre opcional (<=140 chars) com contexto do lancamento. |
| Saldo Diario | **DailyBalance** | Valor agregado dos creditos menos os debitos de um merchant em um dia especifico (UTC). |
| Saldo Consolidado | **ConsolidatedBalance** | Sinonimo de Saldo Diario no contexto do relatorio consolidado. |
| Consolidacao | **Consolidation** | Processo (assincrono) que agrega lancamentos em um saldo diario. |
| Relatorio Diario | **DailyReport** | Visualizacao do Saldo Diario incluindo totais de creditos, debitos e contagem de lancamentos. |
| Janela do Dia | **BusinessDay** | Intervalo [00:00, 23:59:59.999] UTC. Versao 1 nao suporta fuso horario do merchant. |
| Evento de Integracao | **Integration Event** | Mensagem publicada entre Bounded Contexts (ex: `TransactionCreated`). Contrato estavel. |
| Outbox | **Outbox** | Tabela transacional usada para publicar eventos com garantia de at-least-once delivery. |

## Termos Tecnicos com Significado de Negocio

| Termo | Significado |
|---|---|
| **Idempotencia** | Garantia de que reenvios do mesmo lancamento (mesmo `idempotencyKey`) nao geram duplicatas. Obrigatorio em `POST /transactions`. |
| **Eventual Consistency** | O saldo consolidado pode estar temporariamente defasado em relacao aos lancamentos (esperado: <=30s). Contrato explicito com o negocio. |
| **Strong Consistency** | Dentro do Transactions BC, ler um lancamento imediatamente apos cria-lo retorna o valor criado (read-your-writes). |

## Anti-Dicionario (termos proibidos)

Termos que **NAO devem aparecer** em codigo, logs ou APIs publicas por gerar ambiguidade:

- "movimento", "movimentacao" (use **Transaction** ou **Lancamento**)
- "operacao" (overloaded, pode confundir com operacoes HTTP)
- "entrada"/"saida" (use **Credit**/**Debit**)
- "extrato" (nao existe no escopo; use **DailyReport** para o agregado diario ou liste `Transactions`)
- "cliente" quando for merchant (e merchant, nao cliente do merchant)

## Exemplo Canonico

> _"Um **Merchant** registra um **Transaction** do tipo **Credit** com **Amount** de R$ 150,00 em `OccurredOn=2026-04-23T10:30Z`. O **Consolidation Service** consome o evento `TransactionCreated` e atualiza o **DailyBalance** de `2026-04-23` para aquele merchant."_
