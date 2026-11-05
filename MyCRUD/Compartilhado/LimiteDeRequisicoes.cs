using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MyCRUD.Compartilhado;

/// <summary>
/// Configura o limitador de requisições nativo do ASP.NET Core.
/// </summary>
/// <remarks>
/// <para>
/// Problema: sem limite, um script pode testar milhares de senhas por minuto
/// contra <c>POST /login</c> (ataque de força bruta).
/// </para>
/// <para>
/// Solução: janela fixa de <see cref="TentativasPorMinuto"/> requisições por minuto,
/// contadas separadamente para cada endereço IP. A requisição excedente recebe
/// <c>429 Too Many Requests</c> e não chega ao banco.
/// </para>
/// </remarks>
public static class LimiteDeRequisicoes
{
    /// <summary>Nome da política, usado em <c>.RequireRateLimiting(...)</c>.</summary>
    public const string PoliticaAutenticacao = "autenticacao";

    /// <summary>Requisições permitidas por IP a cada minuto.</summary>
    public const int TentativasPorMinuto = 5;

    public static IServiceCollection AddLimiteDeRequisicoes(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PoliticaAutenticacao, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = TentativasPorMinuto,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0 // não enfileira: recusa na hora
                    }));
        });
    }
}
