# Design — Catálogo de serviços e modelo de orçamentos (T-031)

## Revisão e escopo
Não existia design.md desta feature na árvore nem no histórico consultado. Este documento registra a revisão do modelo atual; não certifica tarefas históricas de persistência, endpoints, PDF ou estados.
ServiceItem e Quotation implementam ITenantOwned. Factories recusam tenant vazio; autenticação, filtros EF e RLS continuam responsáveis pelo pertencimento em runtime. Criação e edição conferem cliente e referências ao catálogo no tenant autenticado.
Quotation nasce Draft internamente; CreateQuotationHandler valida conteúdo completo e publica Pending. Isso preserva AC-090 sem remover a montagem do agregado.

## Decisões confirmadas pelo usuário em 12/10/2026
Valores monetários de entrada (preço base/unitário, desconto, sinal fixo), percentual e quantidade aceitam até duas casas decimais. Quantidade deve ser positiva. Valores e agregados monetários e quantidade têm teto 9.999.999.999.999.999,99. Não há normalização silenciosa de entradas com precisão excessiva.
Subtotal = preço unitário × quantidade, arredondado por item para duas casas, MidpointRounding.AwayFromZero. Desconto absoluto incide sobre esse subtotal; pode ser igual a ele, nunca maior. Total geral = soma dos subtotais arredondados − soma dos descontos. Subtotal, desconto e total agregados precisam caber no teto mesmo quando o desconto reduziria o total final.
Entrada percentual = total × percentual / 100, arredondada para duas casas pelo mesmo método; percentual entre zero e 100. Entrada fixa entre zero e total. Saldo = total − entrada já arredondada. Zero e itens gratuitos são válidos. Enum desconhecido é recusado.
Quantidade também fica limitada à precisão já persistida pela OS, sem migration. O consumidor de OS usa o mesmo subtotal arredondado. Recebíveis usam RequiredAmount calculado pelo orçamento, impedindo divergência entre entrada/saldo e valores persistidos.

## Atomicidade e compatibilidade
AddItem/RemoveItem calculam uma lista candidata e validam totais e sinal antes de substituir itens ou campos. Edição pendente mantém o agregado substituto validado antes de qualquer mutação. Validações financeiras usam ArgumentException, mantendo respostas 400 ProblemDetails já documentadas; DTOs, rotas e enums numéricos não mudam.
Não alterar guardas de estados, idempotência financeira, locks, destinatários/condições das notificações, opt-out ou renderização serial do PDF.
Frontend deve aplicar arredondamento por item também na prévia e comparar desconto contra subtotal arredondado; a API permanece autoridade final. JSON number e consumidores JavaScript têm precisão limitada para extremos, uma restrição preexistente do contrato; prova dos extremos do domínio/persistência usa decimal .NET, não certifica exatidão de centavos em JavaScript acima da sua precisão numérica.

## Sobreposição
T-088 compartilha Quotation, itens, entrada e enums, mas abrange estados e critérios próprios. Permanece pendente; T-031 não certifica T-032 a T-037 nem T-088 a T-092 automaticamente. A revisão não altera a política histórica Cancel de Expired para Canceled.

## Provas
QuotationModelValidationTests: limites, arredondamento, entradas inválidas, tenant e ausência de mutação nos métodos de domínio.
QuotationModelHttpTests: recusa HTTP sem persistência/mutação, catálogo externo/inexistente, roundtrip e recebíveis sob PostgreSQL temporário exclusivo com logins restritos/RLS.
Regressões existentes: edição/concorrência, permissões, CPF/PreserveDocument, isolamento, financeira/notificações e PDF semântico concorrente. Logs/TRX/JSON por execução em .local-verification/t031; confirmar motor e record.exitCode separadamente.

## Revisão T-088 — ciclo de vida

O agregado monta Draft, mas a API publica apenas Pending após validar referências e conteúdo. Itens são expostos por coleção somente de leitura. TenantId permanece setter público por ITenantOwned/EF; a guarda de gravação compara tenant original e atual, filtros EF e RLS protegem leitura/persistência. A factory sozinha não certifica isolamento.

Transições de domínio e operações de montagem serão provadas por matriz de estados e snapshots completos. Expiração usa DateTime.UtcNow.Date, considera validade de hoje ainda válida e só muda Pending. Cancel de Expired para Canceled e repetição Canceled permanecem históricos. Estados Approved/Rejected/Converted bloqueiam alterações de conteúdo e decisões no domínio.

Uma recusa no handler de status não deve executar CheckExpiration antecipadamente: isso modificava o objeto rastreado embora não salvasse no banco. Validação de vencimento agora precede a decisão, sem mutação; cancelamento de vencido continua permitido. Não há mudança de DTO/rota/status HTTP/OpenAPI ou migration.

Aprovação pública repetida é uma exceção idempotente implementada no handler para Approved/Converted. O domínio continua estrito. O usuário resolveu Q-022 em 12/10/2026: AC-093 admite aprovação pública repetida idempotente de Approved/Converted sem mutação/duplicação e mantém rejeições repetidas bloqueadas. Conversão e suas proteções permanecem existentes, sem certificação integral de T-091 por esta revisão de domínio.

## Revisão T-089 — mapeamento efetivo e integridade

Quotation tem PK UUID, cliente e tenant obrigatórios, Code varchar(50), Notes/PaymentTerms varchar(2000), status integer com enum estável, IssueDate/ValidUntil timestamptz (validade opcional), totais numeric(18,2). Índices tenant/código único, tenant/status e tenant/cliente permanecem. Customer Restrict e Items Cascade preservados; navegação Items usa explicitamente o campo _items. QuotationItem tem PK UUID, QuotationId/tenant_id obrigatórios, referência de catálogo nullable, nome/unidade obrigatórios e valores numeric sem typmod, como no schema histórico. As regras de entrada Q-020/Q-021 continuam no domínio; esta revisão não afirma que SQL bruto valida dinheiro ou enum.

DepositInfo é owned opcional na mesma tabela de Quotation; sua ausência deixa as colunas físicas null, presença materializa inclusive zeros/IsPaid=false. Value/RequiredAmount/RemainingBalance são numeric(18,2), notas varchar(1000), Type integer e IsPaid boolean. Não há tabela separada nem policy distinta: a linha do orçamento é a barreira RLS.

O filtro EF explícito de QuotationItem usa o TenantId shadow e contexto autenticado. A guarda valida original/current nos itens Modified/Deleted e recusa Added com tenant externo ou contexto ausente antes de preencher shadows. Escritas de DepositInfo validam o owner Quotation rastreado, seu tenant original/current, mesmo quando o owner está Unchanged; owned sem owner é recusado. RLS permanece proteção independente, incluindo IgnoreQueryFilters/SQL direto.

A migration histórica RLS acrescenta FKs compostas cliente/orçamento e item/orçamento por SQL, além das FKs simples descritas pelo modelo/snapshot. A nova EnforceQuotationCatalogTenantReference corrige uma lacuna real: FK composta (tenant_id, ServiceItemId) para ServiceItems(tenant_id, Id), chave alternativa e índice correspondente, Restrict. Itens manuais com ServiceItemId=null continuam válidos. Catálogo referenciado não pode ser excluído; fluxos existentes usam desativação. Referências históricas inválidas impedem upgrade transacional; nenhuma linha é limpa automaticamente. A migration será aplicada somente a bases temporárias nesta tarefa.

T-032 sobrepõe DbContext/configuração, mas inclui persistência e migrations do catálogo como entrega própria; não é encerrada por esta revisão de orçamentos. T-090 a T-092/T-033 a T-037 continuam pendentes. Nenhum contrato HTTP/OpenAPI muda, frontend/schema não precisam regeneração. Concorrência integral de conversão permanece fora da certificação T-089.
