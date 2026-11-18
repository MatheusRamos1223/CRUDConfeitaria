// DTOs (Data Transfer Objects) de produto.
//
// Um DTO é o "formato do pacote" que trafega entre o cliente e a API.
// Ele declara QUAIS campos entram/saem e QUAIS regras de entrada valem.
//
// O prefixo "property:" aplica o atributo à propriedade gerada pelo record
// (e não ao parâmetro do construtor). É isso que permite ao ValidacaoFiltro
// encontrar e verificar as regras.

using System.ComponentModel.DataAnnotations;
using MyCRUD.Compartilhado;

namespace MyCRUD.Produtos;

/// <summary>Dados para cadastrar um produto (<c>POST /produtos</c>).</summary>
/// <param name="Nome">Nome exibido. Obrigatório, até 120 caracteres.</param>
/// <param name="Preco">Preço em reais. Maior que zero.</param>
/// <param name="CategoriaId">Identificador de uma categoria existente (senão, 400).</param>
public record CriarProdutoDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Produto.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome,

    [property: Range(0, Produto.PrecoMaximo, MinimumIsExclusive = true, ErrorMessage = "O preço deve ser maior que zero e menor que 100 milhões.")]
    decimal Preco,

    [property: Range(1, int.MaxValue, ErrorMessage = "Informe um ID de categoria positivo.")]
    int CategoriaId);

/// <summary>Dados para atualizar um produto (<c>PUT /produtos/{id}</c>).</summary>
/// <remarks>
/// Não tem campo <c>Ativo</c>: se tivesse, um Operador poderia desativar um produto
/// pelo formulário de edição. Desativar/reativar são rotas próprias, só de Admin.
/// </remarks>
/// <param name="Nome">Nome exibido. Obrigatório, até 120 caracteres.</param>
/// <param name="Preco">Preço em reais. Maior que zero.</param>
/// <param name="CategoriaId">Identificador de uma categoria existente.</param>
public record AtualizarProdutoDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Produto.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome,

    [property: Range(0, Produto.PrecoMaximo, MinimumIsExclusive = true, ErrorMessage = "O preço deve ser maior que zero e menor que 100 milhões.")]
    decimal Preco,

    [property: Range(1, int.MaxValue, ErrorMessage = "Informe um ID de categoria positivo.")]
    int CategoriaId);

/// <summary>Formato de produto devolvido pela API.</summary>
/// <param name="Id">Identificador gerado pelo banco.</param>
/// <param name="Nome">Nome exibido.</param>
/// <param name="Preco">Preço em reais.</param>
/// <param name="CategoriaId">Identificador da categoria.</param>
/// <param name="CategoriaNome">Nome da categoria, para a tela não precisar de uma segunda consulta.</param>
/// <param name="Ativo">Disponível para venda.</param>
public record ProdutoRespostaDto(
    int Id,
    string Nome,
    decimal Preco,
    int CategoriaId,
    string CategoriaNome,
    bool Ativo);

/// <summary>Quais produtos a listagem devolve, conforme a situação.</summary>
/// <remarks>Na query string trafega como texto: <c>?situacao=Inativos</c>.</remarks>
public enum SituacaoProduto
{
    /// <summary>Só produtos disponíveis para venda (padrão da tela).</summary>
    Ativos,

    /// <summary>Só produtos desativados.</summary>
    Inativos,

    /// <summary>Ativos e inativos.</summary>
    Todos
}

/// <summary>
/// Filtros e paginação de <c>GET /produtos</c>, lidos da query string.
/// </summary>
/// <remarks>
/// <para>
/// No endpoint o parâmetro é marcado com <c>[AsParameters]</c>: o ASP.NET preenche cada
/// campo do record a partir da query string com o mesmo nome
/// (<c>?nome=bolo&amp;categoriaId=2&amp;situacao=Todos&amp;pagina=1&amp;tamanho=20</c>).
/// Campo ausente na URL fica com o valor padrão declarado aqui.
/// </para>
/// <para>
/// Os atributos são verificados pelo mesmo <c>ValidacaoFiltro</c> dos DTOs de corpo
/// (<c>.ComValidacao&lt;FiltroProdutos&gt;()</c>): <c>tamanho=101</c> devolve 400.
/// </para>
/// </remarks>
/// <param name="Nome">Trecho do nome (contém, sem diferenciar maiúsculas). Vazio = sem filtro.</param>
/// <param name="CategoriaId">Só produtos desta categoria. Ausente = todas.</param>
/// <param name="Situacao">Ativos (padrão), Inativos ou Todos.</param>
/// <param name="Pagina">Página pedida, começando em 1.</param>
/// <param name="Tamanho">Itens por página, de 1 a 100.</param>
public record FiltroProdutos(
    [property: MaxLength(Produto.NomeTamanhoMaximo, ErrorMessage = "O filtro de nome deve ter no máximo {1} caracteres.")]
    string? Nome,

    [property: Range(1, int.MaxValue, ErrorMessage = "Informe um ID de categoria positivo.")]
    int? CategoriaId,

    // EnumDataType recusa números fora do enum (ex.: ?situacao=7), que o binding aceitaria.
    [property: EnumDataType(typeof(SituacaoProduto), ErrorMessage = "Situação inválida. Use Ativos, Inativos ou Todos.")]
    SituacaoProduto Situacao = SituacaoProduto.Ativos,

    [property: Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    int Pagina = 1,

    [property: Range(1, Paginacao.TamanhoMaximo, ErrorMessage = "O tamanho da página deve estar entre {1} e {2}.")]
    int Tamanho = Paginacao.TamanhoPadrao);

