# Feature Spec: Gestão de Clientes

## Visão Geral
Esta feature permite que os assinantes do SaaS (marmorarias, pedreiros, carpinteiros, autônomos) cadastrem e gerenciem a carteira de clientes final (Pessoa Física e Pessoa Jurídica), servindo de base para a emissão de orçamentos, contratos e ordens de serviço.

## User Stories (US)

### US-017: Cadastrar Cliente Final (Pessoa Física ou Jurídica)
Como prestador de serviço/pequeno negócio autenticado,
Quero cadastrar um novo cliente informando CPF/CNPJ, nome/razão social, telefone (WhatsApp), e-mail e endereço de entrega/execução do serviço,
Para que eu possa associar orçamentos, contratos e vendas a este cliente.

### US-018: Listar e Buscar Clientes
Como prestador de serviço autenticado,
Quero pesquisar meus clientes por nome, CPF/CNPJ ou telefone,
Para localizar rapidamente o contato e histórico de atendimento.

### US-019: Atualizar e Inativar Clientes
Como prestador de serviço autenticado,
Quero atualizar dados de contato/endereço e inativar clientes antigos,
Para manter a base de dados organizada e atualizada.

## Critérios de Aceite (AC)

- **AC-041**: Todo cliente cadastrado deve pertencer obrigatoriamente ao `TenantId` autenticado (isolamento por multi-tenancy).
- **AC-042**: Deve validar o formato de CPF ou CNPJ (se informado).
- **AC-043**: A busca deve permitir paginação e filtro parcial por nome, documento ou telefone.
- **AC-044**: Tentativa de buscar ou alterar um cliente de outro tenant deve retornar 404/403.
