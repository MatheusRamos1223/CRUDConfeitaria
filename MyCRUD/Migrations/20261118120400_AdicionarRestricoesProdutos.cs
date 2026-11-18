using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCRUD.Migrations
{
    /// <summary>
    /// M5 — CHECKs na tabela antiga <c>Produtos</c>: preço maior que zero e nome não vazio.
    /// </summary>
    /// <remarks>
    /// <para><b>Por que uma migration própria (e não dentro da M1)?</b> <c>Produtos</c> é a única
    /// tabela com dados reais da Fase 1. Um <c>ADD CONSTRAINT ... CHECK</c> confere todas as linhas
    /// existentes e falha se alguma violar a regra. Separada, a falha fica restrita a esta
    /// migration: M1–M4 (papel, categorias, clientes, pedidos) já estão aplicadas, corrige-se o
    /// dado e roda-se de novo só a M5. A M1 continua com um único assunto (papel + RLS).</para>
    /// <para>Antes de aplicar no Supabase, deve voltar vazio:</para>
    /// <code>
    /// SELECT "Id", "Nome", "Preco" FROM "Produtos" WHERE "Preco" &lt;= 0 OR btrim("Nome") = '';
    /// </code>
    /// <para>A API já recusava esses valores (<c>[Range]</c> e <c>[Required]</c> nos DTOs); o CHECK
    /// passa a valer também para SQL manual e seeds. <c>btrim</c> remove só espaços; a API faz
    /// <c>Trim()</c> de todo espaço em branco, então é mais rígida que o banco.</para>
    /// </remarks>
    public partial class AdicionarRestricoesProdutos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Produtos_Nome",
                table: "Produtos",
                sql: "btrim(\"Nome\") <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Produtos_Preco",
                table: "Produtos",
                sql: "\"Preco\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Produtos_Preco",
                table: "Produtos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Produtos_Nome",
                table: "Produtos");
        }
    }
}
