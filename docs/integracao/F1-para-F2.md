# F1 → F2 — o que mudou no back-end e afeta o front

Correções da Fase 2 aprovadas pelo dono. Só o que muda para `wwwroot/**`.

## 1. Anti-CSRF (R44)

`POST`, `PUT`, `PATCH` e `DELETE` sem o cabeçalho exato `X-Requested-With: fetch` → **403**:

```json
{ "title": "Requisição sem o cabeçalho X-Requested-With.", "status": 403 }
```

Vale para **todas** as rotas, inclusive `POST /login`, `POST /login/cadastro` e `POST /login/sair`.
A recusa acontece antes da autenticação: sem cookie e sem o cabeçalho, a resposta é 403 (não 401).
`GET` não exige. Valor comparado com diferenciação de maiúsculas (`fetch`, não `Fetch`).

## 2. `PedidoDetalheDto` — campos novos (R40)

Ordem no JSON (System.Text.Json, camelCase):

```json
{
  "id": 12, "clienteId": 3, "clienteNome": "Maria Souza", "clienteTelefone": "19999990000",
  "dataEntrega": "2026-11-20", "status": "Confirmado",
  "criadoEm": "2026-11-18T14:05:00Z",
  "criadoPorNome": "Ana",
  "statusAlteradoEm": "2026-11-18T15:10:00Z",
  "statusAlteradoPorNome": "Bruno",
  "itens": [ ... ], "total": 91.80
}
```

| Campo | Tipo | Quando é `null` |
| --- | --- | --- |
| `criadoPorNome` | `string` | nunca |
| `statusAlteradoEm` | `string` ISO UTC | pedido nunca saiu de Aberto |
| `statusAlteradoPorNome` | `string` | junto com `statusAlteradoEm` |

Nomes são os **atuais** dos usuários (join), não cópia. `PedidoResumoDto` (lista) não mudou.

## 3. Novas respostas 400

| Rota | Condição | Chave em `errors` | Mensagem |
| --- | --- | --- | --- |
| `POST /pedidos` | entrega > hoje + 365 dias (fuso SP) | `DataEntrega` | "A data de entrega deve ser no máximo 365 dias a partir de hoje." |
| `GET /pedidos` | `busca` > 120 caracteres | `Busca` | "A busca deve ter no máximo 120 caracteres." |
| `GET /pedidos` | `status` fora do enum (ex.: `7`) | `Status` | "Status inválido. Use Aberto, Confirmado, Entregue ou Cancelado." |
| `GET /categorias` | `nome` > 60 caracteres | `Nome` | "O filtro de nome deve ter no máximo 60 caracteres." |
| todas as listagens | `pagina` > 1.000.000 | `Pagina` | "A página deve estar entre 1 e 1000000." |
| qualquer escrita | CHECK do banco recusou (raro; a API valida antes) | — | `title` "Dados recusados por uma regra do banco." |

Sugestão: `max` no `<input type="date">` de entrega = hoje + 365 (espelha a regra; a API continua sendo quem decide).

## 4. Sessão encerrada pelo servidor (R35)

A cada 5 min o cookie é conferido no banco. Se o papel mudou ou o usuário foi apagado, a próxima
requisição volta **401** e o cookie é apagado. O `exigirLogin`/tratamento de 401 existente já leva ao login;
nada novo a fazer além de não tratar 401 como erro genérico no meio de uma tela.

## 5. Cadastro (R42)

`POST /login/cadastro` com o e-mail configurado em `Admin:Email` → **409** "E-mail já cadastrado."
(mesma resposta do duplicado, de propósito). A tela de cadastro não precisa de mudança.

## 6. CSP (R43)

Já atendida pelos commits do F2 (sem `<script>`/`style=`/`on*` inline). Política enviada:

```text
default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:;
frame-ancestors 'none'; base-uri 'self'; form-action 'self'
```

`img-src` aceita `data:`; nada de CDN. `/scalar` e `/openapi` ficam fora da CSP.
