# Revisão T-088 — Entidades de domínio para orçamentos e serviços — 12/10/2026

## Base e preservação

Execução manual, modelo/esforço atuais, auxiliares herdando configuração autorizada. Backend main/977d415 e frontend master/3d8b597 conferidos por status/branch/log. Cinco obj locais e .codex-checks frontend preservados. Cópia das provas anteriores e dos cinco arquivos em .local-verification/t088/preexisting; backup runtime-repair preservado. Nenhum commit/push/deploy, executor headless, serviço em 5050 ou alteração no banco/segredos/cadastros de trabalho.

AGENTS dos dois repos, skill onp-spec-driven, continuidade, PostgreSQL RLS, revisão/audit T-031 e spec/design/tasks lidos. Caminhos históricos substituídos pelos diretórios explicitamente indicados pelo usuário. Nenhuma lição confirmada disponível no motor. Frontend não modificado; contrato HTTP/OpenAPI e schema permanecem iguais.

## Abrangência e sobreposição

Quotation/QuotationItem/QuotationStatus/DepositInfo/DepositType revisados contra US-045/US-046 e AC-090/AC-092. Factories exigem TenantId/CustomerId não vazios; cliente e catálogo têm validação no tenant em criação/edição. Coleção exposta somente de leitura; domínio começa Draft, criação pública valida conteúdo e persiste Pending. POST/GET e roundtrip PostgreSQL provam identidade, associação, itens, total e entrada.

T-031 já certificou dinheiro, quantidade, arredondamento e atomicidade de cálculos. T-103 já certificou edição somente Pending e concorrência com aprovação. T-088 acrescenta matriz de estados, montagem/publicação, identidades/coleção e ausência de mutação em decisões recusadas, sem declarar T-089 a T-092 ou T-032 a T-037 concluídas. Provas de persistência/HTTP complementam o modelo, não substituem revisão própria dessas tarefas.

## Estados e decisão pendente

Draft → Pending exige itens. Pending → Approved exige validade; Pending → Rejected; Approved → Converted. CheckExpiration só altera Pending com validade anterior ao dia UTC; hoje ainda válido, null não expira. Cancel permite Draft/Pending/Expired e repetição Canceled sem mudança; Approved/Rejected/Converted recusam. Montagem direta de itens/entrada somente Draft; edição completa somente Pending. Nenhum enum foi renumerado.

AC-093 exige bloquear decisões repetidas. No domínio Approve/Reject são estritos; no handler, aprovação de Approved/Converted retorna sucesso idempotente. O teste histórico rotulado AC-093 exercitava somente rejeição de Converted, portanto não prova toda a frase. Q-022 foi respondida pelo usuário: documentar exceção idempotente da aprovação pública de Approved/Converted, mantendo rejeições repetidas bloqueadas. AC-093 atualizado segundo essa decisão; nenhuma idempotência removida. Os novos testes HTTP comprovam explicitamente o comportamento atual, sem atribuí-lo como conformidade integral de AC-093.

## Lacuna real corrigida

UpdateQuotationStatusHandler chamava CheckExpiration antes de validar o pedido; recusa de orçamento vencido alterava o agregado rastreado para Expired mesmo sem SaveChanges. Validação agora recusa antes de mutar. Cancelamento vencido continua permitido e persiste Canceled; aprovação/rejeição/estado desconhecido continuam 400 pelo mesmo contrato. Teste rastreado exige EntityState.Unchanged, snapshot integral e SaveChanges posterior zero, eliminando vazamento da recusa para outra gravação no mesmo contexto.

## Provas próprias

QuotationLifecycleTests e QuotationStatusAtomicityTests: 53 PASS, exit 0, zero falhas/skips. Matriz 35 combinações, guardas fora de Draft, associação/coleção, datas, entrada imutável e recusa rastreada/persistência posterior. Release/UseAppHost=false/artifacts exclusivos; domínio em .local-verification/t088/domain/results/domain-01.trx e domain-01.log.

QuotationLifecycleHttpTests: duas provas PASS, exit 0, zero falhas/skips, HTTP InMemory e PostgreSQL temporário exclusivo com RLS/logins restritos. Criação anônima/cliente vazio/inexistente/externo/sem itens recusada sem registros parciais; publicação Pending e itens persistidos; leitura/PDF/decisão/conversão externos bloqueados; snapshots completos nas recusas; aprovação e conversão repetidas não duplicam recebíveis/OS; rejeição/cancelamento/vencimento. TRX inicial e renovado preservados em .local-verification/t088/http/results, focused.log/JSON próprios.

Regressão global concluída: 15 verificações independentes, cada uma com motor=0 e record.exitCode=0, 321 resultados: 315 PASS, zero falhas e seis skips históricos. Cada log/TRX/JSON copiado antes da seguinte em .local-verification/t088/certified; summary.json confere códigos e resultados reais. Q-022 respondida pelo usuário. Audit de revisão --ci --json saiu 0: ok=true, 95/95 critérios provados, zero erros/avisos. Revisão independente somente leitura não encontrou novos achados.

## Fechamento

Somente T-088 marcada concluída após revisão própria, decisão Q-022, provas e audit aprovado. T-089 a T-092 e T-032 a T-037 permanecem pendentes. Plano sequencial regenerado para dez tarefas restantes; executor não iniciado, modelos históricos não adotados. Gate final em .spec/audit-t088.json. Os 53 casos unitários e dois HTTP/PostgreSQL novos somam 55 resultados adicionais às 266 provas anteriores.

Cobertura AC-093 é composta: teste histórico anotado prova rejeição de Converted; matriz de domínio prova decisões estritas; novos QuotationLifecycleHttpTests provam aprovação pública repetida em Approved/Converted sem mutação/duplicação e rejeições repetidas bloqueadas. Não atribuir a abrangência inteira apenas ao teste antigo selecionado pelo motor. Conversão repetida aqui é sequencial; esta tarefa não certifica integralmente concorrência de conversão/T-091.

Conferência final: 15/15 registros motor=0/record.exitCode=0 e 315 PASS/6 skips/0 falhas, hashes dos cinco obj e backup iguais à baseline, diff --check exit0 nos dois repos, HEADs/branches preservados, nenhum listener5050. Evidência em .local-verification/t088/final-environment.json. Nenhuma falha intermediária de testes; sem lição nova por este caminho limpo (candidatos sugeridos pelo motor são históricos anteriores). A suposição global aberta preexistente permanece; Q-022 respondida e nenhuma pergunta aberta no gate.

## Limites operacionais

Sem migrations novas ou aplicadas a agendamento_local. AddCustomerAutomaticReminderPreference e 20261012191900_AddDailyReceivableReminderDeduplication continuam não aplicadas ao banco de trabalho; não iniciar versão nova nesse banco antes de rollout autorizado. Regras Q-020/Q-021, RLS, permissões, CPF/PreserveDocument, idempotência financeira/conversão, notificações/opt-out/aviso diário e PDF serial/semântico preservados. Sem entrega real WhatsApp/e-mail ou telas financeiro/equipe.
