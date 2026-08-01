# Design: Cadastro de tenants

## Fluxo habilitado pela API

1. Criar acesso: dados da pessoa ficam no payload final, sem gravação antecipada.
2. Identificar empresa: `GET /api/v1/company-registry/cnpj/{cnpj}`.
3. Confirmar empresa: usuário revisa os dados retornados e complementa contatos/endereço.
4. Dados fiscais: inscrições, isenção, regime e e-mail fiscal.
5. Responsabilidade e termos: vínculo declara `is_legal_representative` e registra versões/instante dos aceites.
6. Conclusão: `POST /api/v1/tenants` grava todo o agregado e inicia verificação de e-mail.

Não haverá tenant parcial entre telas. O frontend mantém o rascunho local até o POST final.

## Tabelas

- `tenants`: CNPJ, razão social, nome fantasia, natureza jurídica, CNAE principal, categoria, contatos, endereço, situação de onboarding/status e auditoria.
- `tenant_fiscal_profiles`: PK/FK `tenant_id`, inscrições, flags de isenção, regime e e-mail fiscal.
- `users`: identidade global da pessoa física.
- `tenant_memberships`: vínculo explícito, papel `owner_admin`, `is_legal_representative`, cargo e status.
- `idempotency_records`: chave, hash do pedido, estado e resposta da criação.

## CNPJ

O valor canônico tem 14 posições, sem pontuação e em caixa alta. Formatos numérico legado e alfanumérico coexistem. A decisão atual da ADR 002 de armazenar somente dígitos precisa ser atualizada antes da implementação.

## Elegibilidade

`ICompanyRegistryGateway` isola o fornecedor externo. A aplicação decide elegibilidade: situação exatamente ativa e ao menos um CNAE principal/secundário presente no catálogo alimentício versionado. Indisponibilidade externa não é tratada como inelegibilidade.

## Transação

O handler valida entrada, consulta/valida novamente a empresa, inicia transação, garante idempotência e unicidades, grava todos os registros e a caixa de saída de e-mail e então confirma. Conflitos são traduzidos para HTTP 409.

## Privacidade

CPF aparece apenas onde necessário, nunca em claims, logs ou respostas integrais. Banco, documentos e KYC não pertencem aos contratos desta feature.

## Dependências

Depende de `fundacao-api-testes` e dos componentes de `identidade-acesso-inicial`. Só deve ser exposta em produção após `isolamento-multitenancy` e `contrato-openapi-swagger` estarem aprovadas.
