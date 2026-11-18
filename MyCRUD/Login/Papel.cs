namespace MyCRUD.Login;

/// <summary>
/// Papel (perfil de acesso) de um <see cref="Usuario"/>. Define o que ele pode fazer na API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Operador</b> é o uso do dia a dia: cadastra clientes e produtos, cria e acompanha pedidos.
/// <b>Admin</b> pode, além disso, desativar/reativar produtos, gerenciar categorias,
/// excluir clientes e cancelar pedidos já confirmados.
/// </para>
/// <para>
/// No banco e no JSON o valor trafega como <b>texto</b> (<c>"Admin"</c>, <c>"Operador"</c>),
/// não como número: fica legível no Supabase e no Scalar, e reordenar o enum
/// não corrompe os dados já salvos.
/// </para>
/// <para>
/// O nome do valor (<c>nameof(Papel.Admin)</c>) também é o texto da claim de papel
/// no cookie — é ele que a política <c>Politicas.SomenteAdmin</c> confere.
/// </para>
/// </remarks>
public enum Papel
{
    /// <summary>Papel padrão de todo usuário novo. Valor 0 = padrão do C#.</summary>
    Operador = 0,

    /// <summary>Administrador. Definido pela configuração <c>Admin:Email</c>, nunca pelo cadastro.</summary>
    Admin = 1
}
