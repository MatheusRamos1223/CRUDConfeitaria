namespace MyCRUD.Produtos;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int CategoriaId { get; set; }
    public bool Ativo { get; set; } = true;
}
