# Regras do Projeto - Agendamento (SaaS Prestadores de Serviço)

## Automação de Commits
- Ao concluir a implementação completa de uma feature ou módulo (ex: `gestao-clientes`, `gestao-orcamentos`), ou quando todas as tarefas de uma task forem finalizadas com sucesso e verificadas por testes, o agente deve automaticamente efetuar o commit das alterações no Git.
- A mensagem de commit deve ser clara e descritiva no padrão Conventional Commits (ex: `feat(clientes): implementa gestão de clientes e isolamento multi-tenant`).

## Spec-Driven Development (Skill onp-spec-driven)
- O agente deve SEMPRE utilizar a skill `onp-spec-driven` instalada no diretório `.agents/skills/onp-spec-driven` para planejar, executar, verificar e auditar o desenvolvimento das funcionalidades do projeto.
- Siga rigorosamente o ciclo de desenvolvimento orientado a especificações (Especificar → Projetar → Tarefas → Plano → Executar → Auditar → Aprender) e garanta que a auditoria mecânica (`onp-spec audit --ci`) passe com sucesso antes de finalizar a task.

