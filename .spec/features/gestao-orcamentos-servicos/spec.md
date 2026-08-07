# Feature Spec: Gestão de Orçamentos e Serviços

## Visão Geral
Esta feature permite que os prestadores de serviço e pequenos negócios (marmorarias, marcenarias, serralherias, construtores, etc.) cadastrem seu catálogo de serviços/produtos e gerem orçamentos detalhados para clientes cadastrados. O módulo oferece suporte nativo a registro de Sinal/Entrada (ex: 50% de entrada no aceite e 50% na conclusão) e exportação obrigatória de orçamentos diretamente em formato PDF (sem emissão de links públicos de acesso).

## User Stories (US)

### US-020: Cadastrar e Gerenciar Catálogo de Serviços/Produtos
Como prestador de serviço autenticado,  
Quero cadastrar e gerenciar meus serviços/produtos com nome, descrição, unidade de medida (m², metro, unidade, hora, etc.) e preço base,  
Para que eu possa selecionar rapidamente itens ao montar orçamentos para os clientes.

### US-021: Criar Orçamento de Serviços/Produtos para Cliente Final
Como prestador de serviço autenticado,  
Quero emitir um orçamento associado a um cliente existente, incluindo data de emissão, validade, itens com quantidade, preço unitário e desconto,  
Para apresentar uma proposta comercial clara e detalhada ao cliente.

### US-022: Registrar Sinal / Entrada no Orçamento
Como prestador de serviço autenticado,  
Quero definir e registrar a condição de pagamento de Sinal/Entrada (definido em porcentagem ou valor fixo),  
Para formalizar o recebimento inicial exigido para iniciar a execução do serviço/fabricação.

### US-023: Gerar e Exportar Orçamento em PDF
Como prestador de serviço autenticado,  
Quero exportar e baixar o documento do orçamento formatado obrigatoriamente em PDF,  
Para enviar diretamente ao cliente (via WhatsApp ou e-mail) com garantia de privacidade, sem dependência de links públicos na web.

### US-024: Consultar e Atualizar Status do Orçamento
Como prestador de serviço autenticado,  
Quero visualizar o histórico de orçamentos e atualizar seu status (Rascunho, Pendente, Aprovado, Rejeitado, Expirado),  
Para ter controle do pipeline de vendas e negociações da empresa.

## Critérios de Aceite (AC)

- **AC-045**: Todos os itens do catálogo de serviços e orçamentos cadastrados devem pertencer obrigatoriamente ao `TenantId` do prestador autenticado (`ITenantOwned`).
- **AC-046**: O orçamento deve calcular automaticamente os subtotais dos itens, descontos, valor total final e a composição do Sinal/Entrada (ex: valor da entrada e saldo restante).
- **AC-047**: A exportação de orçamento deve ser disponibilizada exclusivamente via stream/download de arquivo PDF (`application/pdf`), atendendo rigorosamente à regra de negócio de não expor links públicos web.
- **AC-048**: Transições de status do orçamento devem seguir validações de negócio (ex: não aprovar orçamento sem itens ou já expirado).
- **AC-049**: Qualquer tentativa de acessar, alterar ou exportar PDF de orçamento pertencente a outro tenant deve retornar status HTTP 404/403.
