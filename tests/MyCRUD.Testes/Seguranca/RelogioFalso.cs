// Relógio controlado pelo teste: só avança quando o teste manda.

namespace MyCRUD.Testes.Seguranca;

/// <summary>
/// <see cref="TimeProvider"/> falso para a revalidação de sessão (R35). Substitui
/// <c>CookieAuthenticationOptions.TimeProvider</c>, o mesmo relógio que o handler do cookie
/// (IssuedUtc/ExpiresUtc) e <c>ValidacaoDeSessao</c> (<c>contexto.Options.TimeProvider</c>) usam.
/// </summary>
public sealed class RelogioFalso(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _agora = inicio;

    public override DateTimeOffset GetUtcNow() => _agora;

    /// <summary>Avança o relógio sem esperar.</summary>
    public void Avancar(TimeSpan tempo) => _agora += tempo;
}
