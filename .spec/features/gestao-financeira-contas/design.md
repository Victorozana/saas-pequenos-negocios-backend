# Revisão do backend financeiro

## Escopo

Corrigir e comprovar AC-094 a AC-096. T-093 a T-096 foram reabertas porque os caminhos registrados estavam desatualizados e a verificação anterior não continha prova individual dos critérios.

## Decisões pendentes do usuário

- Q-011: confirmado registro manual de pagamentos, sem gateway nesta etapa.
- Q-012: confirmada geração de recebíveis na aprovação do orçamento, com entrada e saldo.

## Decisões técnicas

- Consultas exigem financial.read; despesas e pagamentos exigem financial.write.
- Pagamentos devem validar valores e retornar erros de negócio claros, sem alteração parcial de saldo.
- Geração de recebíveis precisa persistir títulos e transição de status de forma atômica e evitar duplicação ao repetir o evento.
- Corrigir a persistência da atualização de status de OS, necessária para um fluxo financeiro associado à conclusão.
- Testar contas, pagamentos e extrato com isolamento entre tenants e nova conexão ao banco para comprovar persistência.
- Publicar DTOs explícitos e erros no OpenAPI; sincronizar o schema do frontend após validar o contrato.

## Rastreabilidade

Normalizar o formato da spec para o parser reconhecer histórias, critérios e perguntas. Corrigir os caminhos das tasks para CreatePayable, RegisterPayment e os testes FinancialApiTests e FinancialIsolationTests. A auditoria global possui problemas preexistentes em outras features; registrar esses problemas sem ocultar erros, enfraquecer critérios ou declarar o gate aprovado.

## Execução proposta

Execução manual sequencial, mantendo modelo e esforço atuais, confirmada pelo usuário em 11/10/2026. Não executar os scripts gerados com modelos default do motor sem confirmação. Sem criação de telas nesta etapa.
