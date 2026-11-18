// DTOs (Data Transfer Objects) de categoria.
//
// Mesmo padrão de Produtos/ProdutoDtos.cs: o prefixo "property:" aplica o
// atributo à propriedade gerada pelo record, que é onde o ValidacaoFiltro
// procura as regras. Os tamanhos vêm das constantes da entidade, para que
// banco e validação nunca divirjam.

using System.ComponentModel.DataAnnotations;
using MyCRUD.Compartilhado;

namespace MyCRUD.Categorias;

/// <summary>Dados para cadastrar uma categoria (<c>POST /categorias</c>, somente Admin).</summary>
/// <param name="Nome">Nome exibido. Obrigatório, até 60 caracteres, único sem diferenciar maiúsculas.</param>
/// <remarks>
/// <c>[Required]</c> também recusa texto só com espaços (R3).
/// A unicidade não é validada aqui: depende do banco e é conferida no endpoint (409).
/// </remarks>
public record CriarCategoriaDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Categoria.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome);

/// <summary>Dados para renomear uma categoria (<c>PUT /categorias/{id}</c>, somente Admin).</summary>
/// <param name="Nome">Novo nome. Obrigatório, até 60 caracteres, único sem diferenciar maiúsculas.</param>
public record AtualizarCategoriaDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Categoria.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome);

/// <summary>Formato de categoria devolvido pela API.</summary>
/// <param name="Id">Identificador gerado pelo banco.</param>
/// <param name="Nome">Nome exibido.</param>
/// <param name="QuantidadeProdutos">
/// Produtos ligados à categoria, ativos <b>e</b> inativos. Maior que zero = não pode ser excluída (R9).
/// </param>
public record CategoriaRespostaDto(
    int Id,
    string Nome,
    int QuantidadeProdutos);

/// <summary>
/// Filtros da listagem <c>GET /categorias?nome=&amp;pagina=&amp;tamanho=</c>.
/// </summary>
/// <remarks>
/// Ligado à query string com <c>[AsParameters]</c>: cada parâmetro do construtor
/// vira um parâmetro de consulta com o mesmo nome. Parâmetro ausente usa o valor padrão.
/// </remarks>
/// <param name="Nome">Trecho do nome (contém, sem diferenciar maiúsculas). Vazio = sem filtro. Até 60 caracteres.</param>
/// <param name="Pagina">Página pedida, de 1 a <see cref="Paginacao.PaginaMaxima"/>.</param>
/// <param name="Tamanho">Itens por página, de 1 a <see cref="Paginacao.TamanhoMaximo"/>.</param>
public record FiltroCategorias(
    // Nenhum nome de categoria é maior que a coluna: termo maior só gastaria o banco.
    [property: MaxLength(Categoria.NomeTamanhoMaximo, ErrorMessage = "O filtro de nome deve ter no máximo {1} caracteres.")]
    string? Nome,

    [property: Range(1, Paginacao.PaginaMaxima, ErrorMessage = "A página deve estar entre {1} e {2}.")]
    int Pagina = 1,

    [property: Range(1, Paginacao.TamanhoMaximo, ErrorMessage = "O tamanho da página deve estar entre {1} e {2}.")]
    int Tamanho = Paginacao.TamanhoPadrao);
