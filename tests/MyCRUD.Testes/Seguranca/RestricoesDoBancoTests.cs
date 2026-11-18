// R45: o banco recusa, por CHECK, o que a API já recusava (SQL manual, seeds, outros clientes).
// R38: RLS ligado em todas as tabelas.

using Npgsql;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Seguranca;

[Collection("Api")]
public class RestricoesDoBancoTests(ApiFactory fabrica)
{
    private const string InserirProduto =
        """INSERT INTO "Produtos" ("Nome", "Preco", "CategoriaId", "Ativo") VALUES (@nome, @preco, @categoria, true)""";

    private const string InserirCliente =
        """INSERT INTO "Clientes" ("Nome", "Telefone", "Email") VALUES (@nome, '19999990000', @email)""";

    /// <summary>Categoria real (a FK de Produtos exige): assim só o CHECK pode recusar o INSERT.</summary>
    private async Task<int> CriarCategoriaAsync() =>
        (int)(await fabrica.EscalarAsync(
            """INSERT INTO "Categorias" ("Nome") VALUES (@nome) RETURNING "Id" """,
            new NpgsqlParameter("nome", $"Cat {Cenario.Sufixo()}")))!;

    [Fact]
    public async Task R45_ProdutoComPrecoZero_ViolaCkProdutosPreco()
    {
        var categoria = await CriarCategoriaAsync();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => fabrica.ExecutarAsync(
            InserirProduto,
            new NpgsqlParameter("nome", $"Produto {Cenario.Sufixo()}"),
            new NpgsqlParameter("preco", 0m),
            new NpgsqlParameter("categoria", categoria)));

        Assert.Equal("23514", erro.SqlState);
        Assert.Equal("CK_Produtos_Preco", erro.ConstraintName);
    }

    [Fact]
    public async Task R45_ProdutoComPrecoPositivo_EAceito()
    {
        var categoria = await CriarCategoriaAsync();

        var linhas = await fabrica.ExecutarAsync(
            InserirProduto,
            new NpgsqlParameter("nome", $"Produto {Cenario.Sufixo()}"),
            new NpgsqlParameter("preco", 0.01m),
            new NpgsqlParameter("categoria", categoria));

        Assert.Equal(1, linhas);
    }

    [Fact]
    public async Task R45_ClienteComEmailMaiusculo_ViolaCkClientesEmail()
    {
        var erro = await Assert.ThrowsAsync<PostgresException>(() => fabrica.ExecutarAsync(
            InserirCliente,
            new NpgsqlParameter("nome", $"Cliente {Cenario.Sufixo()}"),
            new NpgsqlParameter("email", $"MAIUSCULO{Cenario.Sufixo()}@TESTE.DEV")));

        Assert.Equal("23514", erro.SqlState);
        Assert.Equal("CK_Clientes_Email", erro.ConstraintName);
    }

    [Fact]
    public async Task R45_ClienteComEmailMinusculo_EAceito()
    {
        var linhas = await fabrica.ExecutarAsync(
            InserirCliente,
            new NpgsqlParameter("nome", $"Cliente {Cenario.Sufixo()}"),
            new NpgsqlParameter("email", $"minusculo{Cenario.Sufixo()}@teste.dev"));

        Assert.Equal(1, linhas);
    }

    [Theory]
    [InlineData("Produtos")]
    [InlineData("Usuarios")]
    [InlineData("Categorias")]
    [InlineData("Clientes")]
    [InlineData("Pedidos")]
    [InlineData("ItensPedido")]
    [InlineData("__EFMigrationsHistory")]
    public async Task R38_Tabela_TemRlsLigado(string tabela)
    {
        var ligado = await fabrica.EscalarAsync(
            """
            SELECT c.relrowsecurity
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname = @tabela
            """,
            new NpgsqlParameter("tabela", tabela));

        Assert.NotNull(ligado); // a tabela existe
        Assert.True((bool)ligado!, $"RLS desligado em {tabela}.");
    }
}
