# Feature Spec: Gestão de Agendamentos e Ordens de Serviço (OS)

> feature: gestao-agendamentos-ordens-servico
> status: em-implementacao

## Visão Geral
Esta feature permite gerenciar o ciclo de atendimento e execução dos serviços dos pequenos negócios e prestadores de serviço. O módulo contempla a conversão direta de um Orçamento Aprovado em Ordem de Serviço (OS), agendamento de visitas técnicas, medições, fabricação e instalações com alocação de técnicos/responsáveis e gestão de status da OS.

## User Stories (US)

### US-025 — Converter Orçamento Aprovado em Ordem de Serviço (OS)
Como prestador de serviço autenticado,  
Quero converter um orçamento com status "Aprovado" em uma Ordem de Serviço (OS),  
Para iniciar o planejamento de execução e acompanhamento do trabalho contratado sem recadastrar os dados.

#### AC-051 — Conversão exige orçamento aprovado

- **Dado** um orçamento do tenant autenticado
- **Quando** o prestador solicita a conversão em OS
- **Então** a conversão só é permitida se o status for `Accepted` (Aprovado).

#### AC-052 — Conversão copia itens e preserva origem

- **Dado** um orçamento aprovado com itens
- **Quando** o orçamento é convertido em OS
- **Então** todos os itens são copiados, mantendo o `QuotationId` de origem.

### US-026 — Agendar Visita Técnica / Medição / Instalação
Como prestador de serviço autenticado,  
Quero vincular datas, horários e endereços de atendimento à Ordem de Serviço ou registrar um agendamento avulso,  
Para organizar a agenda de trabalho da equipe no campo ou na oficina.

#### AC-050 — OS e agendamentos pertencem ao tenant autenticado

- **Dado** um prestador autenticado em uma empresa
- **Quando** cria ou consulta OS e agendamentos
- **Então** todos os registros pertencem obrigatoriamente ao seu `TenantId` (`ITenantOwned`).

### US-027 — Gerenciar Status e Histórico da Ordem de Serviço
Como prestador de serviço autenticado,  
Quero atualizar o status da OS (Agendado, Em Execução, Aguardando Material, Concluído, Cancelado) e anexar observações técnicas,  
Para manter o cliente e a equipe informados sobre o progresso de cada projeto.

#### AC-054 — Transições de status possuem histórico

- **Dado** uma OS do tenant autenticado
- **Quando** seu status é alterado
- **Então** a transição registra histórico com data/hora e usuário responsável.

### US-028 — Atribuir Responsável / Técnico ao Agendamento
Como gestor do tenant autenticado,  
Quero atribuir membros da equipe/mecanicos/técnicos específicos a um agendamento ou OS,  
Para distribuir a carga de trabalho do time de forma eficiente.

#### AC-053 — Conflitos de agenda do responsável são avisados

- **Dado** um técnico/responsável com agendamento existente
- **Quando** é solicitado outro agendamento com sobreposição de horário para o mesmo responsável
- **Então** o conflito é validado e retorna aviso/alerta de sobreposição de agenda.

## Suposições

Nenhuma nova suposição nesta normalização; os requisitos existentes foram preservados.

## Perguntas em aberto

Nenhuma decisão de produto nova nesta normalização.
