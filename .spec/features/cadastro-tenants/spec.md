# Spec: Cadastro de tenants

> feature: cadastro-tenants
> status: rascunho

## Contexto

O cliente primário é uma pessoa jurídica de uma das categorias de negócio suportadas pelo produto, identificada por CNPJ. O cadastro público precisa consultar e validar a empresa, capturar dados negociais, endereço e perfil fiscal, criar uma pessoa física como primeiro administrador e vincular todos os registros de forma atômica. Dados bancários, documentos e KYC serão coletados somente em um onboarding financeiro futuro.

## Histórias

### US-007 — Identificar uma empresa elegível

Como pessoa responsável, quero consultar meu CNPJ antes de preencher o restante do cadastro, para confirmar que a empresa pode usar a plataforma.

#### AC-014 — CNPJ inválido é rejeitado localmente

- **Dado** um CNPJ com formato ou dígitos verificadores inválidos
- **Quando** `GET /api/v1/company-registry/cnpj/{cnpj}` é solicitado
- **Então** a resposta informa CNPJ inválido e nenhum provedor externo é consultado

#### AC-015 — Empresa inativa não pode continuar

- **Dado** um CNPJ válido cuja situação cadastral não é ativa na fonte consultada
- **Quando** seus dados são buscados
- **Então** o cadastro é bloqueado e a resposta informa que a empresa não está ativa

#### AC-016 — Empresa ativa não é bloqueada pelo CNAE

- **Dado** um CNPJ ativo com qualquer CNAE retornado pela fonte cadastral
- **Quando** seus dados são buscados
- **Então** a consulta continua e devolve os dados da empresa sem aplicar a antiga política alimentar

#### AC-017 — Consulta elegível preenche dados da empresa

- **Dado** um CNPJ válido e ativo
- **Quando** seus dados são buscados
- **Então** a resposta contém CNPJ normalizado, razão social, nome fantasia, natureza jurídica, CNAEs, situação e endereço disponíveis na fonte

### US-008 — Informar dados negociais e fiscais

Como pessoa responsável, quero revisar e complementar os dados da empresa, para que o tenant tenha informações suficientes para operar e evoluir para faturamento fiscal.

#### AC-018 — Dados obrigatórios são validados por etapa

- **Dado** um pedido de cadastro sem razão social, categoria, contato empresarial ou endereço completo
- **Quando** `POST /api/v1/tenants` é solicitado
- **Então** nenhum registro é criado e os campos inválidos são devolvidos em `application/problem+json`

#### AC-019 — Perfil fiscal pertence exclusivamente ao tenant

- **Dado** dados fiscais válidos no cadastro
- **Quando** o tenant é criado
- **Então** existe exatamente um `tenant_fiscal_profile` vinculado ao tenant com inscrições, isenção, regime tributário e e-mail fiscal informados

#### AC-020 — Cadastro inicial não coleta dados financeiros ou documentos

- **Dado** o contrato público de cadastro de tenant
- **Quando** seu esquema de entrada e persistência é inspecionado
- **Então** ele não aceita conta bancária, certificado fiscal, RG/CNH, contrato social, alvará ou arquivos de KYC

### US-009 — Criar empresa e primeiro administrador juntos

Como pessoa responsável, quero concluir um único cadastro, para receber um tenant com meu usuário administrador já vinculado.

#### AC-021 — Cadastro composto cria todos os vínculos

- **Dado** dados válidos de empresa, perfil fiscal e pessoa física ainda não cadastrados
- **Quando** `POST /api/v1/tenants` é solicitado
- **Então** são criados tenant, perfil fiscal, usuário e membership `owner_admin`, preservando a escolha de `is_legal_representative`

#### AC-022 — CNPJ é único globalmente

- **Dado** um tenant existente com determinado CNPJ normalizado
- **Quando** outro cadastro envia o mesmo CNPJ com máscara ou caixa diferentes
- **Então** nenhum novo tenant é criado e a resposta é HTTP 409

#### AC-023 — Repetição idempotente não duplica o cadastro

- **Dado** uma criação concluída com determinada `Idempotency-Key` e o mesmo conteúdo
- **Quando** o pedido é repetido com essa chave
- **Então** a resposta original é reaproveitada e nenhum registro adicional é criado

#### AC-024 — Falha parcial desfaz toda a criação

- **Dado** uma falha ao persistir qualquer componente do cadastro composto
- **Quando** a transação termina
- **Então** tenant, perfil fiscal, usuário, membership, token e pedido de e-mail não permanecem parcialmente gravados

### US-010 — Consultar o cadastro autenticado

Como administrador autenticado, quero consultar minha empresa e minha identidade, para conferir os dados associados à sessão atual.

#### AC-025 — Usuário consulta somente o tenant da sessão

- **Dado** uma sessão válida vinculada a um tenant
- **Quando** `GET /api/v1/tenants/me` é solicitado
- **Então** a resposta contém somente o tenant resolvido pela autenticação e ignora qualquer tentativa de escolher outro tenant

#### AC-026 — Perfil pessoal minimiza dados sensíveis

- **Dado** uma sessão válida
- **Quando** `GET /api/v1/users/me` é solicitado
- **Então** a resposta contém nome, e-mail, telefone, status e vínculo, mas mascara o CPF e não retorna credenciais

### US-011 — Completar o fluxo de cadastro em etapas claras

Como pessoa responsável, quero avançar pelo cadastro em etapas previsíveis, para corrigir apenas os dados relacionados ao passo atual.

#### AC-027 — Contrato suporta as seis etapas aprovadas

- **Dado** o fluxo criar acesso, identificar empresa, confirmar empresa, informar dados fiscais, aceitar responsabilidade/termos e concluir
- **Quando** o cliente usa a consulta de CNPJ e envia o cadastro final
- **Então** os campos e erros da API são agrupáveis por essas seis etapas sem exigir gravação parcial do tenant

## Fora de escopo

- Frontend ou layout visual das seis telas.
- Dados bancários, documentos, upload, KYC e integração com adquirente.
- Emissão de NF-e, NFC-e ou NFS-e e armazenamento de certificados.
- Cadastro de clientes consumidores dos restaurantes.
- Edição de tenant após a criação.

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-004 | `tenant_fiscal_profiles` será uma relação 1:1 com `tenants`. | confirmada | Aprovado pelo dono do produto em 01/08/2026. |
| ASM-005 | Banco, documentos e KYC pertencem a um onboarding posterior. | confirmada | Aprovado pelo dono do produto em 01/08/2026. |
| ASM-006 | O primeiro administrador pode ser representante legal ou funcionário autorizado. | confirmada | Aprovado; `tenant_memberships.is_legal_representative` registra a condição. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Q-003 | Qual provedor de consulta cadastral implementará `ICompanyRegistryGateway`? | respondida | BrasilAPI será o provedor inicial configurável; o gateway mantém o contrato independente do fornecedor. |
| Q-004 | O cadastro aceita CNPJ alfanumérico? | respondida | Sim; aceita 14 posições, com letras nas 12 primeiras e dígitos verificadores nas duas últimas, preservando CNPJs numéricos existentes. |
