# Arquitetura

Visão geral de como o CRUD Confeitaria está organizado e do caminho que uma requisição percorre.

## Visão em camadas

```mermaid
flowchart LR
    subgraph Navegador
        HTML["Páginas HTML<br/>login · cadastro · produtos"]
        JS["js/api.js<br/>fetch + tratamento de erros"]
    end

    subgraph "ASP.NET Core (MyCRUD)"
        MW["Pipeline de middlewares<br/>erros · arquivos estáticos · auth · rate limit"]
        EP["Endpoints<br/>LoginEndpoints · ProdutoEndpoints"]
        FV["ValidacaoFiltro<br/>DTOs + DataAnnotations"]
        DB["AppDbContext<br/>Entity Framework Core"]
    end

    PG[("PostgreSQL<br/>Supabase")]

    HTML --> JS -->|HTTP + cookie| MW --> FV --> EP --> DB -->|Npgsql| PG
```

| Camada | Responsabilidade | Arquivos |
| --- | --- | --- |
| Apresentação | Formulários, tabela, mensagens ao usuário | `wwwroot/*.html`, `wwwroot/js/api.js` |
| Pipeline | Regras transversais: erros, autenticação, autorização, limite de tentativas | `Program.cs`, `Compartilhado/` |
| Endpoints | Recebem o DTO já validado, aplicam a regra de negócio, devolvem o status HTTP | `*/…Endpoints.cs` |
| Contrato | Formato de entrada e saída da API, com regras de validação | `*/…Dtos.cs` |
| Domínio / persistência | Entidades e mapeamento para tabelas | `Produtos/Produto.cs`, `Login/Usuario.cs`, `Dados/AppDbContext.cs` |
| Banco | Armazenamento e integridade (tamanho, precisão, unicidade) | `Migrations/` → Supabase |

## Organização por funcionalidade

As pastas seguem **feature folders**: tudo sobre produtos fica em `Produtos/`, tudo sobre login em `Login/`.
Para adicionar uma entidade nova (ex.: `Categorias/`), cria-se uma pasta com entidade, DTOs e endpoints, e uma linha `app.MapCategoriaEndpoints()` no `Program.cs`.

```text
MyCRUD/
├── Program.cs               # Composição: serviços + ordem do pipeline
├── Compartilhado/           # Código usado por todas as funcionalidades
│   ├── ValidacaoFiltro.cs        # Valida DTOs antes do endpoint
│   ├── TratadorGlobalDeExcecoes.cs # Exceção → Problem Details
│   └── LimiteDeRequisicoes.cs    # Rate limiting do login
├── Dados/AppDbContext.cs    # Sessão com o banco e regras de integridade
├── Login/                   # Usuário, autenticação, cadastro
├── Produtos/                # CRUD de produtos
├── Migrations/              # Histórico versionado do schema
└── wwwroot/                 # Front-end estático
```

## Caminho de uma requisição

Exemplo: `POST /produtos` com o corpo `{ "nome": "", "preco": -1, "categoriaId": 1 }`.

```mermaid
sequenceDiagram
    autonumber
    participant N as Navegador
    participant P as Pipeline
    participant F as ValidacaoFiltro
    participant E as Endpoint Criar
    participant D as AppDbContext / PostgreSQL

    N->>P: POST /produtos + cookie Confeitaria.Auth
    P->>P: UseAuthentication: lê o cookie e monta o usuário
    P->>P: UseAuthorization: grupo exige login → ok
    P->>F: JSON convertido em CriarProdutoDto
    F-->>N: 400 ValidationProblem { errors: { Nome, Preco } }
    Note over F,E: Com dados válidos o filtro chama o endpoint
    F->>E: DTO válido
    E->>D: INSERT INTO "Produtos" ...
    D-->>E: Id gerado
    E-->>N: 201 Created + ProdutoRespostaDto
```

Se algo inesperado acontecer em qualquer etapa (banco fora do ar, bug), o `TratadorGlobalDeExcecoes` intercepta a exceção e devolve `500` em Problem Details, sem expor detalhes internos.

## Ordem do pipeline (`Program.cs`)

| Ordem | Middleware | Por que nessa posição |
| --- | --- | --- |
| 1 | `UseExceptionHandler` | Primeiro, para capturar exceções de tudo que vem depois |
| 2 | `UseStatusCodePages` | Dá corpo Problem Details a 401/404/429 vazios |
| 3 | `UseStaticFiles` | Serve o HTML sem passar por autenticação |
| 4 | `UseAuthentication` | Descobre **quem** é o usuário a partir do cookie |
| 5 | `UseAuthorization` | Decide **se** ele pode acessar a rota |
| 6 | `UseRateLimiter` | Conta tentativas nas rotas marcadas |

## Duas camadas de validação

| Onde | O que garante | Exemplo |
| --- | --- | --- |
| DTO (`[MaxLength]`, `[Range]`...) | Mensagem amigável, campo a campo, antes de tocar no banco | `"O nome deve ter no máximo 120 caracteres."` |
| Banco (`HasMaxLength`, índice único) | Integridade mesmo se a aplicação falhar ou houver concorrência | `varchar(120)`, `IX_Usuarios_Email` |

Os dois lados usam as **mesmas constantes** (`Produto.NomeTamanhoMaximo`, `Usuario.EmailTamanhoMaximo`), então nunca divergem.

## Formato de erro

Toda resposta de erro segue a [RFC 9457 — Problem Details](https://www.rfc-editor.org/rfc/rfc9457):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Dados inválidos.",
  "status": 400,
  "errors": {
    "Nome": ["Informe o nome."],
    "Preco": ["O preço deve ser maior que zero e menor que 100 milhões."]
  }
}
```

| Status | Quando |
| --- | --- |
| 400 | DTO inválido ou JSON malformado |
| 401 | Sem cookie de login, ou e-mail/senha incorretos |
| 404 | Produto não encontrado |
| 409 | E-mail já cadastrado |
| 429 | Mais de 5 tentativas de login/cadastro por minuto no mesmo IP |
| 500 | Erro inesperado (detalhe técnico apenas em Development) |
