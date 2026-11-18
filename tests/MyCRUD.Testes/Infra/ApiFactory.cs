// Fábrica da API para os testes de integração.
//
// COMO FUNCIONA
//   - Um único contêiner PostgreSQL (postgres:17-alpine) por execução de testes, compartilhado
//     por todas as fábricas (PostgresCompartilhado). Cada fábrica cria o PRÓPRIO banco dentro dele,
//     então a variante Development e a de limite 5 não enxergam os dados uma da outra.
//   - A coleção "Api" (ApiColecao) dá UMA ApiFactory a todas as classes de teste marcadas com
//     [Collection("Api")]. Testes que precisam de outra configuração usam IClassFixture com uma
//     das variantes abaixo (ApiFactoryDevelopment, ApiFactoryLimite5).
//   - O banco é criado só por Database.MigrateAsync() (nunca EnsureCreated): assim valem as
//     migrations de verdade, com índice lower("Nome"), CHECKs e RLS.
//
// O QUE DEPENDE DO AMBIENTE NO Program.cs (conferido na leitura do código)
//   Ambiente padrão dos testes = "Testing" (qualquer coisa diferente de "Development"):
//   - Cookie Confeitaria.Auth: Secure = Always (só em Development é SameAsRequest). Por isso o
//     HttpClient usa https://localhost; em http o cookie não seria reenviado.
//   - UseHsts + UseHttpsRedirection: ligados. Com https na BaseAddress não há redirecionamento.
//   - POST /login/cadastro: NÃO existe (só mapeado em Development). Usuários são criados no banco.
//   - /scalar e /openapi/v1.json: NÃO existem (só em Development). Respostas 404.
//   - TratadorGlobalDeExcecoes: o "detail" da exceção só aparece em Development.
//   Ambiente "Development" (ApiFactoryDevelopment): cadastro, Scalar e OpenAPI existem, cookie
//   aceita http, sem HSTS/redirecionamento.
//
// LIMITE DE REQUISIÇÕES (R47)
//   No TestServer o IP remoto é nulo: todas as requisições caem na mesma partição, então o limite
//   padrão (5) estouraria no 6º login da suíte. Fábrica padrão usa 1000; ApiFactoryLimite5 usa 5.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MyCRUD.Dados;
using MyCRUD.Login;
using Testcontainers.PostgreSql;

namespace MyCRUD.Testes.Infra;

/// <summary>
/// Sobe a API real (em memória) ligada a um banco PostgreSQL migrado.
/// Uma instância por coleção "Api" = um banco por execução.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _ambiente;
    private readonly int _tentativasPorMinuto;
    private string _stringDeConexao = string.Empty;

    private readonly Lazy<Task<HttpClient>> _admin;
    private readonly Lazy<Task<HttpClient>> _operador;

    /// <summary>Construtor usado pela coleção "Api": ambiente Testing, limite 1000.</summary>
    public ApiFactory() : this("Testing", 1000)
    {
    }

    /// <summary>Para as variantes: define ambiente e limite de requisições por minuto.</summary>
    protected ApiFactory(string ambiente, int tentativasPorMinuto)
    {
        _ambiente = ambiente;
        _tentativasPorMinuto = tentativasPorMinuto;
        _admin = new Lazy<Task<HttpClient>>(() => this.LogarComoAdminConfiguradoAsync());
        _operador = new Lazy<Task<HttpClient>>(async () =>
            await this.LogarComoAsync(await this.CriarUsuarioAsync(Papel.Operador)));
    }

    /// <summary>Cliente já logado como o Admin configurado em Admin:Email (compartilhado na fábrica).</summary>
    public Task<HttpClient> AdminAsync() => _admin.Value;

    /// <summary>Cliente já logado como um Operador novo (compartilhado na fábrica).</summary>
    public Task<HttpClient> OperadorAsync() => _operador.Value;

    /// <summary>Executa algo com um <see cref="AppDbContext"/> novo (escopo próprio), direto no banco de testes.</summary>
    public async Task<T> ComBancoAsync<T>(Func<AppDbContext, Task<T>> acao)
    {
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return await acao(db);
    }

    /// <summary>Sobe o contêiner (se preciso), cria um banco exclusivo e aplica as migrations.</summary>
    async Task IAsyncLifetime.InitializeAsync()
    {
        _stringDeConexao = await PostgresCompartilhado.CriarBancoAsync();

        // Services monta o host (ConfigureWebHost abaixo já enxerga a conexão).
        using var escopo = Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    // O host é descartado por WebApplicationFactory.DisposeAsync/Dispose; o contêiner é
    // removido ao fim do processo (Ryuk, do Testcontainers). Nada a fazer aqui.
    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_ambiente);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _stringDeConexao);
        builder.UseSetting("Admin:Email", Cenario.EmailAdmin);
        builder.UseSetting("LimiteDeRequisicoes:TentativasPorMinuto", _tentativasPorMinuto.ToString());
    }
}

/// <summary>Variante em ambiente Development: cadastro (R37, R42), Scalar/OpenAPI, sem HSTS.</summary>
public sealed class ApiFactoryDevelopment : ApiFactory
{
    public ApiFactoryDevelopment() : base("Development", 1000)
    {
    }
}

/// <summary>Variante com o limite padrão de 5 tentativas por minuto (R47). Use só para o teste de 429.</summary>
public sealed class ApiFactoryLimite5 : ApiFactory
{
    public ApiFactoryLimite5() : base("Testing", 5)
    {
    }
}

/// <summary>Registra a coleção "Api": todas as classes com [Collection("Api")] dividem uma ApiFactory.</summary>
[CollectionDefinition("Api")]
public sealed class ApiColecao : ICollectionFixture<ApiFactory>
{
}

/// <summary>
/// Contêiner PostgreSQL único do processo de teste. Cada fábrica pede um banco novo dentro dele.
/// </summary>
internal static class PostgresCompartilhado
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static PostgreSqlContainer? _container;

    public static async Task<string> CriarBancoAsync()
    {
        await Trava.WaitAsync();
        try
        {
            if (_container is null)
            {
                var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await container.StartAsync();
                _container = container;
            }
        }
        finally
        {
            Trava.Release();
        }

        var nome = $"teste_{Guid.NewGuid():N}";

        await using (var conexao = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await conexao.OpenAsync();
            await using var comando = new NpgsqlCommand($"CREATE DATABASE \"{nome}\"", conexao);
            await comando.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = nome }
            .ConnectionString;
    }
}
