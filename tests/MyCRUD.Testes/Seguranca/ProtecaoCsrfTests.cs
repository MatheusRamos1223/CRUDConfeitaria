// R44: escritas sem X-Requested-With: fetch são recusadas antes da autenticação.

using System.Net;
using System.Net.Http.Json;
using MyCRUD.Login;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Seguranca;

[Collection("Api")]
public class ProtecaoCsrfTests(ApiFactory fabrica)
{
    [Fact]
    public async Task R44_PostSemCabecalho_Retorna403()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Papel.Operador);
        var cliente = fabrica.NovoClienteHttp(comCabecalhoCsrf: false);

        // Credenciais corretas: se passasse do filtro, seria 200. Só o 403 prova a barreira.
        var resposta = await cliente.PostAsJsonAsync(
            "/login", new LoginDto(usuario.Email, usuario.Senha), Cenario.Json);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.Equal("Requisição sem o cabeçalho X-Requested-With.", problema.Title);
    }

    [Fact]
    public async Task R44_PostComCabecalhoErrado_Retorna403()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Papel.Operador);
        var cliente = fabrica.NovoClienteHttp(comCabecalhoCsrf: false);
        cliente.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        var resposta = await cliente.PostAsJsonAsync(
            "/login", new LoginDto(usuario.Email, usuario.Senha), Cenario.Json);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task R44_PostComCabecalho_SegueParaOEndpoint()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Papel.Operador);
        var cliente = fabrica.NovoClienteHttp(comCabecalhoCsrf: true);

        var resposta = await cliente.PostAsJsonAsync(
            "/login", new LoginDto(usuario.Email, usuario.Senha), Cenario.Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task R44_GetSemCabecalho_NaoExigeCabecalho()
    {
        var cliente = fabrica.NovoClienteHttp(comCabecalhoCsrf: false);

        var resposta = await cliente.GetAsync("/produtos");

        // 401 (sem login) e não 403: a leitura nem passa pelo anti-CSRF.
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
