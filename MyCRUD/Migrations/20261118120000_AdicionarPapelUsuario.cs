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
    /// <para><b>RLS</b> (R38, ADR-003): liga Row Level Security, <b>sem policies</b>, nas tabelas
    /// que já existiam (<c>Produtos</c>, <c>Usuarios</c>) e na de histórico do EF. Sem policy,
    /// os papéis <c>anon</c>/<c>authenticated</c> da Data API do Supabase (REST com a chave
    /// <c>anon</c>, que é pública) não leem nem gravam nada. A API não é afetada: ela conecta
    /// como o dono das tabelas (<c>postgres</c>), e o dono ignora RLS (não usamos
    /// <c>FORCE ROW LEVEL SECURITY</c>). As tabelas novas ligam RLS na própria migration (M2–M4).</para>
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

            // R38: RLS sem policies = nega tudo para quem não é dono (Data API do Supabase).
            // __EFMigrationsHistory também fica em "public": sem RLS, quem tem a chave anon
            // poderia apagar linhas e fazer o próximo "database update" reaplicar migrations.
            migrationBuilder.Sql("""
                ALTER TABLE "Produtos" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "Usuarios" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "__EFMigrationsHistory" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "Usuarios" DISABLE ROW LEVEL SECURITY;
                ALTER TABLE "Produtos" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_Papel",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "Papel",
                table: "Usuarios");
        }
    }
}
