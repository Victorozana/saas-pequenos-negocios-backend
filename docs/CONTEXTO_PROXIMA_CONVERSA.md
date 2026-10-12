# Contexto para a próxima conversa — 12/10/2026

## Projeto e repositórios

SaaS de pequenos negócios, com empresas isoladas por tenant.

- Workspace: `C:\Users\Victor Ozana\Documents\agendamento`.
- Backend: `saas-pequenos-negocios-backend`, .NET 10, EF Core/PostgreSQL, branch `main`, remoto https://github.com/Victorozana/saas-pequenos-negocios-backend.
- Frontend: `saas-pequenos-negocios-frontend`, Next.js 16/React 19, branch `master`, remoto https://github.com/Victorozana/saas-pequenos-negocios-frontend.
- Os caminhos históricos em alguns AGENTS.md apontam para outra máquina. Trabalhar nos diretórios deste workspace.
- Ler AGENTS.md e a skill `.agents/skills/onp-spec-driven/SKILL.md` do backend. Conferir git status e commits antes de editar; preservar mudanças locais, especialmente arquivos obj já rastreados.

## Decisões do usuário

- Execução manual sequencial, mantendo o modelo e esforço atuais; não iniciar agentes ou scripts que escolham outros modelos automaticamente.
- Gerar recebíveis ao aprovar o orçamento, com entrada e saldo.
- Pagamentos manuais nesta etapa. Não integrar gateway agora.
- Esta revisão foi de backend e contrato; as telas de financeiro/equipe ainda serão desenvolvidas.
- O usuário autorizou explicitamente commits e push dos dois repositórios nesta conversa. Isso não certifica a auditoria global como aprovada.

## Ambiente local

PostgreSQL 18 está instalado e usa localhost:5432. Banco do projeto: `agendamento_local`. API Development em http://localhost:5050. A conexão é configurada em segredos locais do .NET, com UserSecretsId `agendamento-local-development`; nunca imprimir senha, token ou connection string.

`scripts/configurar-banco-local.ps1` pede a senha com entrada oculta e configura conexão/migrations. A migration `20261012002349_AddActiveTeamMembership` foi aplicada localmente, mantendo membros existentes ativos. Reiniciar a API pode invalidar sessões por causa da chave de assinatura de desenvolvimento; entrar novamente se necessário.

## O que foi corrigido

1. Conversão orçamento → OS não salvava no banco. Agora persiste a OS e o status convertido; uma repetição sequencial retorna a OS existente. Agendamentos conseguem usar o ID salvo. A atualização de status da OS também passa a persistir.
2. Frontend de orçamento consulta o catálogo ativo do tenant, permite selecionar serviço e preenche nome, unidade e preço; envia serviceItemId. Mantém descrição manual.
3. Alerta de UUID substituído por confirmação e botão “Agendar esta OS”, que abre formulário com vínculo preenchido.
4. Financeiro: recebíveis de entrada/saldo na aprovação, proteção contra duplicação em aprovações simultâneas, pagamentos manuais persistidos, saldo/extrato corretos, DTOs explícitos e permissões financial.read/financial.write.
5. Equipe: ativar/desativar vínculo sem bloquear outras empresas, proteger último Admin/owner_admin, revogar JWT anterior quando vínculo é desativado, aceitar convite sem sessão usando tenant do convite validado, rejeitar replay/expiração e preservar senha de usuário existente.
6. Tokens de convite não aparecem na resposta HTTP e o gateway mock não imprime conteúdo/destinatário. O envio de e-mail é simulado; configurar provedor antes de oferecer convites reais ao usuário.
7. Permissões aplicadas também a serviços, vendas, agenda e relatórios; autenticação verifica usuário e vínculo ativos.
8. OpenAPI/snapshot atualizados e schema tipado do frontend regenerado. IDs de critérios de assinaturas foram separados dos de equipe para evitar provas atribuídas à feature errada.

## Provas e pendências

Última verificação: 153 testes aprovados, zero reprovados, seis testes antigos pulados, 159 resultados lidos. Testes pulados não contam como prova. Financeiro: 3/3 critérios PASS; equipe: 6/6 PASS. As 12 tasks desses módulos estão concluídas, mas as features continuam sem fechamento global.

Teste PostgreSQL usa base temporária exclusiva criada/removida pelo próprio teste. Verifica pagamentos, convite público e aprovação concorrente sem duplicação. Não altera os cadastros do banco de trabalho.

`scripts/verify-spec.ps1` roda as três suítes e converte TRX para JSON granular. Executar:

```powershell
node .agents/skills/onp-spec-driven/scripts/onp-spec.mjs verify gestao-financeira-contas
node .agents/skills/onp-spec-driven/scripts/onp-spec.mjs verify gestao-equipe-permissoes
node .agents/skills/onp-spec-driven/scripts/onp-spec.mjs audit --ci
```

A auditoria global continua com exit 1: 264 erros e 40 avisos; não há achados específicos de financeiro/equipe. São problemas de outras specs e rastreabilidade global: formatos não reconhecidos, referências quebradas, IDs duplicados, arquivos mapeados ausentes, requisitos sem testes, provas fracas/antigas e código sem task. Não suprimir erros, enfraquecer testes ou inventar provas.

Detalhes: `.spec/revisao-financeiro-equipe.md`, `.spec/audit-financeiro-equipe.json` e provas em `.spec/verification/gestao-*.json`. O teste desta revisão comprova persistência local e isolamento HTTP; não certifica RLS de produção.

## Próximos passos recomendados

1. Priorizar e corrigir a auditoria global, começando pela integridade de specs/tasks e critérios duplicados. Revalidar o que for corrigido e implementar cobertura onde falta, sem marcar apenas por aparência.
2. Revisar isolamento-multitenancy/RLS e tarefas que apontam arquivos ausentes. Diferenciar erro documental de requisito realmente não implementado.
3. Sincronizar as tasks de orçamento/serviços que continuam pendentes com o código e testes existentes, respeitando o gate da skill.
4. Quando a base estiver auditada, especificar e implementar frontend financeiro: contas a receber/pagar, entrada/saldo, pagamentos parciais/totais e extrato, com erros e permissões adequados.
5. Implementar frontend de equipe: membros, papéis/permissões, ativação/desativação, convites e aceite. Configurar entrega real de e-mail antes de liberar convites.
6. Deixar gateway para depois. Distinguir cobrança da assinatura do SaaS de cobrança dos clientes de cada tenant; definir provedor/contas, tarifas, webhooks autenticados e idempotência antes de integrar.

Nenhum deploy foi realizado. Commits/push deste ponto de continuidade foram pedidos pelo usuário; obter os hashes pelo git log ou pela mensagem final desta conversa.
