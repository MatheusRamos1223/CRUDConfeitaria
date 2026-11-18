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

    /// <summary>
    /// Tamanho da coluna <c>Papel</c> (<c>varchar(20)</c>). Sobra espaço para
    /// papéis futuros sem precisar de migration só para aumentar a coluna.
    /// </summary>
    public const int PapelTamanhoMaximo = 20;

    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Sempre salvo em minúsculas e sem espaços nas pontas. Único no banco.</summary>
    public string Email { get; set; } = string.Empty;

    public string SenhaHash { get; set; } = string.Empty;

    /// <summary>
    /// Perfil de acesso. Todo usuário nasce <see cref="MyCRUD.Login.Papel.Operador"/>; só vira
    /// <see cref="MyCRUD.Login.Papel.Admin"/> ao entrar com o e-mail configurado em <c>Admin:Email</c>.
    /// </summary>
    /// <remarks>
    /// Salvo como texto (<c>'Admin'</c>/<c>'Operador'</c>) e protegido por um CHECK no banco.
    /// Nunca é rebaixado automaticamente: rebaixar um Admin é uma decisão manual (SQL).
    /// </remarks>
    public Papel Papel { get; set; } = Papel.Operador;

    /// <summary>
    /// Padroniza o e-mail antes de salvar ou comparar, para que
    /// <c>Ana@Email.com</c> e <c>ana@email.com </c> sejam o mesmo usuário.
    /// </summary>
    public static string NormalizarEmail(string email) =>
        email.Trim().ToLowerInvariant();

    public UsuarioRespostaDto ParaResposta() => new(Id, Nome, Email, Papel);
}
