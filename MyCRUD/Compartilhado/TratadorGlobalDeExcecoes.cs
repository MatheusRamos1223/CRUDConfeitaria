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
///   <item><term>Violação de CHECK no PostgreSQL</term><description>400 — um valor passou pela validação da API mas não pelas regras do banco (ex.: e-mail com letra que o <c>lower()</c> do banco converte diferente do .NET). Repetir a mesma requisição não resolve, por isso 400 e não 409.</description></item>
///   <item><term>Qualquer outra</term><description>500 — registrada no log com stack trace.</description></item>
/// </list>
/// <para>A mensagem técnica (<c>detail</c>) só aparece em Development.</para>
/// <para>
/// <b>Log das recusas (4xx):</b> só tipo da exceção, status e código SQLSTATE — nunca
/// <c>exception.Message</c>. A mensagem do PostgreSQL inclui o valor recusado
/// (<c>Key ("Email")=(ana@x.com) already exists</c>), e log não é lugar de dado pessoal.
/// </para>
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

            // 23514: um CHECK do banco recusou o valor (ex.: CK_Clientes_Email, CK_Produtos_Preco).
            (_, PostgresErrorCodes.CheckViolation) =>
                (StatusCodes.Status400BadRequest, "Dados recusados por uma regra do banco."),

            _ => (StatusCodes.Status500InternalServerError, "Erro interno no servidor.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado em {Metodo} {Caminho}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            // Sem exception.Message: ela pode conter o valor recusado (e-mail, nome...).
            logger.LogWarning(
                "Requisição recusada com {Status} ({TipoExcecao}, SQLSTATE {SqlState}) em {Metodo} {Caminho}",
                status,
                exception.GetType().Name,
                ErroDoPostgres(exception)?.SqlState,
                httpContext.Request.Method,
                httpContext.Request.Path);
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
