namespace MyCRUD.Compartilhado;

/// <summary>
/// Monta o padrão de busca "contém" usado pelos filtros de texto das listagens (R5).
/// </summary>
/// <remarks>
/// <para>
/// Todas as telas filtram do mesmo jeito: "contém", sem diferenciar maiúsculas, com
/// <c>EF.Functions.ILike</c> (o <c>LIKE</c> do PostgreSQL que ignora maiúsculas):
/// </para>
/// <code>
/// var padrao = ConsultaTexto.PadraoContem(filtro.Nome);
/// consulta = consulta.Where(p =&gt; EF.Functions.ILike(p.Nome, padrao, ConsultaTexto.CaractereDeEscape));
/// </code>
/// <para>
/// No <c>LIKE</c>, <c>%</c> significa "qualquer sequência" e <c>_</c> "qualquer caractere".
/// Sem escapar, buscar <c>"50%"</c> acharia <c>"500 g"</c>, e <c>"_"</c> acharia tudo.
/// Este helper faz o texto digitado pelo usuário valer literalmente.
/// </para>
/// <para>
/// O termo vai ao banco como <b>parâmetro</b> (<c>@padrao</c>), nunca concatenado no SQL:
/// o escape aqui é sobre o significado dos curingas, não sobre injeção de SQL.
/// </para>
/// </remarks>
public static class ConsultaTexto
{
    /// <summary>
    /// Caractere de escape informado ao <c>ILIKE ... ESCAPE '\'</c>.
    /// </summary>
    /// <remarks>
    /// É também o escape padrão do PostgreSQL, mas passá-lo explicitamente
    /// deixa a consulta correta mesmo que essa configuração mude.
    /// </remarks>
    public const string CaractereDeEscape = "\\";

    /// <summary>
    /// Faz <c>%</c>, <c>_</c> e <c>\</c> digitados pelo usuário valerem como texto comum.
    /// </summary>
    /// <remarks>
    /// A barra é escapada primeiro; senão as barras inseridas antes de <c>%</c> e <c>_</c>
    /// seriam escapadas de novo.
    /// </remarks>
    /// <param name="termo">Texto digitado pelo usuário.</param>
    /// <returns>O mesmo texto, com os curingas precedidos de <see cref="CaractereDeEscape"/>.</returns>
    public static string EscaparCuringas(string termo) =>
        termo
            .Replace(CaractereDeEscape, CaractereDeEscape + CaractereDeEscape)
            .Replace("%", CaractereDeEscape + "%")
            .Replace("_", CaractereDeEscape + "_");

    /// <summary>
    /// Padrão "contém" para o <c>ILIKE</c>: <c>"bolo"</c> vira <c>"%bolo%"</c>.
    /// </summary>
    /// <remarks>Remove os espaços das pontas (R3) e escapa os curingas.</remarks>
    /// <param name="termo">Texto digitado pelo usuário (não vazio; quem chama ignora filtro vazio).</param>
    public static string PadraoContem(string termo) =>
        $"%{EscaparCuringas(termo.Trim())}%";
}
