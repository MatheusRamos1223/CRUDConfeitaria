# Revisão dos testes da Fase 3

Revisão estática (sem .NET/Docker no ambiente): compilação conferida contra o código de `MyCRUD/`
e validade de cada teste (passaria com a regra quebrada? depende de ordem, relógio ou paralelismo?).

## Achados e correções

| # | Tipo | Achado | Correção |
|---|------|--------|----------|
| 1 | Instabilidade | `PostgresCompartilhado` fazia `CREATE DATABASE` fora da trava. As coleções rodam em paralelo (Api, 2× Development, Limite5, Migrations): `CREATE DATABASE` simultâneos copiando o `template1` podem falhar. Aviso de nulo em `_container` depois do `finally`. | `CREATE DATABASE` dentro da trava; string base guardada em variável local (`Infra/ApiFactory.cs`). |
| 2 | Teste frágil | R43 lia `Content-Security-Policy` só em `resposta.Headers`; o `HttpClient` pode pôr o cabeçalho em `Content.Headers`, e aí o teste falharia mesmo com o cabeçalho certo. | `Valor()` procura nas duas coleções (`Seguranca/CabecalhosDeSegurancaTests.cs`). |
| 3 | Assert fraco | R42 com `"  ADMIN@Teste.DEV "` fazia `email.Trim()` antes de enviar: a normalização de espaços pela API nunca era exercitada. | E-mail enviado como veio (`[EmailAddress]` aceita espaços nas pontas) (`Seguranca/CadastroDoAdminTests.cs`). |
| 4 | Instabilidade (relógio) | R39 (365/366 dias): se a meia-noite de São Paulo virar entre o `HojeSp()` do teste e o "hoje" do servidor, a fronteira anda um dia e o 366 vira 201. | `PostarComEntregaEmAsync`: confere o dia antes e depois e repete uma vez se mudou (`Vendas/PedidosTests.cs`). |
| 5 | Assert fraco | R20 só conferia o 409: passaria também se a checagem do endpoint sumisse e o 409 viesse do tratador global da FK ("Registro em uso."). | Confere o título `"Cliente possui pedidos."` (`Vendas/ClientesTests.cs`). |
| 6 | CI | Job `testes` sem `timeout-minutes`: um contêiner que não sobe prenderia o runner até 6 h. | `timeout-minutes: 20` (`.github/workflows/build.yml`). |

## Conferido e correto (sem mudança)

- **Visibilidade:** todos os tipos de `MyCRUD` usados nos testes são `public`; `Program` tem `public partial class Program`. `InternalsVisibleTo` não é necessário.
- **Nomes:** `MyCRUD.Testes.Migrations` não colide com `MyCRUD.Migrations` (nenhum arquivo usa o nome simples `Migrations`); nomes qualificados `MyCRUD.Compartilhado.*` resolvem para o namespace global.
- **DTOs:** records posicionais, nomes e tipos de propriedade conferidos com `*Dtos.cs` (inclui `ResultadoPaginado<T>` com `TotalPaginas` só leitura, ignorado na desserialização). Chaves de erro (`Telefone`, `Itens[0].Quantidade`, `Itens[1].ProdutoId`, `DataEntrega`, `CategoriaId`, `Pagina`, `Tamanho`) e títulos de ProblemDetails conferidos com os endpoints.
- **xUnit 2:** `IAsyncLifetime` com `Task` implementado explicitamente (não colide com `ValueTask DisposeAsync` do `WebApplicationFactory`); `ICollectionFixture<ApiFactory>` + `IClassFixture<ApiFactoryDevelopment/Limite5>` com construtor público sem parâmetros; o xUnit 2 chama `IAsyncLifetime.DisposeAsync` e depois `Dispose` (que descarta o host).
- **WebApplicationFactory:** `UseSetting`/`UseEnvironment` chegam ao `builder.Configuration` do `Program.cs` (viram argumentos de linha de comando). O filtro de `--seed-demo` do `Program.cs` só retira esse argumento. `WithWebHostBuilder` (SessaoTests) reaproveita o `ConfigureWebHost` da fábrica pai, logo usa o mesmo banco. Fábricas em paralelo são seguras: o `HostFactoryResolver` filtra eventos por `AsyncLocal`.
- **Relógio (R35):** `PostConfigure<CookieAuthenticationOptions>` define `TimeProvider`, o mesmo relógio que o handler do cookie e `ValidacaoDeSessao` usam. Só as fábricas derivadas usam o relógio falso; a fábrica compartilhada não é afetada.
- **Estado compartilhado na coleção "Api":** nenhum teste faz logout, rebaixa ou apaga o Admin/Operador compartilhados; R35 cria usuários próprios. Dados com sufixo único; sem contagens globais. Logins na fábrica compartilhada: bem abaixo do limite de 1000/min.
- **Paralelismo:** cada fixture tem banco próprio no mesmo contêiner; o limitador de R47 é por host (fábrica própria com limite 5, teste único).
- **Testcontainers 4:** `new PostgreSqlBuilder("postgres:17-alpine")` existe a partir da 4.10 (`4.*` resolve a mais nova da série 4). **Npgsql:** `PostgresException.SqlState`/`ConstraintName`. **EF:** `GetService<IMigrator>().MigrateAsync(alvo)`, `ExecuteUpdateAsync(s => s.SetProperty(...))` compatível com o `Action<UpdateSettersBuilder<T>>` do EF 10.
- **Seed (`SeedDemo`):** só roda com `--seed-demo`; não cria o usuário de `Admin:Email` nos bancos de teste (R42 continua válido).

## Riscos que restam para o CI

- Nada foi compilado: primeira compilação real é no CI.
- R43 Scalar supõe a rota `/scalar/v1` (Scalar.AspNetCore 2.x serve `/scalar` e `/scalar/{documento}`).
- Data Protection: hosts paralelos do mesmo processo dividem o diretório de chaves do usuário; nenhum teste usa cookie de um host em outro, então não deve afetar.
- `PostgreSqlBuilder(string)` exige Testcontainers ≥ 4.10; se o feed travar numa 4.x anterior, trocar por `new PostgreSqlBuilder().WithImage(...)`.
