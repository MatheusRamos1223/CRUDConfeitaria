using MyCRUD.Categorias;

namespace MyCRUD.Produtos;

/// <summary>
/// Entidade de domínio: um produto vendido pela confeitaria.
/// Mapeada para a tabela <c>Produtos</c> pelo Entity Framework.
/// </summary>
/// <remarks>
/// Esta classe representa o registro do BANCO. Ela nunca é recebida nem
/// devolvida diretamente pela API — para isso existem os DTOs em
/// <c>ProdutoDtos.cs</c>. Isso evita que mudanças no banco quebrem
/// o contrato da API, e que o cliente altere campos que não deveria.
/// </remarks>
public class Produto
{
    /// <summary>Tamanho máximo do nome, usado no banco e na validação do DTO.</summary>
    public const int NomeTamanhoMaximo = 120;

    /// <summary>Maior preço que cabe em <c>numeric(10,2)</c>.</summary>
    public const double PrecoMaximo = 99_999_999.99;

    /// <summary>Chave primária, gerada pelo banco (identity).</summary>
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Preço em reais, com 2 casas decimais (<c>numeric(10,2)</c>).</summary>
    public decimal Preco { get; set; }

    /// <summary>
    /// Chave estrangeira para <c>Categorias.Id</c> (<c>ON DELETE RESTRICT</c>):
    /// o banco recusa produto com categoria inexistente e impede excluir
    /// uma categoria que ainda tem produtos.
    /// </summary>
    public int CategoriaId { get; set; }

    /// <summary>
    /// Navegação para a categoria. Só vem preenchida quando a consulta pede
    /// (ex.: <c>p.Categoria!.Nome</c> dentro de um <c>Select</c>, que vira um JOIN).
    /// </summary>
    public Categoria? Categoria { get; set; }

    /// <summary>
    /// Produto disponível para venda. Nenhum produto é apagado do banco:
    /// "excluir" na API apenas desativa (soft delete), para que pedidos antigos
    /// continuem apontando para um produto que existe.
    /// </summary>
    /// <remarks>Só muda por <c>DELETE /produtos/{id}</c> e <c>POST /produtos/{id}/reativar</c> (Admin).</remarks>
    public bool Ativo { get; set; } = true;

    /// <summary>Converte a entidade para o formato devolvido pela API.</summary>
    /// <param name="categoriaNome">Nome da categoria, lido do banco por quem chama.</param>
    public ProdutoRespostaDto ParaResposta(string categoriaNome) =>
        new(Id, Nome, Preco, CategoriaId, categoriaNome, Ativo);
}
