using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Compartilhado;
using MyCRUD.Dados;

namespace MyCRUD.Login;

/// <summary>
/// Rotas de autenticação (login/logout), sessão atual e cadastro de usuários,
/// agrupadas em <c>/login</c>.
/// </summary>
/// <remarks>
/// <para><b>Fluxo do login por cookie</b></para>
/// <list type="number">
///   <item>O cliente envia e-mail e senha.</item>
///   <item>A API busca o usuário e compara a senha com o hash salvo.</item>
///   <item>Se conferir, cria uma <see cref="ClaimsIdentity"/> (ID, nome, e-mail).</item>
///   <item><c>SignInAsync</c> criptografa essas claims dentro do cookie <c>Confeitaria.Auth</c>.</item>
///   <item>Nas próximas requisições o navegador reenvia o cookie e o ASP.NET reconstrói o usuário.</item>
/// </list>
/// <para>
/// O cookie é criptografado e assinado pelo Data Protection do ASP.NET:
/// o cliente não consegue ler nem alterar as claims.
/// </para>
/// </remarks>
public static class LoginEndpoints
{
    // Hash de uma senha aleatória, calculado uma vez. Usado quando o e-mail não existe,
    // para que o tempo de resposta seja igual ao de um e-mail existente com senha errada.
    // Sem isso, um atacante mediria o tempo e descobriria quais e-mails estão cadastrados.
    private static readonly string HashFicticio =
        new PasswordHasher<Usuario>().HashPassword(new Usuario(), Guid.NewGuid().ToString());

    public static RouteGroupBuilder MapLoginEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/login").WithTags("Autenticação");

        grupo.MapPost("/", Entrar)
            .WithName("Entrar")
            .WithSummary("Autentica com e-mail e senha e devolve o cookie de sessão.")
            .ComValidacao<LoginDto>()
            .RequireRateLimiting(LimiteDeRequisicoes.PoliticaAutenticacao)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        grupo.MapPost("/sair", Sair)
            .WithName("Sair")
            .WithSummary("Encerra a sessão removendo o cookie.")
            .RequireAuthorization();

        grupo.MapGet("/eu", UsuarioAtual)
            .WithName("UsuarioAtual")
            .WithSummary("Devolve o usuário da sessão atual. 401 se não houver login.")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // O cadastro aberto só existe em desenvolvimento: em produção, qualquer
        // pessoa na internet poderia criar uma conta e acessar os produtos.
        if (app.Environment.IsDevelopment())
        {
            grupo.MapPost("/cadastro", Cadastrar)
                .WithName("CadastrarUsuario")
                .WithSummary("Cadastra um usuário. Disponível apenas em Development.")
                .ComValidacao<CriarUsuarioDto>()
                .RequireRateLimiting(LimiteDeRequisicoes.PoliticaAutenticacao)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status429TooManyRequests)
                .AllowAnonymous();
        }

        return grupo;
    }

    private static async Task<Results<Ok<UsuarioRespostaDto>, UnauthorizedHttpResult>> Entrar(
        LoginDto dto, AppDbContext db, HttpContext httpContext)
    {
        var email = Usuario.NormalizarEmail(dto.Email);
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

        var hasher = new PasswordHasher<Usuario>();
        var resultado = hasher.VerifyHashedPassword(
            usuario ?? new Usuario(),
            usuario?.SenhaHash ?? HashFicticio,
            dto.Senha);

        // Mesma resposta para "e-mail não existe" e "senha errada":
        // não revelamos qual dos dois falhou.
        if (usuario is null || resultado == PasswordVerificationResult.Failed)
        {
            return TypedResults.Unauthorized();
        }

        // O algoritmo de hash ficou mais forte desde que esta senha foi salva:
        // aproveitamos que temos a senha correta em mãos para atualizar o hash.
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = hasher.HashPassword(usuario, dto.Senha);
            await db.SaveChangesAsync();
        }

        // Claims: afirmações sobre o usuário que viajam dentro do cookie.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email)
        };

        var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidade),
            new AuthenticationProperties
            {
                // Cookie de sessão: some ao fechar o navegador.
                IsPersistent = false
            });

        return TypedResults.Ok(usuario.ParaResposta());
    }

    private static async Task<NoContent> Sair(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Lê o usuário direto das claims do cookie, sem consultar o banco.
    /// Usado pelas páginas HTML para saber se ainda há sessão ativa.
    /// </summary>
    private static Ok<UsuarioRespostaDto> UsuarioAtual(ClaimsPrincipal user)
    {
        var id = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var nome = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        return TypedResults.Ok(new UsuarioRespostaDto(id, nome, email));
    }

    private static async Task<Results<Created<UsuarioRespostaDto>, ProblemHttpResult>> Cadastrar(
        CriarUsuarioDto dto, AppDbContext db)
    {
        var email = Usuario.NormalizarEmail(dto.Email);

        // Verificação amigável. Se dois cadastros iguais chegarem ao mesmo tempo,
        // o índice único do banco barra o segundo e o TratadorGlobalDeExcecoes devolve 409.
        if (await db.Usuarios.AnyAsync(u => u.Email == email))
        {
            return TypedResults.Problem(
                title: "E-mail já cadastrado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var usuario = new Usuario
        {
            Nome = dto.Nome.Trim(),
            Email = email
        };

        usuario.SenhaHash = new PasswordHasher<Usuario>().HashPassword(usuario, dto.Senha);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        // Não há rota pública de consulta de usuário, então o 201 vai sem cabeçalho Location.
        return TypedResults.Created((string?)null, usuario.ParaResposta());
    }
}
