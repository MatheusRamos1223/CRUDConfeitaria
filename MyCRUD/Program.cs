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

// "--seed-demo" popula o banco com dados de demonstração e encerra (Dados/SeedDemo.cs).
// O argumento é retirado antes de chegar ao host: a leitura de linha de comando da
// configuração trataria "--seed-demo" como chave e "engoliria" o argumento seguinte como valor.
var semearDemo = args.Contains(SeedDemo.ArgumentoLinhaDeComando);
var argumentosDoHost = args
    .Where(argumento => argumento != SeedDemo.ArgumentoLinhaDeComando)
    .ToArray();

var builder = WebApplication.CreateBuilder(argumentosDoHost);

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

        // A cada 5 min o cookie é conferido no banco: usuário apagado ou papel diferente
        // do da claim → sessão encerrada (R35). Detalhes em Login/ValidacaoDeSessao.cs.
        options.Events.OnValidatePrincipal = ValidacaoDeSessao.ValidarAsync;

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
// Limite por IP e por minuto em LimiteDeRequisicoes:TentativasPorMinuto (padrão 5).
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
// Modo seed: roda o seed de demonstração e encerra SEM subir o servidor.
// ---------------------------------------------------------------------------
// Só em Development e só com as migrations já aplicadas (as verificações ficam em
// SeedDemo.ExecutarAsync). O código de saída (0 = ok, 1 = erro) permite usar em scripts.
if (semearDemo)
{
    Environment.ExitCode = await SeedDemo.ExecutarAsync(app);

    // Descartar o app esvazia a fila do log do console antes de o processo terminar
    // (sem isso, a última mensagem do seed pode não aparecer).
    await app.DisposeAsync();
    return;
}

// ---------------------------------------------------------------------------
// Pipeline HTTP — a ORDEM importa: cada middleware envolve os seguintes.
// ---------------------------------------------------------------------------

// Cabeçalhos de segurança (nosniff, Referrer-Policy, X-Frame-Options, CSP) em TODA resposta,
// inclusive arquivos estáticos e erros. Primeiro da fila para envolver todos os outros.
app.UseCabecalhosDeSeguranca();

// Captura exceções não tratadas e as converte em Problem Details.
app.UseExceptionHandler();

// Preenche com Problem Details as respostas de erro sem corpo (401, 404, 429...).
app.UseStatusCodePages();

// Fora de Development: HSTS (o navegador passa a usar só HTTPS neste domínio) e
// redirecionamento HTTP → HTTPS. Em Development ficam desligados para aceitar http://localhost.
// Atrás de proxy que termina o TLS, o redirecionamento depende de saber a porta HTTPS
// (ASPNETCORE_HTTPS_PORT); sem UseForwardedHeaders a API não confia em X-Forwarded-Proto.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

// Anti-CSRF: POST/PUT/PATCH/DELETE sem "X-Requested-With: fetch" → 403 (R44).
// Antes da autenticação: recusa sem nem abrir o cookie.
app.UseProtecaoCsrf();

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

// Torna o Program (gerado pelas instruções de nível superior) visível ao projeto de testes:
// WebApplicationFactory<Program> exige que a classe seja pública.
public partial class Program;
