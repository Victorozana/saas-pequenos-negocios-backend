# Feature Spec: Dashboard Executivo, Métricas e Relatórios do Prestador

> feature: dashboard-relatorios-metricas
> status: em-implementacao

## Visão Geral
Esta feature provê um painel executivo em tempo real com métricas consolidadas para o gestor/proprietário do pequeno negócio ou prestador de serviço. O módulo calcula indicadores operacionais e financeiros cruciais, como faturamento do mês, taxa de conversão de orçamentos, valor total pendente de recebimento, agendamentos do dia/semana e relatórios executivos sintetizados.

## User Stories (US)

### US-037 — Visualizar Dashboard Operacional e Financeiro da Empresa
Como gestor do tenant autenticado,  
Quero visualizar na página inicial do sistema os KPIs principais do mês corrente (faturamento total, total a receber, total em atraso, quantidade de ordens de serviço ativas),  
Para tomar decisões estratégicas rápidas sobre a operação do negócio.

#### AC-065 — Isolamento estrito de dados por tenant

- **Dado** um prestador de serviço autenticado
- **Quando** solicita métricas, agenda ou relatórios no dashboard
- **Então** todos os dados consolidados são filtrados estritamente pelo TenantId do prestador autenticado (`ITenantOwned`)

#### AC-066 — Faturamento considera apenas títulos baixados/pagos

- **Dado** um intervalo de datas consultado
- **Quando** o KPI de faturamento no dashboard é calculado
- **Então** considera apenas títulos efetivamente baixados/pagos dentro do intervalo de datas consultado

#### AC-067 — Consultas agregadas otimizadas

- **Dado** a execução de consultas agregadas do dashboard
- **Quando** o usuário solicita o resumo ou agenda
- **Então** a consulta responde de forma otimizada em menos de 200ms

### US-038 — Acompanhar Taxa de Conversão de Orçamentos
Como gestor do tenant autenticado,  
Quero visualizar o indicador de taxa de aprovação de orçamentos (total emitido vs total aprovado em R$ e quantidade),  
Para avaliar a eficiência da equipe comercial e atratividade das propostas.

#### AC-068 — Cálculo da taxa de conversão de orçamentos

- **Dado** os orçamentos emitidos no período
- **Quando** a taxa de conversão é calculada
- **Então** considera a divisão entre o valor de orçamentos com status `Approved` (ou `Converted`) divididos pelo total de orçamentos finalizados (`Approved` + `Converted` + `Rejected` + `Expired`)

### US-039 — Visualizar Agenda do Dia / Semana Sintetizada
Como prestador de serviço autenticado,  
Quero ver um painel compacto com os próximos agendamentos e visitas técnicas marcadas para hoje e nos próximos 7 dias,  
Para planejar o deslocamento e prioridades de atendimento da equipe.

### US-040 — Exportar Relatório Consolidado de Desempenho (CSV/PDF)
Como gestor do tenant autenticado,  
Quero gerar e baixar um relatório sintético do período contendo a lista de serviços executados, clientes atendidos e faturamento líquido,  
Para acompanhamento contábil ou prestação de contas dos sócios.

#### AC-069 — Relatórios consolidados com filtro de intervalo de datas

- **Dado** parâmetros de filtro por intervalo de datas (Data Inicial e Data Final)
- **Quando** a geração de relatórios consolidados é solicitada
- **Então** os dados contidos no relatório respeitam o intervalo de datas customizado


## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-008 | O formato CSV é suficiente para exportação sintética neste momento. | confirmada | Confirmado na especificação do módulo. |

## Perguntas em aberto

Nenhuma.

