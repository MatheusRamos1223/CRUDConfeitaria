using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Compartilhado;
using MyCRUD.Dados;
using MyCRUD.Login;

namespace MyCRUD.Pedidos;

/// <summary>
/// Rotas HTTP de pedidos, agrupadas em <c>/pedidos</c>.
/// </summary>
/// <remarks>
/// <para>Todas exigem login. Não existe <c>PUT</c> (pedido é imutável, D5) nem
/// <c>DELETE</c> (pedido nunca é excluído, R31).</para>
/// <para>
/// O status muda por <b>rotas de ação</b> (<c>/confirmar</c>, <c>/entregar</c>,
/// <c>/cancelar</c>) em vez de um <c>PATCH</c> genérico: cada rota sabe de quais
/// status pode partir e quem pode chamá-la, e devolve 409 com uma frase clara
/// quando a transição não vale.
/// </para>
/// </remarks>
public static class PedidoEndpoints
{
    public static RouteGroupBuilder MapPedidoEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/pedidos")
            .WithTags("Pedidos")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/", Listar)
            .WithName("ListarPedidos")
            .WithSummary("Lista pedidos com filtro e paginação, ordenados por data de entrega.")
            .ComValidacao<FiltroPedidos>();

        grupo.MapGet("/{id:int}", BuscarPorId)
            .WithName("BuscarPedido")
            .WithSummary("Busca um pedido com cliente, itens, preços congelados e total.");

        grupo.MapPost("/", Criar)
            .WithName("CriarPedido")
            .WithSummary("Cria um pedido Aberto. Os preços são lidos do cadastro e ficam congelados.")
            .ComValidacao<CriarPedidoDto>();

        grupo.MapPost("/{id:int}/confirmar", Confirmar)
            .WithName("ConfirmarPedido")
            .WithSummary("Aberto → Confirmado. Qualquer usuário logado.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:int}/entregar", Entregar)
            .WithName("EntregarPedido")
            .WithSummary("Confirmado → Entregue. Qualquer usuário logado.")
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:int}/cancelar", Cancelar)
            .WithName("CancelarPedido")
            .WithSummary("Aberto → Cancelado (qualquer usuário) ou Confirmado → Cancelado (só Admin).")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return grupo;
    }

    // -----------------------------------------------------------------------
    // Consultas
    // -----------------------------------------------------------------------

    private static async Task<Ok<ResultadoPaginado<PedidoResumoDto>>> Listar(
        [AsParameters] FiltroPedidos filtro, AppDbContext db, CancellationToken ct)
    {
        // A consulta é montada aos poucos: cada filtro preenchido acrescenta um WHERE.
        // Nada vai ao banco até o PaginarAsync (IQueryable é só a "receita" do SQL).
        var consulta = db.Pedidos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var padrao = $"%{EscaparLike(filtro.Busca.Trim())}%";
            consulta = consulta.Where(p => EF.Functions.ILike(p.Cliente!.Nome, padrao, "\\"));
        }

        if (filtro.Status is { } status)
        {
            consulta = consulta.Where(p => p.Status == status);
        }

        if (filtro.De is { } de)
        {
            consulta = consulta.Where(p => p.DataEntrega >= de);
        }

        if (filtro.Ate is { } ate)
        {
            consulta = consulta.Where(p => p.DataEntrega <= ate);
        }

        // Ordem DataEntrega, Id: o Id desempata pedidos do mesmo dia, para a
        // paginação não repetir nem pular registros (R6).
        // As somas viram subconsultas SQL (SUM): o total nunca é armazenado (R25).
        var pagina = await consulta
            .OrderBy(p => p.DataEntrega)
            .ThenBy(p => p.Id)
            .Select(p => new PedidoResumoDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nome,
                p.DataEntrega,
                p.Status,
                p.Itens.Sum(i => i.Quantidade),
                p.Itens.Sum(i => i.Quantidade * i.PrecoUnitario)))
            .PaginarAsync(filtro.Pagina, filtro.Tamanho, ct);

        return TypedResults.Ok(pagina);
    }

    private static async Task<Results<Ok<PedidoDetalheDto>, NotFound>> BuscarPorId(
        int id, AppDbContext db, CancellationToken ct)
    {
        var detalhe = await BuscarDetalheAsync(db, id, ct);

        return detalhe is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(detalhe);
    }

    // -----------------------------------------------------------------------
    // Criação
    // -----------------------------------------------------------------------

    private static async Task<Results<Created<PedidoDetalheDto>, ValidationProblem>> Criar(
        CriarPedidoDto dto, AppDbContext db, CancellationToken ct)
    {
        // Chegando aqui, o ValidacaoFiltro já garantiu formato, 1–50 itens,
        // quantidades 1–999 e nenhum produto repetido.
        // Faltam as regras que dependem do relógio e do banco. Os erros são
        // juntados num dicionário para o usuário ver todos de uma vez.
        var erros = new Dictionary<string, string[]>();

        // R21: "hoje" no fuso da confeitaria, não no do servidor.
        if (dto.DataEntrega < Pedido.HojeNaConfeitaria())
        {
            erros[nameof(CriarPedidoDto.DataEntrega)] = ["A data de entrega não pode ser anterior a hoje."];
        }

        if (!await db.Clientes.AnyAsync(c => c.Id == dto.ClienteId, ct))
        {
            erros[nameof(CriarPedidoDto.ClienteId)] = ["Cliente não encontrado."];
        }

        // R22 + R23: uma única consulta traz o preço ATUAL de cada produto pedido
        // que exista e esteja ativo. Quem não voltar no dicionário está indisponível.
        // SQL (aproximado): SELECT "Id", "Preco" FROM "Produtos" WHERE "Ativo" AND "Id" IN (@ids...)
        var idsPedidos = dto.Itens.Select(i => i.ProdutoId).ToList();

        var precoPorProduto = await db.Produtos
            .AsNoTracking()
            .Where(p => p.Ativo && idsPedidos.Contains(p.Id))
            .Select(p => new { p.Id, p.Preco })
            .ToDictionaryAsync(p => p.Id, p => p.Preco, ct);

        var indisponiveis = idsPedidos.Where(id => !precoPorProduto.ContainsKey(id)).ToList();

        if (indisponiveis.Count > 0)
        {
            erros[nameof(CriarPedidoDto.Itens)] =
                [$"Produto(s) indisponível(is): {string.Join(", ", indisponiveis)}."];
        }

        if (erros.Count > 0)
        {
            return TypedResults.ValidationProblem(erros, title: "Dados inválidos.");
        }

        var pedido = new Pedido
        {
            ClienteId = dto.ClienteId,
            DataEntrega = dto.DataEntrega,
            Status = StatusPedido.Aberto,          // R26
            CriadoEm = DateTime.UtcNow,            // R26 (UTC; a tela converte para o fuso local)
            Itens = dto.Itens
                .Select(i => new ItemPedido
                {
                    ProdutoId = i.ProdutoId,
                    Quantidade = i.Quantidade,
                    PrecoUnitario = precoPorProduto[i.ProdutoId]   // preço congelado (R23)
                })
                .ToList()
        };

        // R24: pedido e itens entram juntos no rastreamento do EF e são gravados
        // por UM SaveChangesAsync. O EF abre uma transação implícita: ou grava o
        // INSERT do pedido e todos os INSERTs dos itens, ou não grava nada.
        // O EF também preenche o PedidoId de cada item com o Id gerado do pedido.
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);

        // Relê pela mesma projeção do GET: a resposta fica idêntica a GET /pedidos/{id}
        // (com nomes de cliente e produtos, subtotais e total).
        var detalhe = await BuscarDetalheAsync(db, pedido.Id, ct);

        return TypedResults.Created($"/pedidos/{pedido.Id}", detalhe);
    }

    // -----------------------------------------------------------------------
    // Transições de status (tabela 2.6 do contrato)
    // -----------------------------------------------------------------------

    private static Task<Results<Ok<PedidoDetalheDto>, NotFound, ProblemHttpResult>> Confirmar(
        int id, AppDbContext db, CancellationToken ct) =>
        MudarStatusAsync(db, id, "confirmado", StatusPedido.Confirmado,
            origem: StatusPedido.Aberto, ct: ct);

    private static Task<Results<Ok<PedidoDetalheDto>, NotFound, ProblemHttpResult>> Entregar(
        int id, AppDbContext db, CancellationToken ct) =>
        MudarStatusAsync(db, id, "entregue", StatusPedido.Entregue,
            origem: StatusPedido.Confirmado, ct: ct);

    /// <summary>
    /// Cancela o pedido. Aberto: qualquer usuário logado. Confirmado: só Admin (R30).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aqui a política <c>SomenteAdmin</c> no grupo não serve: a permissão depende do
    /// <b>status do pedido</b>, que só se conhece no banco. Por isso o papel é conferido
    /// dentro do endpoint e entra na própria condição do UPDATE:
    /// </para>
    /// <list type="bullet">
    ///   <item>Admin: <c>WHERE "Status" IN ('Aberto','Confirmado')</c>.</item>
    ///   <item>Operador: <c>WHERE "Status" = 'Aberto'</c>. Se nada mudar e o pedido estiver
    ///   Confirmado, a resposta é 403 (falta permissão), não 409.</item>
    /// </list>
    /// <para>
    /// Conferir o papel e o status num único UPDATE evita a corrida "li Aberto, outro
    /// usuário confirmou, eu cancelei um Confirmado sem ser Admin".
    /// </para>
    /// </remarks>
    private static Task<Results<Ok<PedidoDetalheDto>, NotFound, ProblemHttpResult>> Cancelar(
        int id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        // O papel vem da claim ClaimTypes.Role gravada no cookie no login (R35).
        var ehAdmin = user.IsInRole(nameof(Papel.Admin));

        return MudarStatusAsync(db, id, "cancelado", StatusPedido.Cancelado,
            origem: StatusPedido.Aberto,
            outraOrigem: ehAdmin ? StatusPedido.Confirmado : null,
            explicarRecusa: statusAtual => !ehAdmin && statusAtual == StatusPedido.Confirmado
                ? TypedResults.Problem(
                    title: "Apenas Admin pode cancelar um pedido Confirmado.",
                    statusCode: StatusCodes.Status403Forbidden)
                : null,
            ct: ct);
    }

    /// <summary>
    /// Aplica uma transição de status de forma atômica (R29).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que não "buscar, conferir o status e salvar"?</b> Entre a leitura e a gravação
    /// outro usuário pode mudar o pedido (ex.: dois cliques em "Confirmar" em telas diferentes).
    /// Em vez disso, a conferência vai DENTRO do UPDATE:
    /// </para>
    /// <code>
    /// UPDATE "Pedidos" SET "Status" = 'Confirmado'
    /// WHERE "Id" = @id AND ("Status" = 'Aberto' OR "Status" = @outraOrigem)
    /// </code>
    /// <para>
    /// O banco executa isso de uma vez só. Se 1 linha mudou, a transição valeu.
    /// Se 0 linhas mudaram, uma consulta curta descobre o motivo:
    /// pedido inexistente → 404; status atual não permite → 409 (ou o que
    /// <paramref name="explicarRecusa"/> decidir, como o 403 do cancelamento).
    /// </para>
    /// <para>
    /// As origens viram comparações <c>=</c> com OR (no máximo duas) em vez de
    /// <c>lista.Contains(...)</c>: assim o SQL é simples e o conversor enum → texto
    /// é aplicado a cada parâmetro sem depender de como o provedor traduz listas.
    /// </para>
    /// </remarks>
    /// <param name="db">Contexto do banco.</param>
    /// <param name="id">Pedido a alterar.</param>
    /// <param name="acao">Particípio usado na mensagem 409 ("Pedido Entregue não pode ser <b>cancelado</b>.").</param>
    /// <param name="destino">Novo status.</param>
    /// <param name="origem">Status de onde a transição pode partir.</param>
    /// <param name="outraOrigem">Segundo status de origem aceito, se houver.</param>
    /// <param name="explicarRecusa">
    /// Opcional: recebe o status atual quando nada mudou e devolve uma resposta específica,
    /// ou <c>null</c> para usar o 409 padrão.
    /// </param>
    /// <param name="ct">Cancelamento da requisição.</param>
    private static async Task<Results<Ok<PedidoDetalheDto>, NotFound, ProblemHttpResult>> MudarStatusAsync(
        AppDbContext db,
        int id,
        string acao,
        StatusPedido destino,
        StatusPedido origem,
        StatusPedido? outraOrigem = null,
        Func<StatusPedido, ProblemHttpResult?>? explicarRecusa = null,
        CancellationToken ct = default)
    {
        // Sem segunda origem, repete a primeira: a condição continua correta.
        var origemAlternativa = outraOrigem ?? origem;

        // ExecuteUpdateAsync (EF Core 7+) gera um UPDATE direto no banco, sem carregar
        // a entidade. Devolve o número de linhas afetadas. No EF Core 10 o argumento é
        // um "construtor de setters": cada SetProperty vira um "SET coluna = valor".
        var alterados = await db.Pedidos
            .Where(p => p.Id == id && (p.Status == origem || p.Status == origemAlternativa))
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Status, destino), ct);

        if (alterados == 1)
        {
            var detalhe = await BuscarDetalheAsync(db, id, ct);
            return detalhe is null ? TypedResults.NotFound() : TypedResults.Ok(detalhe);
        }

        // Nada mudou: o pedido não existe ou está num status que não permite a ação.
        // O cast para StatusPedido? faz "não encontrado" virar null (e não Aberto, o valor 0 do enum).
        var statusAtual = await db.Pedidos
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => (StatusPedido?)p.Status)
            .FirstOrDefaultAsync(ct);

        if (statusAtual is not { } atual)
        {
            return TypedResults.NotFound();
        }

        // R28: "Pedido Entregue não pode ser cancelado."
        return explicarRecusa?.Invoke(atual)
            ?? TypedResults.Problem(
                title: $"Pedido {atual} não pode ser {acao}.",
                statusCode: StatusCodes.Status409Conflict);
    }

    // -----------------------------------------------------------------------
    // Auxiliares
    // -----------------------------------------------------------------------

    /// <summary>
    /// Projeção única do detalhe, usada pelo GET, pela criação e pelas transições.
    /// </summary>
    /// <remarks>
    /// O <c>Select</c> faz o banco devolver só as colunas necessárias, já com os joins de
    /// cliente e produto (nome ATUAL, R32) e com subtotal e total calculados (R25).
    /// Os itens saem na ordem em que foram gravados (<c>Id</c>).
    /// </remarks>
    private static Task<PedidoDetalheDto?> BuscarDetalheAsync(AppDbContext db, int id, CancellationToken ct) =>
        db.Pedidos
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PedidoDetalheDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nome,
                p.Cliente!.Telefone,
                p.DataEntrega,
                p.Status,
                p.CriadoEm,
                p.Itens
                    .OrderBy(i => i.Id)
                    .Select(i => new ItemPedidoRespostaDto(
                        i.ProdutoId,
                        i.Produto!.Nome,
                        i.Quantidade,
                        i.PrecoUnitario,
                        i.Quantidade * i.PrecoUnitario))
                    .ToList(),
                p.Itens.Sum(i => i.Quantidade * i.PrecoUnitario)))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Escapa os curingas do LIKE (<c>%</c>, <c>_</c>) e o próprio caractere de escape (<c>\</c>),
    /// para que buscar "50%" procure o texto "50%" e não "50 seguido de qualquer coisa" (R5).
    /// </summary>
    private static string EscaparLike(string termo) =>
        termo
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
}
