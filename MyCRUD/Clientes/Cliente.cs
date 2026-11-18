using MyCRUD.Login;

namespace MyCRUD.Clientes;

/// <summary>
/// Entidade de domínio: uma pessoa ou empresa que encomenda com a confeitaria.
/// Mapeada para a tabela <c>Clientes</c>.
/// </summary>
/// <remarks>
/// <para>
/// Como em <c>Produto</c>, esta classe representa o registro do BANCO; a API recebe e
/// devolve apenas os DTOs de <c>ClienteDtos.cs</c>.
/// </para>
/// <para>
/// Decisões do dono (docs/fases-2-4.md): <b>sem CPF</b>, telefone <b>não</b> é único
/// (duas pessoas da mesma casa podem usar o mesmo número) e cliente com pedidos
/// não pode ser excluído (o histórico de vendas precisa continuar íntegro).
/// </para>
/// </remarks>
public class Cliente
{
    /// <summary>Tamanho máximo do nome (<c>varchar(120)</c>), usado no banco e no DTO.</summary>
    public const int NomeTamanhoMaximo = 120;

    /// <summary>
    /// Tamanho da coluna de telefone (<c>varchar(11)</c>): DDD + até 9 dígitos,
    /// guardados <b>só com números</b>.
    /// </summary>
    public const int TelefoneTamanhoMaximo = 11;

    /// <summary>Menor quantidade de dígitos aceita: DDD + 8 dígitos (telefone fixo).</summary>
    public const int TelefoneTamanhoMinimo = 10;

    /// <summary>
    /// Limite do texto digitado na tela, que pode vir com máscara:
    /// <c>"+55 (19) 99999-0000"</c> tem 19 caracteres. Depois de normalizado,
    /// o valor precisa caber em <see cref="TelefoneTamanhoMaximo"/>.
    /// </summary>
    public const int TelefoneEntradaTamanhoMaximo = 20;

    /// <summary>Mesmo limite de e-mail do usuário (RFC 5321), para as duas tabelas não divergirem.</summary>
    public const int EmailTamanhoMaximo = Usuario.EmailTamanhoMaximo;

    /// <summary>Chave primária, gerada pelo banco (identity).</summary>
    public int Id { get; set; }

    /// <summary>Nome de exibição, salvo sem espaços nas pontas.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Somente dígitos, com DDD (10 ou 11 caracteres). O banco reforça a regra com
    /// <c>CHECK ("Telefone" ~ '^[0-9]{10,11}$')</c>.
    /// </summary>
    /// <remarks>
    /// Guardar só os dígitos faz a busca funcionar independentemente da máscara:
    /// quem digitar "(19) 9999" ou "199999" encontra o mesmo cliente.
    /// </remarks>
    public string Telefone { get; set; } = string.Empty;

    /// <summary>Opcional. Quando existe, fica em minúsculas e sem espaços nas pontas.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Remove tudo que não for dígito: <c>"(19) 99999-0000"</c> vira <c>"19999990000"</c>.
    /// </summary>
    /// <remarks>
    /// <c>char.IsAsciiDigit</c> aceita só 0–9. <c>char.IsDigit</c> aceitaria também dígitos
    /// de outros alfabetos (ex.: árabe-índicos), que o <c>CHECK</c> do banco recusaria.
    /// </remarks>
    public static string NormalizarTelefone(string telefone) =>
        new string(telefone.Where(char.IsAsciiDigit).ToArray());

    /// <summary>
    /// Padroniza o e-mail opcional: vazio ou só espaços vira <c>null</c>;
    /// o resto segue a mesma regra do usuário (<see cref="Usuario.NormalizarEmail"/>).
    /// </summary>
    public static string? NormalizarEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : Usuario.NormalizarEmail(email);

    /// <summary>Converte a entidade para o formato devolvido pela API.</summary>
    public ClienteRespostaDto ParaResposta() => new(Id, Nome, Telefone, Email);
}
