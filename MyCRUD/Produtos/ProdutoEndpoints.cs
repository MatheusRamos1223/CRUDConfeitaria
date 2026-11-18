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
/// Os retornos usam <c>TypedResults</c> e o tipo <c>Results&lt;...&gt;</c>.
/// Isso faz o compilador conferir os status possíveis e permite que o OpenAPI
/// documente automaticamente cada resposta (200, 201, 404...).
/// </para>
/// </remarks>
public static class ProdutoEndpoints
{
    public static RouteGroupBuilder MapProdutoEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/produtos")
            .WithTags("Produtos")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/", Listar)
            .WithName("ListarProdutos")
            .WithSummary("Lista todos os produtos, ativos e inativos, ordenados por ID.");

        grupo.MapGet("/{id:int}", BuscarPorId)
            .WithName("BuscarProduto")
            .WithSummary("Busca um produto pelo ID.");

        grupo.MapPost("/", Criar)
            .WithName("CriarProduto")
            .WithSummary("Cadastra um produto. Todo produto novo nasce ativo.")
            .ComValidacao<CriarProdutoDto>();

        grupo.MapPut("/{id:int}", Atualizar)
            .WithName("AtualizarProduto")
            .WithSummary("Substitui todos os dados de um produto existente.")
            .ComValidacao<AtualizarProdutoDto>();

        grupo.MapDelete("/{id:int}", Excluir)
            .WithName("ExcluirProduto")
            .WithSummary("Exclui permanentemente um produto.");

        return grupo;
    }

    private static async Task<Ok<List<ProdutoRespostaDto>>> Listar(AppDbContext db)
    {
        // AsNoTracking: leitura pura, o EF não precisa monitorar alterações.
        // Select: o banco devolve só as colunas do DTO, já no formato final.
        var produtos = await db.Produtos
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Select(p => new ProdutoRespostaDto(p.Id, p.Nome, p.Preco, p.CategoriaId, p.Ativo))
            .ToListAsync();

        return TypedResults.Ok(produtos);
    }

    private static async Task<Results<Ok<ProdutoRespostaDto>, NotFound>> BuscarPorId(
        int id, AppDbContext db)
    {
        var produto = await db.Produtos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        return produto is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(produto.ParaResposta());
    }

    private static async Task<Created<ProdutoRespostaDto>> Criar(
        CriarProdutoDto dto, AppDbContext db)
    {
        // Chegando aqui, o ValidacaoFiltro já garantiu que o DTO é válido.
        var produto = new Produto
        {
            Nome = dto.Nome.Trim(),
            Preco = dto.Preco,
            CategoriaId = dto.CategoriaId,
            Ativo = true
        };

        db.Produtos.Add(produto);
        await db.SaveChangesAsync(); // gera o INSERT e preenche produto.Id

        return TypedResults.Created($"/produtos/{produto.Id}", produto.ParaResposta());
    }

    private static async Task<Results<Ok<ProdutoRespostaDto>, NotFound>> Atualizar(
        int id, AtualizarProdutoDto dto, AppDbContext db)
    {
        // FindAsync carrega a entidade COM rastreamento: o EF detecta o que mudou
        // e gera um UPDATE apenas das colunas alteradas no SaveChanges.
        var produto = await db.Produtos.FindAsync(id);

        if (produto is null)
        {
            return TypedResults.NotFound();
        }

        produto.Nome = dto.Nome.Trim();
        produto.Preco = dto.Preco;
        produto.CategoriaId = dto.CategoriaId;
        produto.Ativo = dto.Ativo;

        await db.SaveChangesAsync();

        return TypedResults.Ok(produto.ParaResposta());
    }

    private static async Task<Results<NoContent, NotFound>> Excluir(int id, AppDbContext db)
    {
        // ExecuteDeleteAsync gera um único DELETE ... WHERE "Id" = @id,
        // sem precisar buscar o registro antes. Retorna quantas linhas apagou.
        var removidos = await db.Produtos
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync();

        return removidos == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
    }
}
