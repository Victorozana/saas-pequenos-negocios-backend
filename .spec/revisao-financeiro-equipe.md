# Revisão de financeiro e equipe — 11/10/2026

## Resultado

As correções de financeiro e equipe foram implementadas e verificadas. Os nove critérios dessas features possuem prova PASS e a auditoria global não aponta achados com esses dois nomes de feature. A entrega global não está encerrada: `onp-spec audit --ci` retorna 1, com 264 erros e 40 avisos em outras features e no mapeamento global do código.

## Alterações

- Financeiro: recebíveis na aprovação do orçamento, divididos em entrada e saldo; aprovação concorrente não duplica títulos; saldo e pagamentos persistidos; consultas incluem pagamentos e usam DTOs; validação de pagamento e permissões financial.read/financial.write.
- Equipe: ativação/desativação por vínculo, proteção de Admin e owner_admin, revogação imediata de JWT anterior, aceite público com tenant obtido do convite validado, token fora da resposta HTTP e dos logs, consumo único e rejeição de convite inválido/expirado.
- Operação: atualização de status de OS passa a salvar alterações; permissões aplicadas a serviços, vendas, agenda e relatórios.
- Contrato: OpenAPI atualizado, snapshot validado e schema do frontend regenerado; verificação de tipos aprovada.
- Persistência local: migration AddActiveTeamMembership aplicada; membros existentes permanecem ativos. API atualizada rodando em localhost:5050.
- Verificação: 153 testes aprovados, zero reprovados e seis testes preexistentes pulados. Provas granulares são geradas a partir dos resultados reais TRX, mantendo falhas e skips.

## Tarefas e provas

| Feature | Tarefas | Critérios provados |
| --- | --- | --- |
| Gestão financeira | T-093 a T-096 concluídas | AC-094 a AC-096, 3/3 |
| Gestão de equipe | T-080 a T-087 concluídas | AC-070 a AC-075, 6/6 |

Testes PostgreSQL usaram uma base temporária exclusiva, criada e removida pelo teste. Cobrem pagamentos, convite público e aprovação simultânea. As fixtures HTTP não usam o banco de trabalho. Os critérios de assinaturas receberam IDs próprios para não disputar provas com equipe.

## Pendências para fechar a entrega global

1. Corrigir specs antigas cujo formato não é reconhecido pelo motor, referências de tarefas, códigos duplicados e arquivos mapeados que não existem.
2. Revalidar provas antigas e implementar testes dos requisitos que ainda não possuem cobertura. Testes pulados não contam como prova.
3. Revisar o isolamento PostgreSQL/RLS e as tarefas correspondentes que referenciam arquivos ausentes; o teste de persistência desta entrega não certifica RLS de produção.
4. Auditar o projeto inteiro até exit 0 antes de declarar a entrega encerrada e efetuar o commit de conclusão.

A lista completa está em `audit-financeiro-equipe.json`. Não houve push ou deploy. As alterações permanecem locais e sem commit de conclusão.

## Limites desta etapa

Pagamentos são manuais e não existe gateway de cobrança integrado. O envio de e-mail continua simulado: o convite entra na outbox, mas ainda será necessário configurar um provedor de e-mail para entrega real. As telas de financeiro e equipe não foram criadas nesta revisão.

A API foi reiniciada, portanto sessões emitidas antes do reinício podem exigir novo login. Os cadastros no PostgreSQL foram preservados.
