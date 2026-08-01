# Constituição — v1.1.0

<!--
  Princípios inegociáveis do projeto. Não são estilo: são restrições.
  P-xxx = princípio (código de rastreio, como US/AC/T).
  Níveis: [DEVE] obrigatório · [RECOMENDADO] forte · [PODE] permitido/explícito.
  Todo [DEVE] precisa de verificação executável — senão o audit acusa
  "princípio sem verificação" (PRINCIPIO_SEM_VERIFICACAO). Formatos:
    - verificação(gate): satisfeita pelo próprio audit (só p/ princípios "meta")
    - verificação(teste): @principle:P-xxx
    - verificação(proibido): `regex` em `glob`
    - verificação(obrigatório): `regex` em `glob`
-->

## P-001 [DEVE] Todo requisito tem prova executável

Nenhuma feature é declarada pronta sem o audit em modo CI sair limpo (exit 0).
Este princípio é verificado pelo próprio mecanismo do audit (AC_SEM_TESTE,
AC_SEM_PROVA, TASK_CONCLUIDA_SEM_PROVA) — não precisa de teste extra seu.

- verificação(gate): intrínseca ao audit

## P-002 [RECOMENDADO] Segredos nunca em código

Chaves e senhas vêm de variáveis de ambiente, nunca hard-coded.

- verificação(proibido): `(api[_-]?key|senha|password)\s*[:=]\s*['"][^'"]{8,}` em `src/**/*.cs`

## P-003 [DEVE] O request nunca escolhe o tenant

O contexto de tenant vem exclusivamente da autenticação/autorização. Valores em
body, query string ou headers não confiáveis não substituem a claim emitida pelo
sistema.

- verificação(teste): @principle:P-003

## P-004 [DEVE] PostgreSQL RLS protege todo dado tenant-owned

Toda tabela pertencente ao tenant possui `tenant_id NOT NULL`, chave estrangeira,
índice iniciado por `tenant_id` e policy RLS. O papel da aplicação não possui
`BYPASSRLS`.

- verificação(teste): @principle:P-004

## P-005 [DEVE] Credenciais e identificadores pessoais são minimizados

Senha, hash, token bruto de verificação, CPF integral e credenciais fiscais ou
bancárias não aparecem em respostas, claims ou logs.

- verificação(teste): @principle:P-005
