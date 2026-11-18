// Ponto de entrada da aplicação.
// Aqui fica a "composição" do sistema: quais serviços existem (injeção de dependência)
// e em que ordem cada requisição HTTP passa pelos middlewares (pipeline).

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Categorias;
using MyCRUD.Clientes;
using MyCRUD.Compartilhado;
using MyCRUD.Dados;
using MyCRUD.Login;
using MyCRUD.Pedidos;
using MyCRUD.Produtos;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Banco de dados
// ---------------------------------------------------------------------------
// A string de conexão vem de User Secrets (desenvolvimento) ou de variável de
// ambiente (produção). Nunca fica no código nem no repositório.
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "A conexão DefaultConnection não foi configurada.");

// Registra o AppDbContext com tempo de vida "scoped": uma instância por requisição.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---------------------------------------------------------------------------
// 2. Autenticação (quem é você?) e autorização (o que você pode fazer?)
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Confeitaria.Auth";

        // HttpOnly: o JavaScript da página não consegue ler o cookie (mitiga roubo via XSS).
        options.Cookie.HttpOnly = true;

        // Lax: o cookie não é enviado em POSTs vindos de outros sites (mitiga CSRF).
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Em desenvolvimento aceitamos HTTP local; em produção, somente HTTPS.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromHours(2);

        // Por padrão o ASP.NET redireciona para uma página de login (302).
        // Como somos uma API consumida por fetch, devolvemos o status HTTP correto.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Autorização com as políticas da aplicação (ex.: Politicas.SomenteAdmin → papel Admin).
// Substitui o AddAuthorization() simples: registra o mesmo serviço e acrescenta as políticas.
builder.Services.AddPoliticasDeAcesso();

// ---------------------------------------------------------------------------
// 3. Erros padronizados (RFC 9457 — Problem Details)
// ---------------------------------------------------------------------------
// Todo erro da API sai no mesmo formato JSON: { type, title, status, detail, errors }.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorGlobalDeExcecoes>();

// ---------------------------------------------------------------------------
// 4. Limite de requisições (proteção contra força bruta no login)
// ---------------------------------------------------------------------------
builder.Services.AddLimiteDeRequisicoes();

// ---------------------------------------------------------------------------
// 5. Documentação OpenAPI (contrato da API, exibido pela interface Scalar)
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi();

// ---------------------------------------------------------------------------
// 6. JSON: enums trafegam como texto ("Aberto", "Admin") em vez de números
// ---------------------------------------------------------------------------
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline HTTP — a ORDEM importa: cada middleware envolve os seguintes.
// ---------------------------------------------------------------------------

// Captura exceções não tratadas e as converte em Problem Details.
app.UseExceptionHandler();

// Preenche com Problem Details as respostas de erro sem corpo (401, 404, 429...).
app.UseStatusCodePages();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Depois da autenticação, para que a política possa usar dados do usuário se precisar.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // GET /openapi/v1.json
    app.MapScalarApiReference();    // Interface interativa em /scalar
}

app.MapGet("/", () => "API da confeitaria funcionando!")
    .ExcludeFromDescription();

// Cada feature registra as próprias rotas (feature folders).
// MapLoginEndpoints também avisa no log se Admin:Email não estiver configurado (R36).
app.MapLoginEndpoints();
app.MapProdutoEndpoints();
app.MapCategoriaEndpoints();
app.MapClienteEndpoints();
app.MapPedidoEndpoints();

app.Run();
