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
    /// Identificador da categoria. Ainda sem tabela de categorias
    /// (sem chave estrangeira) — ver "Limites atuais" no README.
    /// </summary>
    public int CategoriaId { get; set; }

    /// <summary>Produto disponível para venda.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Converte a entidade para o formato devolvido pela API.</summary>
    public ProdutoRespostaDto ParaResposta() =>
        new(Id, Nome, Preco, CategoriaId, Ativo);
}
