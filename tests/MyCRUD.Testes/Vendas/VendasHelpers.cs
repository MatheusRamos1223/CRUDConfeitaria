// Helpers extras dos testes de vendas (clientes e pedidos).

using System.Net.Http.Json;
using MyCRUD.Login;
using MyCRUD.Pedidos;
using MyCRUD.Produtos;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Vendas;

internal static class VendasHelpers
{
    /// <summary>Cria categoria nova + produto ativo (como Admin) com o preço informado.</summary>
    public static async Task<ProdutoRespostaDto> CriarProdutoNovoAsync(this HttpClient admin, decimal preco = 10m)
    {
        var categoria = await admin.CriarCategoriaAsync();
        return await admin.CriarProdutoAsync(categoria.Id, preco);
    }

    /// <summary>Nome do usuário logado neste cliente (GET /login/eu).</summary>
    public static async Task<string> NomeLogadoAsync(this HttpClient http)
    {
        var eu = await (await http.GetAsync("/login/eu")).LerJson<UsuarioRespostaDto>();
        return eu.Nome;
    }

    /// <summary>POST /pedidos com o corpo montado à mão; devolve a resposta crua (para testar 400).</summary>
    public static Task<HttpResponseMessage> PostarPedidoAsync(
        this HttpClient http, int clienteId, DateOnly dataEntrega, params (int ProdutoId, int Quantidade)[] itens) =>
        http.PostAsJsonAsync(
            "/pedidos",
            new CriarPedidoDto(
                clienteId,
                dataEntrega,
                itens.Select(i => new CriarItemPedidoDto(i.ProdutoId, i.Quantidade)).ToList()),
            Cenario.Json);
}
