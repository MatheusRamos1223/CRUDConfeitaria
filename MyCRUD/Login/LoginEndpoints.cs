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
///   <item>Se o e-mail for o de <c>Admin:Email</c>, promove o usuário a <see cref="Papel.Admin"/>.</item>
///   <item>Cria uma <see cref="ClaimsIdentity"/> (ID, nome, e-mail e papel).</item>
///   <item><c>SignInAsync</c> criptografa essas claims dentro do cookie <c>Confeitaria.Auth</c>.</item>
///   <item>Nas próximas requisições o navegador reenvia o cookie e o ASP.NET reconstrói o usuário.</item>
/// </list>
/// <para>
/// O cookie é criptografado e assinado pelo Data Protection do ASP.NET:
/// o cliente não consegue ler nem alterar as claims — nem trocar o próprio papel.
/// </para>
/// <para><b>Quem é Admin?</b></para>
/// <para>
/// Não há tela para promover usuários. O Admin é definido na configuração
/// (<c>Admin:Email</c>, via User Secrets ou variável de ambiente <c>Admin__Email</c>).
/// Assim, ninguém vira Admin por uma requisição HTTP, e o e-mail do Admin não fica no repositório.
/// </para>
/// </remarks>
public static class LoginEndpoints
{
    /// <summary>Chave de configuração com o e-mail de quem deve ser Admin.</summary>
    public const string ChaveEmailAdmin = "Admin:Email";

    // Hash de uma senha aleatória, calculado uma vez. Usado quando o e-mail não existe,
    // para que o tempo de resposta seja igual ao de um e-mail existente com senha errada.
    // Sem isso, um atacante mediria o tempo e descobriria quais e-mails estão cadastrados.
    private static readonly string HashFicticio =
        new PasswordHasher<Usuario>().HashPassword(new Usuario(), Guid.NewGuid().ToString());

    public static RouteGroupBuilder MapLoginEndpoints(this WebApplication app)
    {
        // Sem Admin configurado a aplicação funciona, mas ninguém consegue gerenciar
        // categorias nem desativar produtos. Avisamos uma vez, na inicialização.
        if (string.IsNullOrWhiteSpace(app.Configuration[ChaveEmailAdmin]))
        {
            app.Logger.LogWarning("Nenhum Admin configurado (Admin:Email).");
        }

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
        LoginDto dto,
        AppDbContext db,
        HttpContext httpContext,
        IConfiguration configuracao,
        ILoggerFactory loggerFactory)
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
        }

        // Promoção a Admin: só depois de a senha conferir, e nunca o contrário
        // (um Admin não é rebaixado se o e-mail configurado mudar).
        var promovido = DeveSerPromovidoAAdmin(usuario, configuracao);

        if (promovido)
        {
            usuario.Papel = Papel.Admin;
        }

        // Um único UPDATE para o hash novo e/ou o papel novo. Se nada mudou,
        // o EF não envia comando nenhum ao banco.
        await db.SaveChangesAsync();

        // Mudança de privilégio fica registrada no log (auditoria), só com o ID — sem e-mail.
        if (promovido)
        {
            loggerFactory.CreateLogger(typeof(LoginEndpoints))
                .LogInformation("Usuário {UsuarioId} promovido a Admin por Admin:Email.", usuario.Id);
        }

        // Claims: afirmações sobre o usuário que viajam dentro do cookie.
        // ClaimTypes.Role é a claim que RequireRole (política SomenteAdmin) confere.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Papel.ToString())
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
    /// Compara o e-mail do usuário com o configurado em <c>Admin:Email</c>.
    /// </summary>
    /// <returns><c>true</c> se o usuário deve virar Admin agora (ainda não é e o e-mail confere).</returns>
    private static bool DeveSerPromovidoAAdmin(Usuario usuario, IConfiguration configuracao)
    {
        var emailAdmin = configuracao[ChaveEmailAdmin];

        // Sem configuração, ninguém é promovido (o aviso sai na inicialização).
        if (string.IsNullOrWhiteSpace(emailAdmin))
        {
            return false;
        }

        // Mesma normalização do cadastro: " Ana@Email.com" na configuração também vale.
        return usuario.Papel != Papel.Admin
            && usuario.Email == Usuario.NormalizarEmail(emailAdmin);
    }

    /// <summary>
    /// Lê o usuário direto das claims do cookie, sem consultar o banco.
    /// Usado pelas páginas HTML para saber se ainda há sessão ativa e qual o papel.
    /// </summary>
    /// <remarks>
    /// Como vem do cookie, o papel é o do momento do login. A cada 5 min o cookie é
    /// conferido no banco (<see cref="ValidacaoDeSessao"/>): se o papel mudou ou o usuário
    /// foi apagado, a sessão é encerrada e esta rota passa a devolver 401.
    /// </remarks>
    private static Ok<UsuarioRespostaDto> UsuarioAtual(ClaimsPrincipal user)
    {
        var id = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var nome = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        // Cookie emitido antes da existência de papéis não tem a claim: vale o padrão (Operador).
        var papel = Enum.TryParse<Papel>(user.FindFirstValue(ClaimTypes.Role), out var lido)
            ? lido
            : Papel.Operador;

        return TypedResults.Ok(new UsuarioRespostaDto(id, nome, email, papel));
    }

    private static async Task<Results<Created<UsuarioRespostaDto>, ProblemHttpResult>> Cadastrar(
        CriarUsuarioDto dto, AppDbContext db, IConfiguration configuracao)
    {
        var email = Usuario.NormalizarEmail(dto.Email);

        // R42: o e-mail de Admin:Email não pode ser cadastrado por aqui. Sem esta regra,
        // quem chegasse primeiro ao formulário com esse e-mail viraria Admin no login.
        // A resposta é a MESMA do e-mail duplicado: não revela qual e-mail é o do Admin.
        var emailAdmin = configuracao[ChaveEmailAdmin];
        var ehEmailDoAdmin = !string.IsNullOrWhiteSpace(emailAdmin)
            && email == Usuario.NormalizarEmail(emailAdmin);

        // Verificação amigável. Se dois cadastros iguais chegarem ao mesmo tempo,
        // o índice único do banco barra o segundo e o TratadorGlobalDeExcecoes devolve 409.
        if (ehEmailDoAdmin || await db.Usuarios.AnyAsync(u => u.Email == email))
        {
            return TypedResults.Problem(
                title: "E-mail já cadastrado.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Cadastro SEMPRE cria Operador (R37). O usuário do Admin:Email não passa por aqui:
        // ele é criado pelo seed de demonstração ou já existia antes de o e-mail ser configurado.
        var usuario = new Usuario
        {
            Nome = dto.Nome.Trim(),
            Email = email,
            Papel = Papel.Operador
        };

        usuario.SenhaHash = new PasswordHasher<Usuario>().HashPassword(usuario, dto.Senha);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        // Não há rota pública de consulta de usuário, então o 201 vai sem cabeçalho Location.
        return TypedResults.Created((string?)null, usuario.ParaResposta());
    }
}
