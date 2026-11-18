// R35: a sessão é revalidada no banco a cada 5 min. O relógio do cookie é trocado por um falso.

using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyCRUD.Login;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Seguranca;

[Collection("Api")]
public class SessaoTests(ApiFactory fabrica)
{
    private static readonly TimeSpan SeisMinutos = TimeSpan.FromMinutes(6);

    /// <summary>Fábrica derivada (mesmo banco) em que o cookie e a validação usam o relógio falso.</summary>
    private WebApplicationFactory<Program> ComRelogio(RelogioFalso relogio) =>
        fabrica.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<CookieAuthenticationOptions>(
                CookieAuthenticationDefaults.AuthenticationScheme,
                opcoes => opcoes.TimeProvider = relogio)));

    private static HttpClient NovoCliente(WebApplicationFactory<Program> app)
    {
        var cliente = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        cliente.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        return cliente;
    }

    [Fact]
    public async Task R35_AdminRebaixadoNoBanco_AposCincoMinutosSessaoRetorna401()
    {
        var relogio = new RelogioFalso(DateTimeOffset.UtcNow);
        await using var app = ComRelogio(relogio);
        var usuario = await fabrica.CriarUsuarioAsync(Papel.Admin);
        var cliente = await NovoCliente(app).LogarAsync(usuario.Email, usuario.Senha);

        // Antes de rebaixar: sessão válida e papel Admin.
        var antes = await (await cliente.GetAsync("/login/eu")).LerJson<UsuarioRespostaDto>();
        Assert.Equal(Papel.Admin, antes.Papel);

        await fabrica.ComBancoAsync(db => db.Usuarios
            .Where(u => u.Id == usuario.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Papel, Papel.Operador)));

        // Dentro da janela de 5 min o cookie ainda vale (a conferência no banco é periódica).
        relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/login/eu")).StatusCode);

        // Passada a janela, a revalidação vê o papel diferente e encerra a sessão.
        relogio.Avancar(SeisMinutos);
        var depois = await cliente.GetAsync("/login/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, depois.StatusCode);
    }

    [Fact]
    public async Task R35_UsuarioApagadoNoBanco_AposCincoMinutosSessaoRetorna401()
    {
        var relogio = new RelogioFalso(DateTimeOffset.UtcNow);
        await using var app = ComRelogio(relogio);
        var usuario = await fabrica.CriarUsuarioAsync(Papel.Operador);
        var cliente = await NovoCliente(app).LogarAsync(usuario.Email, usuario.Senha);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/login/eu")).StatusCode);

        await fabrica.ComBancoAsync(db => db.Usuarios
            .Where(u => u.Id == usuario.Id)
            .ExecuteDeleteAsync());

        relogio.Avancar(SeisMinutos);
        var depois = await cliente.GetAsync("/login/eu");

        Assert.Equal(HttpStatusCode.Unauthorized, depois.StatusCode);
    }

    [Fact]
    public async Task R35_UsuarioSemMudancaNoBanco_AposCincoMinutosSessaoContinuaValida()
    {
        var relogio = new RelogioFalso(DateTimeOffset.UtcNow);
        await using var app = ComRelogio(relogio);
        var usuario = await fabrica.CriarUsuarioAsync(Papel.Operador);
        var cliente = await NovoCliente(app).LogarAsync(usuario.Email, usuario.Senha);

        relogio.Avancar(SeisMinutos);
        var depois = await cliente.GetAsync("/login/eu");

        var eu = await depois.LerJson<UsuarioRespostaDto>();
        Assert.Equal(usuario.Email, eu.Email);
    }
}
