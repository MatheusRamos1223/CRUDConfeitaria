namespace MyCRUD.Produtos;

public record CriarProdutoDto(string Nome, decimal Preco, int CategoriaId);
public record AtualizarProdutoDto(string Nome, decimal Preco, int CategoriaId, bool Ativo);
public record ProdutoRespostaDto(int Id, string Nome, decimal Preco, int CategoriaId, bool Ativo);
