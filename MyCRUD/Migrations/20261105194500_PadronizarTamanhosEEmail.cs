using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCRUD.Migrations
{
    /// <summary>
    /// Limita o tamanho das colunas de texto e padroniza dados já existentes.
    /// </summary>
    /// <remarks>
    /// <para>Antes de aplicar no Supabase, confira se há e-mails que só diferem por
    /// maiúsculas (eles colidiriam no índice único depois da normalização):</para>
    /// <code>
    /// SELECT lower(btrim("Email")), count(*) FROM "Usuarios"
    /// GROUP BY 1 HAVING count(*) > 1;
    /// </code>
    /// <para>Se a consulta retornar linhas, a migration falha inteira e nada é alterado
    /// (o PostgreSQL executa migrations dentro de uma transação). Resolva os duplicados e rode de novo.</para>
    /// </remarks>
    public partial class PadronizarTamanhosEEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Limpa os dados existentes para que respeitem as novas regras da aplicação.
            migrationBuilder.Sql("""
                UPDATE "Usuarios" SET "Email" = lower(btrim("Email")), "Nome" = btrim("Nome");
                UPDATE "Produtos" SET "Nome" = btrim("Nome");
                """);

            // 2. text -> varchar(n). Falha se algum valor existente for maior que o limite.
            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Usuarios",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Usuarios",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Produtos",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        /// <remarks>Volta as colunas para <c>text</c>. A normalização de e-mail e nomes não é desfeita.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Usuarios",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Usuarios",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "Produtos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);
        }
    }
}
