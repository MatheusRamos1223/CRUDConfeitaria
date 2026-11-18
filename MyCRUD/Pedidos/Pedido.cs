using MyCRUD.Clientes;

namespace MyCRUD.Pedidos;

/// <summary>
/// Entidade de domínio: uma encomenda de um cliente, com data de entrega e itens.
/// Mapeada para a tabela <c>Pedidos</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Imutável depois de criado</b> (decisão D5): cliente, data e itens não mudam.
/// Só o <see cref="Status"/> avança, pelas rotas de ação (<c>/confirmar</c>,
/// <c>/entregar</c>, <c>/cancelar</c>). Para corrigir um pedido, cancela-se e cria-se outro.
/// </para>
/// <para>
/// <b>Não existe coluna de total</b> (regra R25): o total é sempre
/// Σ(Quantidade × PrecoUnitario) calculado na consulta. Guardar o total seria
/// guardar a mesma informação duas vezes — e as duas cópias poderiam divergir.
/// </para>
/// </remarks>
public class Pedido
{
    /// <summary>Máximo de itens (linhas de produto) em um pedido.</summary>
    public const int ItensMaximo = 50;

    /// <summary>Tamanho da coluna <c>Status</c> (<c>varchar(20)</c>): cabe o maior nome do enum.</summary>
    public const int StatusTamanhoMaximo = 20;

    /// <summary>
    /// Fuso horário da confeitaria, no formato IANA. Funciona em Linux e,
    /// a partir do .NET 6, também em Windows (conversão automática via ICU).
    /// </summary>
    public const string FusoHorarioId = "America/Sao_Paulo";

    // static: o EF Core ignora membros estáticos, então isto não vira coluna.
    private static readonly TimeZoneInfo FusoHorario =
        TimeZoneInfo.FindSystemTimeZoneById(FusoHorarioId);

    /// <summary>Chave primária, gerada pelo banco (identity). É o "Nº" do pedido na tela.</summary>
    public int Id { get; set; }

    /// <summary>Cliente que fez o pedido. FK com ON DELETE RESTRICT: cliente com pedido não é excluído.</summary>
    public int ClienteId { get; set; }

    /// <summary>Navegação para o cliente (preenchida só quando a consulta pede o join).</summary>
    public Cliente? Cliente { get; set; }

    /// <summary>
    /// Dia em que a encomenda deve ser entregue (<c>date</c>, sem hora).
    /// <see cref="DateOnly"/> evita o problema clássico de "meia-noite em UTC
    /// virar o dia anterior" que aconteceria com <see cref="DateTime"/>.
    /// </summary>
    public DateOnly DataEntrega { get; set; }

    /// <summary>Situação atual. Nasce <see cref="StatusPedido.Aberto"/> (R26).</summary>
    public StatusPedido Status { get; set; } = StatusPedido.Aberto;

    /// <summary>
    /// Momento da criação, sempre em UTC (<c>timestamp with time zone</c>).
    /// Também marca quando os preços dos itens foram congelados.
    /// </summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Itens do pedido (relação 1:N, ON DELETE CASCADE).</summary>
    public List<ItemPedido> Itens { get; set; } = [];

    /// <summary>
    /// Data de "hoje" no fuso da confeitaria, usada para recusar entrega no passado (R21).
    /// </summary>
    /// <remarks>
    /// O servidor pode rodar em UTC: às 22h de São Paulo já é o dia seguinte em UTC.
    /// Sem converter o fuso, um pedido para "hoje" seria recusado à noite.
    /// </remarks>
    /// <param name="agoraUtc">Instante atual em UTC (parâmetro para facilitar testes).</param>
    public static DateOnly HojeNaConfeitaria(DateTime agoraUtc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, FusoHorario));

    /// <summary>Atalho de <see cref="HojeNaConfeitaria(DateTime)"/> com o relógio atual.</summary>
    public static DateOnly HojeNaConfeitaria() => HojeNaConfeitaria(DateTime.UtcNow);
}
