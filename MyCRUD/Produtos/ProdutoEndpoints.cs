namespace MyCRUD.Produtos;

using Microsoft.EntityFrameworkCore;
using MyCRUD.Dados;

public static class ProdutoEndpoints
{

    public static RouteGroupBuilder MapProdutoEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/produtos")
            .RequireAuthorization(); // Exige autenticação para todos os endpoints do grupo
        grupo.MapGet("/", async (AppDbContext db) =>
        {
            var lista = await db.Produtos
                .AsNoTracking()
                .ToListAsync();

            return Results.Ok(lista);
        });

        grupo.MapPost("/", async (CriarProdutoDto dto, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Preco <= 0)
            {
                return Results.BadRequest(
                    "Informe um nome e um preço maior que zero.");
            }

            var produto = new Produto
            {
                Nome = dto.Nome,
                Preco = dto.Preco,
                CategoriaId = dto.CategoriaId,
                Ativo = true
            };

            db.Produtos.Add(produto);
            await db.SaveChangesAsync();

            return Results.Created($"/produtos/{produto.Id}", produto);
        });

        grupo.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var produto = await db.Produtos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (produto is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(produto);
        });

        grupo.MapPut("/{id:int}", async (
            int id, AtualizarProdutoDto dto, AppDbContext db) =>
        {
            var produto = await db.Produtos.FindAsync(id);

            if (produto is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Preco <= 0)
            {
                return Results.BadRequest(
                    "Informe um nome e um preço maior que zero.");
            }

            produto.Nome = dto.Nome;
            produto.Preco = dto.Preco;
            produto.CategoriaId = dto.CategoriaId;
            produto.Ativo = dto.Ativo;

            await db.SaveChangesAsync();

            return Results.Ok(produto);
        });

        grupo.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var produto = await db.Produtos.FindAsync(id);

            if (produto is null)
            {
                return Results.NotFound();
            }

            db.Produtos.Remove(produto);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return grupo;
    }
}
