using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Compartilhado;
using MyCRUD.Dados;

namespace MyCRUD.Clientes;

/// <summary>
/// Rotas HTTP do cadastro de clientes, agrupadas em <c>/clientes</c>.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader><term>Rota</term><description>Quem pode</description></listheader>
///   <item><term>GET, POST, PUT</term><description>Qualquer usuário logado (Operador atende o balcão).</description></item>
///   <item><term>DELETE</term><description>Somente Admin (política <see cref="Politicas.SomenteAdmin"/>); Operador recebe 403.</description></item>
/// </list>
/// Regras de negócio implementadas aqui: R18 (normalização), R19 (busca), R20 (exclusão).
/// </remarks>
public static class ClienteEndpoints
{
    public static RouteGroupBuilder MapClienteEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/clientes")
            .WithTags("Clientes")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/", Listar)
            .WithName("ListarClientes")
            .WithSummary("Lista clientes paginados, ordenados por nome. Busca por nome ou telefone.")
            .ComValidacao<FiltroClientes>();

        grupo.MapGet("/{id:int}", BuscarPorId)
            .WithName("BuscarCliente")
            .WithSummary("Busca um cliente pelo ID.");

        grupo.MapPost("/", Criar)
            .WithName("CriarCliente")
            .WithSummary("Cadastra um cliente. O telefone é salvo só com dígitos.")
            .ComValidacao<CriarClienteDto>();

        grupo.MapPut("/{id:int}", Atualizar)
            .WithName("AtualizarCliente")
            .WithSummary("Substitui todos os dados de um cliente existente.")
            .ComValidacao<AtualizarClienteDto>();

        // Política por rota: o grupo exige login; esta rota exige também o papel Admin.
        grupo.MapDelete("/{id:int}", Excluir)
            .WithName("ExcluirCliente")
            .WithSummary("Exclui um cliente sem pedidos (somente Admin).")
            .RequireAuthorization(Politicas.SomenteAdmin)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return grupo;
    }

    /// <summary>
    /// <c>GET /clientes?busca=&amp;pagina=&amp;tamanho=</c> — R19.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[AsParameters]</c> monta o record <see cref="FiltroClientes"/> a partir da query string;
    /// o <c>ValidacaoFiltro</c> já recusou página e tamanho fora dos limites (R4).
    /// </para>
    /// <para>
    /// A busca compara o termo com o nome (<c>ILIKE</c>, sem diferenciar maiúsculas) <b>ou</b>
    /// os dígitos do termo com o telefone. Como o telefone é salvo só com dígitos, buscar
    /// "(19) 9" encontra "19999990000". Um termo sem dígitos ("Maria") não compara telefone:
    /// sem isso, <c>Contains("")</c> seria verdadeiro para todos os clientes.
    /// </para>
    /// </remarks>
    private static async Task<Ok<ResultadoPaginado<ClienteRespostaDto>>> Listar(
        [AsParameters] FiltroClientes filtro, AppDbContext db, CancellationToken ct)
    {
        IQueryable<Cliente> consulta = db.Clientes.AsNoTracking();

        var termo = filtro.Busca?.Trim();

        if (!string.IsNullOrEmpty(termo))
        {
            // R5: % e _ são curingas do LIKE; escapados (ConsultaTexto), viram texto comum.
            var padraoNome = ConsultaTexto.PadraoContem(termo);
            var digitos = Cliente.NormalizarTelefone(termo);

            consulta = digitos.Length > 0
                ? consulta.Where(c =>
                    EF.Functions.ILike(c.Nome, padraoNome, ConsultaTexto.CaractereDeEscape)
                    || c.Telefone.Contains(digitos))
                : consulta.Where(c =>
                    EF.Functions.ILike(c.Nome, padraoNome, ConsultaTexto.CaractereDeEscape));
        }

        // R6: a ordenação termina em Id para dois clientes com o mesmo nome não
        // trocarem de lugar entre uma página e outra (nem aparecerem duas vezes).
        var pagina = await consulta
            .OrderBy(c => c.Nome)
            .ThenBy(c => c.Id)
            .Select(c => new ClienteRespostaDto(c.Id, c.Nome, c.Telefone, c.Email))
            .PaginarAsync(filtro.Pagina, filtro.Tamanho, ct);

        return TypedResults.Ok(pagina);
    }

    private static async Task<Results<Ok<ClienteRespostaDto>, NotFound>> BuscarPorId(
        int id, AppDbContext db, CancellationToken ct)
    {
        var cliente = await db.Clientes
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ClienteRespostaDto(c.Id, c.Nome, c.Telefone, c.Email))
            .FirstOrDefaultAsync(ct);

        return cliente is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(cliente);
    }

    private static async Task<Created<ClienteRespostaDto>> Criar(
        CriarClienteDto dto, AppDbContext db, CancellationToken ct)
    {
        // O ValidacaoFiltro já garantiu nome, telefone com 10–11 dígitos e e-mail válido.
        // Aqui só padronizamos o formato de gravação (R3, R18).
        var cliente = new Cliente
        {
            Nome = dto.Nome.Trim(),
            Telefone = Cliente.NormalizarTelefone(dto.Telefone),
            Email = Cliente.NormalizarEmail(dto.Email)
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct); // gera o INSERT e preenche cliente.Id

        return TypedResults.Created($"/clientes/{cliente.Id}", cliente.ParaResposta());
    }

    private static async Task<Results<Ok<ClienteRespostaDto>, NotFound>> Atualizar(
        int id, AtualizarClienteDto dto, AppDbContext db, CancellationToken ct)
    {
        // FindAsync carrega COM rastreamento: o SaveChanges gera UPDATE só do que mudou.
        var cliente = await db.Clientes.FindAsync(new object[] { id }, ct);

        if (cliente is null)
        {
            return TypedResults.NotFound();
        }

        cliente.Nome = dto.Nome.Trim();
        cliente.Telefone = Cliente.NormalizarTelefone(dto.Telefone);
        cliente.Email = Cliente.NormalizarEmail(dto.Email);

        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(cliente.ParaResposta());
    }

    /// <summary>
    /// <c>DELETE /clientes/{id}</c> — R20: só Admin; cliente com pedidos → 409.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A verificação com <c>AnyAsync</c> dá a mensagem amigável "Cliente possui pedidos.".
    /// Quem garante a regra de verdade é a chave estrangeira <c>Pedidos.ClienteId</c>
    /// (ON DELETE RESTRICT): se um pedido for criado entre a verificação e o DELETE,
    /// o banco recusa e o <c>TratadorGlobalDeExcecoes</c> devolve 409 "Registro em uso." (R7).
    /// </para>
    /// <para>
    /// Por isso o DELETE usa <c>Remove</c> + <c>SaveChangesAsync</c>, e não
    /// <c>ExecuteDeleteAsync</c>: assim a violação de FK chega embrulhada em
    /// <c>DbUpdateException</c>, o formato que o tratador global reconhece.
    /// </para>
    /// </remarks>
    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Excluir(
        int id, AppDbContext db, CancellationToken ct)
    {
        var cliente = await db.Clientes.FindAsync(new object[] { id }, ct);

        if (cliente is null)
        {
            return TypedResults.NotFound();
        }

        var possuiPedidos = await db.Pedidos.AnyAsync(p => p.ClienteId == id, ct);

        if (possuiPedidos)
        {
            return TypedResults.Problem(
                title: "Cliente possui pedidos.",
                detail: "Clientes com pedidos não podem ser excluídos, para preservar o histórico de vendas.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.Clientes.Remove(cliente);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // O DELETE afetou 0 linhas: outro Admin excluiu o mesmo cliente
            // entre o FindAsync e o SaveChanges. Para quem pediu, ele não existe mais.
            return TypedResults.NotFound();
        }

        return TypedResults.NoContent();
    }
}
