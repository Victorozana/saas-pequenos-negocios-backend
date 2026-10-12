# Contrato HTTP/OpenAPI — T-023 — 12/10/2026

Execução manual no workspace informado, backend main/96875bb e frontend master/ca1d485.
Modelo/esforço mantidos, com três agentes auxiliares herdando a configuração atual.
Nenhum executor headless de modelo, commit, push ou deploy. Alterações anteriores preservadas.

## Escopo revisado

Todas as 50 operações implementadas do contrato: health (1), identidade (2), perfil de usuário
(1), tenant/cadastro (2), CNPJ (1), clientes (4), serviços (4), orçamentos (7), OS (3), agenda
(3), financeiro (6), notificações (4), dashboard (3), equipe/convites (5), assinaturas (3),
webhook (1). Swagger e o próprio endpoint do documento continuam sendo infraestrutura técnica.

## Correções

- Clientes: 400 agora é ProblemDetails com título seguro; ID divergente tem erro explícito.
  GET/PUT documentam os 404 vazios e PUT documenta 204 vazio.
- Serviços, agenda e OS: sucessos possuem schemas; resultados vazios têm metadata explícita.
  Dados inválidos, conflitos de horário, conversão sem aprovação e alteração de OS finalizada
  recebem erros seguros correspondentes ao domínio. Conversão alternativa mantém 201 e a
  repetição retorna a mesma OS. Não foram alteradas regras de status ou financeira.
- Orçamentos: criação/edição/status/conversão padronizam falhas aplicáveis em ProblemDetails;
  coleções e elementos null são rejeitados antes de mutações. 404/204 vazios preservados.
- Notificações: schedule-reminder/send-quotation-notification documentam e produzem 400
  ProblemDetails. Condições, destinatários, opt-out e fluxo manual/orçamento/convites mantidos.
- PDF: application/pdf, string/binary no OpenAPI 3.0, Content-Disposition e Content-Length.
  CSV: text/csv, string textual e os mesmos headers. Provas verificam bytes PDF (%PDF/%%EOF)
  e conteúdo semântico extraído por PdfPig 0.1.16 (dependência exclusiva de testes, PrivateAssets=all):
  páginas válidas, código, cliente, serviço e total formatado. CSV verifica código/cliente/valor,
  nomes e tamanho; tenant externo não recebe dados.
- Transformer não acrescenta ProblemDetails a todo erro. JWT mantém challenge 401 vazio,
  WWW-Authenticate e IncludeErrorDetails=false. 403 de permissão usa ProblemDetails real.
- Exceções inesperadas têm handler explícito 500 ProblemDetails, inclusive em Development,
  com título fixo e supressão de diagnósticos padrão que poderiam conter credenciais da
  exceção. Transformer documenta esse comportamento universal; provas de falha controlada
  do PDF nos dois ambientes proíbem mensagem interna, stack e Authorization na resposta.
- Binding inválido de corpo/query/header recebe 400/415 seguro pelo StatusCodePages específico
  com ThrowOnBadRequest=false; 204/404/401 intencionais permanecem vazios. A documentação de
  binding considera corpo/query/header; GUID inválido rejeitado pelo roteamento não inventa 400.
- CNPJ/cadastro: mídia real application/problem+json e mensagens fixas seguras. Rate limit
  de CNPJ passa a retornar o 429 documentado; indisponibilidade do provedor continua 503.
- Confirmação de e-mail documenta 400 e 204. Sessão produz o 503 que já estava documentado
  quando há falha de banco/timeout, sem detalhe interno. Credenciais inválidas continuam 401
  ProblemDetails na rota pública, distinto do challenge vazio em operações protegidas.
- Assinatura/plano ausentes retornam 404 seguro; webhook malformado 400 e sucesso 200 vazio.
  Equipe/financeiro mantêm 200/204/404 vazios e seus erros 400/409 reais. Expiração durante
  aceite de convite tem o mesmo 400 seguro, sem mudar entrega/regras de convite.
- /health aparece no contrato com GET e texto 200/503; metadata inclui MethodInfo do handler
  real HealthCheckMiddleware. Prova de Healthy/Unhealthy não revela detalhe da health check.

## Contrato e frontend

tools/export-openapi.ps1 usa exclusivamente host InMemory de teste em Release/UseAppHost=false,
saída e TRX exclusivos e falha explicitamente se exportação falhar, restaurando UPDATE_SNAPSHOTS.
docs/openapi/v1.json foi exportado e schema.d.ts regenerado pelo arquivo, sem consultar API 5050.

Documentação instalada Next.js 16.2.11 consultada antes de editar frontend. Consumidor de
conversão lê title ProblemDetails, aceita 401 vazio e valida workOrderId. Atualização de OS
envia enum numérico conforme schema, preservando nomes usados pelo frontend. Quatro testes
novos cobrem essas respostas e serialização; nenhuma tela financeira/equipe foi iniciada.

## Evidências e preservação

Provas focadas dos agentes: .local-verification/t023-core/, t023-exports-proof/ e
t023-agent-subscriptions/. Provas adicionais: .local-verification/t023/health-handler-results/.
Todos os casos novos têm @spec:AC-037, com health também em AC-036; testes anteriores e suas
asserções foram preservados. EndpointMetadataTests acrescenta verificações transversais de
401 sem content/header, 204 vazio e schema em toda mídia de resposta.

Registros anteriores foram copiados para .local-verification/t023/preexisting-verification/;
snapshot anterior e sinais incluídos. Schema frontend anterior em .codex-checks/t023/schema-before.d.ts.
Renovações intermediárias estão em .local-verification/t023/renewal/ e final inicial em final/.
Renovações intermediárias adicionais em .local-verification/t023/final-all/; uma delas pegou
compilação intermediária do parser e retornou motor=0/runner=1, sendo recusada. Prova focada
final do parser: .local-verification/t023-pdf-semantic-proof/pdf-semantic-final.trx (2 PASS).
Outra rodada em certified/ detectou quebra de linha do código longo no extrator PDF: apenas
espaçamento de apresentação de código/valor foi normalizado, mantendo fixture longa e todos
os caracteres. Três repetições focadas finais passaram (2/2 cada), com logs/TRX próprios em
.local-verification/t023-pdf-layout-*. O coletor foi ajustado para que stderr nativo de teste
falho não aborte a geração do JSON granular atual no Windows PowerShell.
certified-final/ teve 14 execuções aprovadas e uma falha real de extração PDF (NUL), mantendo
motor=0/runner=1; o audit intermediário saiu 0, mas não foi usado para encerrar a tarefa.
A investigação reproduziu 48 PDFs corrompidos em 400 gerações concorrentes; 400 sequenciais
passaram. pypdf confirmou os NULs e os mapas ToUnicode dos arquivos apontavam para <0000>.
Evidências e 48 PDFs falhos em .local-verification/t023-pdf-reproducer/.

QuestPdfQuotationGenerator agora compartilha SemaphoreSlim estático entre instâncias,
com espera cancelável e liberação em finally somente na etapa nativa de renderização.
Layout/dados permanecem iguais; cada processo renderiza um PDF por vez para evitar a falha.
Reprodutor corrigido: 400 concorrentes, zero corrupção; prova HTTP de 24 downloads concorrentes
distintos passou, preservando todas as asserções anteriores. Integração completa focada final:
145 PASS, seis skips antigos, zero falhas; logs/TRX em t023-pdf-render-fixed-proof/.

Renovação definitiva em .local-verification/t023/render-certified/<feature>/, com logs, TRX, JSON e
builds exclusivos. scripts/verify-spec.ps1 aceita SPEC_VERIFY_RUN_DIRECTORY e conserva stdout
de cada suíte; o orquestrador manual exige motor=0 e record.exitCode=0, copiando provas antes
da próxima execução. Nenhum desses scripts executa modelos ou o executor de planos.

Falhas intermediárias preservadas: incompatibilidade de setter IOpenApiResponse na primeira
implementação dos headers (corrigida usando OpenApiResponse concreto), expectativa inicial
incorreta de 403 vazio no teste de exportação (corrigida para exigir ProblemDetails real), e
ausência de /health no ApiExplorer até adicionar metadata do handler. Uma renovação de fundação
teve motor=0/runner=1 por testes de contrato de health; foi recusada e será renovada integralmente.
O primeiro teste de exceção inesperada autenticou com a chave de outro host da fixture e
recebeu 401; corrigida a fixture para fazer login no host exercitado, mantendo a exigência
de 500 seguro. TRX inicial e prova posterior ficam separados em secure-results/secure-final-results/.

## Operação e fechamento

Sem novas migrations. Bases PostgreSQL das regressões são temporárias exclusivas das fixtures.
Banco/cadastros/segredos de trabalho não alterados. Backup runtime-repair/agendamento_local-before-rls.dump
preservado. Nenhum comando encerrou ou reiniciou a API do usuário. Na conferência final,
PID 27004 não estava ativo e não havia listener TCP em 5050; a causa não foi determinada.
AddCustomerAutomaticReminderPreference e
20261012191900_AddDailyReceivableReminderDeduplication continuam NÃO aplicadas a agendamento_local.
Ambas são necessárias antes de iniciar versão nova nesse banco; esta tarefa não executa rollout.

Validação frontend: 225 testes em 48 arquivos passaram; typecheck, lint dos arquivos editados
e build passaram. O schema final inclui health; typecheck renovado após a última exportação.

Fechamento: T-023 concluída após revisão das 50 operações e provas HTTP abrangentes.
As 15 features finais tiveram motor=0 e record.exitCode=0: 250 resultados por execução,
244 PASS, zero falhas e seis skips antigos preservados. JSONs, logs e TRX separados em
.local-verification/t023/render-certified/<feature>/; summary.json reúne os códigos de saída.
Audit --ci: exit 0, zero erros/avisos, 95/95 critérios provados (.spec/audit-t023.json).
A tentativa de regenerar o plano sequencial após conclusão retornou que todas as tarefas
desta feature estão concluídas e não há nada a planejar. Artefatos históricos de plano/
executor foram preservados; estão obsoletos e não devem ser executados nem seus modelos adotados.
Tarefas históricas restantes de outras features continuam pendentes. Sem entrega real de
e-mail/WhatsApp ou telas financeiro/equipe. Sem commit, push ou deploy.
