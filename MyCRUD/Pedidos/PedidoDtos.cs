// DTOs de pedido.
//
// Entrada: CriarPedidoDto (+ CriarItemPedidoDto) e FiltroPedidos.
// Saída:   PedidoResumoDto (lista), PedidoDetalheDto (detalhe) e ItemPedidoRespostaDto.
//
// Repare que NENHUM DTO de entrada tem campo de preço ou de status:
// o preço vem do banco (R23) e o status só muda pelas rotas de ação (R28).
// Se o campo não existe no DTO, o cliente da API não tem como forjá-lo.

using System.ComponentModel.DataAnnotations;
using MyCRUD.Compartilhado;

namespace MyCRUD.Pedidos;

/// <summary>Dados para criar um pedido (<c>POST /pedidos</c>).</summary>
/// <remarks>
/// <para>
/// <b>Por que <see cref="IValidatableObject"/>?</b> O <c>Validator</c> do .NET confere os
/// atributos das propriedades, mas <b>não desce</b> nos objetos de uma lista.
/// Sem o <see cref="Validate"/>, um item com quantidade 0 passaria pela validação.
/// </para>
/// <para>
/// Ordem do <c>Validator</c>: primeiro os atributos (<c>[Range]</c>, <c>[Required]</c>...);
/// só se todos passarem ele chama <see cref="Validate"/>. Por isso uma lista vazia
/// mostra apenas "Informe ao menos um item." e não os erros dos itens.
/// </para>
/// <para>
/// Regras que dependem do banco ou do relógio (cliente existe, produto ativo,
/// data ≥ hoje) ficam no endpoint, que devolve o mesmo formato de erro (400 por campo).
/// </para>
/// </remarks>
/// <param name="ClienteId">Cliente que fez a encomenda. Deve existir.</param>
/// <param name="DataEntrega">Dia da entrega (<c>"2026-11-20"</c>). Hoje ou depois, no fuso de São Paulo.</param>
/// <param name="Itens">De 1 a 50 itens, sem produto repetido.</param>
public record CriarPedidoDto(
    [property: Range(1, int.MaxValue, ErrorMessage = "Selecione o cliente.")]
    int ClienteId,

    DateOnly DataEntrega,

    [property: Required(ErrorMessage = "Informe ao menos um item.")]
    [property: MinLength(1, ErrorMessage = "Informe ao menos um item.")]
    [property: MaxLength(Pedido.ItensMaximo, ErrorMessage = "O pedido pode ter no máximo {1} itens.")]
    List<CriarItemPedidoDto> Itens) : IValidatableObject
{
    /// <summary>
    /// Valida cada item e a repetição de produtos. As chaves de erro seguem o
    /// caminho do campo no JSON (<c>Itens[2].Quantidade</c>), para a tela saber qual linha corrigir.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Defesa extra: [Required] já recusou null, mas o compilador não sabe disso.
        if (Itens is null)
        {
            yield break;
        }

        var produtosVistos = new HashSet<int>();

        for (var i = 0; i < Itens.Count; i++)
        {
            var item = Itens[i];
            var caminho = $"{nameof(Itens)}[{i}]";

            // JSON como "itens": [null] desserializa um item nulo.
            if (item is null)
            {
                yield return new ValidationResult("Item vazio.", [caminho]);
                continue;
            }

            if (item.ProdutoId < 1)
            {
                yield return new ValidationResult(
                    "Selecione o produto.",
                    [$"{caminho}.{nameof(CriarItemPedidoDto.ProdutoId)}"]);
            }
            else if (!produtosVistos.Add(item.ProdutoId))
            {
                // HashSet.Add devolve false quando o valor já estava no conjunto.
                yield return new ValidationResult(
                    $"Produto {item.ProdutoId} repetido. Use um único item e some as quantidades.",
                    [$"{caminho}.{nameof(CriarItemPedidoDto.ProdutoId)}"]);
            }

            if (item.Quantidade is < 1 or > ItemPedido.QuantidadeMaxima)
            {
                yield return new ValidationResult(
                    $"A quantidade deve estar entre 1 e {ItemPedido.QuantidadeMaxima}.",
                    [$"{caminho}.{nameof(CriarItemPedidoDto.Quantidade)}"]);
            }
        }
    }
}

/// <summary>Um item do pedido a criar. Sem preço: ele é lido do cadastro do produto (R23).</summary>
/// <remarks>
/// Não tem atributos de validação porque o <c>Validator</c> não chegaria até aqui;
/// quem valida é <see cref="CriarPedidoDto.Validate"/>.
/// </remarks>
/// <param name="ProdutoId">Produto ativo do cardápio.</param>
/// <param name="Quantidade">Unidades, de 1 a 999.</param>
public record CriarItemPedidoDto(int ProdutoId, int Quantidade);

/// <summary>Linha da listagem de pedidos (<c>GET /pedidos</c>).</summary>
/// <param name="Id">Número do pedido.</param>
/// <param name="ClienteId">Identificador do cliente.</param>
/// <param name="ClienteNome">Nome atual do cliente.</param>
/// <param name="DataEntrega">Dia da entrega.</param>
/// <param name="Status">Situação atual (texto: <c>"Aberto"</c>, <c>"Confirmado"</c>...).</param>
/// <param name="QuantidadeItens">Soma das quantidades de todos os itens (não o número de linhas).</param>
/// <param name="Total">Σ(Quantidade × PrecoUnitario), calculado na consulta.</param>
public record PedidoResumoDto(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateOnly DataEntrega,
    StatusPedido Status,
    int QuantidadeItens,
    decimal Total);

/// <summary>Pedido completo (<c>GET /pedidos/{id}</c>, criação e transições de status).</summary>
/// <param name="Id">Número do pedido.</param>
/// <param name="ClienteId">Identificador do cliente.</param>
/// <param name="ClienteNome">Nome atual do cliente.</param>
/// <param name="ClienteTelefone">Telefone do cliente, só dígitos.</param>
/// <param name="DataEntrega">Dia da entrega.</param>
/// <param name="Status">Situação atual.</param>
/// <param name="CriadoEm">Momento da criação (UTC) — quando os preços foram congelados.</param>
/// <param name="Itens">Itens com preço congelado e subtotal.</param>
/// <param name="Total">Σ dos subtotais.</param>
public record PedidoDetalheDto(
    int Id,
    int ClienteId,
    string ClienteNome,
    string ClienteTelefone,
    DateOnly DataEntrega,
    StatusPedido Status,
    DateTime CriadoEm,
    IReadOnlyList<ItemPedidoRespostaDto> Itens,
    decimal Total);

/// <summary>Item de um pedido já criado.</summary>
/// <param name="ProdutoId">Identificador do produto.</param>
/// <param name="ProdutoNome">Nome ATUAL do produto (R32).</param>
/// <param name="Quantidade">Unidades.</param>
/// <param name="PrecoUnitario">Preço congelado na criação (R23).</param>
/// <param name="Subtotal">Quantidade × PrecoUnitario.</param>
public record ItemPedidoRespostaDto(
    int ProdutoId,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);

/// <summary>
/// Filtros da listagem (<c>GET /pedidos?busca=&amp;status=&amp;de=&amp;ate=&amp;pagina=&amp;tamanho=</c>).
/// </summary>
/// <remarks>
/// Ligado à query string com <c>[AsParameters]</c>: cada parâmetro do record vira
/// um parâmetro da URL com o mesmo nome. Campos vazios são ignorados.
/// </remarks>
/// <param name="Busca">Parte do nome do cliente (contém, sem diferenciar maiúsculas).</param>
/// <param name="Status">Só pedidos neste status (<c>Aberto</c>, <c>Confirmado</c>...).</param>
/// <param name="De">Entrega a partir deste dia (inclusive).</param>
/// <param name="Ate">Entrega até este dia (inclusive).</param>
/// <param name="Pagina">Página pedida, a partir de 1.</param>
/// <param name="Tamanho">Itens por página, de 1 a 100.</param>
public record FiltroPedidos(
    string? Busca,
    StatusPedido? Status,
    DateOnly? De,
    DateOnly? Ate,

    [property: Range(1, int.MaxValue, ErrorMessage = "A página deve ser 1 ou maior.")]
    int Pagina = 1,

    [property: Range(1, Paginacao.TamanhoMaximo, ErrorMessage = "O tamanho da página deve estar entre {1} e {2}.")]
    int Tamanho = Paginacao.TamanhoPadrao) : IValidatableObject
{
    /// <summary>Recusa período invertido (<c>de</c> depois de <c>ate</c>).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (De is { } de && Ate is { } ate && de > ate)
        {
            yield return new ValidationResult(
                "A data final deve ser igual ou posterior à data inicial.",
                [nameof(Ate)]);
        }
    }
}
