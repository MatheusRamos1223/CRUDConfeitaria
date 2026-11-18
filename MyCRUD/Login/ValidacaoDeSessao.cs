using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Compartilhado;
using MyCRUD.Dados;

namespace MyCRUD.Login;

/// <summary>
/// Revalida o cookie de sessão no banco a cada <see cref="Intervalo"/> (R35).
/// Ligado em <c>Program.cs</c>: <c>options.Events.OnValidatePrincipal = ValidacaoDeSessao.ValidarAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Problema.</b> O cookie guarda o papel do momento do login e vale por até 2 h. Sem
/// revalidação, um Admin rebaixado por SQL — ou um usuário apagado — continuaria agindo
/// até o cookie expirar.
/// </para>
/// <para>
/// <b>Solução.</b> A cada requisição autenticada o ASP.NET chama este evento. Se o cookie
/// foi emitido (ou renovado) há menos de 5 min, nada é feito. Senão, uma consulta curta pela
/// chave primária confere se o usuário ainda existe e se o papel do banco é o mesmo da claim:
/// </para>
/// <list type="bullet">
///   <item>Confere: o cookie é renovado (<c>ShouldRenew</c>), o que zera o relógio dos 5 min.</item>
///   <item>Não confere: <c>RejectPrincipal</c> + <c>SignOutAsync</c>. A requisição segue como
///   anônima (401 nas rotas de dados) e o navegador recebe o cookie apagado.</item>
/// </list>
/// <para>
/// <b>Por que rejeitar em vez de trocar o papel (<c>ReplacePrincipal</c>)?</b> Mudança de papel
/// é evento de segurança: encerrar a sessão é o caminho conservador e o mais simples de
/// explicar ("seu acesso mudou, entre de novo"). No novo login todas as claims (nome, e-mail,
/// papel) são lidas do banco, sem código paralelo para remontá-las aqui. A promoção normal
/// (Admin:Email) já acontece no login, então não gera divergência.
/// </para>
/// <para>
/// <b>Custo.</b> No máximo 1 <c>SELECT "Papel" FROM "Usuarios" WHERE "Id" = @id</c> a cada
/// 5 min por sessão ativa. A renovação também desliza a validade de 2 h a partir do último uso,
/// como o <c>SlidingExpiration</c> (ligado por padrão) já fazia.
/// </para>
/// </remarks>
public static class ValidacaoDeSessao
{
    /// <summary>Tempo máximo que uma mudança de papel ou exclusão de usuário leva para valer.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    /// <summary>Handler do evento <c>CookieAuthenticationEvents.OnValidatePrincipal</c>.</summary>
    /// <param name="contexto">Cookie já descriptografado, com o usuário e as propriedades do ticket.</param>
    public static async Task ValidarAsync(CookieValidatePrincipalContext contexto)
    {
        // IssuedUtc = quando o cookie foi emitido ou renovado pela última vez.
        var emitidoEm = contexto.Properties.IssuedUtc;
        if (emitidoEm is { } emitido && DateTimeOffset.UtcNow - emitido < Intervalo)
        {
            return;
        }

        var principal = contexto.Principal;
        var usuarioId = principal?.TentarObterUsuarioId();

        Papel? papelNoBanco = null;
        if (usuarioId is { } id)
        {
            // AppDbContext é "scoped": vem do escopo desta requisição.
            var db = contexto.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            // Cast para Papel?: usuário inexistente vira null (e não Operador, o valor 0 do enum).
            papelNoBanco = await db.Usuarios
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => (Papel?)u.Papel)
                .FirstOrDefaultAsync(contexto.HttpContext.RequestAborted);
        }

        var papelNoCookie = principal?.FindFirstValue(ClaimTypes.Role);

        if (papelNoBanco is { } papel && papel.ToString() == papelNoCookie)
        {
            // Tudo certo: renova o cookie para registrar a nova conferência (IssuedUtc = agora).
            contexto.ShouldRenew = true;
            return;
        }

        // Usuário apagado, papel diferente ou cookie sem Id/papel válidos: encerra a sessão.
        var logger = contexto.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ValidacaoDeSessao));

        logger.LogInformation(
            "Sessão do usuário {UsuarioId} encerrada na revalidação: {Motivo}.",
            usuarioId,
            papelNoBanco is null ? "usuário não encontrado" : "papel mudou");

        contexto.RejectPrincipal();
        await contexto.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
