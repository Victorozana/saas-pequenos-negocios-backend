# Tasks: Onboarding token desenvolvimento

> feature: onboarding-token-desenvolvimento

<!--
  Como ler este arquivo (o formato é verificado por `onp-spec audit`):
  - T-xxx = tarefa (código de rastreio, único no projeto inteiro).
  - Toda tarefa referencia em `Refs:` pelo menos uma história de usuário
    (US-xxx) ou critério de aceite (AC-xxx).
  - Toda tarefa lista os arquivos que cria/altera em `Arquivos:` — capriche:
    é o que decide o que `onp-spec plano` roda em PARALELO (arquivos
    disjuntos) e o que roda em sequência.
  - Campos opcionais por tarefa, usados pelo plano de execução:
    `- Modelo: claude-sonnet-5` e `- Esforço: alto` (baixo|medio|alto|xalto|max).
  - Uma tarefa só pode virar [concluida] quando os critérios de aceite dela
    tiverem prova PASS registrada por `onp-spec verify`.
  Status: pendente | em-andamento | concluida
    (atalho: `onp-spec tarefa <feature> <T-xxx> <status>`)
-->

## T-101 — Documentar o header de verificação de desenvolvimento [concluida]
- Refs: US-053, AC-100
- Arquivos: src/Agendamento.Api/OpenApi/OpenApiExtensions.cs, tests/Agendamento.ArchitectureTests/OpenApi/EndpointMetadataTests.cs
- Notas: O schema público de resposta não pode incluir token bruto; somente o header é documentado em Development/Test.
