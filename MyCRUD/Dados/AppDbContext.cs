using Microsoft.EntityFrameworkCore;
using MyCRUD.Categorias;
using MyCRUD.Clientes;
using MyCRUD.Login;
using MyCRUD.Pedidos;
using MyCRUD.Produtos;

namespace MyCRUD.Dados;

/// <summary>
/// Sessão com o banco de dados (padrão Unit of Work do Entity Framework Core).
/// </summary>
/// <remarks>
/// <para>Cada <see cref="DbSet{TEntity}"/> representa uma tabela.</para>
/// <para>
/// <see cref="OnModelCreating"/> é onde ficam as regras que o BANCO deve garantir
/// (tamanho de coluna, precisão, índice único, chave estrangeira, CHECK). Ao mudar algo
/// aqui, é preciso gerar uma migration: <c>dotnet ef migrations add NomeDaMudanca --project MyCRUD</c>.
/// </para>
/// <para>
/// Regras de entrada (mensagens amigáveis ao usuário) ficam nos DTOs; regras de
/// integridade ficam aqui. As duas camadas usam as mesmas constantes
/// (ex.: <see cref="Produto.NomeTamanhoMaximo"/>) para nunca divergirem.
/// </para>
/// <para>
/// <b>Nulabilidade</b>: com <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c>, <c>string</c> vira
/// <c>NOT NULL</c> e <c>string?</c> vira coluna que aceita nulo — não é preciso <c>IsRequired()</c>.
/// </para>
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Produto>(produto =>
        {
            produto.Property(p => p.Nome)
                .HasMaxLength(Produto.NomeTamanhoMaximo);

            // numeric(10,2): até 99.999.999,99 sem erro de arredondamento de float.
            produto.Property(p => p.Preco)
                .HasPrecision(10, 2);

            // Produto → Categoria (N:1). RESTRICT: o banco recusa apagar categoria que ainda
            // tem produtos, ativos ou inativos (R9). O EF cria o índice IX_Produtos_CategoriaId
            // e nomeia a FK FK_Produtos_Categorias_CategoriaId por convenção.
            // Sem esta configuração, a convenção do EF usaria CASCADE (FK obrigatória):
            // apagar uma categoria apagaria os produtos junto.
            produto.HasOne(p => p.Categoria)
                .WithMany()
                .HasForeignKey(p => p.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);

            // CHECKs da migration AdicionarRestricoesProdutos (M5), separada das demais porque
            // Produtos é a única tabela antiga com dados reais: o README manda conferir antes
            // (SELECT ... WHERE "Preco" <= 0 OR btrim("Nome") = '').
            // btrim só remove espaços; a API já faz Trim() de todo espaço em branco (R3).
            produto.ToTable(tabela =>
            {
                tabela.HasCheckConstraint("CK_Produtos_Nome", "btrim(\"Nome\") <> ''");
                tabela.HasCheckConstraint("CK_Produtos_Preco", "\"Preco\" > 0");
            });
        });

        modelBuilder.Entity<Usuario>(usuario =>
        {
            usuario.Property(u => u.Nome)
                .HasMaxLength(Usuario.NomeTamanhoMaximo);

            usuario.Property(u => u.Email)
                .HasMaxLength(Usuario.EmailTamanhoMaximo);

            // Índice único: o banco recusa dois usuários com o mesmo e-mail,
            // mesmo que a verificação da aplicação falhe por concorrência.
            usuario.HasIndex(u => u.Email)
                .IsUnique();

            // Papel salvo como TEXTO ('Admin'/'Operador'), não como número:
            // legível no Supabase e imune a reordenação do enum.
            usuario.Property(u => u.Papel)
                .HasConversion<string>()
                .HasMaxLength(Usuario.PapelTamanhoMaximo)
                // DEFAULT no banco: usuários que já existiam viram Operador na migration.
                .HasDefaultValue(Papel.Operador);

            // CHECK: o banco recusa qualquer valor fora do enum, mesmo vindo de SQL manual.
            usuario.ToTable(tabela => tabela.HasCheckConstraint(
                "CK_Usuarios_Papel",
                "\"Papel\" IN ('Admin','Operador')"));
        });

        modelBuilder.Entity<Categoria>(categoria =>
        {
            // varchar(60). NOT NULL vem do tipo não anulável (string, com Nullable habilitado).
            categoria.Property(c => c.Nome)
                .HasMaxLength(Categoria.NomeTamanhoMaximo);

            // A unicidade sem diferenciar maiúsculas é um índice de EXPRESSÃO, lower("Nome"),
            // que o EF não modela: ele é criado por SQL na migration CriarCategorias
            // (IX_Categorias_Nome_Minusculo). Por isso NÃO há HasIndex(c => c.Nome) aqui
            // (seria um segundo índice, sensível a maiúsculas) e os testes usam
            // Migrate(), nunca EnsureCreated().

            // O banco recusa nome vazio ou só com espaços, mesmo vindo de SQL manual (R3).
            categoria.ToTable(tabela => tabela.HasCheckConstraint(
                "CK_Categorias_Nome",
                "btrim(\"Nome\") <> ''"));
        });

        modelBuilder.Entity<Cliente>(cliente =>
        {
            cliente.Property(c => c.Nome)
                .HasMaxLength(Cliente.NomeTamanhoMaximo);

            // Só dígitos (10 ou 11). A API já normaliza; o CHECK impede que um INSERT
            // feito por fora da API (SQL manual, seed) grave telefone com máscara.
            cliente.Property(c => c.Telefone)
                .HasMaxLength(Cliente.TelefoneTamanhoMaximo);

            cliente.Property(c => c.Email)
                .HasMaxLength(Cliente.EmailTamanhoMaximo);

            // Índice na MESMA ordem da listagem (R19: ORDER BY "Nome", "Id"):
            // IX_Clientes_Nome_Id. Com o Id no índice, o banco lê a página já ordenada,
            // inclusive o desempate, sem um passo extra de ordenação.
            // Telefone NÃO é único (Q3).
            cliente.HasIndex(c => new { c.Nome, c.Id });

            cliente.ToTable(tabela =>
            {
                // E-mail já normalizado (minúsculas, sem espaços nas pontas), como a API grava.
                // NULL passa: CHECK só recusa quando a condição é FALSE.
                tabela.HasCheckConstraint(
                    "CK_Clientes_Email",
                    "\"Email\" = lower(btrim(\"Email\"))");
                tabela.HasCheckConstraint(
                    "CK_Clientes_Nome",
                    "btrim(\"Nome\") <> ''");
                tabela.HasCheckConstraint(
                    "CK_Clientes_Telefone",
                    "\"Telefone\" ~ '^[0-9]{10,11}$'");
            });
        });

        modelBuilder.Entity<Pedido>(pedido =>
        {
            // Enum gravado como texto ("Aberto"), não como número: legível no banco (D10).
            pedido.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(Pedido.StatusTamanhoMaximo)
                // DEFAULT 'Aberto' no banco (R26): INSERT por SQL manual também nasce Aberto.
                .HasDefaultValue(StatusPedido.Aberto);

            // DEFAULT now(): o relógio do BANCO marca a criação (R26). O EF omite a coluna
            // no INSERT quando o valor em C# é o padrão (DateTime.MinValue) e lê o gerado de volta.
            pedido.Property(p => p.CriadoEm)
                .HasDefaultValueSql("now()");

            // Cliente com pedido não pode ser excluído (D12): RESTRICT em vez de CASCADE.
            pedido.HasOne(p => p.Cliente)
                .WithMany()
                .HasForeignKey(p => p.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Auditoria (R40): quem criou e quem fez a última mudança de status.
            // Duas FKs para a mesma tabela: por isso cada uma é declarada com a sua
            // navegação (sem isso o EF não saberia qual navegação usa qual coluna).
            // RESTRICT: usuário com pedido não pode ser apagado; o histórico não perde o autor.
            // O EF cria os índices IX_Pedidos_CriadoPorId e IX_Pedidos_StatusAlteradoPorId.
            pedido.HasOne(p => p.CriadoPor)
                .WithMany()
                .HasForeignKey(p => p.CriadoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            pedido.HasOne(p => p.StatusAlteradoPor)
                .WithMany()
                .HasForeignKey(p => p.StatusAlteradoPorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Itens pertencem ao pedido. RESTRICT (e não CASCADE): pedido nunca é excluído (R31);
            // se alguém tentar por SQL manual, o banco recusa em vez de apagar os itens junto.
            pedido.HasMany(p => p.Itens)
                .WithOne()
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Índice na MESMA ordem da listagem (R6: ORDER BY "DataEntrega", "Id"):
            // IX_Pedidos_DataEntrega_Id. Também atende os filtros de/até (1ª coluna).
            pedido.HasIndex(p => new { p.DataEntrega, p.Id });

            // O banco recusa qualquer status fora do enum, mesmo vindo de SQL manual.
            pedido.ToTable(tabela => tabela.HasCheckConstraint(
                "CK_Pedidos_Status",
                "\"Status\" IN ('Aberto','Confirmado','Entregue','Cancelado')"));
        });

        modelBuilder.Entity<ItemPedido>(item =>
        {
            // numeric(10,2), igual a Produto.Preco: o preço congelado cabe sem arredondar.
            item.Property(i => i.PrecoUnitario)
                .HasPrecision(10, 2);

            // Produto usado em pedido não pode ser apagado (e nem é: R14 só desativa).
            // Gera também o índice IX_ItensPedido_ProdutoId.
            item.HasOne(i => i.Produto)
                .WithMany()
                .HasForeignKey(i => i.ProdutoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Um produto no máximo uma vez por pedido: IX_ItensPedido_PedidoId_ProdutoId.
            // Este índice também cobre a FK PedidoId (é a 1ª coluna), por isso o EF
            // não cria um IX_ItensPedido_PedidoId separado.
            item.HasIndex(i => new { i.PedidoId, i.ProdutoId })
                .IsUnique();

            // Nome da tabela explícito: por convenção o EF usaria o nome do DbSet
            // (ItensPedido), mas deixar escrito evita surpresa se o DbSet for renomeado.
            item.ToTable("ItensPedido", tabela =>
            {
                tabela.HasCheckConstraint(
                    "CK_ItensPedido_Quantidade",
                    $"\"Quantidade\" BETWEEN 1 AND {ItemPedido.QuantidadeMaxima}");
                tabela.HasCheckConstraint(
                    "CK_ItensPedido_PrecoUnitario",
                    "\"PrecoUnitario\" > 0");
            });
        });
    }
}
