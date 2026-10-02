using MyCRUD.Login;
using MyCRUD.Produtos;
using MyCRUD.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies; // Importa o namespace necessário para autenticação baseada em cookies(da trabalho mas vale a pena)

var builder = WebApplication.CreateBuilder(args); // Cria o construtor do aplicativo web 

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "A conexão DefaultConnection não foi configurada."); // Obtém a string de conexão do arquivo de configuração (appsettings.json) e lança uma exceção se não estiver configurada

builder.Services.AddDbContext<AppDbContext>(options => // Adiciona o DbContext à injeção de dependência
    options.UseNpgsql(connectionString));
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => // Configura a autenticação baseada em cookies
    {
        options.Cookie.Name = "Confeitaria.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        //  teste atual em HTTP local.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromHours(2);  // Define o tempo de expiração do cookie de autenticação para 2 horas

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

builder.Services.AddAuthorization(); // Adiciona serviços de autorização ao contêiner de serviços, permitindo que você defina políticas de acesso e restrições para diferentes partes do aplicativo

var app = builder.Build(); // Cria o aplicativo web a partir do construtor


app.UseStaticFiles(); // Habilita o uso de arquivos estáticos (como HTML, CSS, JS) na pasta wwwroot



app.UseAuthentication(); // Habilita a autenticação no pipeline de middleware, permitindo que o aplicativo verifique a identidade do usuário antes de processar as solicitações
app.UseAuthorization(); // Habilita a autorização no pipeline de middleware, permitindo que o aplicativo verifique se o usuário autenticado tem permissão para acessar recursos específicos

app.MapGet("/", () => "API da confeitaria funcionando!");
app.MapProdutoEndpoints();
app.MapLoginEndpoints();
app.Run();
