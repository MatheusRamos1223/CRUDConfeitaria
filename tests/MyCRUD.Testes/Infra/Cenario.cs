// Helpers de cenário: criam usuários, clientes HTTP logados e dados de domínio, e leem respostas.
//
// Regras de uso (ver tests/README.md):
//   - Todo dado criado leva um sufixo único (Cenario.Sufixo()) e o teste só verifica o que criou.
//   - Usuários nascem direto no banco (o cadastro HTTP só existe em Development).
//   - Categoria/produto/cliente/pedido nascem pela API, como o front faria (exercita as regras).

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Categorias;
using MyCRUD.Clientes;
using MyCRUD.Dados;
using MyCRUD.Login;
using MyCRUD.Pedidos;
using MyCRUD.Produtos;

namespace MyCRUD.Testes.Infra;

/// <summary>Usuário criado no banco para um teste (a senha em texto puro só existe aqui).</summary>
public sealed record UsuarioDeTeste(int Id, string Nome, string Email, string Senha, Papel Papel);

/// <summary>Corpo de erro (Problem Details / ValidationProblem) simplificado.</summary>
public sealed record ProblemaDto(
    string? Title,
    int? Status,
    string? Detail,
    Dictionary<string, string[]>? Errors);

public static class Cenario
{
    /// <summary>Mesmo valor de <c>Admin:Email</c> configurado pela fábrica.</summary>
    public const string EmailAdmin = "admin@teste.dev";

    /// <summary>Senha de todo usuário criado sem senha explícita (atende ao mínimo de 8 caracteres).</summary>
    public const string SenhaPadrao = "Senha@12345";

    /// <summary>JSON como a API fala: camelCase, e enums como texto ("Aberto", "Admin").</summary>
    public static readonly JsonSerializerOptions Json = CriarOpcoesJson();

    private static readonly SemaphoreSlim TravaAdmin = new(1, 1);

    private static JsonSerializerOptions CriarOpcoesJson()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opcoes.Converters.Add(new JsonStringEnumConverter());
        return opcoes;
    }

    /// <summary>Sufixo curto e único (8 hex) para nomes, e-mails e telefones de teste.</summary>
    public static string Sufixo() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Hoje no fuso de São Paulo (a regra R21 compara a data de entrega com ele).</summary>
    public static DateOnly HojeSp() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")));

    // ------------------------------------------------------------------
    // Usuários e clientes HTTP
    // ------------------------------------------------------------------

    /// <summary>
    /// Cria um usuário direto no banco, com hash de senha real. E-mail único por padrão.
    /// O papel é gravado como pedido (não passa pela promoção por Admin:Email).
    /// </summary>
    public static async Task<UsuarioDeTeste> CriarUsuarioAsync(
        this ApiFactory fabrica, Papel papel, string? email = null, string senha = SenhaPadrao)
    {
        var sufixo = Sufixo();
        var usuario = new Usuario
        {
            Nome = $"Usuario {sufixo}",
            Email = Usuario.NormalizarEmail(email ?? $"u{sufixo}@teste.dev"),
            Papel = papel
        };
        usuario.SenhaHash = new PasswordHasher<Usuario>().HashPassword(usuario, senha);

        await fabrica.ComBancoAsync(async db =>
        {
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            return 0;
        });

        return new UsuarioDeTeste(usuario.Id, usuario.Nome, usuario.Email, senha, papel);
    }

    /// <summary>
    /// Garante (uma vez por banco) o usuário de <see cref="EmailAdmin"/> como Operador no banco;
    /// o login o promove a Admin (R34). Devolve o usuário com a senha padrão.
    /// </summary>
    public static async Task<UsuarioDeTeste> GarantirAdminConfiguradoAsync(this ApiFactory fabrica)
    {
        await TravaAdmin.WaitAsync();
        try
        {
            var existente = await fabrica.ComBancoAsync(db =>
                db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == EmailAdmin));

            if (existente is not null)
            {
                return new UsuarioDeTeste(existente.Id, existente.Nome, existente.Email, SenhaPadrao, existente.Papel);
            }

            return await fabrica.CriarUsuarioAsync(Papel.Operador, EmailAdmin);
        }
        finally
        {
            TravaAdmin.Release();
        }
    }

    /// <summary>
    /// Cliente HTTP anônimo: BaseAddress https://localhost (cookie Secure volta), sem redirecionar,
    /// com cookies e com o cabeçalho X-Requested-With: fetch (R44).
    /// </summary>
    public static HttpClient NovoClienteHttp(this ApiFactory fabrica, bool comCabecalhoCsrf = true)
    {
        var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        if (comCabecalhoCsrf)
        {
            cliente.DefaultRequestHeaders.Add("X-Requested-With", "fetch");
        }

        return cliente;
    }

    /// <summary>POST /login com as credenciais; falha o teste se não der 200. Devolve o mesmo cliente (agora com cookie).</summary>
    public static async Task<HttpClient> LogarAsync(this HttpClient cliente, string email, string senha)
    {
        var resposta = await cliente.PostAsJsonAsync("/login", new LoginDto(email, senha), Json);

        Assert.True(
            resposta.IsSuccessStatusCode,
            $"Login de {email} falhou: {(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync()}");

        return cliente;
    }

    /// <summary>Cliente novo, já logado como o usuário informado.</summary>
    public static Task<HttpClient> LogarComoAsync(this ApiFactory fabrica, UsuarioDeTeste usuario) =>
        fabrica.NovoClienteHttp().LogarAsync(usuario.Email, usuario.Senha);

    /// <summary>Cliente novo, logado como o Admin de <c>Admin:Email</c> (promovido no login, R34).</summary>
    public static async Task<HttpClient> LogarComoAdminConfiguradoAsync(this ApiFactory fabrica) =>
        await fabrica.LogarComoAsync(await fabrica.GarantirAdminConfiguradoAsync());

    // ------------------------------------------------------------------
    // Dados de domínio pela API (o cliente HTTP precisa ter permissão para a rota)
    // ------------------------------------------------------------------

    /// <summary>POST /categorias (Admin). Nome padrão: "Cat {sufixo}".</summary>
    public static async Task<CategoriaRespostaDto> CriarCategoriaAsync(this HttpClient admin, string? nome = null)
    {
        var resposta = await admin.PostAsJsonAsync(
            "/categorias", new CriarCategoriaDto(nome ?? $"Cat {Sufixo()}"), Json);
        return await resposta.LerJson<CategoriaRespostaDto>();
    }

    /// <summary>POST /produtos. Nome padrão: "Produto {sufixo}".</summary>
    public static async Task<ProdutoRespostaDto> CriarProdutoAsync(
        this HttpClient http, int categoriaId, decimal preco = 10m, string? nome = null)
    {
        var resposta = await http.PostAsJsonAsync(
            "/produtos", new CriarProdutoDto(nome ?? $"Produto {Sufixo()}", preco, categoriaId), Json);
        return await resposta.LerJson<ProdutoRespostaDto>();
    }

    /// <summary>POST /clientes. Nome padrão: "Cliente {sufixo}"; e-mail nulo.</summary>
    public static async Task<ClienteRespostaDto> CriarClienteAsync(
        this HttpClient http, string? nome = null, string telefone = "(19) 99999-0000", string? email = null)
    {
        var resposta = await http.PostAsJsonAsync(
            "/clientes", new CriarClienteDto(nome ?? $"Cliente {Sufixo()}", telefone, email), Json);
        return await resposta.LerJson<ClienteRespostaDto>();
    }

    /// <summary>
    /// POST /pedidos. Itens como (ProdutoId, Quantidade). Entrega padrão: hoje (SP) + 7 dias.
    /// </summary>
    public static async Task<PedidoDetalheDto> CriarPedidoAsync(
        this HttpClient http,
        int clienteId,
        IEnumerable<(int ProdutoId, int Quantidade)> itens,
        DateOnly? dataEntrega = null)
    {
        var dto = new CriarPedidoDto(
            clienteId,
            dataEntrega ?? HojeSp().AddDays(7),
            itens.Select(i => new CriarItemPedidoDto(i.ProdutoId, i.Quantidade)).ToList());

        var resposta = await http.PostAsJsonAsync("/pedidos", dto, Json);
        return await resposta.LerJson<PedidoDetalheDto>();
    }

    /// <summary>POST /pedidos/{id}/{acao} (confirmar, entregar, cancelar). Devolve a resposta crua.</summary>
    public static Task<HttpResponseMessage> TransicionarPedidoAsync(this HttpClient http, int pedidoId, string acao) =>
        http.PostAsync($"/pedidos/{pedidoId}/{acao}", content: null);

    // ------------------------------------------------------------------
    // Leitura de respostas
    // ------------------------------------------------------------------

    /// <summary>
    /// Lê o corpo como <typeparamref name="T"/> (JSON da API). Se o status não for de sucesso,
    /// falha o teste mostrando o corpo recebido.
    /// </summary>
    public static async Task<T> LerJson<T>(this HttpResponseMessage resposta)
    {
        if (!resposta.IsSuccessStatusCode)
        {
            Assert.Fail($"Esperava sucesso, veio {(int)resposta.StatusCode}: {await resposta.Content.ReadAsStringAsync()}");
        }

        var corpo = await resposta.Content.ReadFromJsonAsync<T>(Json);
        return corpo ?? throw new InvalidOperationException("Resposta sem corpo JSON.");
    }

    /// <summary>Lê o corpo de erro (title, status, detail, errors) de uma resposta 4xx.</summary>
    public static async Task<ProblemaDto> LerProblem(this HttpResponseMessage resposta)
    {
        var problema = await resposta.Content.ReadFromJsonAsync<ProblemaDto>(Json);
        return problema ?? throw new InvalidOperationException("Resposta sem corpo de erro.");
    }
}
