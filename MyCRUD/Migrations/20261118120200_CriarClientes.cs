using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyCRUD.Migrations
{
    /// <summary>
    /// M3 — cria a tabela de clientes (nova e vazia).
    /// </summary>
    /// <remarks>
    /// <para>O CHECK <c>CK_Clientes_Telefone</c> aceita só 10 ou 11 dígitos: a API já salva o
    /// telefone sem máscara, e o banco recusa um INSERT feito por fora dela que não siga a regra.
    /// <c>CK_Clientes_Nome</c> recusa nome vazio; <c>CK_Clientes_Email</c> exige o e-mail já
    /// normalizado (minúsculas, sem espaços nas pontas) ou NULL.</para>
    /// <para>Índice <c>(Nome, Id)</c>: a mesma ordem da listagem (R19). RLS ligado sem policies (R38).</para>
    /// </remarks>
    public partial class CriarClientes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.CheckConstraint("CK_Clientes_Email", "\"Email\" = lower(btrim(\"Email\"))");
                    table.CheckConstraint("CK_Clientes_Nome", "btrim(\"Nome\") <> ''");
                    table.CheckConstraint("CK_Clientes_Telefone", "\"Telefone\" ~ '^[0-9]{10,11}$'");
                });

            // R38: RLS sem policies — a Data API do Supabase (chave anon) não enxerga a tabela.
            migrationBuilder.Sql("""
                ALTER TABLE "Clientes" ENABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Nome_Id",
                table: "Clientes",
                columns: new[] { "Nome", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Clientes" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
