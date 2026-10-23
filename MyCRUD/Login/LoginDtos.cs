namespace MyCRUD.Login;

public record LoginDto(string Email, string Senha);
public record UsuarioRespostaDto(int Id, string Nome, string Email);
public record CriarUsuarioDto(string Nome, string Email, string Senha);

/*DTOs em C#
É um objeto simples usado para transportar dados entre camadas(por exemplo, entre API e serviço), sem lógica de negócio.
Normalmente é uma classe com apenas propriedades públicas (get; set;) e, às vezes, construtores.*/