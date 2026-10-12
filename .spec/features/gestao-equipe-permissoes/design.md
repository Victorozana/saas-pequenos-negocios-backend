# Revisão do backend de equipe

## Escopo

Corrigir e comprovar os critérios AC-070 a AC-075 antes de integrar telas. As tarefas T-080 a T-087 foram reabertas porque a prova anterior registrou falha e o formato de lista não era reconhecido pelo parser de tasks.

## Decisões técnicas

- Desativação pertence ao vínculo tenant/usuário; desativar um funcionário de uma empresa não bloqueia sua conta em outras empresas.
- A autorização consulta vínculo ativo e usuário ativo no banco em cada request. Admin e owner_admin compartilham a definição de papel administrativo.
- O último administrador ativo não pode ser desativado nem perder o papel administrativo. As alterações concorrentes precisam manter essa invariável.
- O aceite público deriva o tenant exclusivamente do convite validado no servidor. Não aceitar TenantId enviado pelo cliente e não desabilitar globalmente o interceptor de isolamento.
- O aceite de convite para usuário existente não troca sua senha. O consumo deve ser único e atômico.
- Convites não retornam token bruto na API. A entrega ocorre pelo canal de notificação, e tokens não devem aparecer nos logs do gateway de desenvolvimento.
- Documentar DTOs e respostas no OpenAPI; regenerar o schema do frontend após validar o contrato.

## Verificação

Testes HTTP para listagem isolada, alteração de permissões, desativação, revogação com JWT anterior, proteção do último administrador, criação e aceite de convite sem sessão, convite expirado e replay. Testar persistência no PostgreSQL em base temporária, sem alterar dados de usuário. Registrar prova por critério em vez de usar somente o exit code agregado.

## Execução proposta

Execução manual sequencial nesta conversa, mantendo modelo e esforço atuais, confirmada pelo usuário em 11/10/2026. Os scripts gerados pelo motor usam defaults próprios e não devem ser executados sem confirmar suas configurações. Sem frontend de produto nesta etapa; somente sincronização do contrato tipado.
