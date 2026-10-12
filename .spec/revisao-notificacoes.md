# Revisão AC-097 / AC-098 — 12/10/2026

## Execução e escopo

Backend main/96875bb e frontend master/ca1d485, inicialmente limpos. Execução manual,
sequencial, mantendo modelo/esforço; sem agentes ou executores de modelos. Os planos
foram regenerados para refletir tarefas concluídas, sem iniciar o executor gerado.
Alterações de obj e histórico de sinais são preservadas. Sem commit/push/deploy nesta etapa.

## Implementação

INotificationService centraliza validação de cliente/contexto, staging na outbox e despacho
com ICommunicationGateway. Não houve renomeação do adaptador: os casos de uso usam o serviço,
e o worker delega seleção/envio/retries. Queue não salva nem envia; o caso de uso confirma a
outbox no mesmo DbContext da operação. O envio continua simulado pelo adaptador mock.

Agenda, lembrete e orçamento informam cliente obrigatório. Manual pode informar cliente;
vínculo inválido é rejeitado sem fallback nem gravação. Manual sem cliente é Standalone;
convite de equipe é System. Esses tipos não contam como interação de cliente e não podem
ter CustomerId. O legado é LegacyUnlinked sem atribuição por telefone/e-mail. Nenhuma nova
notificação de cliente dispensa vínculo; ver spec/design da feature.

A migration 20261012154514_AddNotificationCustomerHistory instala CustomerId/Audience,
CHECK de classificação, FK composta tenant/cliente com Restrict e índice de consulta.
Backfill classifica somente o legado, preservando todos os campos anteriores e retirando
o default após o upgrade. RLS/policies/grants existentes permanecem.

GET histórico aceita customerId e expõe CustomerId/Audience; manual aceita CustomerId opcional
e documenta erro 400 ProblemDetails. docs/openapi/v1.json e schema.d.ts foram regenerados a
partir de host InMemory, sem iniciar API contra banco de trabalho. T-023 permanece pendente.

## Provas significativas

- NotificationServiceTests: queue sem entrega antes do commit, troca entre gateway controlado
  e mock, vínculo persistido após despacho, cliente externo/inexistente/vazio rejeitado antes
  do staging e ausência de tenant rejeitada. Worker real é invocado com escopo DI, testa duas
  empresas, false/exceção do provedor, backoff de 2/4 minutos, falha na terceira tentativa,
  mensagens canceladas e tentativas futuras excluídas. Handlers de lembrete/orçamento
  preservam templates e vínculo do cliente, sem envio síncrono.
- NotificationHistoryHttpTests: clientes com mesmo destinatário não são confundidos; filtro
  por cliente e isolamento de empresa em HTTP; avulsa não fabrica CustomerId; vínculos
  externos/inexistentes/vazios e canal inválido retornam 400 sem mensagens extras. Mesmo ID
  e cliente permanecem no histórico após registrar envio.
- NotificationHistoryPostgreSqlTests: duas bases temporárias exclusivas, upgrade a partir
  da migration RLS, igualdade JSON de todos os campos históricos antes/depois (exceto as
  duas colunas novas), FK composta/CHECK/Restrict sob login restrito, escrita externa
  invisível e fluxo HTTP/outbox/serviço/histórico usando credenciais restritas separadas.
- NotificationDomainTests: cliente obrigatório em Customer, proibido em Standalone/System,
  LegacyUnlinked reservado à migration. Asserções antigas de fila/templates/retries preservadas.
- NotificationTests: teste existente de criação de agenda conserva suas asserções e agora
  também verifica CustomerId/Audience.

verify notificacoes-lembretes-whatsapp: exit 0, 207 resultados, 201 PASS, zero falhas e seis
skips antigos. O texto 8/8 do motor conta apenas critérios com resultado; a feature possui
nove critérios e AC-207 continua sem prova. AC-097/AC-098 estão provados; T-097/T-098/T-100
concluídas. As demais provas globais são renovadas em sequência antes da auditoria final.

Frontend: typecheck exit 0, suíte integral com 209 testes PASS em 45 arquivos. EF has-pending-
model-changes: sem alterações; git diff --check de ambos os repositórios passou.
Logs de verify/renovação: .local-verification/notifications/.

## Falhas intermediárias e limites

O compilador dos testes novos rejeitou Assert.Single sobre Where; corrigido para o overload
com predicado, mantendo a mesma condição. A primeira prova de exclusão esperava SQLSTATE
23503, mas ON DELETE RESTRICT retorna 23001. A fixture passou a usar cliente exclusivo sem
outras referências e exige o nome exato da FK de notificações na rejeição. Nenhuma asserção
existente foi removida ou afrouxada.

O teste de build existente passou na primeira verificação completa desta etapa. A causa da
intermitência histórica continua sem diagnóstico definitivo; a saída integral permanece
registrada com as asserções originais.

Nenhuma migration foi aplicada a agendamento_local e nenhum cadastro/segredo local foi alterado.
As provas temporárias não constituem implantação local. AC-207, US-052 e AC-206 continuam
pendentes; não avançar para financeiro/equipe frontend ou entrega real de e-mail com gate
reprovado. O veredito final ficará em .spec/audit-notificacoes.json.

## Intermitência durante a renovação global

A renovação de gestao-agendamentos-ordens-servico registrou exitCode=1, com os cinco critérios
da feature PASS e falha no teste de build AC-001. O CLI do motor retorna 0 quando os critérios
da feature passam, mesmo se o runner global falha; isso foi detectado e a renovação interrompida.
O registro falho e seu resumo estão preservados em .local-verification/notifications/agenda-
failed-record.json e refresh-gestao-agendamentos-ordens-servico.log. O runner já havia substituído
o TRX dessa execução ao iniciar a seguinte, portanto não foi possível recuperar o diagnóstico
integral daquela falha. Não atribuir uma causa sem essa evidência.

A retomada verifica também record.exitCode e copia TRX/test-results.json para raw-<feature>
antes de executar outra feature. Nenhum teste, asserção ou regra do motor foi alterado para
contornar a falha. A ocorrência e a limitação do diagnóstico permanecem registradas.

## Revalidação final

As 15 features foram revalidadas após a versão final de código/testes. Cada prova final tem
exitCode=0 e 207 resultados: 201 PASS, zero falhas e seis skips antigos. A repetição de agenda
passou; identidade, isolamento e onboarding também terminaram com exit 0. O diagnóstico
intermediário de build permanece sem causa definitivamente resolvida.

T-097/T-098/T-100 concluídas somente após PASS; plano sequencial regenerado sem executar
headless. As sete tarefas históricas/preferência ainda pendentes não foram marcadas por
aparência. Auditoria final é executada depois dessas mudanças concretas; relatório em
.spec/audit-notificacoes.json, sem certificação global antecipada.

## Gate final

Auditoria global: exit 1, três erros, zero avisos; 92/94 critérios com prova granular.
Achados restantes: AC-206 sem teste, US-052 sem critério e AC-207 sem teste.
AC-097/AC-098 deixaram de ser achados. Nenhuma aprovação global foi declarada.

A regeneração do plano produziu três linhas com espaços finais; um normalizador removeu
somente esses espaços, sem alterar conteúdo/ordem/modelos do plano. git diff --check final
passou após essa correção. O stdout JSON do motor inclui um aviso de sinais depois do objeto;
a saída bruta foi preservada e somente o objeto JSON é salvo no relatório consultável.
