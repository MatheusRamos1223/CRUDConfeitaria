// Testes de clientes: R18 (telefone) e R20 (exclusão com pedidos).

using System.Net;
using System.Net.Http.Json;
using MyCRUD.Clientes;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Vendas;

[Collection("Api")]
public class ClientesTests(ApiFactory fabrica)
{
    [Fact]
    public async Task R18_TelefoneComMascara_SalvoSoComDigitos()
    {
        var operador = await fabrica.OperadorAsync();

        var cliente = await operador.CriarClienteAsync(telefone: "(19) 99999-0000");

        Assert.Equal("19999990000", cliente.Telefone);
        var lido = await (await operador.GetAsync($"/clientes/{cliente.Id}")).LerJson<ClienteRespostaDto>();
        Assert.Equal("19999990000", lido.Telefone);
    }

    [Fact]
    public async Task R18_TelefoneCurto_Retorna400ComErroEmTelefone()
    {
        var operador = await fabrica.OperadorAsync();

        var resposta = await operador.PostAsJsonAsync(
            "/clientes", new CriarClienteDto($"Cliente {Cenario.Sufixo()}", "123", null), Cenario.Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.NotNull(problema.Errors);
        Assert.True(problema.Errors.ContainsKey("Telefone"), "Esperava erro na chave Telefone.");
    }

    [Fact]
    public async Task R20_ExcluirClienteComPedido_Retorna409()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        await admin.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);

        var resposta = await admin.DeleteAsync($"/clientes/{cliente.Id}");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        // Título da checagem do endpoint (e não o "Registro em uso." genérico da FK).
        Assert.Equal("Cliente possui pedidos.", (await resposta.LerProblem()).Title);
        // O cliente continua existindo.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/clientes/{cliente.Id}")).StatusCode);
    }
}
