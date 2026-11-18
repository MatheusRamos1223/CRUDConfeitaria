// Helpers extras dos testes de catálogo (categorias e produtos).

using System.Net.Http.Json;
using MyCRUD.Categorias;
using MyCRUD.Compartilhado;
using MyCRUD.Produtos;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Catalogo;

public static class CatalogoHelpers
{
    /// <summary>GET /produtos com a query string informada (ex.: "?categoriaId=3&amp;situacao=Todos"), lida como página.</summary>
    public static async Task<ResultadoPaginado<ProdutoRespostaDto>> ListarProdutosAsync(
        this HttpClient http, string consulta) =>
        await (await http.GetAsync($"/produtos{consulta}")).LerJson<ResultadoPaginado<ProdutoRespostaDto>>();

    /// <summary>GET /categorias com a query string informada, lida como página.</summary>
    public static async Task<ResultadoPaginado<CategoriaRespostaDto>> ListarCategoriasAsync(
        this HttpClient http, string consulta) =>
        await (await http.GetAsync($"/categorias{consulta}")).LerJson<ResultadoPaginado<CategoriaRespostaDto>>();

    /// <summary>DELETE /produtos/{id} (Admin). Devolve a resposta crua.</summary>
    public static Task<HttpResponseMessage> DesativarProdutoAsync(this HttpClient http, int id) =>
        http.DeleteAsync($"/produtos/{id}");

    /// <summary>POST /produtos/{id}/reativar (Admin). Devolve a resposta crua.</summary>
    public static Task<HttpResponseMessage> ReativarProdutoAsync(this HttpClient http, int id) =>
        http.PostAsync($"/produtos/{id}/reativar", content: null);

    /// <summary>GET /produtos/{id}, lido como produto.</summary>
    public static async Task<ProdutoRespostaDto> BuscarProdutoAsync(this HttpClient http, int id) =>
        await (await http.GetAsync($"/produtos/{id}")).LerJson<ProdutoRespostaDto>();

    /// <summary>Mensagens de erro de um campo (sem diferenciar maiúsculas na chave), ou lista vazia.</summary>
    public static string[] ErrosDoCampo(this ProblemaDto problema, string campo)
    {
        if (problema.Errors is null)
        {
            return [];
        }

        foreach (var (chave, mensagens) in problema.Errors)
        {
            if (string.Equals(chave, campo, StringComparison.OrdinalIgnoreCase))
            {
                return mensagens;
            }
        }

        return [];
    }
}
