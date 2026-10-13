# Revisão T-090 — Endpoints de Gestão de Orçamentos — 13/10/2026

## Base, instruções e preservação

Execução manual em backend main/15831f8 e frontend master/3d8b597, modelo/esforço atuais.
Dois auxiliares herdaram configuração: revisão HTTP/atomicidade e provas HTTP/PostgreSQL.
AGENTS dos dois repositórios, skill local onp-spec-driven, continuidade, RLS, revisões/audits
T-023/T-031/T-088/T-089 e spec/design/tasks lidos. Caminhos históricos substituídos pelo
workspace explícito do usuário. Motor não possui lições confirmadas. Sem executor headless,
commit/push/deploy ou serviço em5050; nenhum banco/cadastro/segredo de trabalho alterado.

Cinco obj copiados com hashes, verificação/sinais anteriores copiados em
.local-verification/t090/preexisting; provas T-089 certified e backup runtime-repair têm hashes.
Schema anterior preservado em frontend .codex-checks/t090/schema-before.d.ts. Relatórios/TRX
e evidências anteriores mantidos; cada nova rodada tem log/TRX separado. Builds e testes
Release/UseAppHost=false com outputs exclusivos. Segredos usados apenas em memória pelas
fixtures; conexões/senhas/tokens não impressos.

## Revisão própria e sobreposição

QuotationEndpoints integra POST criação, GET listagem/detalhe e PATCH status pelo Program/DI.
PUT foi examinado como integração T-103, mantendo substituição completa apenas Pending e
lock PostgreSQL compartilhado com aprovação. PDF/conversão revisados para autorização,
isolamento e contrato preservado, sem certificar T-091 ou entregas PDF.
T-034 tem revisão própria de orquestração/AC-045/046/048; T-036 inclui catálogo e PDF;
T-092/T-037 têm escopos próprios de testes. Nenhuma dessas tarefas é concluída automaticamente.
Notas históricas sobre órfãos não são diagnóstico atual: registro de rotas/DI já existe.

Tenant vem exclusivamente da identidade ativa. GET/listagem/detalhe/PDF exigem sales.read;
POST/PUT/PATCH/conversão exigem sales.write. Spoof body/header/query é ignorado como autoridade.
Cliente e catálogo pertencem ao tenant; detalhe retorna DTO completo sem documento do cliente.
Filtros EF, guardas dependentes e RLS permanecem. Criação pública persiste Pending.

## Lacunas reais corrigidas

- Listagem não validava paginação/status, permitia tamanho ilimitado e overflow de int.
  Agora Page>=1, PageSize1..100, defaults1/20, offset calculado em long/limitado a int.MaxValue,
  status Enum.IsDefined; inválidos retornam400 ProblemDetails seguro antes de consultar.
- IssueDate isolada não garantia desempate estável. Ordem agora IssueDate DESC, Id ASC;
  total considera filtros, página além do total é vazia. Estabilidade vale para dados inalterados,
  sem prometer snapshot entre requisições sob gravações concorrentes.
- Code/Notes/PaymentTerms/Deposit.PaymentNotes excedidos chegavam ao PostgreSQL como500.
  QuotationInputValidation compartilha limites físicos50/2000/2000/1000, antes de mutação.
  Datas ausentes/sem UTC também são400 antecipado em vez de erro Npgsql timestamptz.
- Código repetido local chegava à constraint como500 e edição mutava rastreamento antes da
  falha. Precheck local (excluindo o próprioID no PUT) antecede alterações; corrida é resolvida
  pela constraint única e endpoint trata somente23505/IX_Quotations_tenant_id_Code como400.
  Falhas inesperadas permanecem500 seguro. Gravação relacional é atômica, sem linhas parciais.
- Location201 observado estava ausente do OpenAPI; metadata agora descreve header e limites.

Status mantém validação de vencimento antes de mutar, cancelamento vencido permitido,
AC-093/Q-022 (aprovação Approved/Converted idempotente sem duplicação e rejeições repetidas
recusadas). Domínio estrito, Q-020/Q-021, RequiredAmount, concorrência financeira e edição,
CPF/PreserveDocument, notificações/destinatários/opt-out/aviso diário e PDF serial preservados.
Nenhuma migration nova.

## Provas próprias e contrato

QuotationEndpointsHttpTests: dois fatos, mesmo cenário InMemory e PostgreSQL temporário
exclusivo/logins restritos. Round05 exit0,2PASS,zero falhas/skips em
.local-verification/t090/http/results/round05.trx. Matriz7 operações x autenticação/permissões,
401vazio/Bearer,403ProblemDetails, binding400/415, IDs malformados/inexistentes/externos,
detalhe todos campos, Location/mídias, criação Pending, referências externas, spoof,
busca por código/cliente, filtros combinados/total/páginas/empates/limites/overflow,
comprimentos exatos/excedidos, datas, duplicidade local/reutilização entre tenants,
PUT válido/bloqueado e snapshots/contagens sem mutação ou recebíveis/OS adicionais.

QuotationEndpointAtomicityTests: cinco fatos, atomicity-r7 exit0,5PASS,zero falhas/skips em
.local-verification/t090/atomicity-r7. Create/PUT recusados preservam snapshot/EntityState.Unchanged,
conjunto de entidades rastreadas e SaveChanges posterior zero. Corrida real de criação
PostgreSQL garante duas chegadas após precheck, um201/um400 e deltas exatos sem linhas parciais.
Falhas controladas alheias à constraint retornam500 seguro por HTTP. OpenAPI live observa
Location201 e400ProblemDetails de listagem. Não há teste por reflection de método privado.

Build backend exit0/zero erros/avisos. Exportação OpenAPI por host InMemory exit0/1PASS,
log .local-verification/t090/openapi-export.log e TRX no diretório exclusivo ali registrado.
docs/openapi/v1.json exportado e frontend schema.d.ts regenerado pelo arquivo local.
Documentação instalada Next.js16.2.11 (TypeScript/Vitest) lida antes da regeneração.
Consumidores usam toISOString/UTC e pageSize10/default20; nenhum componente/BFF alterado.
Frontend typecheck exit0 e232/232testes PASS (49 arquivos), zero falhas/skips;
logs/JSON em .codex-checks/t090. Aviso jsdom de navegação é preexistente, suíte passou.

## Tentativas intermediárias preservadas

HTTP round01/02: expectativas de ordem de itens/bodyPATCH/contagens seed ajustadas na
preparação; filtros/asserts de produto mantidos. Round04: leitura duplicada do stream de
resposta corrigida usando buffer string. Logs/TRX anteriores preservados, não são provas PASS.
Atomicity-r2: autenticação usava chave de outro host; login passou a ocorrer no host exercitado.
Atomicity-r3: contagens consideravam base vazia embora fixture semeie orçamento; prova passa
a comparar baseline+delta e mantém exigência de duas chegadas reais na barreira.
Reflection de predicate substituída por falha HTTP observada, sem enfraquecer asserções existentes.

Ampliação final: duas edições PUT de orçamentos distintos disputam o mesmo código após
precheck (barreira com duas chegadas), produzindo204/400 seguro. Snapshot persistido do
perdedor permanece idêntico e vencedor tem conteúdo/itens/sinal completos; deltas de itens
e contagens de orçamentos/recebíveis/OS são exatos. Esta prova cobre rollback/persistência
HTTP, não restauração do ChangeTracker após falha relacional: scope de requisição é
descartado. Preservação rastreada/SaveChanges0 refere-se às validações antecipadas.

Coletor de renovação adicional: primeira invocação exit1 antes dos testes por array JSON
encapsulado no Windows PowerShell5 (Specified method is not supported). Atribuição direta
da coleção corrigiu a compatibilidade; log falho preservado em recertify-powershell5-failure.log.
Não foi falha do motor/runner nem alterou asserções; rodada final usa log separado.

## Gate e pendências

Renovação final das 15 features concluída: motor=0/record.exitCode=0 em todas, 367 resultados
por execução (361 PASS, zero falhas, seis skips históricos). As primeiras três execuções
(366 resultados) foram renovadas após inclusão da prova PUT concorrente, preservando as
evidências iniciais. Logs/TRX/JSON em .local-verification/t090/certified e recertified;
índice final em final-certified-summary.json. Audit de revisão --ci exit0, ok=true,
95/95 critérios provados, zero erros/avisos em audit-review.json.
Somente T-090 marcada concluída após revisão própria, provas abrangentes e gate aprovado.
Plano sequencial regenerado para oito tarefas restantes, apenas informativo; executor não
iniciado e modelos históricos não adotados. Gate final em .spec/audit-t090.json.
Conferência final: 486 entradas de evidências/obj mais backup preservados por hash,
HEADs 15831f8/main e 3d8b597/master mantidos, diff --check dos dois repos exit0, nenhum listener5050.
Uma suposição global aberta preexistente permanece; nenhuma pergunta aberta ou lição nova.
T-091/T-092 e T-032 a T-037 permanecem pendentes; T-031/T-088/T-089/T-103 mantêm conclusão.
Concorrência integral da conversão segue sem certificação própria. Sem entrega real
WhatsApp/e-mail ou telas financeiro/equipe.

AddCustomerAutomaticReminderPreference, AddDailyReceivableReminderDeduplication e
EnforceQuotationCatalogTenantReference continuam NÃO aplicadas a agendamento_local.
Não iniciar versão nova nesse banco sem rollout explícito. Backup runtime-repair preservado.
