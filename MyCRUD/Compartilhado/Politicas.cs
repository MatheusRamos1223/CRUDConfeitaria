using Microsoft.AspNetCore.Authorization;
using MyCRUD.Login;

namespace MyCRUD.Compartilhado;

/// <summary>
/// Políticas de autorização da API (o que cada papel pode fazer).
/// </summary>
/// <remarks>
/// <para>
/// <b>Autenticação</b> responde "quem é você?" (cookie válido → 401 se não houver).
/// <b>Autorização</b> responde "você pode fazer isto?" (papel certo → 403 se não tiver).
/// </para>
/// <para>
/// Uso em um endpoint:
/// <code>
/// grupo.MapDelete("/{id:int}", Desativar)
///     .RequireAuthorization(Politicas.SomenteAdmin)
///     .ProducesProblem(StatusCodes.Status403Forbidden);
/// </code>
/// </para>
/// <para>
/// A política lê a claim <c>ClaimTypes.Role</c> gravada no cookie durante o login.
/// A cada 5 min o cookie é conferido no banco (<c>Login.ValidacaoDeSessao</c>): se o papel
/// mudou, a sessão é encerrada e o usuário entra de novo já com o papel atual.
/// </para>
/// <para>
/// Esconder botões na tela (classe <c>so-admin</c>) é apenas conforto visual:
/// quem garante a regra é a API, aqui.
/// </para>
/// </remarks>
public static class Politicas
{
    /// <summary>
    /// Nome da política que só permite usuários com papel <see cref="Papel.Admin"/>.
    /// </summary>
    public const string SomenteAdmin = "SomenteAdmin";

    /// <summary>
    /// Registra o serviço de autorização com as políticas da aplicação.
    /// Substitui o <c>builder.Services.AddAuthorization()</c> do <c>Program.cs</c>.
    /// </summary>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    public static IServiceCollection AddPoliticasDeAcesso(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // RequireRole confere User.IsInRole("Admin"), isto é, a claim ClaimTypes.Role.
            .AddPolicy(SomenteAdmin, politica => politica.RequireRole(nameof(Papel.Admin)));

        return services;
    }
}
