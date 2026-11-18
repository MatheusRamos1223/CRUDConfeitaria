// Testes de categorias: permissões (R10), nome único sem diferenciar maiúsculas (R8),
// exclusão com produtos (R9) e filtro sem curingas (R5).

using System.Net;
using System.Net.Http.Json;
using MyCRUD.Categorias;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Catalogo;

[Collection("Api")]
public class CategoriasTests(ApiFactory fabrica)
{
    [Fact]
    public async Task R10_OperadorCriaCategoria_Retorna403ENaoCria()
    {
        var operador = await fabrica.OperadorAsync();
        var admin = await fabrica.AdminAsync();
        var nome = $"Cat Op {Cenario.Sufixo()}";

        var resposta = await operador.PostAsJsonAsync("/categorias", new CriarCategoriaDto(nome), Cenario.Json);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var busca = await admin.ListarCategoriasAsync($"?nome={Uri.EscapeDataString(nome)}");
        Assert.Equal(0, busca.Total);
    }

    [Fact]
    public async Task R10_OperadorRenomeiaCategoria_Retorna403ENomeNaoMuda()
    {
        var operador = await fabrica.OperadorAsync();
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();

        var resposta = await operador.PutAsJsonAsync(
            $"/categorias/{categoria.Id}",
            new AtualizarCategoriaDto($"Outro {Cenario.Sufixo()}"),
            Cenario.Json);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var depois = await (await admin.GetAsync($"/categorias/{categoria.Id}")).LerJson<CategoriaRespostaDto>();
        Assert.Equal(categoria.Nome, depois.Nome);
    }

    [Fact]
    public async Task R10_OperadorExcluiCategoria_Retorna403ECategoriaContinuaExistindo()
    {
        var operador = await fabrica.OperadorAsync();
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();

        var resposta = await operador.DeleteAsync($"/categorias/{categoria.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var existente = await admin.GetAsync($"/categorias/{categoria.Id}");
        Assert.Equal(HttpStatusCode.OK, existente.StatusCode);
    }

    [Fact]
    public async Task R8_NomeIgualSoNasMaiusculas_Retorna409()
    {
        var admin = await fabrica.AdminAsync();
        var sufixo = Cenario.Sufixo();
        await admin.CriarCategoriaAsync($"Bolos {sufixo}");

        var resposta = await admin.PostAsJsonAsync(
            "/categorias", new CriarCategoriaDto($"bolos {sufixo}"), Cenario.Json);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.Equal("Já existe uma categoria com esse nome.", problema.Title);
    }

    [Fact]
    public async Task R9_CategoriaSoComProdutoInativo_Retorna409()
    {
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();
        var produto = await admin.CriarProdutoAsync(categoria.Id);
        var desativacao = await admin.DesativarProdutoAsync(produto.Id);
        Assert.Equal(HttpStatusCode.NoContent, desativacao.StatusCode);

        var resposta = await admin.DeleteAsync($"/categorias/{categoria.Id}");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.Equal("Categoria possui 1 produto(s).", problema.Title);
        var existente = await admin.GetAsync($"/categorias/{categoria.Id}");
        Assert.Equal(HttpStatusCode.OK, existente.StatusCode);
    }

    [Fact]
    public async Task R9_CategoriaSemProdutos_ExcluiCom204()
    {
        var admin = await fabrica.AdminAsync();
        var categoria = await admin.CriarCategoriaAsync();

        var resposta = await admin.DeleteAsync($"/categorias/{categoria.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var depois = await admin.GetAsync($"/categorias/{categoria.Id}");
        Assert.Equal(HttpStatusCode.NotFound, depois.StatusCode);
    }

    [Fact]
    public async Task R5_FiltroDeCategoriaComPorcento_NaoEhCuringa()
    {
        var admin = await fabrica.AdminAsync();
        var sufixo = Cenario.Sufixo();
        var comPorcento = await admin.CriarCategoriaAsync($"Doce 50% {sufixo}");
        var semPorcento = await admin.CriarCategoriaAsync($"Doce 500 {sufixo}");

        // Sem escape, o padrão "%50% <sufixo>%" também casaria com "Doce 500 <sufixo>".
        var busca = await admin.ListarCategoriasAsync($"?nome={Uri.EscapeDataString($"50% {sufixo}")}");

        Assert.Contains(busca.Itens, c => c.Id == comPorcento.Id);
        Assert.DoesNotContain(busca.Itens, c => c.Id == semPorcento.Id);
    }
}
