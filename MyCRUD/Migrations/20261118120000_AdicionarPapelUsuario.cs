using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCRUD.Migrations
{
    /// <summary>
    /// M1 — adiciona o papel (Admin/Operador) aos usuários.
    /// </summary>
    /// <remarks>
    /// <para>Antes de aplicar no Supabase, guarde o resultado de
    /// <c>SELECT count(*) FROM "Usuarios";</c>: todos esses usuários virarão Operador
    /// (DEFAULT da coluna, R33). O Admin é promovido no primeiro login com o e-mail
    /// configurado em <c>Admin:Email</c> (R34).</para>
    /// <para>O CHECK garante que só os valores do enum <c>Papel</c> sejam gravados,
    /// mesmo por SQL manual.</para>
    /// </remarks>
    public partial class AdicionarPapelUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Papel",
                table: "Usuarios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Operador");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios",
                sql: "\"Papel\" IN ('Admin','Operador')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Papel",
                table: "Usuarios");
        }
    }
}
