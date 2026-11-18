namespace MyCRUD.Login;

/// <summary>
/// Entidade de domínio: uma pessoa que pode acessar o sistema.
/// Mapeada para a tabela <c>Usuarios</c>.
/// </summary>
/// <remarks>
/// A senha NUNCA é armazenada. Guardamos apenas o hash gerado por
/// <c>PasswordHasher</c> (PBKDF2 com salt aleatório e milhares de iterações).
/// Mesmo quem tiver acesso ao banco não consegue recuperar a senha original.
/// </remarks>
public class Usuario
{
    public const int NomeTamanhoMaximo = 120;

    /// <summary>Limite prático de e-mail definido pela RFC 5321.</summary>
    public const int EmailTamanhoMaximo = 254;

    public const int SenhaTamanhoMinimo = 8;

    /// <summary>
    /// Limite superior da senha em texto puro. Evita que alguém envie
    /// uma "senha" de megabytes só para gastar CPU no cálculo do hash.
    /// </summary>
    public const int SenhaTamanhoMaximo = 128;

    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Sempre salvo em minúsculas e sem espaços nas pontas. Único no banco.</summary>
    public string Email { get; set; } = string.Empty;

    public string SenhaHash { get; set; } = string.Empty;

    /// <summary>
    /// Padroniza o e-mail antes de salvar ou comparar, para que
    /// <c>Ana@Email.com</c> e <c>ana@email.com </c> sejam o mesmo usuário.
    /// </summary>
    public static string NormalizarEmail(string email) =>
        email.Trim().ToLowerInvariant();

    public UsuarioRespostaDto ParaResposta() => new(Id, Nome, Email);
}
