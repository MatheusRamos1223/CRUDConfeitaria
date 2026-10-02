# CRUD Confeitaria

Projeto acadêmico em C# com ASP.NET Core (.NET 10), Minimal APIs e PostgreSQL hospedado no Supabase. Inclui CRUD de produtos, cadastro de usuários, login por cookie e páginas HTML com JavaScript.

## Sumário

- [Funcionalidades e limites](#funcionalidades-e-limites)
- [Tecnologias e estrutura](#tecnologias-e-estrutura)
- [Configurar e executar](#configurar-e-executar)
- [Usar pelo navegador](#usar-pelo-navegador)
- [Rotas e regras](#rotas-e-regras)
- [Testar pelo PowerShell](#testar-pelo-powershell)
- [Migrations](#migrations)
- [Resolver problemas](#resolver-problemas)
- [Tutorial de Git para o grupo](#tutorial-de-git-para-o-grupo)

## Funcionalidades e limites

Implementado:

- Listar, buscar, cadastrar, atualizar e excluir produtos no banco.
- Validar nome obrigatório e preço maior que zero no cadastro e atualização.
- Cadastrar usuários com nome, e-mail e senha de pelo menos oito caracteres.
- Armazenar hash de senha com `PasswordHasher<Usuario>`.
- Impedir e-mails idênticos duplicados por índice único.
- Autenticar por cookie e exigir autenticação nas rotas de produtos.
- Encerrar a sessão por `POST /login/sair`.
- Cadastrar usuários, fazer login, listar/cadastrar/editar/excluir produtos pelo navegador.
- Manter produtos e usuários após reiniciar a aplicação.

Limites atuais:

- Categorias, clientes e pedidos ainda não possuem entidades, tabelas ou CRUD.
- `CategoriaId` é um número no produto, sem relação com uma tabela de categorias.
- A edição de produtos usa o mesmo formulário de cadastro, incluindo a opção Ativo.
- O botão Sair na página de produtos encerra a sessão e volta ao login.
- As páginas HTML são públicas; os dados e operações de produtos exigem login.
- O cadastro de usuários só é registrado em `Development`.
- Não há perfis de administrador ou permissões diferentes: qualquer usuário autenticado acessa o CRUD.
- O e-mail recebe `Trim()`, mas não é normalizado para minúsculas. Use a mesma capitalização no cadastro e no login.
- Não há recuperação de senha, confirmação de e-mail, paginação ou testes automatizados no repositório.
- É uma aplicação de estudo local; proteção antiforgery/CSRF e configuração de implantação ainda não foram implementadas.

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

Os pacotes EF Design e Npgsql estão referenciados como `10.*`; o restore resolve uma versão compatível com esse intervalo. O manifesto da ferramenta tem versão específica.

```text
CRUDConfeitaria/
├── .gitignore
├── MyCRUD.slnx
├── dotnet-tools.json
├── README.md
└── MyCRUD/
    ├── MyCRUD.csproj
    ├── Program.cs
    ├── Dados/
    │   └── AppDbContext.cs
    ├── Produtos/
    │   ├── Produto.cs
    │   ├── ProdutoDtos.cs
    │   └── ProdutoEndpoints.cs
    ├── Login/
    │   ├── Usuario.cs
    │   ├── LoginDtos.cs
    │   └── LoginEndpoints.cs
    ├── Migrations/
    │   ├── ...CriarProdutos.cs e .Designer.cs
    │   ├── ...CriarUsuarios.cs e .Designer.cs
    │   ├── ...AdicionarTabelaUsuarios.cs e .Designer.cs
    │   └── AppDbContextModelSnapshot.cs
    └── wwwroot/
        ├── login.html
        ├── cadastro.html
        └── produtos.html
```

`Program.cs` registra banco, autenticação e autorização, habilita arquivos estáticos e chama os métodos de registro dos endpoints. `AppDbContext` expõe `Produtos` e `Usuarios` e configura preço `numeric(10,2)` e índice único de e-mail. As entidades representam os registros; os DTOs descrevem entrada/saída. Os endpoints de produtos atualmente retornam a entidade, embora exista `ProdutoRespostaDto`.

O SDK inclui os arquivos `.cs` automaticamente; não é necessário listá-los no `.csproj` nem criar um equivalente ao `__init__.py`. `bin/` e `obj/` são gerados e ignorados pelo Git.

Para quem vem de Python: NuGet cumpre o papel de gerenciador de pacotes; DTOs lembram schemas do FastAPI; EF Core lembra o ORM do Django. Um `using` importa um namespace, mas não instala o pacote. A injeção de dependência entrega o `AppDbContext` ao parâmetro da rota. `async`/`await` permite aguardar operações de banco sem bloquear a thread durante a espera.

## Configurar e executar

Todos os comandos abaixo são para **PowerShell**, executados na raiz do repositório. Copie apenas o comando, sem o prefixo `PS C:\...>` nem mensagens de saída.

### 1. Pré-requisitos

Instale o SDK do .NET 10, Git e um editor (Visual Studio ou VS Code). Tenha acesso a um projeto Supabase e à senha de seu banco.

```powershell
dotnet --list-sdks
git --version
```

É necessário o SDK, não somente o runtime. Ter apenas .NET 8 não atende ao target `net10.0`.

### 2. Clonar e restaurar

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

Se a senha tiver `;` ou outros caracteres especiais da string de conexão, o valor precisa ser corretamente delimitado conforme o formato Npgsql. Uma aspa simples dentro do comando PowerShell deve ser duplicada. Não cole comandos preenchidos com credenciais em commits, prints ou mensagens.

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

## Usar pelo navegador

| Endereço | Uso |
| --- | --- |
| [Página inicial](http://localhost:5000/) | Mensagem de confirmação da API |
| [Cadastro](http://localhost:5000/cadastro.html) | Criar usuário, com API em Development |
| [Login](http://localhost:5000/login.html) | Entrar e receber cookie |
| [Produtos](http://localhost:5000/produtos.html) | Listar, cadastrar, editar e excluir produtos |
| [JSON de produtos](http://localhost:5000/produtos/) | Consultar lista, após login nesse navegador |

Fluxo:

1. Abra cadastro, informe nome, e-mail e senha de oito ou mais caracteres.
2. Após a confirmação, volte ao login e entre com os mesmos dados.
3. O login redireciona para produtos.
4. Informe nome, preço e ID numérico de categoria e clique em Cadastrar.
5. Confira o registro na tabela e no Table Editor do Supabase, schema `public`.
6. Clique em Editar para preencher o formulário, altere os campos e clique em Salvar alterações. Cancelar edição volta ao cadastro sem gravar mudanças.
7. Use Atualizar lista para consultar novamente e Excluir para remover após confirmação.

A interface usa HTML e JavaScript sem framework de frontend. Cadastro e produtos não têm CSS; o arquivo de login atualmente ainda contém um bloco de CSS inline. A página de produtos permite editar nome, preço, categoria e estado ativo. O botão Sair chama o logout e redireciona para o login.

As páginas e a API são servidas pelo mesmo ASP.NET. Abra pelos endereços HTTP, não diretamente como arquivos do computador.

## Rotas e regras

| Método | Caminho | Autenticação | Resultado principal |
| --- | --- | --- | --- |
| GET | `/` | Pública | 200 e mensagem |
| POST | `/login/cadastro` | Pública, apenas Development | 201 e usuário sem senha/hash |
| POST | `/login/` | Pública | 200 e cookie; 401 para credenciais inválidas |
| POST | `/login/sair` | Exige cookie | 204 |
| GET | `/produtos/` | Exige cookie | 200 e lista |
| GET | `/produtos/{id:int}` | Exige cookie | 200 ou 404 |
| POST | `/produtos/` | Exige cookie | 201 ou 400 |
| PUT | `/produtos/{id:int}` | Exige cookie | 200, 400 ou 404 |
| DELETE | `/produtos/{id:int}` | Exige cookie | 204 ou 404 |

### Dados

Produto: `Id` inteiro gerado pelo banco, `Nome` string, `Preco` decimal, `CategoriaId` inteiro e `Ativo` booleano. O cadastro define `Ativo = true`; a atualização aceita esse campo. Produtos inativos também aparecem na listagem. Nome vazio/branco e preço menor ou igual a zero são rejeitados. A API ainda não valida existência ou positividade de `CategoriaId`.

Usuário: `Id`, `Nome`, `Email`, `SenhaHash`. O cadastro exige campos preenchidos e senha de oito caracteres, remove espaços externos de nome/e-mail, verifica duplicidade e salva o hash. Não há validação completa de formato de e-mail no backend. A resposta expõe somente ID, nome e e-mail.

Login verifica o hash, atualizando-o se necessário, e cria claims de ID, nome e e-mail. O cookie `Confeitaria.Auth` é HttpOnly, SameSite=Lax, não persistente, com validade de ticket configurada em duas horas. Fora de Development, exige transporte seguro. Logout remove o cookie; não exclui o usuário.

O `Location` do cadastro de usuário aponta para `/login/usuarios/{id}`, mas não existe rota de consulta de usuário nesse endereço. Para produtos, existe a rota correspondente.

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

### Cadastrar, listar e buscar produto

```powershell
$corpo = @{ nome = 'Bolo de chocolate'; preco = 45.90; categoriaId = 1 } | ConvertTo-Json
$produto = Invoke-RestMethod -Uri 'http://localhost:5000/produtos/' -Method Post -ContentType 'application/json' -Body $corpo -WebSession $sessao
$idProduto = $produto.id
$produto

Invoke-RestMethod -Uri 'http://localhost:5000/produtos/' -Method Get -WebSession $sessao
Invoke-RestMethod -Uri "http://localhost:5000/produtos/$idProduto" -Method Get -WebSession $sessao
```

Use o ID devolvido, não suponha que será `1`. IDs excluídos não são necessariamente reutilizados. GET por ID recebe o ID na URL, sem JSON no corpo.

### Atualizar

```powershell
$alteracao = @{ nome = 'Bolo grande'; preco = 65.90; categoriaId = 1; ativo = $true } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5000/produtos/$idProduto" -Method Put -ContentType 'application/json' -Body $alteracao -WebSession $sessao
```

### Validar rejeição

```powershell
$invalido = @{ nome = ''; preco = -10; categoriaId = 1 } | ConvertTo-Json
Invoke-RestMethod -Uri 'http://localhost:5000/produtos/' -Method Post -ContentType 'application/json' -Body $invalido -WebSession $sessao
```

Esperado: erro HTTP 400. O PowerShell apresenta respostas 4xx como erros; nesse teste a rejeição é o resultado esperado.

### Excluir e confirmar

```powershell
Invoke-WebRequest -Uri "http://localhost:5000/produtos/$idProduto" -Method Delete -WebSession $sessao -UseBasicParsing | Select-Object StatusCode
Invoke-RestMethod -Uri "http://localhost:5000/produtos/$idProduto" -Method Get -WebSession $sessao
```

Esperado: 204 na exclusão e 404 na busca posterior.

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

No histórico atual, `CriarUsuarios` é vazia: foi gerada antes de o contexto atualizado ser salvo. A criação da tabela foi incluída depois em `AdicionarTabelaUsuarios`. Mantenha esse histórico; não recrie ou apague migrations já compartilhadas/aplicadas sem coordenar o grupo.

No Django, pense em `migrations add` como `makemigrations` e `database update` como `migrate`. Gerar uma migration não altera sozinho o Supabase.

## Resolver problemas

| Sintoma | O que conferir |
| --- | --- |
| Impossível conectar ao servidor | API rodando, `Now listening`, porta e URL corretas |
| DefaultConnection não configurada | Variáveis Development no terminal e User Secrets no projeto certo |
| 401 em produtos | Faça login; no PowerShell envie `-WebSession $sessao` |
| 404 no cadastro | Endpoint só existe em Development; confira também versão compilada |
| 404 na busca de produto | ID não existe ou rota ainda não está na versão em execução |
| 405 | URL existe, mas o método HTTP não foi registrado nessa versão |
| Executável usado por outro processo | Encerre a instância antiga com Ctrl+C ou pare a depuração no Visual Studio |
| Namespace EF não encontrado | Confira PackageReference e execute `dotnet restore` |
| Não resolve DbContextOptions | Confira `AddDbContext` e `UseNpgsql` antes de `builder.Build()` |
| Migration vazia | Salve o contexto/entidades antes de gerar e revise `Up()` |
| Tabela não apareceu | Execute `database update`, confira projeto/schema public e atualize o painel |
| Falha na conexão PostgreSQL | Confira host, usuário completo do pooler, senha, porta e estado do projeto Supabase |
| Comando não reconhecido ao colar | Remova prompts, saídas e mensagens; cole somente o comando no PowerShell |

Não cole saídas de `dotnet user-secrets list` em mensagens: elas podem conter credenciais. O aviso de bloqueio de `profile.ps1` é separado da API e não impede executar comandos diretos como `Invoke-RestMethod`.

## Tutorial de Git para o grupo

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
