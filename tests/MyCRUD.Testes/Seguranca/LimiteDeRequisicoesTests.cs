// R47: limite de tentativas de login por minuto (fábrica própria com limite 5).

using System.Net;
using System.Net.Http.Json;
using MyCRUD.Login;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Seguranca;

public class LimiteDeRequisicoesTests(ApiFactoryLimite5 fabrica) : IClassFixture<ApiFactoryLimite5>
{
    // Único teste da classe: a fábrica é compartilhada e o contador do minuto é global a ela.
    [Fact]
    public async Task R47_SeisLoginsErradosComLimiteCinco_SextoRetorna429()
    {
        var cliente = fabrica.NovoClienteHttp();
        var dto = new LoginDto($"ninguem{Cenario.Sufixo()}@teste.dev", "senha-errada-123");

        var statusPorTentativa = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var resposta = await cliente.PostAsJsonAsync("/login", dto, Cenario.Json);
            statusPorTentativa.Add(resposta.StatusCode);
        }

        // As 5 primeiras chegam ao login (credencial errada = 401); a 6ª é barrada pelo limitador.
        Assert.All(statusPorTentativa.Take(5), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statusPorTentativa[5]);
    }
}
