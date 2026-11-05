using System.ComponentModel.DataAnnotations;

namespace MyCRUD.Compartilhado;

/// <summary>
/// Filtro de endpoint que valida o DTO recebido usando os atributos
/// de <c>System.ComponentModel.DataAnnotations</c> (<c>[Required]</c>, <c>[MaxLength]</c>...).
/// </summary>
/// <remarks>
/// Roda ANTES do código do endpoint. Se o DTO for inválido, a requisição é
/// interrompida e o cliente recebe <c>400 Bad Request</c> no formato
/// <c>ValidationProblem</c>, com a lista de erros por campo:
/// <code>
/// { "status": 400, "errors": { "Nome": ["Informe o nome."] } }
/// </code>
/// Assim as regras de entrada ficam declaradas em um só lugar (o DTO)
/// e os endpoints não repetem <c>if</c>s de validação.
/// </remarks>
/// <typeparam name="T">Tipo do DTO que será validado.</typeparam>
public sealed class ValidacaoFiltro<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // Procura, entre os parâmetros do endpoint, o argumento do tipo T.
        var dto = context.Arguments.OfType<T>().FirstOrDefault();

        if (dto is null)
        {
            return TypedResults.Problem(
                title: "Corpo da requisição ausente.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resultados = new List<ValidationResult>();
        var valido = Validator.TryValidateObject(
            dto,
            new ValidationContext(dto),
            resultados,
            validateAllProperties: true);

        if (valido)
        {
            return await next(context);
        }

        // Agrupa as mensagens por nome do campo: { "Nome": [...], "Preco": [...] }
        var erros = resultados
            .SelectMany(
                r => r.MemberNames.DefaultIfEmpty(string.Empty),
                (r, campo) => (Campo: campo, Mensagem: r.ErrorMessage ?? "Valor inválido."))
            .GroupBy(e => e.Campo)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Mensagem).ToArray());

        return TypedResults.ValidationProblem(erros, title: "Dados inválidos.");
    }
}

/// <summary>
/// Atalho para aplicar <see cref="ValidacaoFiltro{T}"/> em um endpoint.
/// </summary>
public static class ValidacaoExtensions
{
    /// <summary>
    /// Valida o DTO <typeparamref name="T"/> antes de executar o endpoint
    /// e documenta no OpenAPI a possível resposta 400.
    /// </summary>
    /// <typeparam name="T">Tipo do DTO que será validado.</typeparam>
    /// <example><c>grupo.MapPost("/", Criar).ComValidacao&lt;CriarProdutoDto&gt;();</c></example>
    public static RouteHandlerBuilder ComValidacao<T>(this RouteHandlerBuilder builder)
        where T : class
    {
        return builder
            .AddEndpointFilter<ValidacaoFiltro<T>>()
            .ProducesValidationProblem();
    }
}
