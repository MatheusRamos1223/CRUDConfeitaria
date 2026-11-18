using System.Security.Claims;

namespace MyCRUD.Compartilhado;

/// <summary>
/// Leitura do usuário logado a partir das claims do cookie.
/// </summary>
/// <remarks>
/// <para>
/// O login grava o Id do usuário na claim <see cref="ClaimTypes.NameIdentifier"/>
/// (<c>LoginEndpoints.Entrar</c>). Endpoints que precisam saber "quem fez"
/// (auditoria de pedidos, R40) leem daqui, nunca de um campo do corpo da requisição:
/// o cookie é assinado, o corpo o cliente escreve como quiser.
/// </para>
/// <code>
/// var usuarioId = user.ObterUsuarioId();
/// </code>
/// </remarks>
public static class UsuarioLogado
{
    /// <summary>
    /// Id do usuário da claim <see cref="ClaimTypes.NameIdentifier"/>, ou <c>null</c> se faltar
    /// ou não for número (cookie anônimo ou de outro formato).
    /// </summary>
    /// <param name="usuario">Usuário da requisição (<c>HttpContext.User</c>).</param>
    public static int? TentarObterUsuarioId(this ClaimsPrincipal usuario) =>
        int.TryParse(usuario.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// Id do usuário logado, para rotas que exigem login (<c>RequireAuthorization</c>).
    /// </summary>
    /// <remarks>
    /// Numa rota protegida a claim sempre existe: o cookie só é aceito se a revalidação
    /// periódica (<c>ValidacaoDeSessao</c>) encontrou o usuário no banco. Se mesmo assim
    /// faltar, é erro de programação (rota sem <c>RequireAuthorization</c>) e vira 500.
    /// </remarks>
    /// <param name="usuario">Usuário da requisição (<c>HttpContext.User</c>).</param>
    /// <exception cref="InvalidOperationException">A claim não existe ou não é um número.</exception>
    public static int ObterUsuarioId(this ClaimsPrincipal usuario) =>
        usuario.TentarObterUsuarioId()
        ?? throw new InvalidOperationException(
            "Rota sem usuário autenticado: falta a claim NameIdentifier.");
}
