using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Compartilhado;
using MyCRUD.Dados;

namespace MyCRUD.Categorias;

/// <summary>
/// Rotas HTTP do CRUD de categorias, agrupadas em <c>/categorias</c>.
/// </summary>
/// <remarks>
/// <para><b>Quem pode o quê</b> (R10):</para>
/// <list type="bullet">
///   <item>Listar e buscar: qualquer usuário logado (o formulário de produto precisa da lista).</item>
///   <item>Cadastrar, renomear e excluir: somente Admin
///   (<see cref="Politicas.SomenteAdmin"/>); Operador recebe <c>403</c>.</item>
/// </list>
/// <para><b>Duas camadas de proteção</b> para as regras que dependem do banco:</para>
/// <list type="number">
///   <item>O endpoint consulta antes e devolve um <c>409</c> com mensagem clara
///   (nome repetido, categoria com produtos).</item>
///   <item>Se duas requisições passarem pela consulta ao mesmo tempo, o banco
///   barra a segunda (índice único / chave estrangeira) e o
///   <see cref="TratadorGlobalDeExcecoes"/> converte o erro em <c>409</c>.</item>
/// </list>
/// </remarks>
public static class CategoriaEndpoints
{
    public static RouteGroupBuilder MapCategoriaEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/categorias")
            .WithTags("Categorias")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet("/", Listar)
            .WithName("ListarCategorias")
            .WithSummary("Lista categorias por nome, com a quantidade de produtos de cada uma. Paginado.")
            .ComValidacao<FiltroCategorias>();

        grupo.MapGet("/{id:int}", BuscarPorId)
            .WithName("BuscarCategoria")
            .WithSummary("Busca uma categoria pelo ID.");

        grupo.MapPost("/", Criar)
            .WithName("CriarCategoria")
            .WithSummary("Cadastra uma categoria. Somente Admin.")
            .ComValidacao<CriarCategoriaDto>()
            .RequireAuthorization(Politicas.SomenteAdmin)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPut("/{id:int}", Atualizar)
            .WithName("AtualizarCategoria")
            .WithSummary("Renomeia uma categoria. Somente Admin.")
            .ComValidacao<AtualizarCategoriaDto>()
            .RequireAuthorization(Politicas.SomenteAdmin)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete("/{id:int}", Excluir)
            .WithName("ExcluirCategoria")
            .WithSummary("Exclui uma categoria sem produtos (ativos ou inativos). Somente Admin.")
            .RequireAuthorization(Politicas.SomenteAdmin)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return grupo;
    }

    private static async Task<Ok<ResultadoPaginado<CategoriaRespostaDto>>> Listar(
        [AsParameters] FiltroCategorias filtro, AppDbContext db, CancellationToken ct)
    {
        // Chegando aqui, o ValidacaoFiltro já garantiu pagina >= 1 e tamanho 1–100 (R4).
        IQueryable<Categoria> consulta = db.Categorias.AsNoTracking();

        // R5: "contém", sem diferenciar maiúsculas. ILIKE é o LIKE do PostgreSQL que
        // ignora maiúsculas; os curingas digitados pelo usuário são escapados.
        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            var padrao = $"%{EscaparCuringasLike(filtro.Nome.Trim())}%";
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nome, padrao));
        }

        // R6: ordena por Nome e desempata por Id, para que a paginação
        // não repita nem pule categorias com o mesmo nome em páginas vizinhas.
        var pagina = await ComoResposta(
                consulta.OrderBy(c => c.Nome).ThenBy(c => c.Id), db)
            .PaginarAsync(filtro.Pagina, filtro.Tamanho, ct);

        return TypedResults.Ok(pagina);
    }

    private static async Task<Results<Ok<CategoriaRespostaDto>, NotFound>> BuscarPorId(
        int id, AppDbContext db, CancellationToken ct)
    {
        var categoria = await ComoResposta(
                db.Categorias.AsNoTracking().Where(c => c.Id == id), db)
            .FirstOrDefaultAsync(ct);

        return categoria is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(categoria);
    }

    private static async Task<Results<Created<CategoriaRespostaDto>, ProblemHttpResult>> Criar(
        CriarCategoriaDto dto, AppDbContext db, CancellationToken ct)
    {
        var nome = dto.Nome.Trim(); // R3

        if (await NomeEmUsoAsync(db, nome, idIgnorado: null, ct))
        {
            return NomeDuplicado();
        }

        var categoria = new Categoria { Nome = nome };

        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(ct); // gera o INSERT e preenche categoria.Id

        // Categoria recém-criada ainda não tem produtos: a contagem é 0 sem consultar o banco.
        return TypedResults.Created(
            $"/categorias/{categoria.Id}",
            new CategoriaRespostaDto(categoria.Id, categoria.Nome, 0));
    }

    private static async Task<Results<Ok<CategoriaRespostaDto>, NotFound, ProblemHttpResult>> Atualizar(
        int id, AtualizarCategoriaDto dto, AppDbContext db, CancellationToken ct)
    {
        // Consulta COM rastreamento: o EF detecta a mudança de Nome e gera o UPDATE no SaveChanges.
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (categoria is null)
        {
            return TypedResults.NotFound();
        }

        var nome = dto.Nome.Trim();

        // R8: na edição, a própria categoria não conta como duplicada.
        // Assim "Bolos" → "BOLOS" é permitido (só muda a grafia).
        if (await NomeEmUsoAsync(db, nome, idIgnorado: id, ct))
        {
            return NomeDuplicado();
        }

        categoria.Nome = nome;
        await db.SaveChangesAsync(ct);

        var quantidadeProdutos = await db.Produtos.CountAsync(p => p.CategoriaId == id, ct);

        return TypedResults.Ok(new CategoriaRespostaDto(categoria.Id, categoria.Nome, quantidadeProdutos));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Excluir(
        int id, AppDbContext db, CancellationToken ct)
    {
        // Uma consulta responde as duas perguntas: a categoria existe? quantos produtos tem?
        // Resultado null = categoria inexistente.
        var quantidadeProdutos = await db.Categorias
            .Where(c => c.Id == id)
            .Select(c => (int?)db.Produtos.Count(p => p.CategoriaId == c.Id))
            .FirstOrDefaultAsync(ct);

        if (quantidadeProdutos is null)
        {
            return TypedResults.NotFound();
        }

        // R9: conta produtos ativos E inativos. Um produto desativado continua
        // apontando para a categoria (e pode estar em pedidos antigos).
        if (quantidadeProdutos > 0)
        {
            return TypedResults.Problem(
                title: $"Categoria possui {quantidadeProdutos} produto(s).",
                statusCode: StatusCodes.Status409Conflict);
        }

        // DELETE ... WHERE "Id" = @id direto no banco. Se um produto for ligado à categoria
        // entre a contagem acima e este comando, a FK RESTRICT recusa e o
        // TratadorGlobalDeExcecoes responde 409 "Registro em uso." (R7).
        var removidas = await db.Categorias
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(ct);

        // 0 linhas: outra requisição apagou a categoria entre as duas consultas.
        return removidas == 0
            ? TypedResults.NotFound()
            : TypedResults.NoContent();
    }

    /// <summary>
    /// Converte a consulta de entidades no DTO de resposta, já com a quantidade de produtos.
    /// </summary>
    /// <remarks>
    /// A contagem vira uma subconsulta no mesmo SELECT
    /// (<c>(SELECT count(*) FROM "Produtos" WHERE "CategoriaId" = c."Id")</c>):
    /// uma ida ao banco por página, e não uma por categoria (evita o problema N+1).
    /// </remarks>
    private static IQueryable<CategoriaRespostaDto> ComoResposta(
        IQueryable<Categoria> consulta, AppDbContext db) =>
        consulta.Select(c => new CategoriaRespostaDto(
            c.Id,
            c.Nome,
            db.Produtos.Count(p => p.CategoriaId == c.Id)));

    /// <summary>
    /// Verifica se outra categoria já usa o nome, sem diferenciar maiúsculas (R8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// No lado da coluna, <c>c.Nome.ToLower()</c> é traduzido para <c>lower("Nome")</c> no
    /// PostgreSQL — a mesma expressão do índice único <c>IX_Categorias_Nome_Minusculo</c>.
    /// Assim a consulta aproveita o índice e segue a mesma regra que o banco garante.
    /// </para>
    /// <para>
    /// O termo pesquisado é convertido em C# antes (<c>ToLowerInvariant</c>, que não depende
    /// do idioma do servidor) e vai como parâmetro. Se alguma letra rara for convertida de
    /// forma diferente, o índice ainda barra o duplicado e o tratador devolve 409.
    /// </para>
    /// </remarks>
    /// <param name="db">Contexto do banco.</param>
    /// <param name="nome">Nome já sem espaços nas pontas.</param>
    /// <param name="idIgnorado">Id da categoria em edição (não conflita consigo mesma); <c>null</c> no cadastro.</param>
    /// <param name="ct">Cancelamento da requisição.</param>
    private static Task<bool> NomeEmUsoAsync(
        AppDbContext db, string nome, int? idIgnorado, CancellationToken ct)
    {
        var nomeMinusculo = nome.ToLowerInvariant();
        var consulta = db.Categorias.Where(c => c.Nome.ToLower() == nomeMinusculo);

        if (idIgnorado is int id)
        {
            consulta = consulta.Where(c => c.Id != id);
        }

        return consulta.AnyAsync(ct);
    }

    private static ProblemHttpResult NomeDuplicado() =>
        TypedResults.Problem(
            title: "Já existe uma categoria com esse nome.",
            statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// Escapa os caracteres especiais do LIKE para que sejam buscados literalmente.
    /// </summary>
    /// <remarks>
    /// No LIKE do PostgreSQL, <c>%</c> = qualquer sequência, <c>_</c> = um caractere
    /// e <c>\</c> é o caractere de escape padrão. Sem isto, buscar "50%" acharia
    /// "500 g". A barra é escapada primeiro para não duplicar os escapes seguintes.
    /// </remarks>
    private static string EscaparCuringasLike(string termo) =>
        termo
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
}
