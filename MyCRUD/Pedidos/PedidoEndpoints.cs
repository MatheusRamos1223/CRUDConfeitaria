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
            // R5: "contém" no nome do cliente, sem diferenciar maiúsculas, curingas escapados.
            var padrao = ConsultaTexto.PadraoContem(filtro.Busca);
            consulta = consulta.Where(p => EF.Functions.ILike(p.Cliente!.Nome, padrao, ConsultaTexto.CaractereDeEscape));
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
        CriarPedidoDto dto, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        // Chegando aqui, o ValidacaoFiltro já garantiu formato, 1–50 itens,
        // quantidades 1–999 e nenhum produto repetido.
        // Faltam as regras que dependem do relógio e do banco. Os erros são
        // juntados num dicionário para o usuário ver todos de uma vez.
        var erros = new Dictionary<string, string[]>();

        // R21: "hoje" no fuso da confeitaria, não no do servidor.
        // R39: no máximo 365 dias à frente (pega ano digitado errado, ex.: 2062).
        var hoje = Pedido.HojeNaConfeitaria();

        if (dto.DataEntrega < hoje)
        {
            erros[nameof(CriarPedidoDto.DataEntrega)] = ["A data de entrega não pode ser anterior a hoje."];
        }
        else if (dto.DataEntrega > hoje.AddDays(Pedido.AntecedenciaMaximaDias))
        {
            erros[nameof(CriarPedidoDto.DataEntrega)] =
                [$"A data de entrega deve ser no máximo {Pedido.AntecedenciaMaximaDias} dias a partir de hoje."];
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

        // CriadoEm NÃO é preenchido: DEFAULT now() no banco (R26). O EF omite a coluna
        // no INSERT e lê o valor gerado de volta. Status Aberto também é o DEFAULT da coluna.
        var pedido = new Pedido
        {
            ClienteId = dto.ClienteId,
            DataEntrega = dto.DataEntrega,
            Status = StatusPedido.Aberto,          // R26
            CriadoPorId = user.ObterUsuarioId(),   // R40: autor vem do cookie, nunca do corpo
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
        int id, ClaimsPrincipal user, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        MudarStatusAsync(db, loggerFactory, id, user.ObterUsuarioId(), "confirmado", StatusPedido.Confirmado,
            origens: [StatusPedido.Aberto], ct: ct);

    private static Task<Results<Ok<PedidoDetalheDto>, NotFound, ProblemHttpResult>> Entregar(
        int id, ClaimsPrincipal user, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        MudarStatusAsync(db, loggerFactory, id, user.ObterUsuarioId(), "entregue", StatusPedido.Entregue,
            origens: [StatusPedido.Confirmado], ct: ct);

    /// <summary>
    /// Cancela o pedido. Aberto: qualquer usuário logado. Confirmado: só Admin (R30).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aqui a política <c>SomenteAdmin</c> no grupo não serve: a permissão depende do
    /// <b>status do pedido</b>, que só se conhece no banco. Por isso o papel é conferido
    /// dentro do endpoint e decide de quais status o UPDATE pode partir:
    /// </para>
    /// <list type="bullet">
    ///   <item>Admin: <c>WHERE "Status" = 'Aberto'</c>; se nada mudar, <c>WHERE "Status" = 'Confirmado'</c>.</item>
    ///   <item>Operador: só <c>WHERE "Status" = 'Aberto'</c>. Se nada mudar e o pedido estiver
    ///   Confirmado, a resposta é 403 (falta permissão), não 409.</item>
    /// </list>
    /// <para>
    /// Conferir o papel e o status no próprio UPDATE evita a corrida "li Aberto, outro
    /// usuário confirmou, eu cancelei um Confirmado sem ser Admin".
    /// </para>
    /// </remarks>
    private static Task<Results<Ok<PedidoDetalheDto>, NotFound, ProblemHttpResult>> Cancelar(
        int id, ClaimsPrincipal user, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        // O papel vem da claim ClaimTypes.Role do cookie, revalidada no banco a cada 5 min (R35).
        var ehAdmin = user.IsInRole(nameof(Papel.Admin));

        StatusPedido[] origens = ehAdmin
            ? new[] { StatusPedido.Aberto, StatusPedido.Confirmado }
            : new[] { StatusPedido.Aberto };

        return MudarStatusAsync(db, loggerFactory, id, user.ObterUsuarioId(), "cancelado", StatusPedido.Cancelado,
            origens,
            explicarRecusa: statusAtual => !ehAdmin && statusAtual == StatusPedido.Confirmado
                ? TypedResults.Problem(
                    title: "Apenas Admin pode cancelar um pedido Confirmado.",
                    statusCode: StatusCodes.Status403Forbidden)
                : null,
            ct: ct);
    }

    /// <summary>
    /// Aplica uma transição de status de forma atômica (R29) e registra quem a fez (R40, R41).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que não "buscar, conferir o status e salvar"?</b> Entre a leitura e a gravação
    /// outro usuário pode mudar o pedido (ex.: dois cliques em "Confirmar" em telas diferentes).
    /// Em vez disso, a conferência vai DENTRO do UPDATE, uma origem por vez:
    /// </para>
    /// <code>
    /// UPDATE "Pedidos"
    /// SET "Status" = 'Confirmado', "StatusAlteradoEm" = @agora, "StatusAlteradoPorId" = @usuario
    /// WHERE "Id" = @id AND "Status" = 'Aberto'
    /// </code>
    /// <para>
    /// O banco executa cada UPDATE de uma vez só. Se 1 linha mudou, a transição valeu e a
    /// origem é exatamente a do <c>WHERE</c> — por isso o log de auditoria sabe de onde o pedido
    /// saiu sem uma leitura extra (que poderia estar desatualizada). Com duas origens (cancelar
    /// como Admin), tenta-se uma e depois a outra; status nunca volta para Aberto, então a
    /// segunda tentativa não "pula" uma mudança.
    /// </para>
    /// <para>
    /// Se nenhuma origem mudou nada, uma consulta curta descobre o motivo:
    /// pedido inexistente → 404; status atual não permite → 409 (ou o que
    /// <paramref name="explicarRecusa"/> decidir, como o 403 do cancelamento).
    /// </para>
    /// </remarks>
    /// <param name="db">Contexto do banco.</param>
    /// <param name="loggerFactory">Cria o logger da transição (classe estática não tem <c>ILogger&lt;T&gt;</c>).</param>
    /// <param name="id">Pedido a alterar.</param>
    /// <param name="usuarioId">Quem está fazendo a transição (claim do cookie).</param>
    /// <param name="acao">Particípio usado na mensagem 409 ("Pedido Entregue não pode ser <b>cancelado</b>.").</param>
    /// <param name="destino">Novo status.</param>
    /// <param name="origens">Status de onde a transição pode partir, na ordem em que são tentados.</param>
    /// <param name="explicarRecusa">
    /// Opcional: recebe o status atual quando nada mudou e devolve uma resposta específica,
    /// ou <c>null</c> para usar o 409 padrão.
    /// </param>
    /// <param name="ct">Cancelamento da requisição.</param>
    private static async Task<Results<Ok<PedidoDetalheDto>, NotFound, ProblemHttpResult>> MudarStatusAsync(
        AppDbContext db,
        ILoggerFactory loggerFactory,
        int id,
        int usuarioId,
        string acao,
        StatusPedido destino,
        StatusPedido[] origens,
        Func<StatusPedido, ProblemHttpResult?>? explicarRecusa = null,
        CancellationToken ct = default)
    {
        // Tipos anuláveis explícitos: as colunas são "timestamptz NULL" e "integer NULL".
        DateTime? agora = DateTime.UtcNow;
        int? alteradoPor = usuarioId;

        foreach (var origem in origens)
        {
            // ExecuteUpdateAsync (EF Core 7+) gera um UPDATE direto no banco, sem carregar
            // a entidade, e devolve o número de linhas afetadas. No EF Core 10 o argumento é
            // um "construtor de setters": cada SetProperty vira um "SET coluna = valor".
            var alterados = await db.Pedidos
                .Where(p => p.Id == id && p.Status == origem)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, destino)
                    .SetProperty(p => p.StatusAlteradoEm, agora)
                    .SetProperty(p => p.StatusAlteradoPorId, alteradoPor), ct);

            if (alterados == 1)
            {
                // R41: trilha de auditoria no log, só com Ids e status (nenhum dado pessoal).
                loggerFactory.CreateLogger(typeof(PedidoEndpoints)).LogInformation(
                    "Pedido {PedidoId}: {Origem} -> {Destino} por usuário {UsuarioId}.",
                    id, origem, destino, usuarioId);

                var detalhe = await BuscarDetalheAsync(db, id, ct);
                return detalhe is null ? TypedResults.NotFound() : TypedResults.Ok(detalhe);
            }
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
    /// cliente, produto (nome ATUAL, R32) e usuários da auditoria (R40), e com subtotal e
    /// total calculados (R25). <c>StatusAlteradoPor</c> é opcional: vira LEFT JOIN e o nome
    /// sai <c>null</c> enquanto o pedido nunca mudou de status.
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
                p.CriadoPor!.Nome,
                p.StatusAlteradoEm,
                p.StatusAlteradoPor == null ? null : p.StatusAlteradoPor.Nome,
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
}
