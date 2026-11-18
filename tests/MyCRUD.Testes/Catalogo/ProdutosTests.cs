// Testes de produtos: permissões (R2), categoria inexistente (R12), soft delete (R14) e filtro (R5).

using System.Net;
using System.Net.Http.Json;
using MyCRUD.Produtos;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Catalogo;

[Collection("Api")]
public class ProdutosTests(ApiFactory fabrica)
{
    [Fact]
    public async Task R2_OperadorDesativaProduto_Retorna403EProdutoContinuaAtivo()
    {
        var operador = await fabrica.OperadorAsync();
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        var produto = await admin.CriarProdutoAsync(categoria.Id);

        var resposta = await operador.DesativarProdutoAsync(produto.Id);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.True((await admin.BuscarProdutoAsync(produto.Id)).Ativo);
    }

    [Fact]
    public async Task R2_OperadorReativaProduto_Retorna403EProdutoContinuaInativo()
    {
        var operador = await fabrica.OperadorAsync();
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        var produto = await admin.CriarProdutoAsync(categoria.Id);
        await admin.DesativarProdutoAsync(produto.Id);

        var resposta = await operador.ReativarProdutoAsync(produto.Id);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.False((await admin.BuscarProdutoAsync(produto.Id)).Ativo);
    }

    [Fact]
    public async Task R12_ProdutoComCategoriaInexistente_Retorna400ComErroEmCategoriaId()
    {
        var admin = await fabrica.AdminAsync();
        var nome = $"Produto {Cenario.Sufixo()}";

        // int.MaxValue passa pela validação de formato (Range 1..int.MaxValue) e falha só na checagem no banco.
        var resposta = await admin.PostAsJsonAsync(
            "/produtos", new CriarProdutoDto(nome, 10m, int.MaxValue), Cenario.Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.Contains("Categoria não encontrada.", problema.ErrosDoCampo("CategoriaId"));
        var busca = await admin.ListarProdutosAsync($"?nome={Uri.EscapeDataString(nome)}&situacao=Todos");
        Assert.Equal(0, busca.Total);
    }

    [Fact]
    public async Task R14_DesativarProduto_Retorna204EAparecePorInativoEmSituacaoTodos()
    {
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        var produto = await admin.CriarProdutoAsync(categoria.Id);

        var resposta = await admin.DesativarProdutoAsync(produto.Id);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var todos = await admin.ListarProdutosAsync($"?categoriaId={categoria.Id}&situacao=Todos");
        var naLista = Assert.Single(todos.Itens, p => p.Id == produto.Id);
        Assert.False(naLista.Ativo);
        var ativos = await admin.ListarProdutosAsync($"?categoriaId={categoria.Id}");
        Assert.DoesNotContain(ativos.Itens, p => p.Id == produto.Id);
    }

    [Fact]
    public async Task R14_DesativarProdutoJaInativo_Retorna204DeNovo()
    {
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        var produto = await admin.CriarProdutoAsync(categoria.Id);
        await admin.DesativarProdutoAsync(produto.Id);

        var repetida = await admin.DesativarProdutoAsync(produto.Id);

        Assert.Equal(HttpStatusCode.NoContent, repetida.StatusCode);
        Assert.False((await admin.BuscarProdutoAsync(produto.Id)).Ativo);
    }

    [Fact]
    public async Task R14_ReativarProdutoInativo_VoltaAtivoEAparecePorPadrao()
    {
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        var produto = await admin.CriarProdutoAsync(categoria.Id);
        await admin.DesativarProdutoAsync(produto.Id);

        var resposta = await admin.ReativarProdutoAsync(produto.Id);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.True((await admin.BuscarProdutoAsync(produto.Id)).Ativo);
        var ativos = await admin.ListarProdutosAsync($"?categoriaId={categoria.Id}");
        Assert.Contains(ativos.Itens, p => p.Id == produto.Id);
    }

    [Fact]
    public async Task R5_FiltroDeProdutoComPorcento_NaoEhCuringa()
    {
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        var sufixo = Cenario.Sufixo();
        var comPorcento = await admin.CriarProdutoAsync(categoria.Id, nome: $"Torta 50% {sufixo}");
        var semPorcento = await admin.CriarProdutoAsync(categoria.Id, nome: $"Torta 500 {sufixo}");

        // Sem escape, o padrão "%50% <sufixo>%" também casaria com "Torta 500 <sufixo>".
        var busca = await admin.ListarProdutosAsync(
            $"?categoriaId={categoria.Id}&nome={Uri.EscapeDataString($"50% {sufixo}")}");

        Assert.Contains(busca.Itens, p => p.Id == comPorcento.Id);
        Assert.DoesNotContain(busca.Itens, p => p.Id == semPorcento.Id);
    }
}
