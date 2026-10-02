namespace MyCRUD.Login;

public record LoginDto(string Email, string Senha);
public record UsuarioRespostaDto(int Id, string Nome, string Email);
public record CriarUsuarioDto(string Nome, string Email, string Senha);