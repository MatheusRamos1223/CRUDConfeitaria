// DTOs (Data Transfer Objects) de produto.
//
// Um DTO é o "formato do pacote" que trafega entre o cliente e a API.
// Ele declara QUAIS campos entram/saem e QUAIS regras de entrada valem.
//
// O prefixo "property:" aplica o atributo à propriedade gerada pelo record
// (e não ao parâmetro do construtor). É isso que permite ao ValidacaoFiltro
// encontrar e verificar as regras.

using System.ComponentModel.DataAnnotations;

namespace MyCRUD.Produtos;

/// <summary>Dados para cadastrar um produto (<c>POST /produtos</c>).</summary>
/// <param name="Nome">Nome exibido. Obrigatório, até 120 caracteres.</param>
/// <param name="Preco">Preço em reais. Maior que zero.</param>
/// <param name="CategoriaId">Identificador da categoria. Inteiro positivo.</param>
public record CriarProdutoDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Produto.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome,

    [property: Range(0, Produto.PrecoMaximo, MinimumIsExclusive = true, ErrorMessage = "O preço deve ser maior que zero e menor que 100 milhões.")]
    decimal Preco,

    [property: Range(1, int.MaxValue, ErrorMessage = "Informe um ID de categoria positivo.")]
    int CategoriaId);

/// <summary>Dados para atualizar um produto (<c>PUT /produtos/{id}</c>).</summary>
/// <param name="Nome">Nome exibido. Obrigatório, até 120 caracteres.</param>
/// <param name="Preco">Preço em reais. Maior que zero.</param>
/// <param name="CategoriaId">Identificador da categoria. Inteiro positivo.</param>
/// <param name="Ativo">Define se o produto está disponível para venda.</param>
public record AtualizarProdutoDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Produto.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome,

    [property: Range(0, Produto.PrecoMaximo, MinimumIsExclusive = true, ErrorMessage = "O preço deve ser maior que zero e menor que 100 milhões.")]
    decimal Preco,

    [property: Range(1, int.MaxValue, ErrorMessage = "Informe um ID de categoria positivo.")]
    int CategoriaId,

    bool Ativo);

/// <summary>Formato de produto devolvido pela API.</summary>
/// <param name="Id">Identificador gerado pelo banco.</param>
/// <param name="Nome">Nome exibido.</param>
/// <param name="Preco">Preço em reais.</param>
/// <param name="CategoriaId">Identificador da categoria.</param>
/// <param name="Ativo">Disponível para venda.</param>
public record ProdutoRespostaDto(
    int Id,
    string Nome,
    decimal Preco,
    int CategoriaId,
    bool Ativo);

