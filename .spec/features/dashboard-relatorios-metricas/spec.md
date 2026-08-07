# Feature Spec: Dashboard Executivo, Métricas e Relatórios do Prestador

## Visão Geral
Esta feature provê um painel executivo em tempo real com métricas consolidadas para o gestor/proprietário do pequeno negócio ou prestador de serviço. O módulo calcula indicadores operacionais e financeiros cruciais, como faturamento do mês, taxa de conversão de orçamentos, valor total pendente de recebimento, agendamentos do dia/semana e relatórios executivos sintetizados.

## User Stories (US)

### US-037: Visualizar Dashboard Operacional e Financeiro da Empresa
Como gestor do tenant autenticado,  
Quero visualizar na página inicial do sistema os KPIs principais do mês corrente (faturamento total, total a receber, total em atraso, quantidade de ordens de serviço ativas),  
Para tomar decisões estratégicas rápidas sobre a operação do negócio.

### US-038: Acompanhar Taxa de Conversão de Orçamentos
Como gestor do tenant autenticado,  
Quero visualizar o indicador de taxa de aprovação de orçamentos (total emitido vs total aprovado em R$ e quantidade),  
Para avaliar a eficiência da equipe comercial e atratividade das propostas.

### US-039: Visualizar Agenda do Dia / Semana Sintetizada
Como prestador de serviço autenticado,  
Quero ver um painel compacto com os próximos agendamentos e visitas técnicas marcadas para hoje e nos próximos 7 dias,  
Para planejar o deslocamento e prioridades de atendimento da equipe.

### US-040: Exportar Relatório Consolidado de Desempenho (CSV/PDF)
Como gestor do tenant autenticado,  
Quero gerar e baixar um relatório sintético do período contendo a lista de serviços executados, clientes atendidos e faturamento líquido,  
Para acompanhamento contábil ou prestação de contas dos sócios.

## Critérios de Aceite (AC)

- **AC-065**: Todos os dados consolidados, métricas e relatórios são filtrados estritamente pelo `TenantId` do prestador autenticado (`ITenantOwned`).
- **AC-066**: O cálculo dos KPIs de faturamento no dashboard deve considerar apenas títulos efetivamente baixados/pagos dentro do intervalo de datas consultado.
- **AC-067**: As consultas agregadas do dashboard devem ser otimizadas via queries compiladas ou visões com índices para responder em menos de 200ms.
- **AC-068**: A taxa de conversão de orçamentos deve considerar a divisão entre o valor de orçamentos com status `Accepted` divididos pelo total de orçamentos finalizados (`Accepted` + `Rejected` + `Expired`).
- **AC-069**: A geração de relatórios consolidados deve permitir filtros customizados por intervalo de datas (Data Inicial e Data Final).
