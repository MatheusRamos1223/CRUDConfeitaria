using Microsoft.EntityFrameworkCore;
using MyCRUD.Login;
using MyCRUD.Produtos;

namespace MyCRUD.Dados;

/// <summary>
/// Sessão com o banco de dados (padrão Unit of Work do Entity Framework Core).
/// </summary>
/// <remarks>
/// <para>Cada <see cref="DbSet{TEntity}"/> representa uma tabela.</para>
/// <para>
/// <see cref="OnModelCreating"/> é onde ficam as regras que o BANCO deve garantir
/// (tamanho de coluna, precisão, índice único). Ao mudar algo aqui, é preciso gerar
/// uma migration: <c>dotnet ef migrations add NomeDaMudanca --project MyCRUD</c>.
/// </para>
/// <para>
/// Regras de entrada (mensagens amigáveis ao usuário) ficam nos DTOs; regras de
/// integridade ficam aqui. As duas camadas usam as mesmas constantes
/// (ex.: <see cref="Produto.NomeTamanhoMaximo"/>) para nunca divergirem.
/// </para>
/// </remarks>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Produto>(produto =>
        {
            produto.Property(p => p.Nome)
                .HasMaxLength(Produto.NomeTamanhoMaximo);

            // numeric(10,2): até 99.999.999,99 sem erro de arredondamento de float.
            produto.Property(p => p.Preco)
                .HasPrecision(10, 2);
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
        });
    }
}
