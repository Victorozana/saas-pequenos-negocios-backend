<!--
Fonte: RFC-Arquitetura-Sistema-Agendamento-Restaurantes.docx
Formato: Markdown para consumo por pessoas e IAs.
-->

# **RFC 001 - Decisão de Stack e Arquitetura**

Sistema de agendamento de pedidos para restaurantes

**Status:** Proposta para debate

**Autores:** Time de Engenharia

**Data:** 01 de agosto de 2026

**Decisão esperada:** Stack inicial e princípios arquiteturais

# Resumo executivo

Decisão final: iniciar como monólito modular, orientado a domínios e eventos, com C# + ASP.NET Core, PostgreSQL + EF Core/Npgsql, Redis e RabbitMQ + MassTransit, usando transactional outbox. A recomendação não significa depender de um único processo nem colocar toda a lógica na camada HTTP: os módulos serão isolados por interfaces, e workers independentes executarão integrações, notificações e tarefas demoradas. A escolha prioriza o conhecimento sólido do time em C# e ASP.NET Core, manutenção e entrega rápida, preservando uma rota clara para serviços separados quando houver pressão de escala comprovada.

**Atenção: concorrência de pedidos não será resolvida pelo framework HTTP. A fonte de verdade precisa ser o PostgreSQL, com restrições únicas, transações curtas, idempotência e controle explícito nos pontos de disputa (por exemplo, uma janela de agenda/restaurante).**

# 1. Contexto e problema

O produto possui dois painéis: (1) restaurante, para operação, cardápio, disponibilidade e acompanhamento; e (2) cliente, para descoberta, agendamento e pedido de delivery. Espera-se tráfego alto, picos concentrados em horários de refeição, gravações paralelas e concorrência sobre recursos finitos, como horários, capacidade de cozinha e itens de estoque.

O desenho deve sustentar crescimento horizontal, tolerar reentregas de mensagens e manter integridade de pedidos sem bloquear leituras desnecessariamente. Escalabilidade é também operacional: observabilidade, deploy seguro, testes de carga e recuperação devem fazer parte da decisão.

# 2. Objetivos e não objetivos

- Garantir que uma reserva ou slot não seja confirmado duas vezes.
- Manter API responsiva sob picos, delegando trabalho lento e efeitos externos a workers.
- Permitir evolução independente dos painéis e do núcleo de domínio.
- Criar limites claros entre HTTP, regras de negócio e persistência.
- Medir antes de extrair microserviços; evitar complexidade distribuída prematura.
*Não objetivos desta RFC: definir o provedor de nuvem, layout dos painéis, gateway de pagamento específico ou a modelagem final de todos os domínios.*

# 3. Critérios e pesos de decisão

Escala de 1 a 10: 10 representa melhor aderência ao critério. A pontuação ponderada é Σ(nota × peso); máximo = 270. Os pesos foram definidos pelo negócio e não por benchmark sintético.

| Critério | Peso | Como avaliar |
| --- | --- | --- |
| Concorrência | 10 | I/O simultâneo, limites, isolamento e comportamento sob muitas requisições. |
| Paralelismo | 8 | Uso de múltiplos núcleos e execução de trabalho CPU-intensivo/assíncrono. |
| Manutenção | 9 | Maturidade, segurança de tipos, observabilidade, curva do time e facilidade de testes. |

# 4. Alternativas de backend

As notas combinam características da plataforma, ecossistema e o contexto de um time já fluente em Node.js. Não são previsão de RPS: o desempenho real dependerá de banco, índices, cache, payloads, rede e integração de terceiros. O benchmark oficial do Fastify é explicitamente sintético e deve ser reproduzido com o fluxo de pedidos antes de qualquer compromisso de capacidade.

| Alternativa | Conc. | Paral. | Manut. | Total / 270 | Leitura |
| --- | --- | --- | --- | --- | --- |
| TypeScript + Fastify | 8 | 7 | 9 | 217 | Recomendado: rápido, tipado, baixo overhead; workers/processos para CPU. |
| TypeScript + Express | 6 | 6 | 9 | 189 | Muito conhecido; menor desempenho-base e mais decisões manuais. |
| Go + chi/Fiber | 9 | 10 | 6 | 224 | Excelente concorrência/paralelismo; custo de adoção e menor reaproveitamento. |
| C# + ASP.NET Core | 9 | 9 | 7 | 225 | Alternativa forte, madura e operacionalmente completa; exige mudança de stack. |
| Java + Spring Boot | 8 | 8 | 7 | 207 | Sólido em escala; maior custo operacional/memória para o estágio inicial. |

Decisão fechada: C# + ASP.NET Core. Embora Fastify tenha excelente overhead, o domínio do time em ASP.NET Core reduz risco de manutenção, acelera a entrega e mantém uma alternativa de alto desempenho. Utilizar rate limiting nativo, BackgroundService/workers, async/await, OpenTelemetry e testes de carga. A aplicação será escalada horizontalmente; trabalho bloqueante ou demorado não será executado no caminho HTTP.

# 5. Persistência e acesso a dados

| Opção | Conc. | Paral. | Manut. | Total / 270 | Decisão |
| --- | --- | --- | --- | --- | --- |
| PostgreSQL | 10 | 8 | 9 | 244 | Banco transacional principal. |
| EF Core + Npgsql + SQL pontual | 8 | 8 | 9 | 224 | Padrão recomendado; SQL para hotspots, locks e relatórios. |
| Dapper + SQL | 9 | 9 | 6 | 216 | Uso seletivo em consultas críticas e leitura de alta performance. |
| Prisma ORM + SQL pontual | 7 | 7 | 9 | 207 | Alternativa Node.js, não escolhida para esta implementação. |
| MongoDB como primário | 6 | 7 | 7 | 165 | Não recomendado para invariantes transacionais do pedido/agendamento. |

PostgreSQL oferece MVCC, em que leituras e escritas podem progredir sem bloqueio mútuo em muitos cenários, além de locks por linha e isolamento transacional. Para o domínio de pedido, usar constraints como UNIQUE(restaurante_id, slot_inicio, recurso_id), FKs e CHECKs; a aplicação deve tratar falhas de serialização/retry de forma limitada e idempotente.

EF Core + Npgsql é o padrão de persistência pela maturidade no ecossistema C#, migrations e integração natural com DI e observabilidade. Repositórios abstraem a persistência; consultas de alto volume podem usar Dapper ou SQL parametrizado como exceção documentada. Configurar pool por réplica e usar PgBouncer/RDS Proxy quando aplicável: o pool não pode ultrapassar a capacidade do banco, e mais conexões não significam mais throughput.

# 6. Arquitetura proposta

Escolha: monólito modular em C# com arquitetura hexagonal/clean em cada módulo e comunicação assíncrona por eventos. Usar Minimal APIs ou controllers somente como adapter HTTP; Application contém casos de uso; Domain contém invariantes; Infrastructure contém EF Core/Npgsql, Redis e MassTransit/RabbitMQ. É separado no código e no deploy dos workers, mas evita a latência, observabilidade e consistência distribuída de microserviços antes de haver necessidade real.

| Camada | Responsabilidade | Não deve conter |
| --- | --- | --- |
| Adapters HTTP / controllers | Autenticação, validação de entrada, mapeamento HTTP <-> caso de uso, status e DTOs. | Regra de negócio, SQL ou transação complexa. |
| Application / services | Casos de uso, orquestração, autorização de domínio, transações e publicação do outbox. | Detalhe de Fastify, Prisma ou broker. |
| Domain | Entidades, value objects, invariantes e políticas de agenda/pedido. | Dependência de framework ou banco. |
| Ports / repositories | Contratos de persistência, cache, clock, pagamentos e mensageria. | Implementação concreta. |
| Infrastructure | Prisma/PostgreSQL, Redis, broker, HTTP clients, telemetria e adapters. | Regra que determine o negócio. |

Módulos iniciais: Identidade e Acesso; Restaurantes e Catálogo; Agenda/Capacidade; Pedidos; Pagamentos; Notificações; Entregas; Relatórios. Cada módulo expõe casos de uso e eventos de domínio, e não entidades ORM para os demais módulos.

## Fluxo crítico de criação de pedido

- Cliente envia POST /orders com Idempotency-Key; o controller valida e chama CreateOrder.
- O serviço inicia transação curta: valida restaurante/slot, reserva capacidade com constraint/lock de linha apenas se necessário e grava pedido + chave de idempotência.
- Na mesma transação, grava evento OrderCreated em uma tabela outbox. Commit retorna confirmação ou o resultado já associado à chave.
- Worker lê outbox, publica em fila e atualiza estado de publicação. Consumidores de pagamento, notificação e delivery são idempotentes.
- Painéis recebem atualização via WebSocket/SSE ou consultam endpoint; falha de notificação não desfaz o pedido.
# 7. Concorrência, paralelismo e resiliência

| Risco | Mecanismo obrigatório | Sinal de alerta |
| --- | --- | --- |
| Duplo agendamento | Constraint única + transação; lock por linha/advisory lock só no ponto de conflito. | Violação de unique/serialization > limiar. |
| Repetição pelo cliente/retry | Idempotency-Key persistida por operação e resposta reaproveitável. | Pedidos duplicados por mesma chave. |
| Integração lenta/falha | Outbox + fila persistente + retry exponencial + DLQ. | Crescimento de fila ou DLQ. |
| Pico de tráfego | Rate limit por usuário/restaurante, fila limitada, backpressure e cache de leitura. | p95/p99 e rejeições 429/503. |
| CPU em Node | Workers separados; worker_threads somente para computação local bem medida. | Event loop lag e CPU sustentada. |
| Escala do banco | Pool limitado por réplica, índices, query budget e réplicas de leitura quando necessário. | Saturação de conexões/slow queries. |

Redis deve servir para cache, rate limiting distribuído e presença, não como fonte de verdade de pedido. Para a fila, a decisão é RabbitMQ + MassTransit por integração madura com .NET, retries e consumidores. Avaliar Kafka/Redpanda quando houver alto volume de eventos retidos, múltiplos consumidores independentes ou analytics em streaming. Toda fila precisa de retenção, reprocessamento e alertas.

# 8. Topologia inicial

| Componente | Escala inicial | Responsabilidade |
| --- | --- | --- |
| Web cliente e painel restaurante | CDN + deploy independente | Duas aplicações web, preferencialmente React/Next.js ou equivalente dominado. |
| API ASP.NET Core | Réplicas stateless horizontais | HTTP, autenticação, casos de uso e leitura/escrita transacional. |
| Workers | Autoscaling por profundidade da fila | Pagamentos, notificações, eventos, jobs e integrações. |
| PostgreSQL | Primário HA + backup/PITR | Fonte de verdade transacional. |
| Redis | Gerenciado/replicado conforme SLA | Cache, limites e dados efêmeros. |
| Broker | Gerenciado ou cluster | Durabilidade e desacoplamento de trabalho assíncrono. |
| Observabilidade | Centralizada | Logs estruturados, tracing, métricas e alertas. |

# 9. Testes de decisão e critérios de aceitação

- Teste de carga com perfil realista: ramp-up, picos de almoço/jantar, leitura/escrita e filas; comparar Fastify e Express se houver dúvida.
- Teste de corrida: N requisições simultâneas para o mesmo slot/recurso; deve existir no máximo uma confirmação válida.
- Teste de idempotência: repetir criação por timeout e reentrega da fila; o estado final e os efeitos externos devem ocorrer uma vez.
- Teste de falha: indisponibilidade de Redis, broker e provedor de pagamento; confirmar degradação controlada e recuperação por outbox.
- SLO inicial a definir antes do lançamento: sucesso de criação, p95/p99, backlog máximo, tempo de recuperação e RPO/RTO.
# 10. Decisões necessárias na reunião

- Decisão fechada: C# + ASP.NET Core (.NET 10) como backend inicial.
- Decisão fechada: PostgreSQL como fonte de verdade e EF Core + Npgsql como padrão, com Dapper/SQL documentado nos hotspots.
- Decisão fechada: RabbitMQ + MassTransit, transactional outbox, retry com backoff e DLQ.
- Decisão fechada: monólito modular + workers; listar gatilhos mensuráveis para extrair serviço (ownership, escala independente, fronteira de dados).
- Definir SLOs, orçamento de conexões por réplica e plano de benchmark antes do go-live.
# 11. Esqueleto de projeto criado

Foi criado o projeto vazio Agendamento.Api em .NET 10, com endpoint GET /health e pontos de extensão para injeção de dependência. Não há dependências externas registradas ainda: isso permite iniciar compilável e adicionar EF Core/Npgsql, Redis e MassTransit por módulo, com configuração e testes correspondentes.

| Caminho | Papel |
| --- | --- |
| src/Agendamento.Api/Features | Adapters HTTP, endpoints/controllers e DTOs por módulo. |
| src/Agendamento.Api/Application | Casos de uso, validação e portas/interfaces. |
| src/Agendamento.Api/Domain | Entidades, value objects, invariantes e eventos de domínio. |
| src/Agendamento.Api/Infrastructure | Adapters EF Core/Npgsql, Redis, MassTransit/RabbitMQ e observabilidade. |
| src/Agendamento.Api/Program.cs | Composition root: host, health check e DI. |

- Adicionar projetos por camada quando a primeira feature (por exemplo, Agenda/Capacidade) entrar em implementação; impedir referências de Domain para Infrastructure.
- Adicionar testes unitários para Domain/Application e testes de integração contra PostgreSQL/RabbitMQ/Redis em containers.
- Configurar OpenTelemetry, rate limiting, autenticação, tratamento centralizado de erros e validação antes de expor endpoints de negócio.
- Implementar outbox no mesmo DbContext/transação dos pedidos; workers consomem a fila sem participar da transação HTTP.
# 12. Referências e notas de evidência

**Fastify benchmarks:** https://fastify.dev/benchmarks/ - O próprio projeto informa que o teste é sintético e compara overhead; usar como indicador, não dimensionamento.

**Fastify benchmarking guide:** https://fastify.dev/docs/latest/Guides/Benchmarking/ - Guia oficial para automatizar benchmarks com autocannon e comparar versões/branches.

**Prisma - connection pool:** https://docs.prisma.io/docs/orm/prisma-client/setup-and-configuration/databases-connections/connection-pool - Configuração de pool, limites e uso de pooler externo em carga concorrente.

**Prisma - connection management:** https://www.prisma.io/docs/orm/prisma-client/setup-and-configuration/databases-connections/connection-management - Reuso de PrismaClient e cuidados em processos long-running/serverless.

**PostgreSQL - MVCC:** https://www.postgresql.org/docs/16/mvcc-intro.html - Modelo de concorrência, isolamento e locks por linha/tabela.

**Go concurrency resources:** https://go.dev/wiki/LearnConcurrency - Recursos oficiais sobre goroutines, channels e semântica de concorrência.

**Npgsql EF Core provider:** https://www.npgsql.org/efcore/ - Provider PostgreSQL para EF Core e configuração do DbContext pool.

**EF Core transactions:** https://learn.microsoft.com/en-us/ef/core/saving/transactions - Comportamento transacional de SaveChanges e cuidados com transações explícitas/retries.

**ASP.NET Core rate limiting:** https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0 - Rate limiting e concurrency limiting via System.Threading.RateLimiting.

**ASP.NET Core best practices:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices - Recomendação de trabalho longo em serviços de background ou fora do processo.
