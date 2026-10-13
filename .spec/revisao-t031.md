# Revisão T-031 — Modelo do catálogo e orçamentos — 12/10/2026

## Estado inicial e autorização
Execução manual, modelo/esforço atuais, dois auxiliares herdando configuração. Sem executor headless, commit/push/deploy. Bases informadas 96875bb/ca1d485; conferência mostrou HEADs posteriores be79d2c (main) e cf00e8c (master). Backend tinha cinco arquivos obj modificados, frontend .codex-checks não rastreado. Preservados, junto com schema, sinais, relatórios/TRX e prova PDF t023/render-certified. Cópia da verificação anterior em .local-verification/t031/preexisting. Porta5050 sem listener; nenhum processo do usuário reiniciado/encerrado.

## Revisão dos requisitos e sobreposição
AGENTS dos dois repos, skill local, continuidade, PostgreSQL RLS, revisão/audit T-023, spec/tasks e histórico lidos. Design da feature inexistente; novo design.md documenta implementação e decisões confirmadas. ServiceItem/Quotation já implementavam ITenantOwned, agora factories também recusam tenant vazio. QuotationStatus/DepositType revisados sem renumerar valores; Draft interno e Pending público são mantidos. Transições e idempotência financeira anteriores não foram reescritas.
T-088 compartilha as cinco entidades de orçamento, mas tem escopo próprio de estados/AC-090/AC-092. Continua pendente, junto com T-032 a T-037 e T-089 a T-092; presença de código não certifica essas tarefas.

## Lacunas corrigidas
- Enum de sinal desconhecido era aceito silenciosamente; agora é recusado antes de substituição.
- AddItem/RemoveItem modificavam coleção/totais antes de validar entrada fixa ou soma. Agora calculam candidatos e só aplicam após todas as validações; null também é recusado sem mutação.
- Entradas fora de numeric(18,2) ou precisão excessiva podiam chegar ao banco e cálculos em frações de centavo divergiam após persistência. Usuário confirmou duas casas/teto e arredondamento por item/entrada, meio para longe de zero. Quantity também tem duas casas/teto, por decisão explícita para manter conversão OS sem migration.
- Criação não conferia catálogo no tenant, diferentemente da edição. Agora rejeita referência inexistente/externa antes de staging/persistência.
- Financeiro recalculava entrada sem arredondamento; agora usa RequiredAmount do orçamento. Resposta OS aplica subtotal arredondado. Frontend preview e desconto usam cálculo em centavos bigint e validação de precisão/limites antes de envio.
- Cliente obrigatório vazio é rejeitado no agregado; cliente/catálogo persistidos continuam validados sob contexto tenant, sem aceitar tenant vindo do request.

## Provas significativas
QuotationModelValidationTests cobre limites zero/teto, quantidade positiva, desconto igual/acima do subtotal, entradas percentuais/fixas, excesso/overflow, enum desconhecido, subtotal/sinal arredondados, conservação entrada+saldo, atualização inválida do catálogo, inclusão/remoção recusadas, edição pendente atômica e estados finais. Testa também consumidor OS.
QuotationModelHttpTests roda os mesmos casos em HTTP InMemory e PostgreSQL temporário exclusivo com RLS/logins restritos. POST/PUT inválidos verificam snapshots completos e contagens de orçamento/itens/serviços, zero recebíveis e ausência de mutação. Roundtrip prova teto financeiro e quantidade limite. Aprovação com total .03 gera entrada .02 e saldo .01, preservando total.
Focados: domínio final 23 PASS, exit0 (.local-verification/t031/unit-final); HTTP/PostgreSQL 2 PASS, exit0 e record.exitCode0 (.local-verification/t031/http-agent); frontend17 PASS/typecheck/lint exit0 (.codex-checks/t031/consumer-20261012-214849).
OpenAPI reexportado por host InMemory Release/UseAppHost=false, teste1PASS/exit0. Documento/schema têm mesmo conteúdo final, regeneração frontend pelo arquivo local. Respostas/rotas/DTOs/enum numérico/ProblemDetails permanecem compatíveis com T-023.

## Limites e preservação
Nenhuma migration nova ou aplicação ao banco de trabalho. PostgreSqlMigration usada pelas fixtures temporárias existentes sem imprimir conexões/segredos. Backup runtime-repair/agendamento_local-before-rls.dump preservado. AddCustomerAutomaticReminderPreference e AddDailyReceivableReminderDeduplication continuam não aplicadas a agendamento_local; não iniciar esta API nesse banco antes de rollout autorizado.
PDF mantém SemaphoreSlim estático e asserções semânticas/concorrentes; nenhuma asserção existente removida/enfraquecida. CPF mascarado/PreserveDocument, permissões, locks, financeiro/idempotência, notificações/opt-out/aviso diário preservados pelas regressões. Sem entrega real WhatsApp/e-mail ou telas financeiro/equipe.
Limitação preexistente do contrato number: JavaScript não representa exatamente centavos próximos ao teto do decimal. Helper bigint aceita string e prova limite exato, mas o contrato não foi redesenhado para strings monetárias; testes extremos de persistência usam decimal .NET. Formulário de catálogo tem restrição histórica de preço positivo enquanto domínio aceita zero; não se alterou essa regra de UI nem seus testes nesta tarefa de modelo.

## Fechamento
T-031 concluída após revisão e provas próprias abrangentes. As 15 features tiveram motor=0 e record.exitCode=0, 266 resultados por execução: 260 PASS, zero falhas e seis skips antigos. Logs/TRX/JSON preservados por feature em .local-verification/t031/certified; summary.json registra códigos e trx-summary.json confere contadores/resultados reais. Frontend final: 232 PASS em 49 arquivos, typecheck, build e lint relevante exit0. Nenhuma falha intermediária de testes nesta tarefa.

Conferidor adicional de fechamento teve duas recusas administrativas corrigidas, sem alterar TRX/JSON de provas: Windows PowerShell5 mantinha o array de ConvertFrom-Json aninhado com @(); VSTest escreve Counters.notExecuted=0 mesmo com seis UnitTestResult NotExecuted. O conferidor agora lê a coleção JSON diretamente e conta skips pelos resultados reais, exigindo os mesmos 260 PASS/6 skips/0 falhas/266 totais. Não se alterou o motor ou test runner.

Plano sequencial regenerado para as 11 tarefas restantes, apenas como artefato informativo. Executor não iniciado e modelos históricos sugeridos pelo motor não adotados. T-088 e todas as outras tarefas históricas continuam pendentes. Gate final em .spec/audit-t031.json; o resultado desse gate prevalece sobre este status de implementação.

Gate final: onp-spec audit --ci --json saiu 0; ok=true, 15 features, 95/95 critérios provados, zero erros e zero avisos. Uma suposição aberta preexistente global permanece registrada; nenhuma pergunta em aberto. Diff check dos dois repositórios passou após remover apenas espaços finais produzidos no HTML do plano. HEADs mantidos, porta 5050 sem listener, hashes do backup e provas preservadas conferidos em final-environment.json/preserved-evidence.json. Não houve commit/push/deploy.
