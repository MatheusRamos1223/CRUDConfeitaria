using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyCRUD.Migrations
{
    /// <summary>
    /// M2 — cria a tabela de categorias, adota os <c>CategoriaId</c> já usados por produtos
    /// e liga Produtos → Categorias com chave estrangeira RESTRICT.
    /// </summary>
    /// <remarks>
    /// <para>Antes de aplicar no Supabase, guarde o resultado de
    /// <c>SELECT "CategoriaId", count(*) FROM "Produtos" GROUP BY 1 ORDER BY 1;</c>
    /// — são as categorias "Categoria N" que esta migration vai criar (R11).</para>
    /// <para>Os passos 2–4 são SQL manual (o <c>dotnet ef</c> não os gera): ficam logo
    /// depois do <c>CreateTable</c>, antes de o índice e a FK serem criados.</para>
    /// <para>O índice de expressão <c>IX_Categorias_Nome_Minusculo</c> não existe no modelo
    /// do EF (nem no snapshot): por isso os testes criam o banco com <c>Migrate()</c>,
    /// nunca com <c>EnsureCreated()</c>.</para>
    /// <para><b>Aplicar com a API parada.</b> Entre o <c>INSERT ... SELECT DISTINCT "CategoriaId"</c>
    /// e a criação da FK, um produto cadastrado por outra conexão com um <c>CategoriaId</c> novo
    /// faria a FK falhar (e a migration inteira voltar atrás). Antes, rode também
    /// <c>SELECT "Id", "CategoriaId" FROM "Produtos" WHERE "CategoriaId" &lt;= 0;</c>: Ids 0 ou
    /// negativos viram "Categoria 0"/"Categoria -1" (funciona, mas é melhor corrigir antes).</para>
    /// <para>CHECK <c>CK_Categorias_Nome</c>: nome não pode ser vazio nem só espaços.
    /// RLS ligado sem policies (R38).</para>
    /// </remarks>
    public partial class CriarCategorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Tabela nova.
            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                    table.CheckConstraint("CK_Categorias_Nome", "btrim(\"Nome\") <> ''");
                });

            // 1b. R38: RLS sem policies — a Data API do Supabase (chave anon) não enxerga a tabela.
            migrationBuilder.Sql("""
                ALTER TABLE "Categorias" ENABLE ROW LEVEL SECURITY;
                """);

            // 2. R11: cada CategoriaId já usado vira "Categoria N" com o MESMO Id,
            //    senão a FK do passo 6 falharia para os produtos existentes.
            //    Identity "BY DEFAULT" aceita Id explícito no INSERT.
            migrationBuilder.Sql("""
                INSERT INTO "Categorias" ("Id", "Nome")
                SELECT DISTINCT "CategoriaId", 'Categoria ' || "CategoriaId"
                FROM "Produtos";
                """);

            // 3. Os INSERTs acima não avançam a sequência do identity: sem isto, o próximo
            //    cadastro tentaria Id 1 e colidiria. GREATEST(..., 0): a sequência começa em 1
            //    (MINVALUE 1); se só houver Ids 0/negativos, um setval negativo daria erro.
            migrationBuilder.Sql("""
                SELECT setval(
                    pg_get_serial_sequence('"Categorias"', 'Id'),
                    GREATEST(COALESCE((SELECT MAX("Id") FROM "Categorias"), 0), 0) + 1,
                    false);
                """);

            // 4. R8/D6: nome único sem diferenciar maiúsculas ("Bolos" = "bolos").
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Categorias_Nome_Minusculo" ON "Categorias" (lower("Nome"));
                """);

            // 5. Índice da FK (gerado pelo EF).
            migrationBuilder.CreateIndex(
                name: "IX_Produtos_CategoriaId",
                table: "Produtos",
                column: "CategoriaId");

            // 6. FK RESTRICT (gerada pelo EF): categoria com produtos não pode ser apagada (R9).
            migrationBuilder.AddForeignKey(
                name: "FK_Produtos_Categorias_CategoriaId",
                table: "Produtos",
                column: "CategoriaId",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Produtos_Categorias_CategoriaId",
                table: "Produtos");

            migrationBuilder.DropIndex(
                name: "IX_Produtos_CategoriaId",
                table: "Produtos");

            // RLS e IX_Categorias_Nome_Minusculo cairiam junto com a tabela; o DISABLE explícito
            // deixa o Down() simétrico ao Up().
            migrationBuilder.Sql("""
                ALTER TABLE "Categorias" DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "Categorias");
        }
    }
}
