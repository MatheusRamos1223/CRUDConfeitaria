# CRUD Confeitaria

Projeto acadêmico em C# com ASP.NET Core (.NET 10), Minimal APIs e PostgreSQL hospedado no Supabase. Inclui pedidos com itens e preço congelado, clientes, produtos, categorias, papéis Admin/Operador, login por cookie e páginas HTML com JavaScript.

## Sumário

- [Funcionalidades e limites](#funcionalidades-e-limites)
- [Tecnologias e estrutura](#tecnologias-e-estrutura)
- [Configurar e executar](#configurar-e-executar)
- [Usar pelo navegador](#usar-pelo-navegador)
- [Documentação interativa (Scalar)](#documentação-interativa-scalar)
- [Rotas e regras](#rotas-e-regras)
- [Testar pelo PowerShell](#testar-pelo-powershell)
- [Migrations](#migrations)
- [Resolver problemas](#resolver-problemas)
- [Tutorial de Git para o grupo](#tutorial-de-git-para-a-rapaziada)

## Funcionalidades e limites

Implementado:

- **Pedidos**: criar pedido com 1–50 itens para hoje ou depois (fuso `America/Sao_Paulo`), preço de cada item copiado do produto na criação (congelado), total calculado; status Aberto → Confirmado → Entregue, ou Cancelado, por rotas de ação; pedido nunca é editado nem excluído.
- **Clientes**: cadastrar, editar, buscar por nome ou telefone; telefone salvo só com dígitos (10 ou 11); e-mail opcional; cliente com pedidos não pode ser excluído.
- **Produtos**: cadastrar e editar em uma categoria existente; `DELETE` **desativa** (soft delete) e `POST /produtos/{id}/reativar` reativa; listagem com filtro por nome, categoria e situação.
- **Categorias**: nome único sem diferenciar maiúsculas; categoria com produtos (ativos ou inativos) não pode ser excluída.
- **Papéis**: `Operador` (padrão) e `Admin`, definido pela configuração `Admin:Email`; rotas de Admin devolvem `403` para Operador.
- Listagens paginadas com o mesmo envelope `{ itens, pagina, tamanhoPagina, total, totalPaginas }` (até 100 por página) e filtro "contém" sem diferenciar maiúsculas.
- Validar entrada com DataAnnotations nos DTOs e filtros, devolvendo erros por campo (`400 ValidationProblem`).
- Padronizar todos os erros em Problem Details (RFC 9457), sem stack trace fora de Development; conflitos de estado em `409`.
- Garantir integridade também no banco: chaves estrangeiras, `CHECK`, índices únicos e tamanhos de coluna.
- Cadastrar usuários com nome, e-mail válido e senha entre 8 e 128 caracteres (sempre como Operador).
- Normalizar e-mail (sem espaços, minúsculas) no cadastro e no login.
- Limitar login e cadastro a 5 tentativas por minuto por IP (`429`).
- Documentar a API em OpenAPI com interface Scalar em `/scalar` (Development).
- Compilar e conferir migrations a cada push pelo GitHub Actions.
- Armazenar hash de senha com `PasswordHasher<Usuario>`.
- Autenticar por cookie e exigir autenticação em todas as rotas de dados.
- Encerrar a sessão por `POST /login/sair`.
- Telas com o mesmo cabeçalho, menu e CSS: Pedidos, Detalhe do pedido, Clientes, Produtos e Categorias.

Limites atuais:

- O papel fica no cookie: uma promoção a Admin só vale depois de sair e entrar de novo (até 2 h).
- Admin nunca é rebaixado automaticamente; rebaixar é SQL manual. Não há tela de gestão de usuários.
- Os selects das telas (clientes, produtos, categorias) carregam no máximo 100 registros.
- Pedido não pode ser editado: para corrigir, cancele e crie outro.
- Não há testes automatizados ainda (Fase 3).
- As páginas HTML são públicas; os dados e operações exigem login. As telas redirecionam ao login quando não há sessão.
- O cadastro de usuários só é registrado em `Development`.

## Tecnologias e estrutura

| Tecnologia/arquivo | Função |
| --- | --- |
| C# | Linguagem da aplicação |
| .NET 10 | Plataforma, runtime e ferramentas |
| ASP.NET Core | Servidor web e pipeline HTTP |
| Minimal APIs | Rotas registradas com `MapGet`, `MapPost`, `MapPut` e `MapDelete` |
| Entity Framework Core | ORM para consultar e alterar registros |
| Npgsql | Provedor PostgreSQL do EF Core |
| Supabase | Hospedagem do PostgreSQL; não usamos Supabase Auth |
| HTML e JavaScript | Formulários e chamadas `fetch` à API |
| `MyCRUD.csproj` | SDK Web, target `net10.0`, pacotes e identificador de User Secrets |
| `MyCRUD.slnx` | Solução XML que agrupa o projeto |
| `dotnet-tools.json` | Manifesto da ferramenta local `dotnet-ef` (10.0.12) |
| Microsoft.AspNetCore.OpenApi | Gera o documento OpenAPI a partir dos endpoints |
| Scalar.AspNetCore | Interface web para ler e testar a API |
| GitHub Actions | Compila o projeto a cada push/PR (`.github/workflows/build.yml`) |

Os pacotes EF Design, Npgsql e OpenApi estão referenciados como `10.*` e o Scalar como `2.*`; o restore resolve uma versão compatível com esse intervalo. O manifesto da ferramenta tem versão específica.

```text
CRUDConfeitaria/
├── .github/workflows/build.yml
├── .gitignore
├── docs/
│   ├── arquitetura.md
│   ├── autenticacao.md
│   └── decisoes.md
├── MyCRUD.slnx
├── dotnet-tools.json
├── README.md
└── MyCRUD/
    ├── MyCRUD.csproj
    ├── Program.cs
    ├── Compartilhado/
    │   ├── ValidacaoFiltro.cs
    │   ├── TratadorGlobalDeExcecoes.cs
    │   ├── LimiteDeRequisicoes.cs
    │   ├── Paginacao.cs          # ResultadoPaginado<T> + PaginarAsync
    │   ├── ConsultaTexto.cs      # filtro "contém" (ILIKE com curingas escapados)
    │   └── Politicas.cs          # política SomenteAdmin
    ├── Dados/
    │   └── AppDbContext.cs
    ├── Login/                    # Usuario, Papel, LoginDtos, LoginEndpoints
    ├── Produtos/                 # Produto, ProdutoDtos, ProdutoEndpoints
    ├── Categorias/               # Categoria, CategoriaDtos, CategoriaEndpoints
    ├── Clientes/                 # Cliente, ClienteDtos, ClienteEndpoints
    ├── Pedidos/                  # Pedido, ItemPedido, StatusPedido, PedidoDtos, PedidoEndpoints
    ├── Migrations/               # 7 migrations + AppDbContextModelSnapshot.cs
    └── wwwroot/
        ├── css/app.css
        ├── js/api.js             # exigirLogin, mensagemDeErro
        ├── js/layout.js          # cabeçalho, menu, paginador, helpers
        ├── login.html
        ├── cadastro.html
        ├── pedidos.html
        ├── pedido-detalhe.html
        ├── clientes.html
        ├── produtos.html
        ├── categorias.html
        └── prototipos/           # protótipos estáticos (saem na Fase 4)
```

`Program.cs` registra banco, autenticação, autorização, Problem Details, rate limiting e OpenAPI, define a ordem do pipeline e chama os métodos de registro dos endpoints. `Compartilhado/` reúne o que todas as funcionalidades usam: validação, tratamento de exceções, limite de requisições, paginação, filtro de texto e políticas de acesso. `AppDbContext` expõe `Produtos`, `Usuarios`, `Categorias`, `Clientes`, `Pedidos` e `ItensPedido` e configura tamanhos de coluna, preços `numeric(10,2)`, enums como texto, chaves estrangeiras, `CHECK` e índices. As entidades representam os registros; os DTOs descrevem entrada/saída e suas regras. A API nunca devolve entidades, apenas DTOs de resposta.

Para entender o projeto em profundidade:

| Documento | Conteúdo |
| --- | --- |
| [docs/arquitetura.md](docs/arquitetura.md) | Camadas, caminho de uma requisição, ordem do pipeline, formato de erro |
| [docs/autenticacao.md](docs/autenticacao.md) | Fluxo do login, papéis, proteções e o ataque que cada uma evita |
| [docs/decisoes.md](docs/decisoes.md) | ADRs: por que Minimal API, cookie, Supabase só como banco, papéis, soft delete, preço congelado, etc. |
| [docs/fases-2-4.md](docs/fases-2-4.md) | Contrato das fases 2–4: telas, rotas, regras R1–R37 |

O SDK inclui os arquivos `.cs` automaticamente; não é necessário listá-los no `.csproj` nem criar um equivalente ao `__init__.py`. `bin/` e `obj/` são gerados e ignorados pelo Git.

Para quem vem de Python, como eu: NuGet cumpre o papel de gerenciador de pacotes; DTOs lembram schemas do FastAPI; EF Core lembra o ORM do Django. Um `using` importa um namespace, mas não instala o pacote. A injeção de dependência entrega o `AppDbContext` ao parâmetro da rota. `async`/`await` permite aguardar operações de banco sem bloquear a thread durante a espera.

## Configurar e executar

Todos os comandos abaixo são para **PowerShell**, executados na raiz do repositório. Copie apenas o comando, sem o prefixo `PS C:\...>` nem mensagens de saída.

### 1. Pré-requisitos

Instale o [SDK do .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0), o [Git](https://git-scm.com/downloads) e um editor (Visual Studio ou VS Code). Tenha acesso a um projeto Supabase e à senha de seu banco.

```powershell
dotnet --list-sdks
git --version
```

É necessário o SDK, não somente o runtime. Ter apenas .NET 8 não atende ao target `net10.0`.

### 2. Clonar e restaurar

Abra um PowerShell na pasta onde deseja guardar o projeto, por exemplo `C:\Users\SeuUsuario\source\repos`. O `git clone` cria a pasta `CRUDConfeitaria`; o `cd` entra nela. Não execute o clone dentro de uma cópia já existente.

```powershell
git clone https://github.com/MatheusRamos1223/CRUDConfeitaria.git
cd CRUDConfeitaria
dotnet restore MyCRUD/MyCRUD.csproj
dotnet tool restore
```

Não recrie o manifesto nem reinstale os pacotes manualmente ao clonar: eles já são descritos pelos arquivos versionados.

### 3. Configurar o Supabase

Para um banco novo, crie um projeto no painel e guarde a senha do banco. Para usar o banco do grupo, obtenha os dados de acesso com o responsável; não crie tabelas manualmente para duplicar as migrations.

No painel: **Connect → Direct → Connection Method → Session pooler**. Use host, porta e usuário mostrados no seu projeto. Essa opção atende conexões por IPv4. Veja a [documentação de conexão do Supabase](https://supabase.com/docs/guides/database/connecting-to-postgres).

Uma URI como:

```text
postgresql://postgres.PROJECT_REF:[YOUR-PASSWORD]@HOST:5432/postgres
```

vira uma string Npgsql como:

```text
Host=HOST;Port=5432;Database=postgres;Username=postgres.PROJECT_REF;Password=SUA_SENHA;SSL Mode=Require
```

A senha é a do PostgreSQL. Não use chave `anon` ou `service_role` no lugar dela.

### 4. Guardar a conexão localmente

O `.csproj` já contém `UserSecretsId`; não precisa inicializá-lo novamente. Substitua os exemplos e execute na sua máquina:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Host=SEU_HOST;Port=5432;Database=postgres;Username=postgres.SEU_PROJECT_REF;Password=SUA_SENHA;SSL Mode=Require' --project MyCRUD/MyCRUD.csproj
```

Cada integrante configura seu próprio segredo local. Ele não acompanha `git clone` nem `git pull`. O identificador no `.csproj` pode ser versionado; a senha, não. User Secrets fica fora do repositório e não é um cofre criptografado. Consulte a [documentação da Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).

Para ter um **Admin** (quem gerencia categorias, desativa produtos, exclui clientes e cancela pedido confirmado), guarde também o e-mail dele. Quem entrar com esse e-mail vira Admin no login; todos os outros são Operador:

```powershell
dotnet user-secrets set "Admin:Email" "seu-email@exemplo.com" --project MyCRUD/MyCRUD.csproj
```

Em produção, use a variável de ambiente `Admin__Email`. Sem essa configuração a API funciona, mas registra o aviso `Nenhum Admin configurado (Admin:Email).` e ninguém consegue as ações de Admin. Depois de configurar, quem já estava logado precisa sair e entrar de novo.

Se a senha tiver `;` ou outros caracteres especiais da string de conexão, o valor precisa ser corretamente delimitado conforme o formato Npgsql. Uma aspa simples dentro do comando PowerShell deve ser duplicada. Não cole comandos preenchidos com credenciais em commits, prints ou mensagens.

### Configurar o acesso em OUTRA máquina

**Clonar o repositório não copia a conexão do Supabase.** Mesmo usando a mesma conta GitHub, cada computador e usuário do Windows precisa configurar os próprios User Secrets. Não é necessário criar `appsettings.json`.

O que obter com o responsável pelo banco, por um canal privado:

| Dado | Onde encontrar / significado |
| --- | --- |
| Host | Painel do projeto Supabase, Connect, Session pooler |
| Porta | A mostrada na conexão do Session pooler, normalmente 5432 |
| Database | Nome do banco, normalmente `postgres` |
| Username | Usuário completo, como `postgres.PROJECT_REF` |
| Password | Senha do banco definida ao criar o projeto; não é a senha da conta Supabase nem do login do aplicativo |

Se receber uma URI `postgresql://USUARIO:[YOUR-PASSWORD]@HOST:5432/postgres`, converta os campos para `Host=HOST;Port=5432;Database=postgres;Username=USUARIO;Password=SENHA_REAL;SSL Mode=Require`. Não mantenha `[YOUR-PASSWORD]` nem os outros placeholders no comando final.

Depois de clonar e entrar na raiz `CRUDConfeitaria`, execute **na máquina nova**:

```powershell
dotnet restore MyCRUD/MyCRUD.csproj
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DefaultConnection" 'Host=SEU_HOST;Port=5432;Database=postgres;Username=postgres.SEU_PROJECT_REF;Password=SUA_SENHA;SSL Mode=Require' --project MyCRUD/MyCRUD.csproj
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet ef database update --project MyCRUD/MyCRUD.csproj
dotnet run --project MyCRUD/MyCRUD.csproj -- --urls http://localhost:5000
```

Substitua os dados no comando `user-secrets set` antes de executar. Esse comando também pode corrigir/substituir uma conexão salva incorretamente. O `UserSecretsId` já está no `.csproj`: não rode `init` novamente nem altere esse identificador para configurar cada computador. Em banco compartilhado, coordene a aplicação de migrations; não gere novas migrations para apenas conectar outra máquina.

Quando aparecer `Now listening on: http://localhost:5000`, abra `http://localhost:5000/cadastro.html` para criar uma conta do aplicativo, ou faça login com uma conta já existente nesse banco. A senha de login do aplicativo é diferente da senha de conexão PostgreSQL.

### Onde a conexão fica salva?

No Windows, o arquivo local deste projeto é:

```text
%APPDATA%\Microsoft\UserSecrets\f3047712-0f96-4bfe-be4e-903a46c50213\secrets.json
```

`%APPDATA%` representa a pasta de dados do usuário atual. Para abrir essa localização pelo Explorador de Arquivos, cole o caminho na barra de endereço. Não procure o arquivo dentro de `MyCRUD`, de `wwwroot` ou do GitHub.

- `MyCRUD/MyCRUD.csproj` contém somente o `UserSecretsId`, que identifica o armazenamento local.
- `Program.cs` usa `GetConnectionString("DefaultConnection")` para ler a chave `ConnectionStrings:DefaultConnection`.
- A senha e o host reais ficam no `secrets.json` da máquina, salvo pelo comando `user-secrets set`.
- Ao trocar de usuário do Windows ou de computador, esse arquivo não acompanha o projeto.

Para conferir os valores localmente:

```powershell
dotnet user-secrets list --project MyCRUD/MyCRUD.csproj
```

**Esse comando exibe a senha do banco.** Não envie sua saída em prints, mensagens ou commits. Não é necessário executá-lo para iniciar a aplicação; serve apenas para conferir a configuração.

Se aparecer `DefaultConnection não foi configurada`, confira o comando `set`, o nome exato da chave e o ambiente Development no mesmo terminal da aplicação. Se a conexão foi encontrada, mas houver erro de autenticação ou rede PostgreSQL, confira os dados de acesso e se o projeto Supabase está ativo. Para investigar um erro do `AppDbContext`, compartilhe apenas a mensagem, removendo credenciais.
### 5. Definir Development e aplicar migrations

No terminal que executará a aplicação:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet build MyCRUD/MyCRUD.csproj
dotnet ef database update --project MyCRUD/MyCRUD.csproj
```

`Development` carrega os User Secrets e habilita `/login/cadastro`. As variáveis valem para a sessão desse terminal; repita ao abrir outro.

Ao preparar uma cópia do projeto, aplique as migrations existentes com `database update`. Não gere migrations duplicadas. Em banco compartilhado, combine com o grupo quem aplica mudanças de estrutura.

### 6. Iniciar o servidor

```powershell
dotnet run --project MyCRUD/MyCRUD.csproj -- --urls http://localhost:5000
```

Espere `Now listening on: http://localhost:5000`. Deixe esse terminal aberto e use outro para testes. `Ctrl+C` encerra a API. Antes de compilar novamente após mudar C#, encerre a instância antiga para liberar o executável. Salve o editor antes de executar comandos.

`localhost` aponta para a sua própria máquina. Compartilhar esse endereço não disponibiliza a aplicação para os colegas em seus computadores.

### 7. Primeiro acesso e confirmação

Abra `http://localhost:5000/cadastro.html`, crie um usuário (com o e-mail de `Admin:Email`, se quiser ser Admin) e depois entre em `http://localhost:5000/login.html`. O login abre a tela de Pedidos. Como Admin, cadastre uma categoria; depois um produto, um cliente e um pedido; confira no Supabase e faça logout. Para confirmar persistência, reinicie a API e faça login novamente: os dados devem continuar disponíveis.

Não existe usuário padrão criado automaticamente. A senha escolhida nesse formulário pertence ao usuário do aplicativo; não é a senha do PostgreSQL nem a senha da conta Supabase.

Se usar o banco do grupo, os cadastros, edições e exclusões afetam os dados compartilhados. Um projeto Supabase separado permite testar sem alterar o banco dos colegas.

### 8. Nas próximas execuções

Clone, restore de ferramentas e configuração dos User Secrets são etapas de preparação. Com o ambiente já preparado, basta entrar na pasta e executar:

```powershell
cd C:\CAMINHO\CRUDConfeitaria
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project MyCRUD/MyCRUD.csproj -- --urls http://localhost:5000
```

Substitua o caminho. As variáveis precisam ser definidas novamente em um terminal novo. Se receber mudanças do Git, restaure dependências/ferramentas quando necessário e aplique migrations recebidas conforme combinado com o grupo.

## Usar pelo navegador

| Endereço | Uso |
| --- | --- |
| [Página inicial](http://localhost:5000/) | Mensagem de confirmação da API |
| [Cadastro](http://localhost:5000/cadastro.html) | Criar usuário (Operador), com API em Development |
| [Login](http://localhost:5000/login.html) | Entrar e receber cookie; abre Pedidos |
| [Pedidos](http://localhost:5000/pedidos.html) | Filtrar pedidos, criar pedido com itens e total estimado |
| Detalhe do pedido (`pedido-detalhe.html?id=N`) | Itens com preço congelado, total, Confirmar / Marcar como entregue / Cancelar |
| [Clientes](http://localhost:5000/clientes.html) | Buscar por nome ou telefone, cadastrar, editar; excluir (Admin) |
| [Produtos](http://localhost:5000/produtos.html) | Filtrar por nome, categoria e situação; cadastrar, editar; desativar/reativar (Admin) |
| [Categorias](http://localhost:5000/categorias.html) | Listar com quantidade de produtos; cadastrar, renomear, excluir (Admin) |
| [JSON de produtos](http://localhost:5000/produtos/) | Consultar a primeira página, após login nesse navegador |
| [Scalar](http://localhost:5000/scalar) | Documentação interativa da API (Development) |

Todas as telas internas têm o mesmo esqueleto: cabeçalho com menu (Pedidos · Clientes · Produtos · Categorias), nome e papel do usuário e Sair; barra de filtro; lista com paginador ("1–20 de 57"); formulário à direita (empilha abaixo de 960 px). Botões só de Admin somem para o Operador — isso é só conforto visual: a API recusa com `403` e a tela mostra "Você não tem permissão para esta ação.".

Fluxo de um pedido:

1. (Admin) Em Categorias, cadastre "Bolos". Em Produtos, cadastre "Bolo de cenoura" nessa categoria.
2. Em Clientes, cadastre um cliente; o telefone aceita máscara, `(19) 99999-0000`, e é salvo só com dígitos.
3. Em Pedidos, escolha o cliente, a data de entrega (hoje ou depois) e os itens; o total estimado usa o preço atual.
4. Criar pedido abre o detalhe: o preço de cada item fica congelado a partir daqui.
5. Confirmar → Marcar como entregue. Cancelar: qualquer usuário cancela pedido Aberto; pedido Confirmado só o Admin.
6. Altere o preço do produto e reabra o pedido: o total não muda.

A interface usa HTML, CSS (`css/app.css`) e JavaScript sem framework. Texto vindo da API entra sempre por `textContent`.

As páginas e a API são servidas pelo mesmo ASP.NET. Abra pelos endereços HTTP, não diretamente como arquivos do computador.

## Documentação interativa (Scalar)

Com a API em Development, abra `http://localhost:5000/scalar`. A página lista todas as rotas, os campos de cada DTO, as regras de validação e os status possíveis, e permite enviar requisições direto do navegador.

Para testar rotas protegidas: execute `POST /login` pelo Scalar; o cookie fica no navegador e é enviado nas chamadas seguintes. O documento bruto fica em `/openapi/v1.json`.

Os textos exibidos vêm dos comentários `///` do código (`GenerateDocumentationFile` no `.csproj`) e dos `.WithSummary(...)` dos endpoints.

## Rotas e regras

Todas as rotas de dados exigem cookie (`401` sem login). "Admin" = política `SomenteAdmin` (`403` para Operador).

| Método | Caminho | Papel | Resultado principal |
| --- | --- | --- | --- |
| GET | `/` | Pública | 200 e mensagem |
| POST | `/login/cadastro` | Pública, apenas Development | 201 (sempre Operador); 400, 409 ou 429 |
| POST | `/login/` | Pública | 200, usuário com `papel` e cookie; 400, 401 ou 429 |
| POST | `/login/sair` | Logado | 204 |
| GET | `/login/eu` | Logado | 200 e usuário da sessão com `papel` |
| GET | `/produtos?nome=&categoriaId=&situacao=&pagina=&tamanho=` | Logado | 200 página (`situacao`: `Ativos` padrão, `Inativos`, `Todos`) |
| GET | `/produtos/{id}` | Logado | 200 ou 404 (ativo ou inativo) |
| POST | `/produtos/` | Logado | 201; 400 (inclui categoria inexistente) |
| PUT | `/produtos/{id}` | Logado | 200, 400 ou 404 — **não altera `ativo`** |
| DELETE | `/produtos/{id}` | Admin | 204 — **desativa**, idempotente; 404 |
| POST | `/produtos/{id}/reativar` | Admin | 204, idempotente; 404 |
| GET | `/categorias?nome=&pagina=&tamanho=` | Logado | 200 página, com `quantidadeProdutos` |
| GET | `/categorias/{id}` | Logado | 200 ou 404 |
| POST | `/categorias/` | Admin | 201; 400, 409 nome duplicado |
| PUT | `/categorias/{id}` | Admin | 200; 400, 404, 409 nome duplicado |
| DELETE | `/categorias/{id}` | Admin | 204; 404, 409 possui produtos |
| GET | `/clientes?busca=&pagina=&tamanho=` | Logado | 200 página (busca no nome ou nos dígitos do telefone) |
| GET | `/clientes/{id}` | Logado | 200 ou 404 |
| POST | `/clientes/` | Logado | 201 ou 400 |
| PUT | `/clientes/{id}` | Logado | 200, 400 ou 404 |
| DELETE | `/clientes/{id}` | Admin | 204; 404, 409 possui pedidos |
| GET | `/pedidos?busca=&status=&de=&ate=&pagina=&tamanho=` | Logado | 200 página de resumos |
| GET | `/pedidos/{id}` | Logado | 200 detalhe ou 404 |
| POST | `/pedidos/` | Logado | 201 detalhe + `Location`; 400 |
| POST | `/pedidos/{id}/confirmar` | Logado | 200 detalhe; 404, 409 |
| POST | `/pedidos/{id}/entregar` | Logado | 200 detalhe; 404, 409 |
| POST | `/pedidos/{id}/cancelar` | Logado; Admin se Confirmado | 200 detalhe; 403, 404, 409 |

Não existe `PUT` nem `DELETE` de pedido. As listagens devolvem sempre o mesmo envelope:

```json
{ "itens": [ ... ], "pagina": 1, "tamanhoPagina": 20, "total": 57, "totalPaginas": 3 }
```

`pagina` começa em 1 e `tamanho` vai de 1 a 100 (padrão 20); fora disso, `400`. Página além do fim devolve `itens: []` com o `total` real. Filtros de texto são "contém", sem diferenciar maiúsculas, e `%`/`_` são buscados literalmente. Enums trafegam como texto (`"Aberto"`, `"Admin"`); datas de entrega como `"2026-11-20"`; `criadoEm` em UTC.

### Dados

Categoria: `Id`, `Nome` até 60 caracteres, único sem diferenciar maiúsculas ("Bolos" = "bolos").

Produto: `Id`, `Nome` até 120, `Preco` `numeric(10,2)` maior que zero, `CategoriaId` de uma categoria existente (senão `400 errors.CategoriaId`) e `Ativo`. Nasce ativo; só muda por `DELETE` (desativa) e `reativar`. Nenhum produto é apagado do banco. A resposta inclui `categoriaNome`.

Cliente: `Id`, `Nome` até 120, `Telefone` com DDD (10 ou 11 dígitos; a máscara é aceita e removida; não precisa ser único), `Email` opcional (`null`, ou válido até 254, salvo em minúsculas). Sem CPF.

Pedido: `Id`, `ClienteId`, `DataEntrega` (hoje ou depois no fuso de São Paulo), `Status` (`Aberto`, `Confirmado`, `Entregue`, `Cancelado`), `CriadoEm` e itens. Cada item tem `ProdutoId` (produto ativo, sem repetir no pedido), `Quantidade` 1–999 e `PrecoUnitario`, que a API copia do produto — o cliente nunca envia preço. `Total` e `Subtotal` são calculados na consulta. Transições: Aberto → Confirmado → Entregue; Aberto → Cancelado (qualquer); Confirmado → Cancelado (Admin). Qualquer outra → `409` "Pedido Entregue não pode ser cancelado.".

Usuário: `Id`, `Nome` (até 120), `Email` (até 254, formato validado), `SenhaHash`, `Papel` (`Operador` padrão ou `Admin`). O cadastro exige senha entre 8 e 128 caracteres, salva o e-mail em minúsculas e sem espaços, verifica duplicidade e salva o hash. A resposta expõe ID, nome, e-mail e papel.

Erros seguem Problem Details. Exemplo de `400`:

```json
{ "title": "Dados inválidos.", "status": 400, "errors": { "Nome": ["Informe o nome."] } }
```

Detalhes e tabela completa de status em [docs/arquitetura.md](docs/arquitetura.md#formato-de-erro).

Login normaliza o e-mail, verifica o hash (atualizando-o se necessário), promove a Admin se o e-mail for o de `Admin:Email` e cria claims de ID, nome, e-mail e papel. E-mail inexistente e senha errada recebem a mesma resposta `401`, no mesmo tempo. Login e cadastro aceitam 5 requisições por minuto por IP; a sexta recebe `429`. O cookie `Confeitaria.Auth` é HttpOnly, SameSite=Lax, não persistente, com validade de ticket configurada em duas horas. Fora de Development, exige transporte seguro. Logout remove o cookie; não exclui o usuário. Papéis em detalhe: [docs/autenticacao.md](docs/autenticacao.md#papéis).

O cadastro de usuário responde `201` sem cabeçalho `Location`, porque não existe rota pública de consulta de usuário. Para os demais recursos, `Location` aponta para `/<recurso>/{id}`.

## Testar pelo PowerShell

O cookie do navegador não é compartilhado com o PowerShell. Use uma WebSession para guardar o cookie e enviá-lo nas chamadas seguintes. Deixe a API rodando no primeiro terminal; execute esta sequência no segundo.

### Cadastro e login

Crie um e-mail de teste novo para não conflitar com usuários anteriores:

```powershell
$emailTeste = "teste.$([Guid]::NewGuid().ToString('N'))@example.com"
$senhaTeste = 'TesteLocal123!'
$cadastro = @{ nome = 'Usuario de teste'; email = $emailTeste; senha = $senhaTeste } | ConvertTo-Json
Invoke-RestMethod -Uri 'http://localhost:5000/login/cadastro' -Method Post -ContentType 'application/json' -Body $cadastro

$login = @{ email = $emailTeste; senha = $senhaTeste } | ConvertTo-Json
Invoke-RestMethod -Uri 'http://localhost:5000/login/' -Method Post -ContentType 'application/json' -Body $login -SessionVariable sessao
```

Cadastro retorna usuário; login retorna usuário e armazena cookie em `$sessao`. Esses exemplos criam dados reais no banco de teste.

Para os exemplos de Admin (categoria, desativar produto, excluir cliente, cancelar confirmado), use um usuário cujo e-mail esteja em `Admin:Email`. Com um Operador, essas chamadas devolvem `403`.

### Categoria (Admin)

```powershell
$categoria = Invoke-RestMethod -Uri 'http://localhost:5000/categorias/' -Method Post -ContentType 'application/json' -Body (@{ nome = 'Bolos' } | ConvertTo-Json) -WebSession $sessao
$idCategoria = $categoria.id
Invoke-RestMethod -Uri 'http://localhost:5000/categorias?nome=bol' -Method Get -WebSession $sessao
```

Repetir o cadastro com `'bolos'` devolve `409` "Já existe uma categoria com esse nome.".

### Cadastrar, listar e buscar produto

```powershell
$corpo = @{ nome = 'Bolo de chocolate'; preco = 45.90; categoriaId = $idCategoria } | ConvertTo-Json
$produto = Invoke-RestMethod -Uri 'http://localhost:5000/produtos/' -Method Post -ContentType 'application/json' -Body $corpo -WebSession $sessao
$idProduto = $produto.id
$produto

$pagina = Invoke-RestMethod -Uri 'http://localhost:5000/produtos?nome=chocolate&tamanho=10' -Method Get -WebSession $sessao
$pagina.total
$pagina.itens
Invoke-RestMethod -Uri "http://localhost:5000/produtos/$idProduto" -Method Get -WebSession $sessao
```

Use o ID devolvido, não suponha que será `1`. A listagem devolve o envelope paginado: os produtos ficam em `.itens`. GET por ID recebe o ID na URL, sem JSON no corpo.

### Atualizar

```powershell
$alteracao = @{ nome = 'Bolo grande'; preco = 65.90; categoriaId = $idCategoria } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5000/produtos/$idProduto" -Method Put -ContentType 'application/json' -Body $alteracao -WebSession $sessao
```

O `PUT` não recebe `ativo`: se o campo for enviado, é ignorado. Ativar e desativar têm rotas próprias, só de Admin.

### Validar rejeição

```powershell
$invalido = @{ nome = ''; preco = -10; categoriaId = 9999 } | ConvertTo-Json
Invoke-RestMethod -Uri 'http://localhost:5000/produtos/' -Method Post -ContentType 'application/json' -Body $invalido -WebSession $sessao
```

Esperado: erro HTTP 400 com `errors.Nome` e `errors.Preco` (a categoria só é conferida depois que o formato está válido; com nome e preço corretos, viria `errors.CategoriaId`). O PowerShell apresenta respostas 4xx como erros; nesse teste a rejeição é o resultado esperado. Para ver o corpo do erro:

```powershell
try { Invoke-RestMethod -Uri 'http://localhost:5000/produtos/' -Method Post -ContentType 'application/json' -Body $invalido -WebSession $sessao }
catch { $_.ErrorDetails.Message }
```

Paginação fora do limite também é rejeitada: `http://localhost:5000/produtos?tamanho=101` → 400.

### Desativar e reativar (Admin)

```powershell
Invoke-WebRequest -Uri "http://localhost:5000/produtos/$idProduto" -Method Delete -WebSession $sessao -UseBasicParsing | Select-Object StatusCode
(Invoke-RestMethod -Uri "http://localhost:5000/produtos/$idProduto" -Method Get -WebSession $sessao).ativo
Invoke-RestMethod -Uri 'http://localhost:5000/produtos?situacao=Inativos' -Method Get -WebSession $sessao
Invoke-WebRequest -Uri "http://localhost:5000/produtos/$idProduto/reativar" -Method Post -WebSession $sessao -UseBasicParsing | Select-Object StatusCode
```

Esperado: 204, `False`, o produto na lista de inativos, 204. O `DELETE` não apaga a linha: o produto continua existindo para os pedidos antigos. Repetir qualquer um dos dois devolve 204 de novo.

### Cliente

```powershell
$cliente = Invoke-RestMethod -Uri 'http://localhost:5000/clientes/' -Method Post -ContentType 'application/json' -Body (@{ nome = 'Maria Souza'; telefone = '(19) 99999-0000'; email = $null } | ConvertTo-Json) -WebSession $sessao
$cliente.telefone
Invoke-RestMethod -Uri 'http://localhost:5000/clientes?busca=9999' -Method Get -WebSession $sessao
```

Esperado: telefone `19999990000` (só dígitos). E-mail vazio deve ir como `$null`; texto vazio é recusado.

### Pedido

```powershell
$amanha = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
$novoPedido = @{ clienteId = $cliente.id; dataEntrega = $amanha; itens = @(@{ produtoId = $idProduto; quantidade = 2 }) } | ConvertTo-Json -Depth 3
$pedido = Invoke-RestMethod -Uri 'http://localhost:5000/pedidos/' -Method Post -ContentType 'application/json' -Body $novoPedido -WebSession $sessao
$pedido.status
$pedido.total

Invoke-RestMethod -Uri "http://localhost:5000/pedidos/$($pedido.id)/confirmar" -Method Post -WebSession $sessao
Invoke-RestMethod -Uri "http://localhost:5000/pedidos/$($pedido.id)/entregar" -Method Post -WebSession $sessao
Invoke-RestMethod -Uri "http://localhost:5000/pedidos?status=Entregue&de=$amanha" -Method Get -WebSession $sessao
```

Esperado: `Aberto` e o total (2 × preço do produto); depois `Confirmado` e `Entregue`. `-Depth 3` é necessário para o `ConvertTo-Json` não achatar a lista de itens. O pedido não aceita campo de preço. Confirmar de novo devolve `409` "Pedido Entregue não pode ser confirmado.". Agora o cliente tem pedido: `DELETE /clientes/{id}` devolve `409` "Cliente possui pedidos.".

### Logout e acesso bloqueado

```powershell
Invoke-WebRequest -Uri 'http://localhost:5000/login/sair' -Method Post -WebSession $sessao -UseBasicParsing | Select-Object StatusCode
Invoke-RestMethod -Uri 'http://localhost:5000/produtos/' -Method Get -WebSession $sessao
```

Esperado: 204 e depois 401. Para testar persistência, cadastre um produto, reinicie a API, faça login novamente e consulte o ID retornado.

## Migrations

Uma migration é um arquivo com mudanças de estrutura do banco. `Up` aplica as mudanças; `Down` descreve sua reversão. O snapshot registra o modelo conhecido pelo EF. `__EFMigrationsHistory` guarda o histórico aplicado no banco.

| Comando | Função |
| --- | --- |
| `dotnet ef migrations add Nome --project MyCRUD/MyCRUD.csproj` | Gera arquivos locais comparando o modelo ao snapshot |
| `dotnet ef database update --project MyCRUD/MyCRUD.csproj` | Aplica migrations pendentes ao banco |
| `dotnet ef migrations list --project MyCRUD/MyCRUD.csproj` | Consulta as migrations |
| `dotnet ef migrations script --project MyCRUD/MyCRUD.csproj` | Produz SQL das migrations para revisão |

Ao alterar entidades ou mapeamento: salve, gere uma migration com nome descritivo, leia `Up`, aplique e confira no Supabase. Alterações somente de HTML ou lógica de endpoints não exigem migration.

A antiga migration vazia `CriarUsuarios` foi removida (motivo em [ADR-007](docs/decisoes.md#adr-007)). Bancos onde ela já foi aplicada continuam funcionando: o EF ignora a linha extra em `__EFMigrationsHistory`. Fora esse caso documentado, não recrie ou apague migrations já compartilhadas/aplicadas sem coordenar o grupo.

`PadronizarTamanhosEEmail` converte nomes e e-mail para `varchar` com limite e normaliza dados existentes. **Antes de aplicar no banco do grupo**, rode no SQL Editor do Supabase:

```sql
SELECT lower(btrim("Email")), count(*) FROM "Usuarios" GROUP BY 1 HAVING count(*) > 1;
```

Se retornar linhas, há e-mails que só diferem por maiúsculas; resolva antes, ou a migration falha (sem alterar nada, pois roda em transação).

Fase 2 — quatro migrations, aplicadas em ordem (`database update` aplica todas):

| Migration | O que faz | Dados existentes |
| --- | --- | --- |
| `AdicionarPapelUsuario` | Coluna `Papel` (`'Operador'` por padrão) + `CHECK` | Todos viram Operador; o Admin é promovido no 1º login |
| `CriarCategorias` | Tabela `Categorias`, índice único `lower("Nome")`, FK `Produtos.CategoriaId` RESTRICT | Cada `CategoriaId` já usado vira a categoria "Categoria N" com o mesmo Id; renomeie depois pela tela |
| `CriarClientes` | Tabela `Clientes` + `CHECK` de telefone | Tabela nova |
| `CriarPedidos` | Tabelas `Pedidos` e `ItensPedido` + `CHECK`s, FKs e índices | Tabelas novas |

**Antes de aplicar no banco do grupo**, rode e guarde o resultado:

```sql
SELECT "CategoriaId", count(*) FROM "Produtos" GROUP BY 1 ORDER BY 1;  -- categorias que serão criadas
SELECT count(*) FROM "Usuarios";                                        -- todos virarão Operador
```

Essas migrations foram escritas à mão no formato do `dotnet ef` (o ambiente de desenvolvimento delas não tinha .NET). O CI confere o snapshot; se acusar diferença, rode `dotnet ef migrations add Conferencia --project MyCRUD/MyCRUD.csproj`, leia o `Up()` gerado e remova-a com `dotnet ef migrations remove --project MyCRUD/MyCRUD.csproj` depois de corrigir. O índice `lower("Nome")` só existe na migration (o EF não modela índice de expressão): crie bancos de teste com `database update`/`Migrate()`, nunca com `EnsureCreated()`.

O GitHub Actions executa `dotnet ef migrations has-pending-model-changes` a cada push: se alguém alterar uma entidade e esquecer a migration, o build fica vermelho.

No Django, pense em `migrations add` como `makemigrations` e `database update` como `migrate`. Gerar uma migration não altera sozinho o Supabase.

## Resolver problemas

| Sintoma | O que conferir |
| --- | --- |
| Impossível conectar ao servidor | API rodando, `Now listening`, porta e URL corretas |
| DefaultConnection não configurada | Variáveis Development no terminal e User Secrets no projeto certo |
| 401 nas rotas de dados | Faça login; no PowerShell envie `-WebSession $sessao` |
| 403 | A rota é só de Admin: confira `Admin:Email` e saia e entre de novo (o papel vai no cookie) |
| Aviso `Nenhum Admin configurado` | Configure `Admin:Email` nos User Secrets (ou `Admin__Email` em produção) |
| 409 ao excluir categoria/cliente | Categoria com produtos (inclusive inativos) ou cliente com pedidos não podem ser excluídos |
| 400 `DataEntrega` ao criar pedido | A data de entrega não pode ser anterior a hoje (fuso de São Paulo) |
| 404 no cadastro | Endpoint só existe em Development; confira também versão compilada |
| 429 no login ou cadastro | Mais de 5 tentativas no último minuto; aguarde |
| 404 em `/scalar` | Scalar só existe em Development |
| 404 na busca por ID | ID não existe ou rota ainda não está na versão em execução |
| 405 | URL existe, mas o método HTTP não foi registrado nessa versão |
| Executável usado por outro processo | Encerre a instância antiga com Ctrl+C ou pare a depuração no Visual Studio |
| Namespace EF não encontrado | Confira PackageReference e execute `dotnet restore` |
| Não resolve DbContextOptions | Confira `AddDbContext` e `UseNpgsql` antes de `builder.Build()` |
| Migration vazia | Salve o contexto/entidades antes de gerar e revise `Up()` |
| Tabela não apareceu | Execute `database update`, confira projeto/schema public e atualize o painel |
| Falha na conexão PostgreSQL | Confira host, usuário completo do pooler, senha, porta e estado do projeto Supabase |
| Comando não reconhecido ao colar | Remova prompts, saídas e mensagens; cole somente o comando no PowerShell |

Não cole saídas de `dotnet user-secrets list` em mensagens: elas podem conter credenciais. O aviso de bloqueio de `profile.ps1` é separado da API e não impede executar comandos diretos como `Invoke-RestMethod`.

## Tutorial de Git para a Rapaziada

### 1. Entender os termos

| Termo | Significado |
| --- | --- |
| Git | Controle local de versões |
| GitHub | Hospedagem remota e colaboração |
| Working tree | Arquivos que você está editando |
| Stage/index | Mudanças selecionadas para o próximo commit |
| Commit | Registro local das mudanças selecionadas |
| Branch | Linha de trabalho separada |
| Origin | Nome do remoto do repositório |
| Push | Envia commits ao remoto |
| Fetch | Busca referências/commits remotos, sem integrar ao código atual |
| Pull | Busca e integra mudanças na branch atual |
| Pull request (PR) | Proposta de integrar uma branch no GitHub |
| Merge | Integra duas linhas de trabalho |

Salvar no editor não cria commit; commit não envia ao GitHub; push não executa migrations no banco. São ações diferentes.

### 2. Configurar sua identidade

Faça uma vez na máquina, usando seus dados:

```powershell
git config --global user.name 'Seu Nome'
git config --global user.email 'seu-email@example.com'
git config --global --get user.name
git config --global --get user.email
```

Essa identidade aparece nos commits; não é a autenticação da conta GitHub. Para push, use a autenticação oferecida pelo Git Credential Manager/Visual Studio. A senha comum da conta não é usada como senha Git por HTTPS.

### 3. Clonar ou abrir a cópia existente

Quem ainda não tem o projeto usa `git clone` conforme o início deste guia. Quem já tem entra na pasta existente; não precisa clonar a cada sessão.

```powershell
git status
git branch --show-current
git remote -v
```

A branch principal verificada neste repositório é **master**. Use esse nome nos exemplos; se o grupo renomeá-la para main, adapte os comandos.

### 4. Começar uma tarefa em branch

Com mudanças locais já commitadas ou guardadas:

```powershell
git switch master
git pull --ff-only origin master
git switch -c codex/melhorar-cadastro
```

Escolha um nome descritivo para sua tarefa. O prefixo usado nos exemplos é `codex/`. `--ff-only` evita integrar automaticamente históricos divergentes; se falhar, revise a situação com `git status` e `git log`, sem forçar. Veja a [documentação de git pull](https://git-scm.com/docs/git-pull).

Se já editou arquivos na master e ainda não commitou, é possível criar a branch com `git switch -c codex/minha-tarefa`; as alterações acompanham a nova branch. Não faça pull sobre trabalho pendente sem conferir primeiro.

### 5. Editar, revisar e selecionar arquivos

Salve no editor e confira:

```powershell
git status
git diff
dotnet build MyCRUD/MyCRUD.csproj
```

`git diff` mostra mudanças em arquivos já rastreados; arquivos novos aparecem no status e passam a aparecer no diff staged depois do add.

Selecione os arquivos da tarefa, por exemplo:

```powershell
git add README.md
git add MyCRUD/Login/LoginEndpoints.cs MyCRUD/Login/LoginDtos.cs
git diff --staged
git diff --cached --check
```

Para registrar reorganização, migrations e páginas deste projeto, após revisar tudo:

```powershell
git add -A MyCRUD
git add MyCRUD.slnx dotnet-tools.json README.md
```

`-A` inclui arquivos novos, modificados e exclusões dentro do caminho indicado. Confira `git diff --staged` antes de commitar. Não inclua senhas, arquivos de User Secrets ou saídas contendo credenciais. O `.gitignore` cobre bin, obj e .vs; ignore não remove arquivos que já eram rastreados.

### 6. Criar o commit

```powershell
git commit -m 'Implementa cadastro e login por cookie'
git log -5 --oneline
```

Use mensagem que descreva a mudança, como `Corrige validacao de preco` ou `Documenta configuracao do Supabase`. Prefira commits por tarefa, com o projeto compilando.

### 7. Enviar e abrir pull request

No primeiro envio da branch:

```powershell
git push -u origin codex/melhorar-cadastro
```

Nos seguintes:

```powershell
git push
```

No GitHub, abra o repositório, escolha Compare & pull request, confirme destino `master` e origem sua branch. Descreva o que mudou e como testou. Peça revisão de um colega e faça merge quando estiver aprovado. Se não houver permissão para push, o responsável precisa adicionar o integrante como colaborador ou combinar trabalho por fork.

### 8. Depois do merge

Com sua pasta sem trabalho pendente:

```powershell
git switch master
git pull --ff-only origin master
dotnet restore MyCRUD/MyCRUD.csproj
dotnet tool restore
```

Se entraram migrations e o grupo combinou aplicar nesse banco:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef database update --project MyCRUD/MyCRUD.csproj
```

Não gere outra migration só porque recebeu uma pelo Git.

### 9. Atualizar a sua branch durante o trabalho

Com mudanças commitadas, estando na branch da tarefa:

```powershell
git fetch origin
git merge origin/master
```

Isso integra o estado remoto da master na sua branch. Compile/teste e envie novamente. Se aparecer conflito, siga a seção abaixo.

### 10. Resolver conflitos

```powershell
git status
```

Abra os arquivos indicados. Os marcadores `<<<<<<<`, `=======` e `>>>>>>>` separam versões conflitantes. Escolha o conteúdo final correto, preservando as partes necessárias, e remova os marcadores. Não escolha automaticamente uma versão inteira quando ambas têm trabalho útil.

```powershell
git add caminho/do/arquivo
git diff --staged
dotnet build MyCRUD/MyCRUD.csproj
git commit
```

O commit conclui um merge em andamento depois de resolver todos os conflitos. Para cancelar um merge que você acabou de iniciar:

```powershell
git merge --abort
```

Não resolva conflitos de migrations/snapshot sem revisar os modelos e o histórico com o grupo.

### 11. Guardar trabalho temporariamente

```powershell
git stash push -u -m 'Cadastro em andamento'
git stash list
```

O `-u` inclui arquivos novos não rastreados. Para recuperar mantendo uma cópia no stash:

```powershell
git stash apply
```

Pode haver conflitos. Confira os arquivos antes de excluir o stash com `git stash drop`. Stash não é backup enviado ao GitHub.

### 12. Desfazer com cuidado

Retirar um arquivo do stage sem perder suas edições:

```powershell
git restore --staged README.md
```

Descartar edições de um arquivo rastreado, voltando à versão do stage (**perde essas edições**):

```powershell
git restore README.md
```

Desfazer um commit já compartilhado criando um novo commit inverso:

```powershell
git revert HASH_DO_COMMIT
```

Substitua o hash pelo obtido em `git log`. Se houver conflitos, resolva e siga a orientação do Git. Evite `reset --hard`, `clean -fd` e push forçado no trabalho do grupo; eles podem descartar trabalho ou reescrever histórico compartilhado.

### 13. Rotina recomendada

1. Confira status e salve/guarde mudanças pendentes.
2. Atualize master e crie uma branch por tarefa.
3. Edite e salve os arquivos.
4. Compile e teste a funcionalidade.
5. Revise diff, selecione arquivos e faça commit.
6. Envie a branch e abra PR com validação descrita.
7. Após merge, atualize sua master.
8. Aplique migrations recebidas somente quando necessário e coordenado.

Para referência dos comandos, consulte a [documentação oficial do Git](https://git-scm.com/docs).
