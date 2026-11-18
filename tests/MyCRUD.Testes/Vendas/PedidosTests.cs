// Testes de pedidos: criação (R21–R26, R39), preço congelado (R23), transições (R28–R30) e auditoria (R40).

using System.Net;
using System.Net.Http.Json;
using MyCRUD.Pedidos;
using MyCRUD.Produtos;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Vendas;

[Collection("Api")]
public class PedidosTests(ApiFactory fabrica)
{
    // ------------------------------------------------------------------
    // Criação
    // ------------------------------------------------------------------

    [Fact]
    public async Task R24_PedidoValido_Retorna201AbertoComTotalIgualASomaDosItens()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var p1 = await admin.CriarProdutoNovoAsync(10.50m);
        var p2 = await admin.CriarProdutoNovoAsync(4m);

        var resposta = await admin.PostarPedidoAsync(
            cliente.Id, Cenario.HojeSp().AddDays(3), (p1.Id, 2), (p2.Id, 3));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var pedido = await resposta.LerJson<PedidoDetalheDto>();
        Assert.Equal(StatusPedido.Aberto, pedido.Status);
        Assert.Equal(2, pedido.Itens.Count);
        // 2 x 10,50 + 3 x 4,00 = 33,00
        Assert.Equal(33m, pedido.Total);
        Assert.Equal(33m, pedido.Itens.Sum(i => i.Subtotal));
    }

    [Fact]
    public async Task R25_TotalDoGet_EhSomaDeQuantidadeVezesPrecoDosItens()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var p1 = await admin.CriarProdutoNovoAsync(7.25m);
        var p2 = await admin.CriarProdutoNovoAsync(12m);
        var criado = await admin.CriarPedidoAsync(cliente.Id, [(p1.Id, 4), (p2.Id, 1)]);

        var lido = await (await admin.GetAsync($"/pedidos/{criado.Id}")).LerJson<PedidoDetalheDto>();

        // 4 x 7,25 + 1 x 12,00 = 41,00
        Assert.Equal(41m, lido.Total);
    }

    [Fact]
    public async Task R26_PedidoRecemCriado_TemStatusAlteradoEmNulo()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();

        var pedido = await admin.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);

        Assert.Equal(StatusPedido.Aberto, pedido.Status);
        Assert.Null(pedido.StatusAlteradoEm);
        Assert.Null(pedido.StatusAlteradoPorNome);
    }

    [Fact]
    public async Task R22_PedidoComProdutoInativo_Retorna400ComErroEmItens()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/produtos/{produto.Id}")).StatusCode);

        var resposta = await admin.PostarPedidoAsync(cliente.Id, Cenario.HojeSp().AddDays(1), (produto.Id, 1));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.NotNull(problema.Errors);
        Assert.True(problema.Errors.ContainsKey("Itens"), "Esperava erro na chave Itens.");
    }

    [Fact]
    public async Task R22_PedidoComProdutoRepetido_Retorna400()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();

        var resposta = await admin.PostarPedidoAsync(
            cliente.Id, Cenario.HojeSp().AddDays(1), (produto.Id, 1), (produto.Id, 2));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.NotNull(problema.Errors);
        Assert.Contains(problema.Errors.Keys, k => k.StartsWith("Itens[1]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task R21_PedidoComQuantidadeZero_Retorna400ComErroNaQuantidade()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();

        var resposta = await admin.PostarPedidoAsync(cliente.Id, Cenario.HojeSp().AddDays(1), (produto.Id, 0));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.NotNull(problema.Errors);
        Assert.True(problema.Errors.ContainsKey("Itens[0].Quantidade"), "Esperava erro em Itens[0].Quantidade.");
    }

    [Fact]
    public async Task R39_EntregaEm366Dias_Retorna400ComErroEmDataEntrega()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();

        var resposta = await admin.PostarPedidoAsync(cliente.Id, Cenario.HojeSp().AddDays(366), (produto.Id, 1));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.NotNull(problema.Errors);
        Assert.True(problema.Errors.ContainsKey("DataEntrega"), "Esperava erro na chave DataEntrega.");
    }

    [Fact]
    public async Task R39_EntregaEm365Dias_Retorna201()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();

        var resposta = await admin.PostarPedidoAsync(cliente.Id, Cenario.HojeSp().AddDays(365), (produto.Id, 1));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    // ------------------------------------------------------------------
    // Preço congelado
    // ------------------------------------------------------------------

    [Fact]
    public async Task R23_AlterarPrecoDoProdutoDepois_NaoMudaTotalNemPrecoDoPedido()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync(10m);
        var pedido = await admin.CriarPedidoAsync(cliente.Id, [(produto.Id, 3)]);
        Assert.Equal(30m, pedido.Total);

        var put = await admin.PutAsJsonAsync(
            $"/produtos/{produto.Id}",
            new AtualizarProdutoDto(produto.Nome, 99m, produto.CategoriaId),
            Cenario.Json);
        Assert.Equal(99m, (await put.LerJson<ProdutoRespostaDto>()).Preco);

        var lido = await (await admin.GetAsync($"/pedidos/{pedido.Id}")).LerJson<PedidoDetalheDto>();
        Assert.Equal(30m, lido.Total);
        Assert.Equal(10m, lido.Itens.Single().PrecoUnitario);
    }

    // ------------------------------------------------------------------
    // Transições
    // ------------------------------------------------------------------

    [Fact]
    public async Task R28_EntregarPedidoAberto_Retorna409()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        var pedido = await admin.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);

        var resposta = await admin.TransicionarPedidoAsync(pedido.Id, "entregar");

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var lido = await (await admin.GetAsync($"/pedidos/{pedido.Id}")).LerJson<PedidoDetalheDto>();
        Assert.Equal(StatusPedido.Aberto, lido.Status);
    }

    [Fact]
    public async Task R29_ConfirmarDuasVezes_PrimeiraRetorna200ESegundaRetorna409()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        var pedido = await admin.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);

        var primeira = await admin.TransicionarPedidoAsync(pedido.Id, "confirmar");
        var segunda = await admin.TransicionarPedidoAsync(pedido.Id, "confirmar");

        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        Assert.Equal(StatusPedido.Confirmado, (await primeira.LerJson<PedidoDetalheDto>()).Status);
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task R30_OperadorCancelaPedidoAberto_Retorna200Cancelado()
    {
        var operador = await fabrica.OperadorAsync();
        var cliente = await operador.CriarClienteAsync();
        var admin = await fabrica.AdminAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        var pedido = await operador.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);

        var resposta = await operador.TransicionarPedidoAsync(pedido.Id, "cancelar");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(StatusPedido.Cancelado, (await resposta.LerJson<PedidoDetalheDto>()).Status);
    }

    [Fact]
    public async Task R30_OperadorCancelaPedidoConfirmado_Retorna403EPedidoSegueConfirmado()
    {
        var operador = await fabrica.OperadorAsync();
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        var pedido = await admin.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);
        await admin.TransicionarPedidoAsync(pedido.Id, "confirmar");

        var resposta = await operador.TransicionarPedidoAsync(pedido.Id, "cancelar");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var lido = await (await admin.GetAsync($"/pedidos/{pedido.Id}")).LerJson<PedidoDetalheDto>();
        Assert.Equal(StatusPedido.Confirmado, lido.Status);
    }

    [Fact]
    public async Task R30_AdminCancelaPedidoConfirmado_Retorna200Cancelado()
    {
        var admin = await fabrica.AdminAsync();
        var cliente = await admin.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        var pedido = await admin.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);
        await admin.TransicionarPedidoAsync(pedido.Id, "confirmar");

        var resposta = await admin.TransicionarPedidoAsync(pedido.Id, "cancelar");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(StatusPedido.Cancelado, (await resposta.LerJson<PedidoDetalheDto>()).Status);
    }

    // ------------------------------------------------------------------
    // Auditoria
    // ------------------------------------------------------------------

    [Fact]
    public async Task R40_CriadoPorOperadorConfirmadoPorAdmin_GuardaNomesDeCadaUm()
    {
        var operador = await fabrica.OperadorAsync();
        var admin = await fabrica.AdminAsync();
        var nomeOperador = await operador.NomeLogadoAsync();
        var nomeAdmin = await admin.NomeLogadoAsync();
        Assert.NotEqual(nomeOperador, nomeAdmin);

        var cliente = await operador.CriarClienteAsync();
        var produto = await admin.CriarProdutoNovoAsync();
        var criado = await operador.CriarPedidoAsync(cliente.Id, [(produto.Id, 1)]);
        Assert.Equal(nomeOperador, criado.CriadoPorNome);
        Assert.Null(criado.StatusAlteradoPorNome);

        var confirmado = await (await admin.TransicionarPedidoAsync(criado.Id, "confirmar")).LerJson<PedidoDetalheDto>();

        Assert.Equal(nomeOperador, confirmado.CriadoPorNome);
        Assert.Equal(nomeAdmin, confirmado.StatusAlteradoPorNome);
        Assert.NotNull(confirmado.StatusAlteradoEm);
    }
}
