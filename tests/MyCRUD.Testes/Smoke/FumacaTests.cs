// Testes de fumaça: provam que a infraestrutura (contêiner, migrations, cookie, helpers) funciona.

using System.Net;
using MyCRUD.Login;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Smoke;

[Collection("Api")]
public class FumacaTests(ApiFactory fabrica)
{
    [Fact]
    public async Task R1_RotaDeDadosSemCookie_Retorna401()
    {
        var anonimo = fabrica.NovoClienteHttp();

        var resposta = await anonimo.GetAsync("/produtos");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task R34_LoginComEmailDoAdmin_EuDevolvePapelAdmin()
    {
        var admin = await fabrica.AdminAsync();

        var resposta = await admin.GetAsync("/login/eu");

        var eu = await resposta.LerJson<UsuarioRespostaDto>();
        Assert.Equal(Papel.Admin, eu.Papel);
        Assert.Equal(Cenario.EmailAdmin, eu.Email);
    }

    [Fact]
    public async Task R34_LoginDeOutroUsuario_EuDevolvePapelOperador()
    {
        var operador = await fabrica.OperadorAsync();

        var eu = await (await operador.GetAsync("/login/eu")).LerJson<UsuarioRespostaDto>();

        Assert.Equal(Papel.Operador, eu.Papel);
    }
}
