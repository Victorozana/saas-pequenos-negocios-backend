# Preferência de lembretes e edição pendente — 12/10/2026

Bases preservadas: backend main/96875bb e frontend master/ca1d485. Execução manual,
com agentes auxiliares autorizados pelo usuário e sem alterar modelo/esforço.
Modificações anteriores, obj, schema gerado, sinais e relatórios foram mantidos.

## AC-207 / T-104

Customer.AutomaticRemindersEnabled é persistido e exposto no cadastro, edição, lista e
detalhe. Default de criação true; update nullable preserva o valor se omitido. A migration
AddCustomerAutomaticReminderPreference faz backfill true e remove o default transitório.
Não altera valores antigos, índices, grants ou policies. Não foi aplicada ao banco de trabalho.

QueueAutomaticReminderAsync centraliza validação de cliente/tenant e opt-out antes de staging.
Criação de agenda e handler ScheduleAppointmentReminder usam essa entrada. Mensagens manuais,
orçamento, avulsas e convites System mantêm seus caminhos. Outbox histórica permanece intacta.
O critério cobre preparação de novos lembretes, conforme especificação.

Frontend possui checkbox no cadastro/edição e carrega o valor persistido. CPF mascarado continua
usando PreserveDocument; substituição por CPF completo continua disponível e tem regressão.

Sete casos de integração novos passaram na execução focada, com PostgreSQL real em base exclusiva:
dois caminhos automáticos; cliente desativado, outro habilitado e outra empresa; despacho;
manual/orçamento/System; rejeição de vínculo externo e contexto ausente; criação default/false;
edição/leitura/lista/omissão/reativação com CPF; upgrade comparando todos os campos antigos e RLS.

## Ambiente e limites

API preexistente PID 27004 está ouvindo 5050. Foi preservada. Build Debug falhou por arquivos
exe/DLL bloqueados por esse processo; Release com UseAppHost=false e output de testes exclusivo
funcionou. Não confundir essa causa observada com a intermitência histórica do teste Release.
Uma compilação Release sobreposta a teste do agente encontrou lock de obj; verificações finais
serão sequenciais. Nenhuma proteção de runtime ou asserção foi removida.

Contrato regenerado por WebApplicationFactory InMemory, sem API/banco de trabalho. Segredos
não foram impressos nem modificados. Nenhum commit/push/deploy foi feito nesta etapa.
US-052 foi definido com o usuário nesta retomada; nenhum aviso de fatura vencida foi implementado por inferência.

## AC-206 / T-103

PUT /api/v1/quotations/{id} atualiza conteúdo completo somente Pending. Valida cliente,
catálogo, itens e entrada antes de modificar o agregado; substitui itens e recalcula totais.
Identidade/status permanecem. Estados Approved/Rejected/Converted rejeitam edição sem alteração.
Transação e SELECT FOR UPDATE compartilham bloqueio com aprovação para impedir gravação tardia
de conteúdo baseado num estado Pending obsoleto. Não cria recebíveis durante edição; aprovação
continua responsável por entrada/saldo, e conversão mantém início manual da OS.

Seis provas focadas HTTP/PostgreSQL passaram. A concorrência é observada por pg_stat_activity
com espera real de bloqueio; após aprovação confirmada, edição retorna 400 e conteúdo permanece.
Não há enfraquecimento das asserções existentes. Frontend de edição e verificações finais
estão em integração; gate será acrescentado após revalidação das 15 features.

OpenAPI/schema foram regenerados na versão integrada; build Release teve zero avisos/erros.
EF has-pending-model-changes confirmou nenhuma diferença após recompilação sequencial.

## Falha de build observada e correção

A primeira renovação de notificações teve runner=0 e 220 resultados. A renovação seguinte de
orçamentos teve motor=0 mas runner=1: AC-206 passou, porém AC-001 falhou exigindo zero avisos.
O script interrompeu e preservou TRX/JSON/log em
`.local-verification/customer-reminders/failed-build-evidence/`.

StdOut do teste registra MSB3026 em recurso zh-Hans do testhost, bloqueado no OutDir temporário
compartilhado entre projetos da solução. O build já usava uma pasta exclusiva por execução,
mas cópias concorrentes entre projetos podiam colidir. SolutionBuildTests agora passa
maxcpucount:1 para serializar os projetos nesse OutDir. Todas as asserções foram mantidas:
exit zero, resumo zero avisos e ausência de linhas warning. Prova focada passou após a correção;
as 15 features são reexecutadas na árvore final. Esta causa explica a ocorrência com TRX desta
etapa; não certifica retrospectivamente falhas históricas sem diagnóstico preservado.

A segunda renovação de orçamento, já com build corrigido, falhou em AC-034: a asserção de
mesmo PID encontrou 21684/24740. Evidência preservada em `failed-pool-evidence/`. O teste paralelo
LocalPostgreSqlIsolationTests limpava todos os pools do processo, incluindo o pool exclusivo
que AC-034 exigia reutilizar. Cleanup agora chama ClearPool somente para cada conexão de base
temporária criada pelo próprio teste. A asserção de mesmo PID e as de isolamento/transação
foram mantidas. Nenhuma alteração de runtime ou relaxamento da prova RLS foi feita.

Frontend final integrado: 47 arquivos e 221 testes PASS, typecheck/build PASS. Uma primeira
execução de UI falhou por seletor ambíguo no teste novo (label/opção com o mesmo texto); o teste
passou a selecionar role option e a suíte integral final foi repetida. Produto não foi alterado
para contornar teste. A edição está disponível somente Pending e preserva campos completos.

## US-052 definido com o usuário

Usuário confirmou disparo automático diário de conta vencida com saldo aberto e um aviso por
conta por dia até quitar ou cancelar. Registrados Q-018/Q-019 respondidos, critério AC-208 e
tarefa T-105 pendente. Critério cobre repetição/reinício sem duplicação no dia, novo aviso no
dia seguinte, exclusão de contas quitadas/canceladas/não vencidas e isolamento por tenant.
O futuro produtor também respeitará opt-out e usará retries da mesma mensagem. Esta etapa
fecha a definição solicitada; não declara implementação nem prova de AC-208. O gate continua
pendente até essa implementação ser validada. Horário do job não foi fixado pelo usuário.

## Revalidação final

Após as correções de infraestrutura, as 15 features foram reexecutadas em sequência, todas
com motor=0 e runner=0. Cada execução final possui 220 resultados: 214 PASS, zero falhas e
seis skips antigos. AC-207 e AC-206 possuem prova granular; T-104/T-103 concluídas. Logs e
TRX separados por feature em `.local-verification/customer-reminders/raw-<feature>/`.
O teste de build manteve suas asserções e passou em todas as 15 execuções finais; a prova de
mesmo PID também. As evidências intermediárias foram preservadas, sem substituir pelo sucesso.

Frontend final: 221 testes/47 arquivos PASS, typecheck/build e ESLint dos arquivos alterados
PASS. OpenAPI/schema sincronizados, EF sem pending model changes e diff --check dos dois
repositórios PASS. Planos de notificações/orçamentos regenerados sequencialmente sem execução
headless nem adoção de modelos históricos. Auditoria final em `.spec/audit-preferencia-clientes.json`.
Sem commit/push/deploy; a migration nova não foi aplicada ao banco de trabalho.
