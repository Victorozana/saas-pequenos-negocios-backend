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
