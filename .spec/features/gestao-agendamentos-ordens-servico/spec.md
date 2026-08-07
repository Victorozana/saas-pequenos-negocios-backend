# Feature Spec: Gestão de Agendamentos e Ordens de Serviço (OS)

## Visão Geral
Esta feature permite gerenciar o ciclo de atendimento e execução dos serviços dos pequenos negócios e prestadores de serviço. O módulo contempla a conversão direta de um Orçamento Aprovado em Ordem de Serviço (OS), agendamento de visitas técnicas, medições, fabricação e instalações com alocação de técnicos/responsáveis e gestão de status da OS.

## User Stories (US)

### US-025: Converter Orçamento Aprovado em Ordem de Serviço (OS)
Como prestador de serviço autenticado,  
Quero converter um orçamento com status "Aprovado" em uma Ordem de Serviço (OS),  
Para iniciar o planejamento de execução e acompanhamento do trabalho contratado sem recadastrar os dados.

### US-026: Agendar Visita Técnica / Medição / Instalação
Como prestador de serviço autenticado,  
Quero vincular datas, horários e endereços de atendimento à Ordem de Serviço ou registrar um agendamento avulso,  
Para organizar a agenda de trabalho da equipe no campo ou na oficina.

### US-027: Gerenciar Status e Histórico da Ordem de Serviço
Como prestador de serviço autenticado,  
Quero atualizar o status da OS (Agendado, Em Execução, Aguardando Material, Concluído, Cancelado) e anexar observações técnicas,  
Para manter o cliente e a equipe informados sobre o progresso de cada projeto.

### US-028: Atribuir Responsável / Técnico ao Agendamento
Como gestor do tenant autenticado,  
Quero atribuir membros da equipe/mecanicos/técnicos específicos a um agendamento ou OS,  
Para distribuir a carga de trabalho do time de forma eficiente.

## Critérios de Aceite (AC)

- **AC-050**: Todas as Ordens de Serviço e Agendamentos pertencem obrigatoriamente ao `TenantId` do prestador autenticado (`ITenantOwned`).
- **AC-051**: A conversão de um orçamento em OS só é permitida se o orçamento estiver com status `Accepted` (Aprovado).
- **AC-052**: Ao converter o orçamento em OS, todos os itens do orçamento devem ser copiados como itens da OS mantendo rastreabilidade do `QuotationId` de origem.
- **AC-053**: Conflitos de horário no agendamento do mesmo técnico/responsável devem ser validados, retornando aviso/alerta de sobreposição de agenda.
- **AC-054**: Transições de status da OS devem registrar histórico de alterações com data/hora e usuário responsável.
