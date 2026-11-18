using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Compartilhado;
using MyCRUD.Dados;

namespace MyCRUD.Produtos;

/// <summary>
/// Rotas HTTP do CRUD de produtos, agrupadas em <c>/produtos</c>.
/// </summary>
/// <remarks>
/// <para>Todas as rotas exigem usuário autenticado (cookie de login).</para>
/// <para>
/// Desativar e reativar exigem, além disso, o papel Admin
/// (<see cref="Politicas.SomenteAdmin"/>): um Operador recebe <c>403 Forbidden</c>.
/// </para>
/// <para>
/// <b>Soft delete</b>: nenhum produto é apagado do banco. <c>DELETE</c> só marca
/// <c>Ativo = false</c>. Assim os pedidos antigos continuam apontando para um
/// produto que existe, e o Admin pode desfazer com <c>POST /produtos/{id}/reativar</c>.
/// </para>
/// <para>
/// Os retornos usam <c>TypedResults</c> e o tipo <c>Results&lt;...&gt;</c>.
/// Isso faz o compilador conferir os status possíveis e permite que o OpenAPI
/// documente automaticamente cada resposta (200, 201, 404...).
/// </para>
/// </remarks>
public static class ProdutoEndpoints
{
    /// <summary>
    /// Projeção entidade → DTO usada nas consultas.
    /// </summary>
    /// <remarks>
    /// Por ser uma <see cref="Expression{TDelegate}"/> (e não um método comum), o EF consegue
    /// traduzi-la para SQL: <c>p.Categoria!.Nome</c> vira um <c>JOIN</c> com <c>Categorias</c>
    /// e o banco devolve só as colunas do DTO. O <c>!</c> apenas avisa o compilador;
    /// a FK obrigatória garante que toda linha tem categoria.
    /// </remarks>
    private static readonly Expression<Func<Produto, ProdutoRespostaDto>> ParaRespostaDto =
        p => new ProdutoRespostaDto(p.Id, p.Nome, p.Preco, p.CategoriaId, p.Categoria!.Nome, p.Ativo);

    public static RouteGroupBuilder MapProdutoEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/produtos")
            .WithTags("Produtos")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/", Listar)
            .WithName("ListarProdutos")
            .WithSummary("Lista produtos paginados, com filtro por nome, categoria e situação (padrão: só ativos). Ordem: ID.")
            .ComValidacao<FiltroProdutos>();

        grupo.MapGet("/{id:int}", BuscarPorId)
            .WithName("BuscarProduto")
            .WithSummary("Busca um produto pelo ID (ativo ou inativo).");

        grupo.MapPost("/", Criar)
            .WithName("CriarProduto")
            .WithSummary("Cadastra um produto em uma categoria existente. Todo produto novo nasce ativo.")
            .ComValidacao<CriarProdutoDto>();

        grupo.MapPut("/{id:int}", Atualizar)
            .WithName("AtualizarProduto")
            .WithSummary("Altera nome, preço e categoria. Não muda a situação (ativo/inativo).")
            .ComValidacao<AtualizarProdutoDto>();

        grupo.MapDelete("/{id:int}", Desativar)
            .WithName("DesativarProduto")
            .WithSummary("Desativa o produto (soft delete). Apenas Admin. Repetir não dá erro.")
            .RequireAuthorization(Politicas.SomenteAdmin)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        grupo.MapPost("/{id:int}/reativar", Reativar)
            .WithName("ReativarProduto")
            .WithSummary("Reativa um produto desativado. Apenas Admin. Repetir não dá erro.")
            .RequireAuthorization(Politicas.SomenteAdmin)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return grupo;
    }

    private static async Task<Ok<ResultadoPaginado<ProdutoRespostaDto>>> Listar(
        [AsParameters] FiltroProdutos filtro, AppDbContext db, CancellationToken ct)
    {
        // A consulta é montada aos poucos: cada Where só entra se o filtro foi informado.
        // Nada vai ao banco até o PaginarAsync (execução adiada do LINQ).
        // AsNoTracking: leitura pura, o EF não precisa monitorar alterações.
        var consulta = db.Produtos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            // ILIKE = LIKE sem diferenciar maiúsculas (PostgreSQL). R5: os curingas
            // digitados (%, _) são escapados por ConsultaTexto e buscados literalmente.
            var padrao = ConsultaTexto.PadraoContem(filtro.Nome);
            consulta = consulta.Where(p => EF.Functions.ILike(p.Nome, padrao, ConsultaTexto.CaractereDeEscape));
        }

        if (filtro.CategoriaId is int categoriaId)
        {
            consulta = consulta.Where(p => p.CategoriaId == categoriaId);
        }

        consulta = filtro.Situacao switch
        {
            SituacaoProduto.Ativos => consulta.Where(p => p.Ativo),
            SituacaoProduto.Inativos => consulta.Where(p => !p.Ativo),
            _ => consulta // Todos
        };

        // Ordem estável (Id é único): a paginação não repete nem pula registros.
        var pagina = await consulta
            .OrderBy(p => p.Id)
            .Select(ParaRespostaDto)
            .PaginarAsync(filtro.Pagina, filtro.Tamanho, ct);

        return TypedResults.Ok(pagina);
    }

    private static async Task<Results<Ok<ProdutoRespostaDto>, NotFound>> BuscarPorId(
        int id, AppDbContext db, CancellationToken ct)
    {
        var produto = await db.Produtos
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ParaRespostaDto)
            .FirstOrDefaultAsync(ct);

        return produto is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(produto);
    }

    private static async Task<Results<Created<ProdutoRespostaDto>, ValidationProblem>> Criar(
        CriarProdutoDto dto, AppDbContext db, CancellationToken ct)
    {
        // Chegando aqui, o ValidacaoFiltro já garantiu o formato do DTO.
        // Falta a regra que depende do banco: a categoria precisa existir.
        var categoriaNome = await BuscarNomeDaCategoriaAsync(db, dto.CategoriaId, ct);

        if (categoriaNome is null)
        {
            return CategoriaNaoEncontrada();
        }

        var produto = new Produto
        {
            Nome = dto.Nome.Trim(),
            Preco = dto.Preco,
            CategoriaId = dto.CategoriaId,
            Ativo = true
        };

        db.Produtos.Add(produto);
        await db.SaveChangesAsync(ct); // gera o INSERT e preenche produto.Id

        return TypedResults.Created($"/produtos/{produto.Id}", produto.ParaResposta(categoriaNome));
    }

    private static async Task<Results<Ok<ProdutoRespostaDto>, NotFound, ValidationProblem>> Atualizar(
        int id, AtualizarProdutoDto dto, AppDbContext db, CancellationToken ct)
    {
        // FindAsync carrega a entidade COM rastreamento: o EF detecta o que mudou
        // e gera um UPDATE apenas das colunas alteradas no SaveChanges.
        var produto = await db.Produtos.FindAsync(new object[] { id }, ct);

        if (produto is null)
        {
            return TypedResults.NotFound();
        }

        var categoriaNome = await BuscarNomeDaCategoriaAsync(db, dto.CategoriaId, ct);

        if (categoriaNome is null)
        {
            return CategoriaNaoEncontrada();
        }

        // Ativo NÃO é alterado aqui: só pelas rotas de Admin (desativar/reativar).
        produto.Nome = dto.Nome.Trim();
        produto.Preco = dto.Preco;
        produto.CategoriaId = dto.CategoriaId;

        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(produto.ParaResposta(categoriaNome));
    }

    /// <summary>Soft delete: marca o produto como inativo, sem apagar a linha.</summary>
    private static Task<Results<NoContent, NotFound>> Desativar(int id, AppDbContext db, CancellationToken ct) =>
        DefinirSituacaoAsync(id, ativo: false, db, ct);

    /// <summary>Desfaz o soft delete.</summary>
    private static Task<Results<NoContent, NotFound>> Reativar(int id, AppDbContext db, CancellationToken ct) =>
        DefinirSituacaoAsync(id, ativo: true, db, ct);

    /// <summary>
    /// Grava <c>Ativo</c> com um único <c>UPDATE ... WHERE "Id" = @id</c>,
    /// sem buscar o registro antes.
    /// </summary>
    /// <remarks>
    /// Idempotente: desativar um produto já inativo continua devolvendo 204, porque o
    /// PostgreSQL conta a linha encontrada mesmo que o valor não mude. 0 linhas = id inexistente.
    /// </remarks>
    private static async Task<Results<NoContent, NotFound>> DefinirSituacaoAsync(
        int id, bool ativo, AppDbContext db, CancellationToken ct)
    {
        var alterados = await db.Produtos
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(definir => definir.SetProperty(p => p.Ativo, ativo), ct);

        return alterados == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
    }

    /// <summary>Nome da categoria, ou <c>null</c> se ela não existir.</summary>
    private static Task<string?> BuscarNomeDaCategoriaAsync(AppDbContext db, int categoriaId, CancellationToken ct) =>
        db.Categorias
            .Where(c => c.Id == categoriaId)
            .Select(c => (string?)c.Nome)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// 400 no mesmo formato do <c>ValidacaoFiltro</c>, com o erro na chave do campo,
    /// para a tela mostrar a mensagem junto dos outros erros de validação.
    /// </summary>
    private static ValidationProblem CategoriaNaoEncontrada() =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [nameof(CriarProdutoDto.CategoriaId)] = ["Categoria não encontrada."]
            },
            title: "Dados inválidos.");
}
