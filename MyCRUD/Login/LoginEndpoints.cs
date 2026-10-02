using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Dados;
using System.Security.Claims; // Importa o namespace necessário para trabalhar com Claims
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies; // Importa o namespace necessário para autenticação baseada em cookies

namespace MyCRUD.Login;

public static class LoginEndpoints
{
    public static RouteGroupBuilder MapLoginEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/login");

        grupo.MapPost("/", async (LoginDto dto, AppDbContext db, HttpContext httpContext) => // Endpoint para autenticação de login(HttpContext é necessário para manipular a autenticação baseada em cookies)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Senha))
            {
                return Results.BadRequest("Informe e-mail e senha.");
            }

            var usuario = await db.Usuarios
                .FirstOrDefaultAsync(u => u.Email == dto.Email.Trim()); // busca o usuário pelo e-mail fornecido

            if (usuario is null)
            {
                return Results.Unauthorized();
            }

            var hasher = new PasswordHasher<Usuario>(); // instância do PasswordHasher para verificar a senha

            var resultado = hasher.VerifyHashedPassword(
                usuario,
                usuario.SenhaHash,
                dto.Senha);

            if (resultado == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
            {
                usuario.SenhaHash = hasher.HashPassword(usuario, dto.Senha);
                await db.SaveChangesAsync();
            }

            var claims = new[] // Cria uma lista de claims para o usuário autenticado(Claims são informações sobre o usuário, como ID, nome e e-mail)
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Email, usuario.Email)
            };

            var identidade = new ClaimsIdentity( // Papo de profissional aqui, ClaimsIdentity representa a identidade do usuário com base nas claims fornecidas
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidade),
                new AuthenticationProperties
                {
                    IsPersistent = false
                });
            //ASP.NET protege o cookie do usuario identificado pelo Claims então não precisamos de Hash nele
            return Results.Ok(
                new UsuarioRespostaDto(
                    usuario.Id,
                    usuario.Nome,
                    usuario.Email));
        }); // buceta de endpoint para autenticação 

        grupo.MapPost("/sair", async (HttpContext httpContext) => // Stackoverflow: Endpoint para sair (logout)
        {
            await httpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Results.NoContent();
        }).RequireAuthorization();

        if (app.Environment.IsDevelopment())
        {
            grupo.MapPost("/cadastro", async (
                CriarUsuarioDto dto, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(dto.Nome) ||
                    string.IsNullOrWhiteSpace(dto.Email) ||
                    string.IsNullOrWhiteSpace(dto.Senha) ||
                    dto.Senha.Length < 8)
                {
                    return Results.BadRequest(
                        "Informe nome, e-mail e senha com pelo menos 8 caracteres.");
                }

                var email = dto.Email.Trim();

                if (await db.Usuarios.AnyAsync(u => u.Email == email))
                {
                    return Results.Conflict("E-mail já cadastrado.");
                }

                var usuario = new Usuario
                {
                    Nome = dto.Nome.Trim(),
                    Email = email
                };

                var hasher = new PasswordHasher<Usuario>();
                usuario.SenhaHash = hasher.HashPassword(usuario, dto.Senha);

                db.Usuarios.Add(usuario);
                await db.SaveChangesAsync();

                return Results.Created(
                    $"/login/usuarios/{usuario.Id}",
                    new UsuarioRespostaDto(
                        usuario.Id, usuario.Nome, usuario.Email));
            }).AllowAnonymous();
        }

        return grupo;
    }
}