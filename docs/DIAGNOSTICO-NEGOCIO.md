# Diagnóstico de negócio — Nexo Local

Data: 04/11/2026. Escopo: leitura do frontend e backend locais e verificações automatizadas; não representa homologação com clientes nem inspeção de produção.

## O que significa estar completo

Para um primeiro cliente prestador de serviços, o produto precisa permitir: cadastrar a empresa e a equipe, cadastrar o cliente, elaborar e aprovar orçamento, organizar a execução, cobrar, receber, corrigir erros e consultar o resultado. Sem depender do desenvolvedor para operações cotidianas.

A base já cobre cadastro, autenticação, clientes, catálogo, orçamentos, conversão em OS, agenda, recebíveis por aprovação, despesas, baixas manuais, equipe e permissões. Ter essas telas não comprova que o ciclo inteiro está pronto para uso real.

Antes de ampliar módulos, validar a hipótese de público: o fluxo atual de visita técnica, medição e instalação atende principalmente prestadores de serviço. Escolher um segmento piloto e observar sua rotina; não tentar atender simultaneamente todos os pequenos negócios.

## Lacunas e entregas por prioridade

| Prioridade | Necessidade do cliente | Evidência no código e lacuna | Critério de aceite proposto |
| --- | --- | --- | --- |
| P0 — piloto | Corrigir um recebimento lançado errado | `Features/Financial/FinancialEndpoints.cs` oferece listas, despesa, baixa e resumo; faltam endpoints de histórico, estorno e manutenção de títulos. | Exibir cada baixa com data, valor, meio e responsável; estornar com motivo, preservar trilha e recalcular saldo sem apagar histórico. Definir edição/cancelamento com títulos pagos. |
| P0 — piloto | Saber quanto dinheiro entrou e saiu no mês | `GetFinancialStatementHandler` filtra pelo vencimento do título e soma todas as baixas dele. Isso não mede movimentação por data de pagamento. | Separar previsão por vencimento de caixa por pagamento; pagamento em outro mês aparece no caixa correto. Definir saldo inicial e apresentar claramente o significado de cada indicador. |
| P0 — piloto | Distribuir e reorganizar serviços | `appointment-modal.tsx` pede UUID de OS e não oferece seleção de profissional. `/agenda` lista e cria; API já tem atualização e conflito por profissional. | Selecionar cliente/OS por nome ou código e responsável por nome; reagendar pela tela, filtrar por profissional/período e tratar conflitos. Definir cancelamento e conclusão de compromissos. |
| P0 — piloto | Colocar o colaborador para trabalhar | Equipe e aceite existem; README registra ausência de gestão de convites pendentes. E-mail padrão é Mock. | Listar pendentes/expirados, reenviar e revogar; comprovar recebimento real e aceite em conta nova/existente; validar permissões com operador e administrador. |
| P0 — piloto | Recuperar acesso sem chamar suporte | Não foi encontrado fluxo de recuperação de senha nas rotas de identidade inspecionadas. | Solicitar recuperação e definir senha com código temporário de uso único, respostas que não revelem cadastro e sessão posterior funcional. |
| P0 — piloto | Confiar nos dados após reiniciar o sistema | README usa memória como padrão Development. Produção e restauração não foram verificadas nesta análise. | Homologar com PostgreSQL persistente, migrations e isolamento entre empresas; executar backup e restauração de teste; documentar operação e responsável. |
| P1 — rotina | Acompanhar todo o trabalho do cliente | Existem orçamento, OS e agenda, mas o vínculo na agenda ainda exige identificador técnico. | Navegação contínua cliente → orçamento → OS → agenda → contas; mostrar situação, responsável e próxima ação. Homologar cancelamento/rejeição após aprovação e decidir impacto financeiro. |
| P1 — rotina | Registrar recebimento fora de orçamento | Recebíveis atuais nascem da aprovação de orçamento; não há criação manual nas rotas financeiras. | Validar necessidade no segmento; se necessária, permitir receita avulsa com cliente, vencimento e origem. Definir parcelamento e renegociação antes de implementar. |
| P1 — rotina | Cobrar e acompanhar inadimplência | Há infraestrutura de notificações e lembretes; WhatsApp/SMS continuam simulados. SMTP é configurável, entrega externa não foi comprovada. | Homologar um canal real, acompanhar falhas/reenvios e preferências do cliente; priorizar vencidos com contato e próximo passo. |
| P1 — gestão | Tomar decisões e levar dados ao contador | Resumo financeiro atual não equivale a lucro ou conciliação bancária. | Exportar contas e movimentos com filtros; decidir categorias e custos por OS com o piloto. Não rotular saldo de caixa como lucro. |
| P1 — comercialização | Contratar e pagar pelo SaaS | API possui planos e webhook; `ChangePlanHandler` apenas altera o domínio e comenta a futura integração com gateway. Não há módulo de assinatura no menu. | Antes de cobrar automaticamente, implementar contratação e gestão da assinatura, gateway real, validação de autenticidade de webhook e regras de acesso/inadimplência. Pode iniciar piloto com cobrança comercial manual definida. |
| P1 — operação | Usar o sistema no celular e obter ajuda | Testes de código não comprovam usabilidade, entrega de e-mail ou operação publicada. | Homologar os fluxos em celular, estados vazios/erros, acessos restritos, suporte e tratamento de dados; validar ambiente publicado, monitoramento e procedimento de atualização. |
| P2 — expansão | Reduzir tarefas repetitivas | Necessidades ainda dependem do segmento escolhido. | Só priorizar recorrência, comissões, estoque, emissão fiscal e integrações bancárias quando houver demanda observada e retorno claro. Não são requisitos universais do MVP. |

## Ordem recomendada

1. Entrevistar um segmento piloto e acompanhar um serviço real do primeiro contato até o recebimento. Registrar problema, frequência, alternativa atual e impacto em dinheiro/tempo.
2. Fechar financeiro confiável: histórico, estorno e caixa por pagamento. Homologar pagamento parcial, pagamento fora do mês e correção de erro.
3. Conectar equipe à execução: convites operáveis, profissional na agenda, vínculo de OS sem UUID e reagendamento.
4. Homologar acesso, persistência, entrega real de e-mail e recuperação de dados em ambiente de piloto.
5. Rodar piloto acompanhado e só então priorizar cobrança do SaaS e expansões.

## Roteiro de homologação com o cliente

Administrador cria empresa e convida operador → operador recebe convite e entra → cadastra cliente e serviço → gera orçamento com entrada e saldo → aprova sem duplicar recebíveis → converte em OS → agenda profissional e tenta horário conflitante → reagenda → conclui execução → registra recebimento parcial e restante → lança despesa → corrige baixa errada por estorno → confere caixa e valores em aberto → exporta resultado.

Repetir com operador sem acesso financeiro e com segunda empresa para confirmar isolamento. Reiniciar aplicação e conferir persistência. Fazer restauração em ambiente separado. Os passos ainda não suportados representam backlog, não capacidade já entregue.

## Métricas para decidir se o piloto funciona

- Tempo entre cadastro do cliente e orçamento enviado.
- Serviços executados com responsável e horário definidos.
- Valor vencido e tempo gasto para cobrar.
- Quantidade de correções que exigem suporte do desenvolvedor.
- Frequência de uso pelo dono e operador, com relato de tarefas que deixaram de usar planilha ou papel.

Definir metas com o piloto após medir a rotina atual. A próxima entrega deve resolver um desses resultados com cenário de aceite completo, em vez de apenas acrescentar outra tela.
