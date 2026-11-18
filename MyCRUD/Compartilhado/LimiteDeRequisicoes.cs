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
/// Solução: janela fixa de N requisições por minuto, contadas separadamente para cada
/// endereço IP. A requisição excedente recebe <c>429 Too Many Requests</c> e não chega ao banco.
/// </para>
/// <para>
/// <b>N vem da configuração</b> <c>LimiteDeRequisicoes:TentativasPorMinuto</c>
/// (padrão <see cref="TentativasPorMinutoPadrao"/>). Os testes de integração (Fase 3) sobem a API
/// num servidor em memória em que todas as requisições têm o mesmo IP (nulo → "desconhecido"):
/// com 5 fixo, o 6º login da suíte inteira levaria 429. Lá o valor é aumentado.
/// </para>
/// <para>
/// <b>Limite conhecido:</b> a partição é <c>Connection.RemoteIpAddress</c>. Atrás de um proxy
/// reverso (Nginx, Cloudflare...) todos os clientes chegam com o IP do proxy e dividem o mesmo
/// limite. O projeto não usa <c>UseForwardedHeaders</c> de propósito: confiar em
/// <c>X-Forwarded-For</c> sem saber quem é o proxy deixaria qualquer cliente inventar o próprio IP.
/// </para>
/// </remarks>
public static class LimiteDeRequisicoes
{
    /// <summary>Nome da política, usado em <c>.RequireRateLimiting(...)</c>.</summary>
    public const string PoliticaAutenticacao = "autenticacao";

    /// <summary>Chave de configuração com o limite por IP e por minuto.</summary>
    public const string ChaveTentativasPorMinuto = "LimiteDeRequisicoes:TentativasPorMinuto";

    /// <summary>Requisições permitidas por IP a cada minuto quando a configuração não diz outra coisa.</summary>
    public const int TentativasPorMinutoPadrao = 5;

    /// <summary>Registra o limitador com a política <see cref="PoliticaAutenticacao"/>.</summary>
    /// <remarks>
    /// A configuração é lida <b>na primeira vez que as opções são usadas</b> (quando o pipeline
    /// é montado), não no <c>Program.cs</c>: assim um valor definido pela fábrica de testes
    /// (<c>UseSetting</c>) já está visível.
    /// </remarks>
    /// <param name="services">Coleção de serviços da aplicação.</param>
    /// <returns>A mesma coleção, para encadear chamadas.</returns>
    /// <exception cref="InvalidOperationException">Limite configurado menor que 1.</exception>
    public static IServiceCollection AddLimiteDeRequisicoes(this IServiceCollection services)
    {
        // Registra os serviços do limitador; a política é configurada logo abaixo,
        // quando a configuração completa já existe.
        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IConfiguration>((options, configuracao) =>
            {
                var tentativas = configuracao.GetValue(ChaveTentativasPorMinuto, TentativasPorMinutoPadrao);

                if (tentativas < 1)
                {
                    throw new InvalidOperationException(
                        $"{ChaveTentativasPorMinuto} deve ser 1 ou maior (valor atual: {tentativas}).");
                }

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(PoliticaAutenticacao, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = tentativas,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0 // não enfileira: recusa na hora
                        }));
            });

        return services;
    }
}
