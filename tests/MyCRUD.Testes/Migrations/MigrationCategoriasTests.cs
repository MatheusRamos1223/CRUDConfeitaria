// R11: a migration CriarCategorias adota os CategoriaId já usados por produtos (mesmo Id) e
// continua a sequência em MAX(Id)+1. Cada teste usa um banco PRÓPRIO (migração parcial não
// pode vazar para a fábrica compartilhada).

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using MyCRUD.Dados;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Migrations;

public class MigrationCategoriasTests
{
    private const string UltimaAntesDeCategorias = "20261105194500_PadronizarTamanhosEEmail";

    /// <summary>Banco novo, migrado até PadronizarTamanhosEEmail, com produtos de CategoriaId 3, 3 e 9.</summary>
    private static async Task<AppDbContext> PrepararBancoAsync()
    {
        var stringDeConexao = await PostgresCompartilhado.CriarBancoAsync();
        var opcoes = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(stringDeConexao).Options;
        var db = new AppDbContext(opcoes);

        await db.GetService<IMigrator>().MigrateAsync(UltimaAntesDeCategorias);

        // Neste ponto ainda não existe tabela Categorias: Produtos guarda só o número.
        foreach (var categoriaId in new[] { 3, 3, 9 })
        {
            await db.Database.ExecuteSqlRawAsync(
                """INSERT INTO "Produtos" ("Nome", "Preco", "CategoriaId", "Ativo") VALUES ({0}, 10, {1}, true)""",
                $"Produto {Cenario.Sufixo()}", categoriaId);
        }

        return db;
    }

    [Fact]
    public async Task R11_ProdutosComCategoriaId3E9_MigrationCriaCategoriasComEssesIds()
    {
        await using var db = await PrepararBancoAsync();

        // Antes da migration a tabela não existe (prova que o resultado vem dela).
        Assert.Null(await ExisteTabelaAsync(db, "Categorias"));

        await db.Database.MigrateAsync();

        var categorias = await db.Categorias.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
        Assert.Collection(categorias,
            c => { Assert.Equal(3, c.Id); Assert.Equal("Categoria 3", c.Nome); },
            c => { Assert.Equal(9, c.Id); Assert.Equal("Categoria 9", c.Nome); });
    }

    [Fact]
    public async Task R11_AposMigration_ProximaCategoriaRecebeId10()
    {
        await using var db = await PrepararBancoAsync();
        await db.Database.MigrateAsync();

        await using var conexao = new NpgsqlConnection(db.Database.GetConnectionString());
        await conexao.OpenAsync();
        await using var comando = new NpgsqlCommand(
            """INSERT INTO "Categorias" ("Nome") VALUES (@nome) RETURNING "Id" """, conexao);
        comando.Parameters.AddWithValue("nome", $"Nova {Cenario.Sufixo()}");
        var id = (int)(await comando.ExecuteScalarAsync())!;

        Assert.Equal(10, id);
    }

    [Fact]
    public async Task R11_AposMigration_ProdutosContinuamLigadosAsCategoriasAdotadas()
    {
        await using var db = await PrepararBancoAsync();

        await db.Database.MigrateAsync();

        var ids = await db.Produtos.AsNoTracking().Select(p => p.CategoriaId).OrderBy(i => i).ToListAsync();
        Assert.Equal(new[] { 3, 3, 9 }, ids);
        // Os produtos antigos continuam lidos, agora com a FK para Categorias valendo.
        Assert.Equal(0, await db.Produtos.CountAsync(p => !db.Categorias.Any(c => c.Id == p.CategoriaId)));
    }

    private static async Task<object?> ExisteTabelaAsync(AppDbContext db, string nome)
    {
        await using var conexao = new NpgsqlConnection(db.Database.GetConnectionString());
        await conexao.OpenAsync();
        await using var comando = new NpgsqlCommand("SELECT to_regclass(@n)::text", conexao);
        comando.Parameters.AddWithValue("n", $"public.\"{nome}\"");
        var r = await comando.ExecuteScalarAsync();
        return r is DBNull ? null : r;
    }
}
