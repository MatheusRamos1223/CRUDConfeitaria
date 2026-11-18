# Verificação estática da Fase 2 (sem compilador)

O ambiente da integração não tem .NET/NuGet: nada aqui foi compilado. Este registro lista o que foi
conferido à mão, arquivo por arquivo, o que foi corrigido e o que **só o CI confirma**.

Data: 2026-11-17 · branch `fase-2` · merges de `a5`, `a2`, `a3`, `a4` sem conflito.

## Método

Para cada `.cs` (exceto migrations antigas):

1. `using` necessários para cada tipo usado (lembrando os *implicit usings* do SDK Web:
   `System`, `System.Linq`, `System.Collections.Generic`, `Microsoft.AspNetCore.Http`,
   `Microsoft.AspNetCore.Builder`, `Microsoft.Extensions.DependencyInjection`, `...Logging`, `...Configuration`, `...Hosting`).
2. Namespace do arquivo × pasta.
3. Nomes cruzados entre agentes: cada `Tipo.Membro` usado foi procurado na declaração (script de `grep`
   sobre todos os `.cs`; único "faltante" foi `Papel.ToString`, falso positivo).
4. Aridade e ordem dos construtores de records/DTOs × cada `new XDto(...)`.
5. `Results<...>` declarado × cada `return` (incluindo os ternários tipados pelo alvo, C# 9+).
6. Nulabilidade (`string?`, `int?`, `T?` em `FirstOrDefaultAsync`, `!` em navegações).
7. Métodos de extensão e assinaturas do EF Core 10 / Npgsql 10 / ASP.NET Core 10.
8. Conflitos de nome: membro igual ao tipo que o contém (CS0542), namespace × tipo, tipos duplicados.

## Resultado por arquivo

| Arquivo | Conferido | Observação |
| --- | --- | --- |
| `Program.cs` | usings `MyCRUD.Categorias/Clientes/Pedidos`; `AddPoliticasDeAcesso`; `Map*Endpoints` existem e são `this WebApplication` | ok |
| `Dados/AppDbContext.cs` | DbSets; `ToTable(Action<TableBuilder<T>>)`; `HasConversion<string>()`; `HasDefaultValue(Papel.Operador)`; `HasCheckConstraint`; `HasOne/WithMany/HasForeignKey/OnDelete` | dentro da classe, `Pedido`, `Cliente`, `Categoria`, `Papel` não colidem com membros (`Pedidos`, `Clientes`... são plurais) |
| `Compartilhado/ConsultaTexto.cs` | novo; `const string` usada dentro de expressão LINQ (vira constante) | substitui 4 cópias privadas |
| `Compartilhado/Politicas.cs` | `AddAuthorizationBuilder().AddPolicy(string, Action<AuthorizationPolicyBuilder>)`; `RequireRole(params string[])` | ok |
| `Compartilhado/TratadorGlobalDeExcecoes.cs` | `switch` sobre tupla `(Exception, string?)`; padrão constante `PostgresErrorCodes.UniqueViolation/ForeignKeyViolation` (`const string`) | ok |
| `Compartilhado/Paginacao.cs`, `ValidacaoFiltro.cs`, `LimiteDeRequisicoes.cs` | sem mudança | — |
| `Login/Papel.cs`, `Usuario.cs` | `Papel Papel` (padrão "Color Color", permitido); cref qualificado `MyCRUD.Login.Papel.Operador` dentro de `Usuario` | ok |
| `Login/LoginDtos.cs` | `UsuarioRespostaDto(int, string, string, Papel)` × `Usuario.ParaResposta()` e `UsuarioAtual` (4 args) | ok |
| `Login/LoginEndpoints.cs` | `app.Logger`; `ILoggerFactory.CreateLogger(Type)` com `typeof(LoginEndpoints)` (classe estática: permitido); `Enum.TryParse<Papel>(string?, out)` | ok |
| `Produtos/Produto.cs`, `ProdutoDtos.cs` | `ParaResposta(string)` × 2 chamadas; `ProdutoRespostaDto` 6 args; `EnumDataType` | ok |
| `Produtos/ProdutoEndpoints.cs` | `Expression<Func<Produto, ProdutoRespostaDto>>` em `Select`; `ExecuteUpdateAsync(s => s.SetProperty(p => p.Ativo, ativo), ct)`; argumento nomeado não final `ativo:` na posição correta (C# 7.2+) | helper de LIKE removido |
| `Categorias/*` | `NomeEmUsoAsync(db, nome, idIgnorado: null, ct)` (nomeado na posição certa); subconsulta `db.Produtos.Count(...)` dentro do `Select`; `int?` comparado com `> 0` | escape agora explícito no `ILike` |
| `Clientes/*` | `TelefoneBrasileiroAttribute : ValidationAttribute` (ctor `protected` da base acessível); `char.IsAsciiDigit` (.NET 7+); `db.Pedidos.AnyAsync(p => p.ClienteId == id)` resolve após A4 | `FindAsync([id], ct)` → `FindAsync(new object[] { id }, ct)` (evita depender da resolução de *collection expression* em `params`) |
| `Pedidos/StatusPedido.cs`, `Pedido.cs`, `ItemPedido.cs` | `static readonly TimeZoneInfo` (EF ignora estáticos); `List<ItemPedido> Itens = []` | ok |
| `Pedidos/PedidoDtos.cs` | `IValidatableObject` em record com corpo; `ValidationResult(string, IEnumerable<string>)` com `[caminho]`; `De is { } de` | ok |
| `Pedidos/PedidoEndpoints.cs` | `PedidoResumoDto` 7 args, `PedidoDetalheDto` 9 args, `ItemPedidoRespostaDto` 5 args; `ToDictionaryAsync(k, v, ct)`; ternário `ehAdmin ? StatusPedido.Confirmado : null` tipado pelo alvo `StatusPedido?`; lambda `Func<StatusPedido, ProblemHttpResult?>`; `x?.Invoke(a) ?? TypedResults.Problem(...)` → conversão implícita para `Results<...>`; `TypedResults.Created(string, PedidoDetalheDto?)` infere `Created<PedidoDetalheDto>` | ok |
| `Migrations/2026111812*` + snapshot | ver seção própria | escritas à mão |

## Conflitos de nome

- **Tipos duplicados:** nenhum (45 tipos, todos com nome único no projeto).
- **Tipo com nome de namespace:** nenhum (`Categorias`, `Clientes`, `Compartilhado`, `Dados`, `Login`,
  `Migrations`, `Pedidos`, `Produtos` só existem como namespace). Os DbSets plurais (`db.Pedidos`) são
  sempre acessados como membro (`db.`), então não disputam com o namespace `MyCRUD.Pedidos`.
- **CS0542** (membro com o nome do tipo que o contém): nenhum. `Usuario.Papel`, `Pedido.Cliente`,
  `ItemPedido.Produto`, `Produto.Categoria` têm o nome do **tipo da propriedade**, não da classe — permitido.

## Correções feitas na integração

| Arquivo | Correção |
| --- | --- |
| `ProdutoEndpoints`, `CategoriaEndpoints`, `ClienteEndpoints`, `PedidoEndpoints` | Helper de escape do `ILIKE` deduplicado em `Compartilhado/ConsultaTexto.cs` (`PadraoContem`, `CaractereDeEscape`). Categorias passou a informar o escape explicitamente. |
| `ClienteEndpoints` | `FindAsync([id], ct)` → `FindAsync(new object[] { id }, ct)`, igual a Produtos. |
| `AppDbContext` (trecho do A3) | `HasDatabaseName("IX_Clientes_Nome")` removido: é o nome padrão do EF; o snapshot fica igual ao que o `dotnet ef` gera. |

## Migrations e snapshot

| # | Arquivo | Conteúdo |
| --- | --- | --- |
| M1 | `20261118120000_AdicionarPapelUsuario` | `AddColumn Papel varchar(20) NOT NULL DEFAULT 'Operador'` + `CK_Usuarios_Papel` |
| M2 | `20261118120100_CriarCategorias` | `CreateTable Categorias` → SQL manual (INSERT "Categoria N", `setval` com `GREATEST`, índice `lower("Nome")`) → `IX_Produtos_CategoriaId` → FK RESTRICT |
| M3 | `20261118120200_CriarClientes` | `CreateTable Clientes` + `CK_Clientes_Telefone` + `IX_Clientes_Nome` |
| M4 | `20261118120300_CriarPedidos` | `Pedidos` (+CHECK, FK RESTRICT) → `ItensPedido` (+2 CHECKs, FK CASCADE/RESTRICT) → 4 índices |

Conferido contra o modelo (`OnModelCreating` + convenções):

- Tipos: `integer` identity, `character varying(n)`, `numeric(10,2)`, `date` (`DateOnly`),
  `timestamp with time zone` (`DateTime`), `boolean`, `text` (`SenhaHash`).
- Enums: `b.Property<string>(...)` com `IsRequired()` e `HasMaxLength(20)` (tipo de provedor após `HasConversion<string>`).
- Default: `Papel` com `.ValueGeneratedOnAdd()` + `.HasDefaultValue("Operador")` (valor já convertido).
- CHECKs no `ToTable(nome, t => ...)`, ordenados por nome como o gerador faz.
- Índices: `IX_Produtos_CategoriaId`, `IX_Clientes_Nome`, `IX_Pedidos_ClienteId`, `IX_Pedidos_DataEntrega`,
  `IX_ItensPedido_ProdutoId`, `IX_ItensPedido_PedidoId_ProdutoId` (único; cobre a FK `PedidoId`, sem índice separado).
- FKs: `Produto→Categoria` Restrict, `Pedido→Cliente` Restrict, `ItemPedido→Pedido` Cascade
  (`HasOne(..., null).WithMany("Itens")`), `ItemPedido→Produto` Restrict; navegações `Categoria`, `Cliente`,
  `Produto` no bloco do dependente e `Itens` no bloco final do principal.
- **Índice de expressão** `lower("Nome")`: só na M2 via SQL. O modelo **não** declara `HasIndex` em
  `Categoria.Nome` (conferido), então não há índice concorrente nem diferença no `has-pending-model-changes`.
- Cada `.Designer.cs` contém o modelo **daquele ponto** (M1: sem Categoria; M2: + Categoria e FK; M3: + Cliente; M4: tudo).
- `ProductVersion` mantido em `10.0.12` (igual às migrations anteriores e ao `dotnet-tools.json`).

## Riscos restantes (para o CI)

Em ordem de probabilidade de quebrar:

1. **`has-pending-model-changes`** — snapshot escrito à mão. Se divergir, o dono roda
   `dotnet ef migrations add Verificacao --project MyCRUD` local, compara o `Up()` gerado (deve ser vazio) e descarta (Q4).
2. **Possível aviso do EF sobre `HasDefaultValue` em `Papel`** (o valor padrão do C#, Operador = 0, é igual ao
   DEFAULT do banco): é log de *warning* em tempo de execução, não erro de build. O comportamento é o desejado
   (o EF omite a coluna no INSERT e o banco grava 'Operador').
3. **`[AsParameters]` + `ValidacaoFiltro<T>`**: o filtro procura o argumento pelo tipo
   (`context.Arguments.OfType<T>()`). Espera-se que o objeto `[AsParameters]` apareça como um argumento só;
   é comportamento de execução, confirmar no 1º teste de `?tamanho=101` → 400 (Fase 3).
4. **`ExecuteUpdateAsync(s => s.SetProperty(...))`** com a assinatura nova do EF Core 10
   (`Action<UpdateSettersBuilder<T>>`): a lambda de expressão é aceita como `Action`. Baixo risco.
5. **`EF.Functions.ILike(texto, padrao, escape)`** — overload de 3 argumentos do Npgsql. Baixo risco.
6. **Avisos de documentação XML** (`cref` não resolvido) aparecem como *warning* (`GenerateDocumentationFile`); não quebram o build.

## Front-end (Playwright, API simulada)

Script fora do repositório: Chromium 1280 px, `page.route` servindo `wwwroot/` e uma API em memória que
segue o contrato 2.3/2.4 (e acusa erro se o front mandar `ativo` no PUT, `precoUnitario` no pedido ou e-mail vazio).

| Verificação | Admin | Operador |
| --- | --- | --- |
| Menu com 4 links, todos existentes; `body[data-papel]` | ok | ok |
| As 5 telas carregam `/css/app.css`, `/js/api.js` → `/js/layout.js` | ok | ok |
| Produtos: lista paginada (só ativos), editar, cadastrar, Desativar (Admin) / oculto (Operador), filtro Inativos → Reativar | ok | ok |
| Categorias: cadastrar, 409 de duplicado na tela (Admin); campos e botões ocultos + aviso (Operador) | ok | ok |
| Clientes: cadastrar com máscara e e-mail vazio → `null`; Excluir (Admin) / oculto (Operador) | ok | ok |
| Pedidos: total estimado, criar → abre `pedido-detalhe.html?id=N` | ok | ok |
| Detalhe: Confirmar → Entregar; "Cancelar" de Confirmado só para Admin; Operador cancela Aberto | ok | ok |
| `mensagemDeErro` com 403 → "Você não tem permissão para esta ação." | ok | ok |
| **Erros de console / exceções de página** | **0** | **0** |

O único log de console do Admin é o "Failed to load resource: 409" do teste de duplicado, provocado de propósito.
