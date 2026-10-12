# Spec: Onboarding sem exposição de token

> feature: onboarding-token-desenvolvimento
> status: pronta

## Contexto

A instrução explícita de 12/10/2026 exige minimização sem exceção silenciosa para Development. A regra histórica que documentava um header bruto contradizia AC-038/P-005 e foi substituída: o cadastro retorna apenas uma mensagem em todos os ambientes. A confirmação aceita token como entrada legítima, obtido por canal de entrega separado. A entrega real de e-mail permanece pendente; esta mudança não a implementa.

## Histórias

### US-053 — Confirmar e-mail sem revelar credencial no cadastro

Como pessoa usuária, quero cadastrar minha empresa sem expor o token de confirmação na resposta, para manter a credencial restrita ao canal de confirmação.

#### AC-100 — Cadastro e contrato não revelam token em nenhum ambiente

- **Dado** a API executada em Development, Test ou Production
- **Quando** o cadastro é realizado e o contrato de `POST /api/v1/tenants` é consultado
- **Então** a resposta HTTP 201 contém apenas a mensagem, sem token bruto no corpo ou no header `X-Development-Verification-Token`, e o contrato não documenta esse header nem propriedade de token na saída.

## Fora de escopo

- Implementar provedor real de e-mail.
- Alterar a validação de tokens recebidos na confirmação.

## Suposições

| ID | Suposição | Status | Resolução |
|---|---|---|---|
| ASM-013 | Development/Test pode revelar token bruto via header. | invalidada | Instrução explícita de 12/10/2026: não expor token bruto em respostas ou exemplos, sem exceção silenciosa para Development. |

## Perguntas em aberto

Nenhuma.
