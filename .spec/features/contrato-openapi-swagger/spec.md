# Spec: Contrato OpenAPI e Swagger

> feature: contrato-openapi-swagger
> status: pronta

## Contexto

Todas as rotas desenvolvidas precisam possuir documentação Swagger sincronizada com o comportamento real. O OpenAPI será um artefato versionado e testado: mudanças de rota, método, autenticação, payload ou erro exigirão atualização explícita do contrato.

## Histórias

### US-015 — Descobrir e integrar todas as rotas

Como pessoa desenvolvedora de frontend ou integração, quero um documento OpenAPI completo, para consumir a API sem depender da leitura do backend.

#### AC-036 — Todas as rotas implementadas aparecem no contrato

- **Dado** os endpoints mapeados pela aplicação
- **Quando** o documento OpenAPI v1 é gerado
- **Então** ele contém `GET /health`, consulta de CNPJ, cadastro de tenant, confirmação de e-mail, login e perfis atuais com seus métodos corretos

#### AC-037 — Operações descrevem sucesso e falhas

- **Dado** uma operação pública ou autenticada documentada
- **Quando** seu contrato é inspecionado
- **Então** ele informa autenticação, headers, parâmetros, request, respostas de sucesso e erros `application/problem+json` aplicáveis

#### AC-038 — Schemas não expõem dados secretos

- **Dado** os schemas de resposta e os exemplos do OpenAPI
- **Quando** o documento é inspecionado
- **Então** não existem senha, hash, token bruto de verificação, CPF integral, string de conexão ou credencial fiscal/bancária em respostas ou exemplos

### US-016 — Detectar divergência entre código e documentação

Como pessoa mantenedora, quero que a CI detecte alterações não revisadas no contrato, para que o Swagger continue verdadeiro após mudanças na API.

#### AC-039 — Documento versionado coincide com a aplicação

- **Dado** a API compilada e o arquivo `docs/openapi/v1.json` versionado
- **Quando** o teste de contrato gera uma versão canônica atual
- **Então** os documentos são semanticamente equivalentes e qualquer divergência falha o teste

#### AC-040 — Swagger UI permite explorar a versão vigente

- **Dado** a aplicação executada em ambiente de desenvolvimento ou teste
- **Quando** a interface Swagger é aberta
- **Então** ela carrega o mesmo documento OpenAPI v1 validado pelos testes

## Fora de escopo

- Gerar SDKs clientes.
- Publicar portal externo de desenvolvedores.
- Documentar rotas futuras ainda não implementadas.
- Expor Swagger UI em produção sem decisão operacional posterior.

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-008 | OpenAPI JSON é o contrato versionado; Swagger UI é apenas uma visualização desse contrato. | confirmada | Deriva do requisito de manter um documento Swagger de todas as rotas e testá-lo. |

## Perguntas em aberto

| ID | Pergunta | Status | Resposta |
|---|---|---|---|
| Q-006 | O contrato deve listar rotas planejadas ainda não implementadas? | respondida | Não. Deve refletir somente rotas desenvolvidas; propostas permanecem nas specs até existirem no código. |
