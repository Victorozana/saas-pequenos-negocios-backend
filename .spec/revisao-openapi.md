# Revisão de minimização OpenAPI — 12/10/2026

## Escopo

Execução manual sequencial, sem agentes ou executores de modelos. Backend em main/fb5836d e frontend do workspace em master/37cd243, inicialmente limpos. Nenhuma API externa foi iniciada; somente hosts de teste. PostgreSQL usado pelas suítes permanece em bases temporárias exclusivas. Não aplicar migrations nem modificar cadastros/segredos de agendamento_local.

## Implementação e contrato

- Removido VerificationToken do DTO, retorno do handler e header X-Development-Verification-Token do runtime. Removidos o transformer que ocultava a propriedade e a documentação da exceção Development/Test. Cadastro retorna apenas message em todos os ambientes; falhas não retornam ex.Message.
- AC-100 e ASM-013 foram revisados explicitamente conforme a instrução de 12/10/2026. A expectativa antiga de revelar o header não foi mantida como exceção silenciosa. As asserções que exigiam o vazamento foram substituídas por proibição de exposição; confirmação, login, permissões e perfil continuam verificados. Os demais testes e os seis skips antigos foram preservados.
- A fixture de onboarding verifica a criação do token persistido e adiciona um token controlado apenas para o teste de confirmação. Não recupera token bruto de HTTP/digest. Isso prova confirmação e login, não entrega real de e-mail.
- Inspeção do mapeamento revelou CPF integral sob o alias documentValue. Consultas de lista/detalhe agora retornam CPF mascarado com apenas dois dígitos finais; CNPJ permanece disponível. O schema declara pattern que rejeita CPF integral. Tipo desconhecido retorna null. Persistência mantém o documento original.
- UpdateCustomerCommand aceita PreserveDocument para editar outros campos sem reenviar CPF; entrada substituta junto dessa flag é rejeitada. Entradas originais continuam aceitando documento integral, e null sem preservação mantém a semântica de remover documento.
- Respostas JSON antes sem schema foram tipadas: perfil do tenant, IDs de criação/conversão, clientes, notificações, planos e assinatura atual. O payload JSON foi mantido, exceto CPF mascarado e retirada do token. Bearer agora é documentado por operação segundo RequireAuthorization/AllowAnonymous.
- docs/openapi/v1.json e src/lib/api/generated/schema.d.ts do frontend foram regenerados a partir da API compilada, sem iniciar serviço contra banco de trabalho.
- Frontend de cadastro/BFF retorna só mensagem e aceita código informado separadamente para confirmar e-mail. Não obtém credencial da resposta nem transforma ausência do header em erro de cadastro. Entrega real de e-mail permanece pendente. Formulário de cliente preserva CPF mascarado na edição; lista mantém a máscara.

## Provas

OpenApiDataMinimization parte dos usos de respostas, inclui headers e resolve referências locais com proteção contra ciclos; percorre propriedades, items, allOf/oneOf/anyOf e additionalProperties. Examina exemplos de entrada/saída em todo o documento, exemplos referenciados e JSON serializado. Referências inválidas e exemplos externos não passam silenciosamente. Campos legítimos de entrada não são classificados como saída pela terminação do nome do schema. Token de sessão é permitido somente na resposta 200 do login; não há exceção para token de verificação.

A prova combina percurso estrutural, restrição semântica de documentValue e testes HTTP. Casos negativos artificiais exercitam cada caminho do detector, incluindo schema de nome Request usado como resposta. Não se declara minimização somente por busca de palavras.

- verify contrato-openapi-swagger: exit 0, 5/5 critérios PASS, 198 resultados.
- verify onboarding-token-desenvolvimento: exit 0, 1/1 critério PASS, 198 resultados.
- Suítes completas nas execuções finais: 192 aprovados, zero falhas, seis skips preservados. As 15 features foram revalidadas após a última alteração de código/testes; cada execução final leu 198 resultados e retornou exit 0. Logs da última renovação em .local-verification/openapi-refresh.
- Frontend: typecheck e ESLint dos arquivos editados, exit 0; 38 testes relevantes após a correção do CPF, zero falhas. A suíte inteira havia passado 207 testes antes da mudança de CPF; esse número não é apresentado como execução integral após a última mudança.
- git diff --check dos dois repositórios: exit 0.

## Falhas intermediárias

Uma fixture de arquitetura em Development tentou usar a conexão local configurada e foi recusada pela exigência de credenciais administrativas separadas. Corrigida para DbContext exclusivamente InMemory em todos os ambientes; nenhuma operação de cadastro/migration no banco de trabalho ocorreu.

Uma compilação inicial dos testes novos falhou por literais JSON interpolados; corrigida sem remover casos. Um caso negativo detectou que o percurso de exemplos não preservava o caminho do campo; corrigido. O analisador xUnit recusou Assert.True/False para regex; trocadas por Assert.Matches/DoesNotMatch com as mesmas condições.

Uma execução completa retornou exit 1 com 198 resultados por falha do teste existente de build Release sem avisos. O build diagnóstico e a repetição completa retornaram zero avisos/erros e 192 aprovados. A falha reapareceu na renovação da fundação, ainda sem diagnóstico completo na saída antiga. SolutionBuildTests passou a registrar stdout/stderr do build em ITestOutputHelper, mantendo as três asserções originais. A suíte de arquitetura e o verify da fundação passaram depois disso. A causa do aviso intermitente ainda não foi isolada; registrar esse limite mesmo se a renovação final passar.

## Tarefas e pendências

T-025 e T-101 concluídas após verify exit 0. T-023 continua pendente: completar a coerência de erros reais de notificações/conversão/clientes com ProblemDetails, documentar retornos sem corpo e exportações binárias. A prova granular de AC-037 não certifica essas divergências como resolvidas.

A auditoria intermediária está em audit-openapi-intermediaria.json: exit 1, 18 erros, zero avisos; cinco requisitos restantes e 13 verificações obsoletas após alterações concretas. A cobertura granular alcançou 90/94 critérios, com AC-038 provado. Não declarar aprovação global.

Requisitos restantes: AC-206 (edição pendente), US-052 (critério para vencimento), AC-097 (serviço centralizado), AC-098 (vínculo com cliente) e AC-207 (preferência de lembretes). Telas de financeiro/equipe e e-mail real seguem posteriores à base auditada.

Sem commit/push/deploy. Alterações geradas de obj e histórico de sinais preservados. A revisão automática bloqueou por política a limpeza de quatro diretórios build-openapi-diagnostic criados pela tarefa; ficaram preservados e ignorados pelo Git.

O veredito final está em .spec/audit-openapi.json. A auditoria global permanece independente dos testes aprovados e da conclusão de T-025/T-101.
