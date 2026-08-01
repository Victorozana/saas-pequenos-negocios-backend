from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.section import WD_SECTION

OUT = 'RFC-Arquitetura-Sistema-Agendamento-Restaurantes.docx'

BLUE = '2E74B5'; DARK = '1F4D78'; NAVY = '0B2545'; LIGHT = 'E8EEF5'; GRAY = 'F2F4F7'; RED = '9B1C1C'

def set_cell_shading(cell, fill):
    tcPr = cell._tc.get_or_add_tcPr(); shd = OxmlElement('w:shd'); shd.set(qn('w:fill'), fill); tcPr.append(shd)

def set_cell_margins(cell, top=80, start=120, bottom=80, end=120):
    tc = cell._tc; tcPr = tc.get_or_add_tcPr(); tcMar = tcPr.first_child_found_in('w:tcMar')
    if tcMar is None: tcMar = OxmlElement('w:tcMar'); tcPr.append(tcMar)
    for side, value in [('top',top),('start',start),('bottom',bottom),('end',end)]:
        node = tcMar.find(qn('w:'+side))
        if node is None: node = OxmlElement('w:'+side); tcMar.append(node)
        node.set(qn('w:w'), str(value)); node.set(qn('w:type'), 'dxa')

def set_table_widths(table, widths):
    table.autofit = False
    tblPr = table._tbl.tblPr
    tblW = tblPr.first_child_found_in('w:tblW')
    if tblW is None: tblW = OxmlElement('w:tblW'); tblPr.append(tblW)
    tblW.set(qn('w:w'), str(sum(widths))); tblW.set(qn('w:type'), 'dxa')
    ind = tblPr.first_child_found_in('w:tblInd')
    if ind is None: ind = OxmlElement('w:tblInd'); tblPr.append(ind)
    ind.set(qn('w:w'), '120'); ind.set(qn('w:type'), 'dxa')
    grid = table._tbl.tblGrid
    for gridcol, width in zip(grid.gridCol_lst, widths): gridcol.set(qn('w:w'), str(width))
    for row in table.rows:
        for cell, width in zip(row.cells, widths):
            cell.width = Inches(width/1440)
            tcW = cell._tc.tcPr.tcW
            tcW.set(qn('w:w'), str(width)); tcW.set(qn('w:type'), 'dxa')
            set_cell_margins(cell); cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER

def font(run, size=11, bold=None, color='000000', italic=None):
    run.font.name='Calibri'; run._element.rPr.rFonts.set(qn('w:ascii'), 'Calibri'); run._element.rPr.rFonts.set(qn('w:hAnsi'), 'Calibri')
    run.font.size=Pt(size); run.font.color.rgb=RGBColor.from_string(color)
    if bold is not None: run.bold=bold
    if italic is not None: run.italic=italic

def ptext(doc, text='', style=None, size=11, bold=None, color='000000', after=6, before=0, italic=None):
    p=doc.add_paragraph(style=style)
    p.paragraph_format.space_after=Pt(after); p.paragraph_format.space_before=Pt(before); p.paragraph_format.line_spacing=1.10
    r=p.add_run(text); font(r,size,bold,color,italic); return p

def bullet(doc, text, level=0):
    p=doc.add_paragraph(style='List Bullet' if level==0 else 'List Bullet 2'); p.paragraph_format.space_after=Pt(4); p.paragraph_format.line_spacing=1.10
    font(p.add_run(text)); return p

def table(doc, headers, rows, widths):
    t=doc.add_table(rows=1, cols=len(headers)); t.style='Table Grid'; t.alignment=WD_TABLE_ALIGNMENT.LEFT; set_table_widths(t,widths)
    trPr = t.rows[0]._tr.get_or_add_trPr(); tblHeader = OxmlElement('w:tblHeader'); tblHeader.set(qn('w:val'), 'true'); trPr.append(tblHeader)
    for cell, h in zip(t.rows[0].cells,headers):
        set_cell_shading(cell,LIGHT); p=cell.paragraphs[0]; p.alignment=WD_ALIGN_PARAGRAPH.CENTER; font(p.add_run(h),9,True,NAVY)
    for row in rows:
        cells=t.add_row().cells
        for cell,value in zip(cells,row):
            p=cell.paragraphs[0]; p.paragraph_format.space_after=Pt(0); p.paragraph_format.line_spacing=1.05; font(p.add_run(str(value)),9)
    doc.add_paragraph().paragraph_format.space_after=Pt(2)
    return t

doc=Document()
sec=doc.sections[0]; sec.top_margin=Inches(1); sec.bottom_margin=Inches(1); sec.left_margin=Inches(1); sec.right_margin=Inches(1); sec.header_distance=Inches(.492); sec.footer_distance=Inches(.492)
styles=doc.styles
for name,size,color,before,after in [('Normal',11,'000000',0,6),('Heading 1',16,BLUE,16,8),('Heading 2',13,BLUE,12,6),('Heading 3',12,DARK,8,4)]:
    s=styles[name]; s.font.name='Calibri'; s._element.rPr.rFonts.set(qn('w:ascii'),'Calibri'); s._element.rPr.rFonts.set(qn('w:hAnsi'),'Calibri'); s.font.size=Pt(size); s.font.color.rgb=RGBColor.from_string(color); s.font.bold=(name!='Normal'); s.paragraph_format.space_before=Pt(before); s.paragraph_format.space_after=Pt(after); s.paragraph_format.line_spacing=1.10

# Header/footer
hp=sec.header.paragraphs[0]; hp.alignment=WD_ALIGN_PARAGRAPH.RIGHT; font(hp.add_run('RFC | Arquitetura de Agendamento'),9,False,'666666')
fp=sec.footer.paragraphs[0]; fp.alignment=WD_ALIGN_PARAGRAPH.CENTER; font(fp.add_run('Documento para debate técnico - 01 de agosto de 2026'),9,False,'666666')

# Masthead
ptext(doc,'RFC 001 - Decisão de Stack e Arquitetura',size=23,bold=True,color=NAVY,after=4)
ptext(doc,'Sistema de agendamento de pedidos para restaurantes',size=14,color='555555',after=14)
for label,value in [('Status','Proposta para debate'),('Autores','Time de Engenharia'),('Data','01 de agosto de 2026'),('Decisão esperada','Stack inicial e princípios arquiteturais')]:
    p=doc.add_paragraph(); p.paragraph_format.space_after=Pt(2); r=p.add_run(label+': '); font(r,11,True); font(p.add_run(value),11)
p=doc.add_paragraph(); p.paragraph_format.space_before=Pt(12); p.paragraph_format.space_after=Pt(10)
pPr=p._p.get_or_add_pPr(); borders=OxmlElement('w:pBdr'); bottom=OxmlElement('w:bottom'); bottom.set(qn('w:val'),'single'); bottom.set(qn('w:sz'),'12'); bottom.set(qn('w:space'),'6'); bottom.set(qn('w:color'),BLUE); borders.append(bottom); pPr.append(borders)

ptext(doc,'Resumo executivo',style='Heading 1')
ptext(doc,'Decisão final: iniciar como monólito modular, orientado a domínios e eventos, com C# + ASP.NET Core, PostgreSQL + EF Core/Npgsql, Redis e RabbitMQ + MassTransit, usando transactional outbox. A recomendação não significa depender de um único processo nem colocar toda a lógica na camada HTTP: os módulos serão isolados por interfaces, e workers independentes executarão integrações, notificações e tarefas demoradas. A escolha prioriza o conhecimento sólido do time em C# e ASP.NET Core, manutenção e entrega rápida, preservando uma rota clara para serviços separados quando houver pressão de escala comprovada.')
ptext(doc,'Atenção: concorrência de pedidos não será resolvida pelo framework HTTP. A fonte de verdade precisa ser o PostgreSQL, com restrições únicas, transações curtas, idempotência e controle explícito nos pontos de disputa (por exemplo, uma janela de agenda/restaurante).',size=11,bold=True,color=DARK,after=10)

ptext(doc,'1. Contexto e problema',style='Heading 1')
ptext(doc,'O produto possui dois painéis: (1) restaurante, para operação, cardápio, disponibilidade e acompanhamento; e (2) cliente, para descoberta, agendamento e pedido de delivery. Espera-se tráfego alto, picos concentrados em horários de refeição, gravações paralelas e concorrência sobre recursos finitos, como horários, capacidade de cozinha e itens de estoque.')
ptext(doc,'O desenho deve sustentar crescimento horizontal, tolerar reentregas de mensagens e manter integridade de pedidos sem bloquear leituras desnecessariamente. Escalabilidade é também operacional: observabilidade, deploy seguro, testes de carga e recuperação devem fazer parte da decisão.')

ptext(doc,'2. Objetivos e não objetivos',style='Heading 1')
for x in ['Garantir que uma reserva ou slot não seja confirmado duas vezes.', 'Manter API responsiva sob picos, delegando trabalho lento e efeitos externos a workers.', 'Permitir evolução independente dos painéis e do núcleo de domínio.', 'Criar limites claros entre HTTP, regras de negócio e persistência.', 'Medir antes de extrair microserviços; evitar complexidade distribuída prematura.']: bullet(doc,x)
ptext(doc,'Não objetivos desta RFC: definir o provedor de nuvem, layout dos painéis, gateway de pagamento específico ou a modelagem final de todos os domínios.',italic=True,color='555555')

ptext(doc,'3. Critérios e pesos de decisão',style='Heading 1')
ptext(doc,'Escala de 1 a 10: 10 representa melhor aderência ao critério. A pontuação ponderada é Σ(nota × peso); máximo = 270. Os pesos foram definidos pelo negócio e não por benchmark sintético.')
table(doc,['Critério','Peso','Como avaliar'],[
('Concorrência','10','I/O simultâneo, limites, isolamento e comportamento sob muitas requisições.'),
('Paralelismo','8','Uso de múltiplos núcleos e execução de trabalho CPU-intensivo/assíncrono.'),
('Manutenção','9','Maturidade, segurança de tipos, observabilidade, curva do time e facilidade de testes.')], [2200,900,6260])

ptext(doc,'4. Alternativas de backend',style='Heading 1')
ptext(doc,'As notas combinam características da plataforma, ecossistema e o contexto de um time já fluente em Node.js. Não são previsão de RPS: o desempenho real dependerá de banco, índices, cache, payloads, rede e integração de terceiros. O benchmark oficial do Fastify é explicitamente sintético e deve ser reproduzido com o fluxo de pedidos antes de qualquer compromisso de capacidade.')
table(doc,['Alternativa','Conc.','Paral.','Manut.','Total / 270','Leitura'],[
('TypeScript + Fastify','8','7','9','217','Recomendado: rápido, tipado, baixo overhead; workers/processos para CPU.'),
('TypeScript + Express','6','6','9','189','Muito conhecido; menor desempenho-base e mais decisões manuais.'),
('Go + chi/Fiber','9','10','6','224','Excelente concorrência/paralelismo; custo de adoção e menor reaproveitamento.'),
('C# + ASP.NET Core','9','9','7','225','Alternativa forte, madura e operacionalmente completa; exige mudança de stack.'),
('Java + Spring Boot','8','8','7','207','Sólido em escala; maior custo operacional/memória para o estágio inicial.')], [2050,700,700,800,1100,4010])
ptext(doc,'Decisão fechada: C# + ASP.NET Core. Embora Fastify tenha excelente overhead, o domínio do time em ASP.NET Core reduz risco de manutenção, acelera a entrega e mantém uma alternativa de alto desempenho. Utilizar rate limiting nativo, BackgroundService/workers, async/await, OpenTelemetry e testes de carga. A aplicação será escalada horizontalmente; trabalho bloqueante ou demorado não será executado no caminho HTTP.',after=8)

ptext(doc,'5. Persistência e acesso a dados',style='Heading 1')
table(doc,['Opção','Conc.','Paral.','Manut.','Total / 270','Decisão'],[
('PostgreSQL','10','8','9','244','Banco transacional principal.'),
('EF Core + Npgsql + SQL pontual','8','8','9','224','Padrão recomendado; SQL para hotspots, locks e relatórios.'),
('Dapper + SQL','9','9','6','216','Uso seletivo em consultas críticas e leitura de alta performance.'),
('Prisma ORM + SQL pontual','7','7','9','207','Alternativa Node.js, não escolhida para esta implementação.'),
('MongoDB como primário','6','7','7','165','Não recomendado para invariantes transacionais do pedido/agendamento.')], [1900,700,700,800,1100,4160])
ptext(doc,'PostgreSQL oferece MVCC, em que leituras e escritas podem progredir sem bloqueio mútuo em muitos cenários, além de locks por linha e isolamento transacional. Para o domínio de pedido, usar constraints como UNIQUE(restaurante_id, slot_inicio, recurso_id), FKs e CHECKs; a aplicação deve tratar falhas de serialização/retry de forma limitada e idempotente.')
ptext(doc,'EF Core + Npgsql é o padrão de persistência pela maturidade no ecossistema C#, migrations e integração natural com DI e observabilidade. Repositórios abstraem a persistência; consultas de alto volume podem usar Dapper ou SQL parametrizado como exceção documentada. Configurar pool por réplica e usar PgBouncer/RDS Proxy quando aplicável: o pool não pode ultrapassar a capacidade do banco, e mais conexões não significam mais throughput.',color=DARK)

ptext(doc,'6. Arquitetura proposta',style='Heading 1')
ptext(doc,'Escolha: monólito modular em C# com arquitetura hexagonal/clean em cada módulo e comunicação assíncrona por eventos. Usar Minimal APIs ou controllers somente como adapter HTTP; Application contém casos de uso; Domain contém invariantes; Infrastructure contém EF Core/Npgsql, Redis e MassTransit/RabbitMQ. É separado no código e no deploy dos workers, mas evita a latência, observabilidade e consistência distribuída de microserviços antes de haver necessidade real.')
table(doc,['Camada','Responsabilidade','Não deve conter'],[
('Adapters HTTP / controllers','Autenticação, validação de entrada, mapeamento HTTP <-> caso de uso, status e DTOs.','Regra de negócio, SQL ou transação complexa.'),
('Application / services','Casos de uso, orquestração, autorização de domínio, transações e publicação do outbox.','Detalhe de Fastify, Prisma ou broker.'),
('Domain','Entidades, value objects, invariantes e políticas de agenda/pedido.','Dependência de framework ou banco.'),
('Ports / repositories','Contratos de persistência, cache, clock, pagamentos e mensageria.','Implementação concreta.'),
('Infrastructure','Prisma/PostgreSQL, Redis, broker, HTTP clients, telemetria e adapters.','Regra que determine o negócio.')], [1750,4700,2910])
ptext(doc,'Módulos iniciais: Identidade e Acesso; Restaurantes e Catálogo; Agenda/Capacidade; Pedidos; Pagamentos; Notificações; Entregas; Relatórios. Cada módulo expõe casos de uso e eventos de domínio, e não entidades ORM para os demais módulos.')

ptext(doc,'Fluxo crítico de criação de pedido',style='Heading 2')
for x in ['Cliente envia POST /orders com Idempotency-Key; o controller valida e chama CreateOrder.', 'O serviço inicia transação curta: valida restaurante/slot, reserva capacidade com constraint/lock de linha apenas se necessário e grava pedido + chave de idempotência.', 'Na mesma transação, grava evento OrderCreated em uma tabela outbox. Commit retorna confirmação ou o resultado já associado à chave.', 'Worker lê outbox, publica em fila e atualiza estado de publicação. Consumidores de pagamento, notificação e delivery são idempotentes.', 'Painéis recebem atualização via WebSocket/SSE ou consultam endpoint; falha de notificação não desfaz o pedido.']: bullet(doc,x)

ptext(doc,'7. Concorrência, paralelismo e resiliência',style='Heading 1')
table(doc,['Risco','Mecanismo obrigatório','Sinal de alerta'],[
('Duplo agendamento','Constraint única + transação; lock por linha/advisory lock só no ponto de conflito.','Violação de unique/serialization > limiar.'),
('Repetição pelo cliente/retry','Idempotency-Key persistida por operação e resposta reaproveitável.','Pedidos duplicados por mesma chave.'),
('Integração lenta/falha','Outbox + fila persistente + retry exponencial + DLQ.','Crescimento de fila ou DLQ.'),
('Pico de tráfego','Rate limit por usuário/restaurante, fila limitada, backpressure e cache de leitura.','p95/p99 e rejeições 429/503.'),
('CPU em Node','Workers separados; worker_threads somente para computação local bem medida.','Event loop lag e CPU sustentada.'),
('Escala do banco','Pool limitado por réplica, índices, query budget e réplicas de leitura quando necessário.','Saturação de conexões/slow queries.')], [1550,4800,3010])
ptext(doc,'Redis deve servir para cache, rate limiting distribuído e presença, não como fonte de verdade de pedido. Para a fila, a decisão é RabbitMQ + MassTransit por integração madura com .NET, retries e consumidores. Avaliar Kafka/Redpanda quando houver alto volume de eventos retidos, múltiplos consumidores independentes ou analytics em streaming. Toda fila precisa de retenção, reprocessamento e alertas.')

ptext(doc,'8. Topologia inicial',style='Heading 1')
table(doc,['Componente','Escala inicial','Responsabilidade'],[
('Web cliente e painel restaurante','CDN + deploy independente','Duas aplicações web, preferencialmente React/Next.js ou equivalente dominado.'),
('API ASP.NET Core','Réplicas stateless horizontais','HTTP, autenticação, casos de uso e leitura/escrita transacional.'),
('Workers','Autoscaling por profundidade da fila','Pagamentos, notificações, eventos, jobs e integrações.'),
('PostgreSQL','Primário HA + backup/PITR','Fonte de verdade transacional.'),
('Redis','Gerenciado/replicado conforme SLA','Cache, limites e dados efêmeros.'),
('Broker','Gerenciado ou cluster','Durabilidade e desacoplamento de trabalho assíncrono.'),
('Observabilidade','Centralizada','Logs estruturados, tracing, métricas e alertas.')], [2450,2400,4510])

ptext(doc,'9. Testes de decisão e critérios de aceitação',style='Heading 1')
for x in ['Teste de carga com perfil realista: ramp-up, picos de almoço/jantar, leitura/escrita e filas; comparar Fastify e Express se houver dúvida.', 'Teste de corrida: N requisições simultâneas para o mesmo slot/recurso; deve existir no máximo uma confirmação válida.', 'Teste de idempotência: repetir criação por timeout e reentrega da fila; o estado final e os efeitos externos devem ocorrer uma vez.', 'Teste de falha: indisponibilidade de Redis, broker e provedor de pagamento; confirmar degradação controlada e recuperação por outbox.', 'SLO inicial a definir antes do lançamento: sucesso de criação, p95/p99, backlog máximo, tempo de recuperação e RPO/RTO.']: bullet(doc,x)

ptext(doc,'10. Decisões necessárias na reunião',style='Heading 1')
for x in ['Decisão fechada: C# + ASP.NET Core (.NET 10) como backend inicial.', 'Decisão fechada: PostgreSQL como fonte de verdade e EF Core + Npgsql como padrão, com Dapper/SQL documentado nos hotspots.', 'Decisão fechada: RabbitMQ + MassTransit, transactional outbox, retry com backoff e DLQ.', 'Decisão fechada: monólito modular + workers; listar gatilhos mensuráveis para extrair serviço (ownership, escala independente, fronteira de dados).', 'Definir SLOs, orçamento de conexões por réplica e plano de benchmark antes do go-live.']: bullet(doc,x)

ptext(doc,'11. Esqueleto de projeto criado',style='Heading 1')
ptext(doc,'Foi criado o projeto vazio Agendamento.Api em .NET 10, com endpoint GET /health e pontos de extensão para injeção de dependência. Não há dependências externas registradas ainda: isso permite iniciar compilável e adicionar EF Core/Npgsql, Redis e MassTransit por módulo, com configuração e testes correspondentes.')
table(doc,['Caminho','Papel'],[
('src/Agendamento.Api/Features','Adapters HTTP, endpoints/controllers e DTOs por módulo.'),
('src/Agendamento.Api/Application','Casos de uso, validação e portas/interfaces.'),
('src/Agendamento.Api/Domain','Entidades, value objects, invariantes e eventos de domínio.'),
('src/Agendamento.Api/Infrastructure','Adapters EF Core/Npgsql, Redis, MassTransit/RabbitMQ e observabilidade.'),
('src/Agendamento.Api/Program.cs','Composition root: host, health check e DI.')], [3300,6060])
for x in ['Adicionar projetos por camada quando a primeira feature (por exemplo, Agenda/Capacidade) entrar em implementação; impedir referências de Domain para Infrastructure.', 'Adicionar testes unitários para Domain/Application e testes de integração contra PostgreSQL/RabbitMQ/Redis em containers.', 'Configurar OpenTelemetry, rate limiting, autenticação, tratamento centralizado de erros e validação antes de expor endpoints de negócio.', 'Implementar outbox no mesmo DbContext/transação dos pedidos; workers consomem a fila sem participar da transação HTTP.']: bullet(doc,x)

ptext(doc,'12. Referências e notas de evidência',style='Heading 1')
sources=[
('Fastify benchmarks','https://fastify.dev/benchmarks/','O próprio projeto informa que o teste é sintético e compara overhead; usar como indicador, não dimensionamento.'),
('Fastify benchmarking guide','https://fastify.dev/docs/latest/Guides/Benchmarking/','Guia oficial para automatizar benchmarks com autocannon e comparar versões/branches.'),
('Prisma - connection pool','https://docs.prisma.io/docs/orm/prisma-client/setup-and-configuration/databases-connections/connection-pool','Configuração de pool, limites e uso de pooler externo em carga concorrente.'),
('Prisma - connection management','https://www.prisma.io/docs/orm/prisma-client/setup-and-configuration/databases-connections/connection-management','Reuso de PrismaClient e cuidados em processos long-running/serverless.'),
('PostgreSQL - MVCC','https://www.postgresql.org/docs/16/mvcc-intro.html','Modelo de concorrência, isolamento e locks por linha/tabela.'),
('Go concurrency resources','https://go.dev/wiki/LearnConcurrency','Recursos oficiais sobre goroutines, channels e semântica de concorrência.'),
('ASP.NET Core rate limiting','https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0','Rate limiting e concurrency limiting via System.Threading.RateLimiting.'),
('ASP.NET Core best practices','https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices','Recomendação de trabalho longo em serviços de background ou fora do processo.')]
sources.insert(6, ('Npgsql EF Core provider','https://www.npgsql.org/efcore/','Provider PostgreSQL para EF Core e configuração do DbContext pool.'))
sources.insert(7, ('EF Core transactions','https://learn.microsoft.com/en-us/ef/core/saving/transactions','Comportamento transacional de SaveChanges e cuidados com transações explícitas/retries.'))
for name,url,note in sources:
    p=doc.add_paragraph(); p.paragraph_format.space_after=Pt(4); r=p.add_run(name+': '); font(r,10,True,DARK); r=p.add_run(url); font(r,9,False,BLUE); r=p.add_run(' - '+note); font(r,9,False)

doc.save(OUT)
print(OUT)
