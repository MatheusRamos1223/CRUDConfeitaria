using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyCRUD.Migrations
{
    /// <summary>
    /// M4 — cria as tabelas de pedidos e itens (novas e vazias).
    /// </summary>
    /// <remarks>
    /// <para>Sem coluna de total: o total é calculado na consulta (R25).</para>
    /// <para>Defaults (R26): <c>Status</c> = <c>'Aberto'</c> e <c>CriadoEm</c> = <c>now()</c>.</para>
    /// <para>Auditoria (R40): <c>CriadoPorId</c> (obrigatório) e <c>StatusAlteradoEm</c> /
    /// <c>StatusAlteradoPorId</c> (nulos até a 1ª transição), FKs para <c>Usuarios</c>.</para>
    /// <para>Todas as FKs são RESTRICT: <c>Pedidos → Clientes</c>, <c>Pedidos → Usuarios</c> (2),
    /// <c>ItensPedido → Produtos</c> e <c>ItensPedido → Pedidos</c> (pedido nunca é excluído, R31;
    /// o banco recusa em vez de apagar itens em cascata).</para>
    /// <para>O índice único <c>(PedidoId, ProdutoId)</c> também serve à FK <c>PedidoId</c>,
    /// por isso não há <c>IX_ItensPedido_PedidoId</c> separado. <c>(DataEntrega, Id)</c> segue a
    /// ordem da listagem (R6). RLS ligado sem policies nas duas tabelas (R38).</para>
    /// </remarks>
    public partial class CriarPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    DataEntrega = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Aberto"),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CriadoPorId = table.Column<int>(type: "integer", nullable: false),
                    StatusAlteradoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusAlteradoPorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.CheckConstraint("CK_Pedidos_Status", "\"Status\" IN ('Aberto','Confirmado','Entregue','Cancelado')");
                    table.ForeignKey(
                        name: "FK_Pedidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Usuarios_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Usuarios_StatusAlteradoPorId",
                        column: x => x.StatusAlteradoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    ProdutoId = table.Column<int>(type: "integer", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPedido", x => x.Id);
                    table.CheckConstraint("CK_ItensPedido_PrecoUnitario", "\"PrecoUnitario\" > 0");
                    table.CheckConstraint("CK_ItensPedido_Quantidade", "\"Quantidade\" BETWEEN 1 AND 999");
                    table.ForeignKey(
                        name: "FK_ItensPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensPedido_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // R38: RLS sem policies — a Data API do Supabase (chave anon) não enxerga as tabelas.
            migrationBuilder.Sql("""
                ALTER TABLE "Pedidos" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "ItensPedido" ENABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_PedidoId_ProdutoId",
                table: "ItensPedido",
                columns: new[] { "PedidoId", "ProdutoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_ProdutoId",
                table: "ItensPedido",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClienteId",
                table: "Pedidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_CriadoPorId",
                table: "Pedidos",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_DataEntrega_Id",
                table: "Pedidos",
                columns: new[] { "DataEntrega", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_StatusAlteradoPorId",
                table: "Pedidos",
                column: "StatusAlteradoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ItensPedido" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "Pedidos" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "ItensPedido");

            migrationBuilder.DropTable(
                name: "Pedidos");
        }
    }
}
