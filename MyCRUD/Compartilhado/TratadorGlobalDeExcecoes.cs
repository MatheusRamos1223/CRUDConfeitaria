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
        var (status, titulo) = exception switch
        {
            BadHttpRequestException erro =>
                (erro.StatusCode, "Requisição inválida."),

            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Registro duplicado."),

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
}
