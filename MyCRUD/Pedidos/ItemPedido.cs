using MyCRUD.Produtos;

namespace MyCRUD.Pedidos;

/// <summary>
/// Uma linha do pedido: qual produto, quantas unidades e por qual preço.
/// Mapeada para a tabela <c>ItensPedido</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Preço congelado</b> (R23): <see cref="PrecoUnitario"/> é copiado de
/// <c>Produto.Preco</c> no momento da criação. Se o cardápio mudar depois,
/// o pedido antigo continua com o valor combinado com o cliente.
/// </para>
/// <para>
/// O nome do produto, ao contrário, NÃO é copiado: a tela mostra o nome atual
/// (R32). Como produtos nunca são apagados (só desativados), o join sempre encontra o registro.
/// </para>
/// <para>
/// Restrições no banco: quantidade 1–999, preço &gt; 0 e um mesmo produto
/// no máximo uma vez por pedido (índice único <c>PedidoId + ProdutoId</c>).
/// </para>
/// </remarks>
public class ItemPedido
{
    /// <summary>Maior quantidade de um produto em um único item.</summary>
    public const int QuantidadeMaxima = 999;

    /// <summary>Chave primária, gerada pelo banco (identity).</summary>
    public int Id { get; set; }

    /// <summary>
    /// Pedido dono do item. FK com ON DELETE RESTRICT: pedido nunca é excluído (R31), e o
    /// banco recusa apagar um pedido que tenha itens em vez de levar os itens junto em silêncio.
    /// </summary>
    public int PedidoId { get; set; }

    /// <summary>Produto vendido. FK com ON DELETE RESTRICT.</summary>
    public int ProdutoId { get; set; }

    /// <summary>Navegação para o produto (nome atual na tela de detalhe).</summary>
    public Produto? Produto { get; set; }

    /// <summary>Unidades do produto, de 1 a <see cref="QuantidadeMaxima"/>.</summary>
    public int Quantidade { get; set; }

    /// <summary>Preço de uma unidade no momento da criação do pedido (<c>numeric(10,2)</c>).</summary>
    public decimal PrecoUnitario { get; set; }
}
