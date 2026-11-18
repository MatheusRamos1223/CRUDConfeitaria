using Microsoft.AspNetCore.Mvc;

namespace MyCRUD.Compartilhado;

/// <summary>
/// Defesa contra CSRF (Cross-Site Request Forgery) por cabeçalho personalizado (R44).
/// </summary>
/// <remarks>
/// <para>
/// <b>O ataque.</b> Um site malicioso aberto no mesmo navegador monta um formulário que envia
/// <c>POST /pedidos/7/cancelar</c> para cá. O navegador anexa o cookie de sessão e a ação é
/// executada em nome da vítima. <c>SameSite=Lax</c> já barra POST vindo de <b>outro site</b>,
/// mas não de um subdomínio irmão (mesmo "site") nem navegadores antigos.
/// </para>
/// <para>
/// <b>A defesa.</b> Toda requisição que altera dados (<c>POST</c>, <c>PUT</c>, <c>PATCH</c>,
/// <c>DELETE</c>) precisa do cabeçalho <c>X-Requested-With: fetch</c>:
/// </para>
/// <list type="bullet">
///   <item>Um <c>&lt;form&gt;</c> HTML não consegue acrescentar cabeçalhos.</item>
///   <item>Um <c>fetch()</c> de outra origem com cabeçalho personalizado dispara um
///   <i>preflight</i> CORS (<c>OPTIONS</c>), e esta API não habilita CORS: o navegador nem envia o POST.</item>
/// </list>
/// <para>
/// As telas enviam o cabeçalho pela função <c>api()</c> de <c>js/api.js</c>; no PowerShell e no
/// Scalar ele é acrescentado à mão. <c>GET</c>/<c>HEAD</c> não precisam: não alteram dados.
/// </para>
/// <para>
/// Sem o cabeçalho: <c>403</c> Problem Details "Requisição sem o cabeçalho X-Requested-With.",
/// antes da autenticação (não consulta o banco nem o cookie).
/// </para>
/// </remarks>
public static class ProtecaoCsrf
{
    /// <summary>Nome do cabeçalho exigido.</summary>
    public const string NomeCabecalho = "X-Requested-With";

    /// <summary>Valor exigido (comparação exata, diferenciando maiúsculas).</summary>
    public const string ValorCabecalho = "fetch";

    /// <summary>Registra o middleware. Fica antes de <c>UseAuthentication</c>.</summary>
    /// <param name="app">Aplicação.</param>
    /// <returns>A mesma aplicação, para encadear chamadas.</returns>
    public static IApplicationBuilder UseProtecaoCsrf(this IApplicationBuilder app) =>
        app.Use(async (contexto, proximo) =>
        {
            if (AlteraDados(contexto.Request.Method)
                && !string.Equals(contexto.Request.Headers[NomeCabecalho], ValorCabecalho, StringComparison.Ordinal))
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;

                var problemDetails = contexto.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = contexto,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = $"Requisição sem o cabeçalho {NomeCabecalho}."
                    }
                });
                return;
            }

            await proximo(contexto);
        });

    /// <summary>Métodos que alteram estado e, por isso, exigem o cabeçalho.</summary>
    private static bool AlteraDados(string metodo) =>
        HttpMethods.IsPost(metodo)
        || HttpMethods.IsPut(metodo)
        || HttpMethods.IsPatch(metodo)
        || HttpMethods.IsDelete(metodo);
}
