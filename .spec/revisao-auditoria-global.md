# Revisão da auditoria global — 12/10/2026

## Escopo e execução

Execução manual sequencial, mantendo modelo e esforço da conversa. Gateway permanece para etapa futura. As telas de financeiro/equipe e a entrega real de e-mail aguardam a resolução da base auditada. Nenhuma migration foi aplicada ao banco de trabalho nesta revisão.

## Integridade documental

- Baseline reproduzida: 264 erros e 40 avisos, exit 1 (`audit-global-atual.json`).
- Auditoria intermediária: 143 erros e 33 avisos, exit 1 (`audit-global-intermediaria.json`).
- Histórias com dois-pontos e critérios em listas foram normalizados para a gramática da skill, preservando os requisitos.
- Requisitos antigos de serviços/orçamentos, financeiro e notificações foram recuperados dos commits f326f63, 9012638 e cdcd95b, respectivamente. Specs posteriores haviam substituído esses requisitos, deixando testes órfãos.
- As tarefas históricas foram recuperadas pendentes; recuperação documental não comprova implementação.
- Diretórios em `Arquivos:` foram expandidos em arquivos explícitos. O motor verifica existência de caminhos e mapeia código arquivo por arquivo.
- Referências a CustomerApiTests, ConversionMetricsDto separado, Features/Health e QuotationItemConfiguration foram corrigidas para o código existente.
- IDs duplicados de suposições/perguntas foram separados, sem alterar decisões.

## Falha de isolamento reproduzida e corrigida

`TenantWriteGuardTests` adicionou sete casos: leitura de A/B, inserção divergente síncrona/assíncrona e alteração/exclusão de registro de B pelo contexto A, com e sem troca do TenantId.

Antes da correção: cinco casos passaram e dois falharam. Trocar o TenantId de uma entidade carregada de B para A permitia alterar ou apagar o registro de B.

`TenantSaveChangesInterceptor` passou a validar também o TenantId original em registros modificados/excluídos. Depois da correção: sete casos passaram, zero falharam. Essa prova cobre EF com entidades rastreadas; não certifica SQL direto/RLS.

`TenantContextTests` cobre tenant externo em header, query e body, além de acesso sem sessão. `LocalPostgreSqlIsolationTests` aplica migrations em duas bases temporárias exclusivas, valida versão e ausência de compartilhamento de dados. Os seis testes antigos pulados foram preservados.

## RLS: lacunas técnicas ainda abertas

1. `20260807010000_AddRowLevelSecurity.cs` não possui atributos de descoberta de migration/DbContext nem companion Designer. A lista de migrations do assembly compilado, obtida com EF `migrations list --no-connect`, não contém AddRowLevelSecurity. Isso não inspeciona o estado do banco de trabalho; comprova sua ausência na cadeia descoberta.
2. O SQL dessa migration usa nomes de tabelas PascalCase sem aspas e TenantId para tabelas fiscais/vínculos. Antes de executar qualquer correção, confrontar nomes físicos reais e ordem de criação das tabelas.
3. A migration antiga habilita RLS em títulos, notificações e assinaturas, mas não define policies para essas tabelas. WorkOrderItems e pagamentos também precisam de revisão.
4. `UnitOfWork.BeginTransactionAsync` usa `SET LOCAL ROLE agendamento_app_user` e `agendamento.current_tenant_id`; o design pede `app.tenant_id`. O único handler que chama essa entrada é o cadastro de tenant. Outros caminhos abrem transações diretamente ou consultam sem essa configuração.
5. `CustomerConfiguration` exige TenantId e índices, mas não configura FK para Tenant. Revisar todas as entidades tenant-owned contra a constituição.
6. A prova P-004 foi retirada de `QuotationApiTests`: esse teste usa EF InMemory, não PostgreSQL. As asserções foram preservadas. Não reatribuir essa prova sem teste real com papel restrito, SQL direto e pool reutilizado.
7. T-019/T-020/T-021 foram reabertas. Arquivos ausentes continuam listados como trabalho planejado, sem inventar substitutos.

## Decisões confirmadas nesta conversa

- Orçamento enviado pode ser editado enquanto pendente; fica bloqueado após aprovação, rejeição ou conversão. Critério AC-206 e tarefa pendente T-103.
- Converter orçamento cria a OS e aguarda início manual da execução; não inicia automaticamente.
- Prestador pode desativar lembretes automáticos por cliente. Critério AC-207 e tarefa pendente T-104.

## Cobertura e semântica a revisar

- AC-038: minimização completa de respostas/exemplos OpenAPI ainda não está provada; não basta anotar o teste atual de palavras sensíveis.
- AC-097 pede serviço centralizado INotificationService; a implementação usa ICommunicationGateway. Resolver o contrato, sem mudar o requisito apenas para aprovar.
- AC-098 exige vínculo com cliente; NotificationMessage registra destinatário, mas não CustomerId.
- US-052 (aviso de fatura vencida) ainda não possui critério executável de disparo.
- AC-093 inclui aprovação/rejeição de estados já finais; o teste atual verifica convertido. Aprovação repetida é idempotente no financeiro. Revisar a compatibilidade entre requisitos, sem enfraquecer prova.
- Critérios históricos de PDF, transições, outbox, templates, expiração/cotas de assinatura e extrato precisam de revisão de abrangência. PASS do teste anotado não transforma uma asserção parcial em cobertura integral.

## Ordem de continuidade

1. Concluir a revalidação granular e manter tarefas pendentes quando a prova não cobre todo o resultado.
2. Implementar RLS com migration nova e testável, papel restrito, contexto transacional e caminhos públicos/administrativos explícitos; testar SQL direto, gravações e reutilização de pool em bases exclusivas antes de aplicar ao banco local.
3. Resolver cobertura/minimização de dados e divergências dos requisitos; implementar edição pendente e preferência por cliente.
4. Reexecutar verify e auditoria global. Apenas exit 0 autoriza declarar fechamento global.
5. Implementar telas de financeiro/equipe e configurar provedor de e-mail; gateway segue fora desta etapa.

## Resultado final

A revalidação terminou. O gate global retorna exit 1, com 10 erros e 5 avisos; o detalhamento está abaixo e em audit-global-final.json.

### Gate final executado

- onp-spec audit --ci: exit 1; 10 erros e 5 avisos.
- 86/94 critérios com prova granular; 15 features revalidadas.
- Suítes: 163 aprovados, zero falhas e seis skips antigos (169 resultados).
- Relatório completo: audit-global-final.json. Frontend limpo, commit 37cd243; alterações desta revisão permanecem locais no backend.
- A terceira auditoria manteve lacunas de RLS e requisitos sem teste. A skill determina parar e apresentar achados quando o mesmo problema persiste por três auditorias; nenhuma entrega global foi encerrada.

Os HTMLs de plano passaram por formatação automática de linhas em branco; parâmetros e ordem gerados foram preservados. git diff --check: exit 0.

## Gate após a retomada RLS

- Auditoria global: exit 1, **6 erros e 0 avisos**; 89/94 critérios com prova granular.
- 15 features revalidadas depois da correção da fixture de concorrência. Cada execução final lê 175 resultados: 169 aprovados, zero falhas e seis skips antigos.
- Isolamento: 8/8 critérios PASS; AC-033/AC-034/AC-035 e P-004 possuem prova PostgreSQL real. T-019/T-020/T-021 concluídas com essas provas; isso não fecha a auditoria global.
- O teste financeiro agora exige duas consultas FOR UPDATE bloqueadas simultaneamente no PostgreSQL antes de liberar o orçamento. Pool de uma conexão apenas na prova específica de reutilização.
- Uma execução intermediária de equipe retornou exit 1. A repetição diagnóstica executou as três suítes e retornou exit 0; a causa intermediária não foi reproduzida. Não foi descartado teste nem suprimido erro. Log da repetição em .local-verification/rls-refresh-concurrency/equipe-runner.raw.log; prova final em .spec/verification/gestao-equipe-permissoes.json.
- Pendências do gate: AC-038 (minimização OpenAPI), AC-206 (edição pendente), US-052 (critério para fatura vencida), AC-097 (serviço centralizado), AC-098 (vínculo com cliente) e AC-207 (preferência de lembretes).
- Relatório final desta retomada: .spec/audit-rls.json. Relatório intermediário: .spec/audit-rls-intermediaria.json. Os relatórios anteriores e o histórico de sinais foram preservados.
- Sem aplicação em agendamento_local, sem alteração de cadastros ou segredos locais, sem commit/push. Frontend limpo. Antes de iniciar a API com esta versão, provisionar a migration e as duas credenciais restritas conforme docs/POSTGRESQL_RLS.md.
