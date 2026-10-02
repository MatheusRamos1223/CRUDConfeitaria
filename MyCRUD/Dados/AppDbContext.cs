using Microsoft.EntityFrameworkCore;
using MyCRUD.Produtos;
using MyCRUD.Login;


namespace MyCRUD.Dados;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>(); 
    public DbSet<Usuario> Usuarios => Set<Usuario>(); // não esquece isso plmds

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //Muito confuso ainda onde essas verificações vão, mas vamos tentar colocar aqui
        modelBuilder.Entity<Produto>() // Configurações adicionais para a entidade Produto
            .Property(p => p.Preco)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Usuario>() // Configurações adicionais para a entidade Usuario
            .HasIndex(u => u.Email) 
            .IsUnique(); // Garante que o email seja único na tabela de usuários

    }
}

