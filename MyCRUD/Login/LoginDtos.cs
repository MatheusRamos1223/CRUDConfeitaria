// DTOs de autenticação e cadastro de usuário.
// As regras de entrada ficam nos atributos; quem as aplica é o ValidacaoFiltro.
// Ver explicação completa em Produtos/ProdutoDtos.cs.

using System.ComponentModel.DataAnnotations;

namespace MyCRUD.Login;

/// <summary>Credenciais para <c>POST /login</c>.</summary>
/// <param name="Email">E-mail cadastrado (maiúsculas e espaços são ignorados).</param>
/// <param name="Senha">Senha em texto puro; trafega apenas no corpo da requisição.</param>
public record LoginDto(
    [property: Required(ErrorMessage = "Informe o e-mail.")]
    [property: MaxLength(Usuario.EmailTamanhoMaximo, ErrorMessage = "E-mail muito longo.")]
    string Email,

    [property: Required(ErrorMessage = "Informe a senha.")]
    [property: MaxLength(Usuario.SenhaTamanhoMaximo, ErrorMessage = "Senha muito longa.")]
    string Senha);

/// <summary>Dados para <c>POST /login/cadastro</c>.</summary>
/// <remarks>
/// De propósito, NÃO existe campo de papel: se existisse, qualquer pessoa poderia
/// se cadastrar já como Admin. Todo cadastro cria um <see cref="Papel.Operador"/>.
/// </remarks>
/// <param name="Nome">Nome de exibição. Até 120 caracteres.</param>
/// <param name="Email">E-mail válido e ainda não cadastrado.</param>
/// <param name="Senha">Entre 8 e 128 caracteres.</param>
public record CriarUsuarioDto(
    [property: Required(ErrorMessage = "Informe o nome.")]
    [property: MaxLength(Usuario.NomeTamanhoMaximo, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    string Nome,

    [property: Required(ErrorMessage = "Informe o e-mail.")]
    [property: EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [property: MaxLength(Usuario.EmailTamanhoMaximo, ErrorMessage = "O e-mail deve ter no máximo {1} caracteres.")]
    string Email,

    [property: Required(ErrorMessage = "Informe a senha.")]
    [property: StringLength(Usuario.SenhaTamanhoMaximo, MinimumLength = Usuario.SenhaTamanhoMinimo, ErrorMessage = "A senha deve ter entre {2} e {1} caracteres.")]
    string Senha);

/// <summary>Usuário devolvido pela API. Nunca inclui senha nem hash.</summary>
/// <param name="Id">Identificador gerado pelo banco.</param>
/// <param name="Nome">Nome de exibição.</param>
/// <param name="Email">E-mail normalizado.</param>
/// <param name="Papel">Perfil de acesso; no JSON vem como texto (<c>"Admin"</c> ou <c>"Operador"</c>).</param>
public record UsuarioRespostaDto(int Id, string Nome, string Email, Papel Papel);
