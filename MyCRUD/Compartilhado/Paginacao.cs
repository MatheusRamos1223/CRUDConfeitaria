using Microsoft.EntityFrameworkCore;

namespace MyCRUD.Compartilhado;

/// <summary>
/// Envelope padrão de toda listagem da API.
/// </summary>
/// <remarks>
/// Em vez de devolver um array solto, as rotas de lista devolvem a página pedida
/// junto com os dados necessários para montar o paginador na tela:
/// <code>
/// { "itens": [...], "pagina": 2, "tamanhoPagina": 20, "total": 57, "totalPaginas": 3 }
/// </code>
/// </remarks>
/// <typeparam name="T">Tipo de cada item (sempre um DTO de resposta, nunca a entidade).</typeparam>
/// <param name="Itens">Registros da página atual.</param>
/// <param name="Pagina">Número da página, começando em 1.</param>
/// <param name="TamanhoPagina">Quantidade máxima de itens por página.</param>
/// <param name="Total">Total de registros que atendem ao filtro, em todas as páginas.</param>
public record ResultadoPaginado<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int Total)
{
    /// <summary>Quantidade de páginas; 0 quando não há registros.</summary>
    public int TotalPaginas => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)TamanhoPagina);
}

/// <summary>
/// Limites de paginação e extensão que pagina uma consulta do Entity Framework.
/// </summary>
public static class Paginacao
{
    /// <summary>Itens por página quando o cliente não informa <c>tamanho</c>.</summary>
    public const int TamanhoPadrao = 20;

    /// <summary>
    /// Maior página aceita. Impede que alguém peça <c>tamanho=1000000</c>
    /// e obrigue o banco a devolver a tabela inteira de uma vez.
    /// </summary>
    public const int TamanhoMaximo = 100;

    /// <summary>
    /// Maior número de página aceito (R4). Sem teto, <c>pagina=2147483647</c> faria
    /// <c>(pagina - 1) * tamanho</c> estourar o <c>int</c> e virar um OFFSET negativo (erro 500).
    /// Com 1.000.000 × 100 o cálculo cabe folgado no <c>int</c>.
    /// </summary>
    public const int PaginaMaxima = 1_000_000;

    /// <summary>
    /// Conta o total e busca apenas os registros da página pedida.
    /// </summary>
    /// <remarks>
    /// Gera duas consultas: <c>SELECT count(*)</c> e <c>SELECT ... OFFSET x LIMIT y</c>.
    /// A consulta recebida <b>já deve estar ordenada</b>; sem <c>OrderBy</c> o banco
    /// não garante a ordem e um registro poderia aparecer em duas páginas.
    /// </remarks>
    public static async Task<ResultadoPaginado<T>> PaginarAsync<T>(
        this IQueryable<T> consulta,
        int pagina,
        int tamanho,
        CancellationToken ct = default)
    {
        var total = await consulta.CountAsync(ct);

        var itens = await consulta
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(ct);

        return new ResultadoPaginado<T>(itens, pagina, tamanho, total);
    }
}
