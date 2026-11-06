# Project agent rules

## Encerramento e continuidade (preferência permanente do usuário)

- Nesta máquina, o backend é este repositório e o frontend é o diretório irmão `../saas-pequenos-negocios-frontend`. Os caminhos antigos abaixo são históricos.
- Ao concluir uma entrega de desenvolvimento neste projeto, executar as verificações pertinentes, revisar o diff e fazer commit e push das alterações da entrega nos repositórios afetados. O usuário autoriza esse fluxo habitual; isso substitui a exigência genérica de nova aprovação de push abaixo. Uma instrução posterior do usuário prevalece.
- Não incluir segredos, logs locais, caches ou artefatos de build no commit. Preservar alterações alheias à entrega; não usar force push, reset destrutivo ou descarte para limpar o status.
- Conferir branch, remoto e upstream antes de publicar. Após o push, confirmar que HEAD e upstream coincidem e informar branch, hash, verificações e pendências locais. Se houver falha de autenticação, conflito, teste falhando ou dúvida sobre arquivos, relatar o impedimento e o que ficou salvo; nunca afirmar que houve push sem confirmação.
- Registrar no repositório o estado funcional e o próximo passo quando houver trabalho incompleto. Não encerrar silenciosamente com trabalho existente apenas nesta máquina.

- Em entregas que toquem agenda, ordens de serviço, autorização, rotas ou PDF, execute a regressão completa com `pwsh ./scripts/regression.ps1` a partir do frontend irmão. Registre os SHAs das duas aplicações e os resultados de PostgreSQL e Playwright no fechamento.

## Frontend integration authority

- The canonical frontend repository is `C:\Users\victo\OneDrive\Documentos\projetos\front-agendamento`.
- Agents working on the shared MVP may inspect, start, build, test, debug, and modify this backend without requesting permission again when the work is directly required by the active frontend/backend task.
- Read the applicable project-local skill under `.agents/skills/` before using its workflow.
- The Development HTTP profile is defined in `src/Agendamento.Api/Properties/launchSettings.json` and listens on `http://localhost:5050`.
- Keep the OpenAPI document complete and accurate. Backend contract changes must include backend tests and must be regenerated into the frontend typed schema before frontend integration is considered complete.
- Run `git status` and inspect recent commits before making changes. Preserve all pre-existing modifications. In particular, do not discard or overwrite existing changes in `.spec/verification/sinais.json` or generated `obj/` files unless the active task explicitly requires them.
- Local builds, tests, Development service startup, temporary databases, and scratch worktrees are authorized. Start background services with hidden windows and report the process/port to the user.
- Use safe Development data. Do not expose or log passwords, JWTs, verification tokens, connection strings, personal data, or other secrets.
- Do not push, deploy, access production, execute destructive migrations or database operations, force Git operations, or delete user-owned work without explicit approval.
- Keep the user informed when starting or stopping services, changing contracts, applying migrations, modifying either repository, or encountering blockers.
