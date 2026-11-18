// R43: cabeçalhos de segurança em toda resposta; /scalar fica sem CSP (só existe em Development).

using System.Net;
using MyCRUD.Compartilhado;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Seguranca;

public class CabecalhosDeSegurancaTests(ApiFactoryDevelopment fabrica) : IClassFixture<ApiFactoryDevelopment>
{
    // O HttpClient separa cabeçalhos "de resposta" e "de conteúdo"; procura nos dois para o
    // teste não depender de em qual coleção o TestServer colocou o cabeçalho.
    private static string Valor(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues(nome, out var valores)
            || resposta.Content.Headers.TryGetValues(nome, out valores)
            ? string.Join(",", valores)
            : string.Empty;

    [Fact]
    public async Task R43_LoginHtml_TemCspNosniffEXFrameOptions()
    {
        var cliente = fabrica.NovoClienteHttp();

        var resposta = await cliente.GetAsync("/login.html");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(CabecalhosDeSeguranca.PoliticaDeConteudo, Valor(resposta, "Content-Security-Policy"));
        Assert.Equal("nosniff", Valor(resposta, "X-Content-Type-Options"));
        Assert.Equal("DENY", Valor(resposta, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Valor(resposta, "Referrer-Policy"));
    }

    [Fact]
    public async Task R43_RespostaDeErro_TambemTemOsCabecalhos()
    {
        var cliente = fabrica.NovoClienteHttp();

        var resposta = await cliente.GetAsync("/produtos"); // 401 sem login

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("nosniff", Valor(resposta, "X-Content-Type-Options"));
        Assert.NotEmpty(Valor(resposta, "Content-Security-Policy"));
    }

    [Fact]
    public async Task R43_Scalar_NaoTemCspMasTemOsDemaisCabecalhos()
    {
        var cliente = fabrica.NovoClienteHttp();

        var resposta = await cliente.GetAsync("/scalar/v1");

        // A rota existe (Development) e o middleware rodou (nosniff presente), mas sem CSP.
        Assert.NotEqual(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("nosniff", Valor(resposta, "X-Content-Type-Options"));
        Assert.False(resposta.Headers.Contains("Content-Security-Policy"));
        Assert.False(resposta.Content.Headers.Contains("Content-Security-Policy"));
    }
}
