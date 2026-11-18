// R4: limites de paginação das listagens de categorias e produtos.

using System.Net;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Catalogo;

[Collection("Api")]
public class PaginacaoCatalogoTests(ApiFactory fabrica)
{
    [Theory]
    [InlineData("/categorias", "tamanho=101", "Tamanho")]
    [InlineData("/categorias", "pagina=0", "Pagina")]
    [InlineData("/categorias", "pagina=1000001", "Pagina")]
    [InlineData("/produtos", "tamanho=101", "Tamanho")]
    [InlineData("/produtos", "pagina=0", "Pagina")]
    [InlineData("/produtos", "pagina=1000001", "Pagina")]
    public async Task R4_PaginacaoForaDosLimites_Retorna400ComErroNoCampo(string rota, string consulta, string campo)
    {
        var operador = await fabrica.OperadorAsync();

        var resposta = await operador.GetAsync($"{rota}?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.NotEmpty(problema.ErrosDoCampo(campo));
    }

    [Fact]
    public async Task R4_ProdutosPaginaAlemDoFim_Retorna200VazioComTotal()
    {
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        await admin.CriarProdutoAsync(categoria.Id);

        var resposta = await admin.GetAsync($"/produtos?categoriaId={categoria.Id}&pagina=5&tamanho=10");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var pagina = await resposta.LerJson<MyCRUD.Compartilhado.ResultadoPaginado<MyCRUD.Produtos.ProdutoRespostaDto>>();
        Assert.Empty(pagina.Itens);
        Assert.Equal(1, pagina.Total);
        Assert.Equal(5, pagina.Pagina);
    }

    [Fact]
    public async Task R4_CategoriasPaginaAlemDoFim_Retorna200VazioComTotal()
    {
        var admin = await fabrica.AdminAsync();
        var sufixo = Cenario.Sufixo();
        await admin.CriarCategoriaAsync($"Paginada {sufixo}");

        var resposta = await admin.GetAsync($"/categorias?nome={sufixo}&pagina=5&tamanho=10");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var pagina = await resposta.LerJson<MyCRUD.Compartilhado.ResultadoPaginado<MyCRUD.Categorias.CategoriaRespostaDto>>();
        Assert.Empty(pagina.Itens);
        Assert.Equal(1, pagina.Total);
        Assert.Equal(5, pagina.Pagina);
    }
}
