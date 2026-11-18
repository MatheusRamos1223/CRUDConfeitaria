namespace MyCRUD.Pedidos;

/// <summary>
/// Situação de um pedido no fluxo da confeitaria.
/// </summary>
/// <remarks>
/// <para>Transições permitidas (seção 2.6 do contrato):</para>
/// <code>
/// Aberto ──confirmar──► Confirmado ──entregar──► Entregue
///   │                       │
///   └──cancelar──► Cancelado ◄──cancelar (Admin)──┘
/// </code>
/// <para>
/// <see cref="Entregue"/> e <see cref="Cancelado"/> são finais: nenhuma ação os altera.
/// </para>
/// <para>
/// No banco e no JSON o valor trafega como <b>texto</b> (<c>"Aberto"</c>), não como número:
/// fica legível no Supabase e no Scalar, e um CHECK no banco recusa qualquer outro valor.
/// Por isso a ordem dos membros pode mudar sem quebrar dados já gravados.
/// </para>
/// </remarks>
public enum StatusPedido
{
    /// <summary>Recém-criado; ainda pode ser confirmado ou cancelado por qualquer usuário.</summary>
    Aberto,

    /// <summary>Aceito pela confeitaria; pode ser entregue (qualquer) ou cancelado (só Admin).</summary>
    Confirmado,

    /// <summary>Entregue ao cliente. Final.</summary>
    Entregue,

    /// <summary>Cancelado. Final. O pedido continua no banco para histórico (nunca é excluído).</summary>
    Cancelado
}
