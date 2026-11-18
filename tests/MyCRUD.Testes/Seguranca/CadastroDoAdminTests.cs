// R42: o e-mail de Admin:Email não pode ser cadastrado (só existe em Development).

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Login;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Seguranca;

// Fábrica da classe = banco próprio: o usuário de Admin:Email NUNCA é criado aqui. Assim o 409 só
// pode vir da regra R42 (e não de "e-mail já existe").
public class CadastroDoAdminTests(ApiFactoryDevelopment fabrica) : IClassFixture<ApiFactoryDevelopment>
{
    [Theory]
    [InlineData(Cenario.EmailAdmin)]
    [InlineData("  ADMIN@Teste.DEV ")]
    public async Task R42_CadastroComEmailDoAdmin_Retorna409(string email)
    {
        var cliente = fabrica.NovoClienteHttp();

        var resposta = await cliente.PostAsJsonAsync(
            // Sem Trim: o e-mail vai como veio, para provar que a API normaliza (espaços e maiúsculas).
            // [EmailAddress] aceita espaços nas pontas (só confere um único '@' no meio).
            "/login/cadastro", new CriarUsuarioDto($"Intruso {Cenario.Sufixo()}", email, Cenario.SenhaPadrao), Cenario.Json);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var problema = await resposta.LerProblem();
        Assert.Equal("E-mail já cadastrado.", problema.Title);
    }

    [Fact]
    public async Task R42_CadastroComEmailDoAdmin_NaoCriaUsuario()
    {
        var cliente = fabrica.NovoClienteHttp();

        await cliente.PostAsJsonAsync(
            "/login/cadastro", new CriarUsuarioDto("Intruso", Cenario.EmailAdmin, Cenario.SenhaPadrao), Cenario.Json);

        var existe = await fabrica.ComBancoAsync(db => db.Usuarios.AnyAsync(u => u.Email == Cenario.EmailAdmin));
        Assert.False(existe);
    }

    [Fact]
    public async Task R42_CadastroComOutroEmail_Retorna201ComoOperador()
    {
        var cliente = fabrica.NovoClienteHttp();
        var email = $"novo{Cenario.Sufixo()}@teste.dev";

        var resposta = await cliente.PostAsJsonAsync(
            "/login/cadastro", new CriarUsuarioDto("Novo Usuario", email, Cenario.SenhaPadrao), Cenario.Json);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await resposta.LerJson<UsuarioRespostaDto>();
        Assert.Equal(Papel.Operador, criado.Papel);
    }
}
