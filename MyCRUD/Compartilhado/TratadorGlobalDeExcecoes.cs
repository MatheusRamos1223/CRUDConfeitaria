using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MyCRUD.Compartilhado;

/// <summary>
/// Último ponto de defesa: transforma qualquer exceção não tratada em uma
/// resposta HTTP padronizada (Problem Details), sem vazar detalhes internos.
/// </summary>
/// <remarks>
/// Sem este tratador, um erro inesperado devolveria uma página HTML com
/// stack trace (em desenvolvimento) ou um 500 vazio (em produção).
/// <list type="table">
///   <listheader><term>Exceção</term><description>Resposta</description></listheader>
///   <item><term><see cref="BadHttpRequestException"/></term><description>400 — JSON malformado, tipo errado no corpo.</description></item>
///   <item><term>Violação de índice único no PostgreSQL</term><description>409 — ex.: dois cadastros simultâneos com o mesmo e-mail.</description></item>
///   <item><term>Violação de chave estrangeira no PostgreSQL</term><description>409 — ex.: categoria excluída enquanto outro usuário ligava um produto a ela (R7).</description></item>
///   <item><term>Qualquer outra</term><description>500 — registrada no log com stack trace.</description></item>
/// </list>
/// A mensagem técnica (<c>detail</c>) só aparece em Development.
/// </remarks>
public sealed class TratadorGlobalDeExcecoes(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment ambiente,
    ILogger<TratadorGlobalDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Tupla (exceção, código SQLSTATE do PostgreSQL ou null): permite decidir
        // pelo tipo da exceção E pelo código do erro do banco no mesmo switch.
        var (status, titulo) = (exception, ErroDoPostgres(exception)?.SqlState) switch
        {
            (BadHttpRequestException erro, _) =>
                (erro.StatusCode, "Requisição inválida."),

            // 23505: dois registros com o mesmo valor em um índice único.
            (_, PostgresErrorCodes.UniqueViolation) =>
                (StatusCodes.Status409Conflict, "Registro duplicado."),

            // 23503: a chave estrangeira recusou a operação — o registro é referenciado
            // por outro (ex.: categoria com produto) ou aponta para um que não existe mais.
            // É a "corrida" entre a verificação do endpoint e a gravação (R7).
            (_, PostgresErrorCodes.ForeignKeyViolation) =>
                (StatusCodes.Status409Conflict, "Registro em uso."),

            _ => (StatusCodes.Status500InternalServerError, "Erro interno no servidor.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado em {Metodo} {Caminho}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Requisição recusada com {Status}: {Mensagem}",
                status, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = ambiente.IsDevelopment() ? exception.Message : null
            }
        });
    }

    /// <summary>
    /// Extrai o erro do PostgreSQL de dentro da exceção, quando houver.
    /// </summary>
    /// <remarks>
    /// O erro do banco chega de dois jeitos:
    /// <list type="bullet">
    ///   <item><c>SaveChangesAsync</c> embrulha o erro em <see cref="DbUpdateException"/>
    ///   (o <see cref="PostgresException"/> fica em <c>InnerException</c>).</item>
    ///   <item><c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c> executam o comando
    ///   direto, sem esse embrulho: o <see cref="PostgresException"/> chega sozinho.</item>
    /// </list>
    /// </remarks>
    private static PostgresException? ErroDoPostgres(Exception exception) =>
        exception switch
        {
            PostgresException erro => erro,
            DbUpdateException { InnerException: PostgresException erro } => erro,
            _ => null
        };
}
