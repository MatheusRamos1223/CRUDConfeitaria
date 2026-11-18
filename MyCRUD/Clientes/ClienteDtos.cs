// DTOs de cliente.
// As regras de entrada ficam nos atributos; quem as aplica é o ValidacaoFiltro.
// Ver explicação completa sobre DTOs e o prefixo "property:" em Produtos/ProdutoDtos.cs.

using System.ComponentModel.DataAnnotations;
using MyCRUD.Compartilhado;

namespace MyCRUD.Clientes;

/// <summary>Dados para cadastrar um cliente (<c>POST /clientes</c>).</summary>
/// <param name="Nome">Nome de exibição. Obrigatório, até 120 caracteres.</param>
/// <param name="Telefone">Com DDD; aceita máscara (<c>"(19) 99999-0000"</c>). Salvo só com os 10 ou 11 dígitos.</param>
/// <param name="Email">Opcional. Envie <c>null</c> quando não houver (texto vazio é recusado).</param>
public record CriarClienteDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Cliente.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome,

    [property: Required(ErrorMessage = "Informe o telefone.")]
    [property: MaxLength(Cliente.TelefoneEntradaTamanhoMaximo, ErrorMessage = "O telefone deve ter no máximo {1} caracteres.")]
    [property: TelefoneBrasileiro]
    string Telefone,

    [property: EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [property: MaxLength(Cliente.EmailTamanhoMaximo, ErrorMessage = "O e-mail deve ter no máximo {1} caracteres.")]
    string? Email);

/// <summary>Dados para atualizar um cliente (<c>PUT /clientes/{id}</c>). Substitui todos os campos.</summary>
/// <param name="Nome">Nome de exibição. Obrigatório, até 120 caracteres.</param>
/// <param name="Telefone">Com DDD; aceita máscara. Salvo só com os 10 ou 11 dígitos.</param>
/// <param name="Email">Opcional. <c>null</c> apaga o e-mail que existia.</param>
public record AtualizarClienteDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Cliente.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome,

    [property: Required(ErrorMessage = "Informe o telefone.")]
    [property: MaxLength(Cliente.TelefoneEntradaTamanhoMaximo, ErrorMessage = "O telefone deve ter no máximo {1} caracteres.")]
    [property: TelefoneBrasileiro]
    string Telefone,

    [property: EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [property: MaxLength(Cliente.EmailTamanhoMaximo, ErrorMessage = "O e-mail deve ter no máximo {1} caracteres.")]
    string? Email);

/// <summary>Formato de cliente devolvido pela API.</summary>
/// <param name="Id">Identificador gerado pelo banco.</param>
/// <param name="Nome">Nome de exibição.</param>
/// <param name="Telefone">Somente dígitos (10 ou 11). A tela formata com <c>formatarTelefone</c>.</param>
/// <param name="Email">E-mail em minúsculas, ou <c>null</c>.</param>
public record ClienteRespostaDto(int Id, string Nome, string Telefone, string? Email);

/// <summary>
/// Filtros de <c>GET /clientes</c>, lidos da query string com <c>[AsParameters]</c>:
/// <c>/clientes?busca=maria&amp;pagina=2&amp;tamanho=20</c>.
/// </summary>
/// <param name="Busca">
/// Opcional. Procura o termo no nome (contém, sem diferenciar maiúsculas) <b>ou</b>
/// os dígitos do termo no telefone. Vazio = sem filtro.
/// </param>
/// <param name="Pagina">Página pedida, de 1 a <see cref="Paginacao.PaginaMaxima"/>.</param>
/// <param name="Tamanho">Itens por página, de 1 a 100.</param>
public record FiltroClientes(
    [property: MaxLength(Cliente.NomeTamanhoMaximo, ErrorMessage = "A busca deve ter no máximo {1} caracteres.")]
    string? Busca,

    [property: Range(1, Paginacao.PaginaMaxima, ErrorMessage = "A página deve estar entre {1} e {2}.")]
    int Pagina = 1,

    [property: Range(1, Paginacao.TamanhoMaximo, ErrorMessage = "O tamanho da página deve estar entre {1} e {2}.")]
    int Tamanho = Paginacao.TamanhoPadrao);

/// <summary>
/// Validação de telefone brasileiro: depois de remover a máscara, sobram 10 ou 11 dígitos
/// (DDD + número fixo ou celular).
/// </summary>
/// <remarks>
/// <para>
/// Um atributo próprio deixa a regra declarada no DTO, junto das outras, e o erro sai
/// na chave <c>Telefone</c> do <c>ValidationProblem</c> (R18), sem <c>if</c> no endpoint.
/// </para>
/// <para>
/// <c>null</c> é considerado válido aqui de propósito: quem recusa campo ausente é o
/// <c>[Required]</c>. Assim o usuário não recebe duas mensagens para o mesmo problema.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TelefoneBrasileiroAttribute : ValidationAttribute
{
    public TelefoneBrasileiroAttribute()
        : base("Informe o telefone com DDD: 10 ou 11 dígitos.")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string texto)
        {
            return false;
        }

        var digitos = Cliente.NormalizarTelefone(texto).Length;
        return digitos is >= Cliente.TelefoneTamanhoMinimo and <= Cliente.TelefoneTamanhoMaximo;
    }
}
