namespace MyCRUD.Categorias;

/// <summary>
/// Entidade de domínio: uma categoria que agrupa produtos do cardápio
/// (ex.: "Bolos", "Doces finos"). Mapeada para a tabela <c>Categorias</c>.
/// </summary>
/// <remarks>
/// <para>
/// Como em <c>Produto</c>, esta classe representa o registro do BANCO: a API
/// recebe e devolve apenas os DTOs de <c>CategoriaDtos.cs</c>.
/// </para>
/// <para><b>Regras garantidas pelo banco</b> (não só pela aplicação):</para>
/// <list type="bullet">
///   <item>Nome único sem diferenciar maiúsculas: índice único sobre
///   <c>lower("Nome")</c> (<c>IX_Categorias_Nome_Minusculo</c>), criado por SQL
///   na migration — o EF não modela índice de expressão.</item>
///   <item>Categoria com produtos não pode ser apagada: a chave estrangeira
///   <c>Produtos.CategoriaId</c> usa <c>ON DELETE RESTRICT</c>.</item>
/// </list>
/// <para>
/// Não há navegação <c>Produtos</c> aqui de propósito: a contagem de produtos
/// é feita por subconsulta na listagem, e a relação é configurada a partir de
/// <c>Produto.Categoria</c> no <c>AppDbContext</c>.
/// </para>
/// </remarks>
public class Categoria
{
    /// <summary>Tamanho máximo do nome, usado no banco (<c>varchar(60)</c>) e na validação do DTO.</summary>
    public const int NomeTamanhoMaximo = 60;

    /// <summary>Chave primária, gerada pelo banco (identity).</summary>
    public int Id { get; set; }

    /// <summary>Nome exibido, já sem espaços nas pontas (<c>Trim</c>).</summary>
    public string Nome { get; set; } = string.Empty;
}
