# Feature Spec: Gestão de Clientes

> feature: gestao-clientes
> status: pronta

## Visão Geral
Esta feature permite que os assinantes do SaaS (marmorarias, pedreiros, carpinteiros, autônomos) cadastrem e gerenciem a carteira de clientes final (Pessoa Física e Pessoa Jurídica), servindo de base para a emissão de orçamentos, contratos e ordens de serviço.

## User Stories (US)

### US-017: Cadastrar Cliente Final (Pessoa Física ou Jurídica)
Como prestador de serviço/pequeno negócio autenticado,
Quero cadastrar um novo cliente informando CPF/CNPJ, nome/razão social, telefone (WhatsApp), e-mail e endereço de entrega/execução do serviço,
Para que eu possa associar orçamentos, contratos e vendas a este cliente.

#### AC-041 — Cliente pertence ao Tenant autenticado
- **Dado** um novo cliente a ser cadastrado
- **Quando** o cadastro é realizado
- **Então** o registro deve pertencer obrigatoriamente ao TenantId autenticado.

#### AC-042 — Validação de CPF ou CNPJ
- **Dado** um CPF ou CNPJ informado no cadastro
- **Quando** a criação é validada
- **Então** o formato do documento deve ser validado (11 dígitos para CPF e 14 para CNPJ).

### US-018: Listar e Buscar Clientes
Como prestador de serviço autenticado,
Quero pesquisar meus clientes por nome, CPF/CNPJ ou telefone,
Para localizar rapidamente o contato e histórico de atendimento.

#### AC-043 — Busca paginada e filtro parcial
- **Dado** uma lista de clientes cadastrados
- **Quando** a busca é executada informando filtro por nome, documento ou telefone
- **Então** o sistema deve retornar os resultados paginados e filtrados.

### US-019: Atualizar e Inativar Clientes
Como prestador de serviço autenticado,
Quero atualizar dados de contato/endereço e inativar clientes antigos,
Para manter a base de dados organizada e atualizada.

#### AC-044 — Proteção de isolamento entre tenants
- **Dado** um cliente pertencente a outro tenant
- **Quando** um tenant tenta buscar ou alterar este cliente
- **Então** a operação deve negar o acesso ou retornar nulo/404.
