using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyCRUD.Categorias;
using MyCRUD.Clientes;
using MyCRUD.Login;
using MyCRUD.Pedidos;
using MyCRUD.Produtos;

namespace MyCRUD.Dados;

/// <summary>
/// Seed de demonstração: popula um banco vazio com uma confeitaria de bairro fictícia
/// (usuários, categorias, produtos, clientes e pedidos) para apresentar o sistema.
/// </summary>
/// <remarks>
/// <para><b>Como rodar</b> (detalhes no README, seção "Dados de demonstração"):</para>
/// <code>dotnet run --project MyCRUD/MyCRUD.csproj -- --seed-demo</code>
/// <para><b>Proteções</b></para>
/// <list type="bullet">
///   <item>Só roda em <c>Development</c>: nunca popula um banco de produção por engano.</item>
///   <item>Não aplica migrations: se houver migration pendente, recusa e explica o comando.
///     Quem muda a estrutura do banco é sempre uma ação explícita (<c>dotnet ef database update</c>).</item>
///   <item>Idempotente: se já existe algum pedido, não faz nada. Rodar duas vezes não duplica dados.</item>
///   <item>Tudo em <b>uma transação</b>: ou grava tudo, ou nada.</item>
///   <item>A senha dos usuários de demonstração vem de <c>Demo:Senha</c> (User Secrets), nunca do código.</item>
/// </list>
/// <para><b>Por que não é uma migration?</b> Migration muda a <i>estrutura</i> e roda em todo ambiente,
/// inclusive produção; dados de demonstração são opcionais, dependem de "hoje" e de segredos.
/// Motivo completo em <c>docs/decisoes.md</c>, ADR-017.</para>
/// <para><b>Exceção à regra R21</b>: a API recusa pedido com entrega no passado. O seed grava
/// direto no banco, sem passar pelos endpoints, justamente para a demo ter histórico
/// (pedidos entregues e cancelados nos últimos 10 dias).</para>
/// </remarks>
public static class SeedDemo
{
    /// <summary>Argumento de linha de comando que executa o seed em vez de subir o servidor.</summary>
    public const string ArgumentoLinhaDeComando = "--seed-demo";

    /// <summary>Chave de configuração (User Secrets) com a senha dos usuários de demonstração.</summary>
    public const string ChaveSenha = "Demo:Senha";

    /// <summary>E-mail do usuário Operador de demonstração (domínio reservado para exemplos).</summary>
    public const string EmailOperador = "operador@exemplo.com";

    /// <summary>
    /// Semente fixa do gerador aleatório: com a mesma semente, <see cref="Random"/> sorteia
    /// sempre a mesma sequência. Assim, duas pessoas que rodam o seed no mesmo dia veem os mesmos dados.
    /// </summary>
    private const int Semente = 42;

    // Fuso da confeitaria (o mesmo de Pedido.HojeNaConfeitaria): os horários do seed são
    // pensados em horário de São Paulo e convertidos para UTC antes de gravar.
    private static readonly TimeZoneInfo FusoHorario =
        TimeZoneInfo.FindSystemTimeZoneById(Pedido.FusoHorarioId);

    // -----------------------------------------------------------------------
    // Dados fixos da demonstração
    // -----------------------------------------------------------------------

    /// <summary>
    /// Um produto do cardápio e a faixa de quantidade que faz sentido pedir dele.
    /// A quantidade sorteada é múltipla de <c>Passo</c> (docinhos vão de 10 em 10).
    /// </summary>
    private sealed record ProdutoDemo(
        string Nome,
        decimal Preco,
        string Categoria,
        bool Ativo,
        int QuantidadeMinima,
        int QuantidadeMaxima,
        int Passo = 1);

    /// <summary>Cliente fictício. Telefone já só com dígitos (DDD 19), como a API grava.</summary>
    private sealed record ClienteDemo(string Nome, string Telefone, string? Email);

    /// <summary>
    /// Um pedido: em quantos dias (a partir de hoje) é a entrega e em que status ele está.
    /// Negativo = entrega no passado.
    /// </summary>
    private sealed record PedidoDemo(int DiasAteEntrega, StatusPedido Status);

    private static readonly string[] NomesDasCategorias =
        ["Bolos", "Tortas", "Doces finos", "Salgados", "Bebidas"];

    // 15 produtos, 2 inativos (Ativo = false): aparecem no filtro "Inativos" e nunca em pedidos.
    private static readonly ProdutoDemo[] ProdutosDemo =
    [
        new("Bolo de cenoura com cobertura de chocolate (1 kg)", 65.00m, "Bolos", true, 1, 2),
        new("Bolo de chocolate com morango (2 kg)", 169.90m, "Bolos", true, 1, 1),
        new("Bolo prestígio (1,5 kg)", 129.90m, "Bolos", true, 1, 1),
        new("Bolo de fubá com goiabada (1 kg)", 48.00m, "Bolos", false, 1, 2),
        new("Torta de limão (inteira)", 89.90m, "Tortas", true, 1, 2),
        new("Torta holandesa (inteira)", 119.90m, "Tortas", true, 1, 1),
        new("Cheesecake de frutas vermelhas (inteira)", 129.90m, "Tortas", true, 1, 1),
        new("Brigadeiro gourmet (unidade)", 4.50m, "Doces finos", true, 20, 100, 10),
        new("Bem-casado (unidade)", 6.00m, "Doces finos", true, 30, 150, 10),
        new("Trufa de maracujá (unidade)", 5.50m, "Doces finos", true, 10, 60, 10),
        new("Camafeu de nozes (unidade)", 7.00m, "Doces finos", false, 20, 80, 10),
        new("Coxinha de frango (cento)", 110.00m, "Salgados", true, 1, 3),
        new("Empada de palmito (cento)", 130.00m, "Salgados", true, 1, 2),
        new("Suco natural de laranja (1 L)", 18.00m, "Bebidas", true, 1, 6),
        new("Refrigerante (2 L)", 14.00m, "Bebidas", true, 1, 6)
    ];

    // Telefones claramente fictícios (prefixos 9 0000 e 3000), DDD 19, 10 ou 11 dígitos
    // (CHECK CK_Clientes_Telefone). E-mails em minúsculas (CK_Clientes_Email); 4 clientes sem e-mail.
    private static readonly ClienteDemo[] ClientesDemo =
    [
        new("Ana Paula Souza", "19900000101", "ana.souza@exemplo.com"),
        new("Carlos Eduardo Lima", "19900000202", "carlos.lima@exemplo.com"),
        new("Fernanda Oliveira", "19900000303", null),
        new("João Pedro Santos", "19900000404", "joao.santos@exemplo.com"),
        new("Mariana Costa", "1930000505", "mariana.costa@exemplo.com"),
        new("Rafael Almeida", "19900000606", null),
        new("Juliana Pereira", "19900000707", "juliana.pereira@exemplo.com"),
        new("Lucas Ferreira", "19900000808", "lucas.ferreira@exemplo.com"),
        new("Beatriz Rodrigues", "1930000909", null),
        new("Gustavo Ribeiro", "19900001010", "gustavo.ribeiro@exemplo.com"),
        new("Patrícia Gomes", "19900001111", "patricia.gomes@exemplo.com"),
        new("Rodrigo Martins", "19900001212", null)
    ];

    // 25 pedidos com entrega de −10 a +14 dias. O status é COERENTE com a data:
    // entrega no passado → Entregue ou Cancelado; hoje ou no futuro → Aberto ou Confirmado.
    // A lista é fixa (e não sorteada) para garantir que todos os status apareçam.
    // Totais: 9 Entregue, 2 Cancelado, 6 Confirmado, 8 Aberto.
    private static readonly PedidoDemo[] PedidosDemo =
    [
        new(-10, StatusPedido.Entregue), new(-9, StatusPedido.Entregue),
        new(-8, StatusPedido.Cancelado), new(-7, StatusPedido.Entregue),
        new(-6, StatusPedido.Entregue), new(-5, StatusPedido.Entregue),
        new(-4, StatusPedido.Entregue), new(-3, StatusPedido.Cancelado),
        new(-2, StatusPedido.Entregue), new(-1, StatusPedido.Entregue),
        new(-1, StatusPedido.Entregue),
        new(0, StatusPedido.Confirmado), new(0, StatusPedido.Confirmado),
        new(0, StatusPedido.Aberto), new(1, StatusPedido.Confirmado),
        new(2, StatusPedido.Confirmado), new(2, StatusPedido.Aberto),
        new(3, StatusPedido.Aberto), new(5, StatusPedido.Confirmado),
        new(6, StatusPedido.Aberto), new(7, StatusPedido.Aberto),
        new(9, StatusPedido.Confirmado), new(10, StatusPedido.Aberto),
        new(12, StatusPedido.Aberto), new(14, StatusPedido.Aberto)
    ];

    // -----------------------------------------------------------------------
    // Ponto de entrada pela linha de comando (chamado pelo Program.cs)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Executa o seed a partir do <c>Program.cs</c> e devolve o código de saída do processo
    /// (0 = sucesso ou nada a fazer; 1 = erro).
    /// </summary>
    /// <remarks>
    /// Aqui ficam as verificações de <b>ambiente</b> (Development, migrations aplicadas);
    /// <see cref="SemearDemoAsync"/> fica só com a criação dos dados.
    /// </remarks>
    public static async Task<int> ExecutarAsync(WebApplication app)
    {
        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(SeedDemo));

        // Fora de Development o seed não roda: é ferramenta de demonstração, não de produção.
        if (!app.Environment.IsDevelopment())
        {
            logger.LogError(
                "{Argumento} só roda em Development (ambiente atual: {Ambiente}). Nada foi gravado.",
                ArgumentoLinhaDeComando,
                app.Environment.EnvironmentName);
            return 1;
        }

        // Configuração conferida antes de abrir conexão: erro de digitação aparece na hora.
        var (erro, _, _) = LerConfiguracao(app.Configuration);
        if (erro is not null)
        {
            logger.LogError("{Erro} Nada foi gravado.", erro);
            return 1;
        }

        // O AppDbContext é "scoped" (um por requisição). Fora de uma requisição,
        // criamos o escopo à mão e o descartamos no fim (await using).
        await using var escopo = app.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // O seed NÃO aplica migrations: aplicar estrutura é decisão explícita
            // (em banco compartilhado, combinada com o grupo). Só confere e explica.
            var pendentes = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pendentes.Count > 0)
            {
                logger.LogError(
                    "Há {Quantidade} migration(s) pendente(s): {Migrations}. " +
                    "Rode 'dotnet ef database update --project MyCRUD/MyCRUD.csproj' e tente de novo. Nada foi gravado.",
                    pendentes.Count,
                    string.Join(", ", pendentes));
                return 1;
            }

            // Ctrl+C encerra o processo; a transação não confirmada é desfeita pelo próprio banco.
            await SemearDemoAsync(db, app.Configuration, logger, CancellationToken.None);
            return 0;
        }
        catch (Exception ex)
        {
            // Qualquer falha (conexão, CHECK, FK...) desfaz a transação inteira.
            logger.LogError(ex, "O seed de demonstração falhou. A transação foi desfeita: nada foi gravado.");
            return 1;
        }
    }

    // -----------------------------------------------------------------------
    // Criação dos dados
    // -----------------------------------------------------------------------

    /// <summary>
    /// Popula o banco com os dados de demonstração, em uma única transação.
    /// </summary>
    /// <returns>
    /// <c>true</c> se gravou; <c>false</c> se o banco já tinha pedidos (nada é alterado).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <c>Admin:Email</c> ausente/inválido ou <c>Demo:Senha</c> ausente/curta.
    /// </exception>
    /// <remarks>
    /// Exige o banco com todas as migrations aplicadas (quem chama confere;
    /// ver <see cref="ExecutarAsync"/>). Usuários já existentes são reutilizados e
    /// mantêm a senha; categorias com o mesmo nome (sem diferenciar maiúsculas) também.
    /// </remarks>
    public static async Task<bool> SemearDemoAsync(
        AppDbContext db,
        IConfiguration configuracao,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var (erro, emailAdmin, senha) = LerConfiguracao(configuracao);
        if (erro is not null)
        {
            throw new InvalidOperationException(erro);
        }

        // Uma transação explícita envolve TUDO: a verificação de idempotência, as leituras
        // de usuários/categorias e o único SaveChangesAsync. Se algo falhar antes do
        // CommitAsync, o "await using" descarta a transação e o banco desfaz o que foi feito.
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        // Idempotência: um pedido já existente indica banco em uso (ou seed já rodado).
        if (await db.Pedidos.AnyAsync(cancellationToken))
        {
            logger.LogInformation("O banco já tem pedidos: seed de demonstração não executado (nada foi alterado).");
            return false;
        }

        // "Agora" lido uma única vez: todas as datas do seed partem do mesmo instante.
        var agoraUtc = DateTime.UtcNow;
        var hoje = Pedido.HojeNaConfeitaria(agoraUtc);
        var sorteio = new Random(Semente);

        var (admin, operador) = await ObterUsuariosAsync(db, emailAdmin, senha, logger, cancellationToken);
        var categorias = await ObterCategoriasAsync(db, cancellationToken);
        var produtosAtivos = AdicionarProdutos(db, categorias);
        var clientes = AdicionarClientes(db);

        var pedidos = PedidosDemo
            .Select(definicao => MontarPedido(
                definicao, hoje, agoraUtc, sorteio, clientes, produtosAtivos, admin, operador))
            .ToList();

        // Inseridos em ordem de criação: o Nº (Id) do pedido cresce junto com CriadoEm,
        // como aconteceria no uso real.
        db.Pedidos.AddRange(pedidos.OrderBy(p => p.CriadoEm));

        // Um único SaveChangesAsync: o EF descobre a ordem dos INSERTs pelas navegações
        // (Usuario → Categoria → Produto → Cliente → Pedido → ItemPedido) e preenche as FKs
        // com os Ids gerados pelo banco.
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        // Só Ids no log, nunca e-mail nem senha (mesma regra do login).
        logger.LogInformation(
            "Seed de demonstração concluído: {Categorias} categorias, {Produtos} produtos, {Clientes} clientes, " +
            "{Pedidos} pedidos. Admin = usuário {AdminId}; Operador = usuário {OperadorId}.",
            categorias.Count,
            ProdutosDemo.Length,
            clientes.Count,
            pedidos.Count,
            admin.Id,
            operador.Id);

        return true;
    }

    /// <summary>
    /// Lê e confere <c>Admin:Email</c> e <c>Demo:Senha</c>.
    /// </summary>
    /// <returns><c>Erro</c> preenchido (mensagem pronta para o log) ou os valores normalizados.</returns>
    private static (string? Erro, string EmailAdmin, string Senha) LerConfiguracao(IConfiguration configuracao)
    {
        var emailConfigurado = configuracao[LoginEndpoints.ChaveEmailAdmin];
        var senha = configuracao[ChaveSenha];

        if (string.IsNullOrWhiteSpace(emailConfigurado))
        {
            return ("Admin:Email não configurado. Rode: dotnet user-secrets set \"Admin:Email\" " +
                    "\"seu-email@exemplo.com\" --project MyCRUD/MyCRUD.csproj", "", "");
        }

        // Mesma normalização do login: " Ana@Exemplo.com" vira "ana@exemplo.com".
        var emailAdmin = Usuario.NormalizarEmail(emailConfigurado);

        if (emailAdmin.Length > Usuario.EmailTamanhoMaximo || !emailAdmin.Contains('@'))
        {
            return ("Admin:Email não parece um e-mail válido.", "", "");
        }

        if (emailAdmin == EmailOperador)
        {
            return ($"Admin:Email não pode ser {EmailOperador}: esse é o e-mail do Operador de demonstração.", "", "");
        }

        if (string.IsNullOrWhiteSpace(senha)
            || senha.Length < Usuario.SenhaTamanhoMinimo
            || senha.Length > Usuario.SenhaTamanhoMaximo)
        {
            return ($"Demo:Senha ausente ou fora do tamanho ({Usuario.SenhaTamanhoMinimo} a " +
                    $"{Usuario.SenhaTamanhoMaximo} caracteres). Rode: dotnet user-secrets set \"Demo:Senha\" " +
                    "\"UMA_SENHA_FORTE\" --project MyCRUD/MyCRUD.csproj", "", "");
        }

        return (null, emailAdmin, senha);
    }

    /// <summary>
    /// Garante o Admin (<c>Admin:Email</c>) e o Operador (<see cref="EmailOperador"/>).
    /// Quem já existe é reutilizado e mantém a senha atual.
    /// </summary>
    /// <remarks>
    /// O cadastro pela tela recusa o e-mail de <c>Admin:Email</c> (R42), então este é um dos
    /// caminhos previstos para o Admin existir. Se o usuário já existia como Operador,
    /// vira Admin aqui, como aconteceria no próximo login (R34); nunca o contrário.
    /// </remarks>
    private static async Task<(Usuario Admin, Usuario Operador)> ObterUsuariosAsync(
        AppDbContext db,
        string emailAdmin,
        string senha,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var hasher = new PasswordHasher<Usuario>();

        var admin = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == emailAdmin, cancellationToken);
        if (admin is null)
        {
            admin = new Usuario { Nome = "Administrador (demo)", Email = emailAdmin, Papel = Papel.Admin };
            admin.SenhaHash = hasher.HashPassword(admin, senha);
            db.Usuarios.Add(admin);
        }
        else
        {
            logger.LogInformation("Usuário {UsuarioId} (Admin:Email) já existia: reutilizado, senha mantida.", admin.Id);

            if (admin.Papel != Papel.Admin)
            {
                admin.Papel = Papel.Admin;
                logger.LogInformation("Usuário {UsuarioId} promovido a Admin pelo seed (Admin:Email).", admin.Id);
            }
        }

        var operador = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == EmailOperador, cancellationToken);
        if (operador is null)
        {
            operador = new Usuario { Nome = "Operador (demo)", Email = EmailOperador, Papel = Papel.Operador };
            operador.SenhaHash = hasher.HashPassword(operador, senha);
            db.Usuarios.Add(operador);
        }
        else
        {
            logger.LogInformation("Usuário {UsuarioId} (Operador da demo) já existia: reutilizado, senha mantida.",
                operador.Id);
        }

        return (admin, operador);
    }

    /// <summary>
    /// Devolve as 5 categorias da demo, reaproveitando as que já existem com o mesmo nome.
    /// </summary>
    /// <remarks>
    /// Reaproveitar é obrigatório, não gentileza: o índice único <c>lower("Nome")</c>
    /// (IX_Categorias_Nome_Minusculo) recusaria uma segunda "Bolos" e a transação inteira falharia.
    /// </remarks>
    private static async Task<Dictionary<string, Categoria>> ObterCategoriasAsync(
        AppDbContext db, CancellationToken cancellationToken)
    {
        var nomesMinusculos = NomesDasCategorias.Select(n => n.ToLowerInvariant()).ToList();

        // ToLower() vira lower("Nome") no SQL: a mesma expressão do índice único.
        var existentes = await db.Categorias
            .Where(c => nomesMinusculos.Contains(c.Nome.ToLower()))
            .ToListAsync(cancellationToken);

        var categorias = new Dictionary<string, Categoria>();

        foreach (var nome in NomesDasCategorias)
        {
            var categoria = existentes.FirstOrDefault(c =>
                string.Equals(c.Nome, nome, StringComparison.OrdinalIgnoreCase));

            if (categoria is null)
            {
                categoria = new Categoria { Nome = nome };
                db.Categorias.Add(categoria);
            }

            categorias[nome] = categoria;
        }

        return categorias;
    }

    /// <summary>
    /// Adiciona os 15 produtos e devolve só os ativos (os únicos que podem entrar em pedido, R22).
    /// </summary>
    private static List<(ProdutoDemo Definicao, Produto Produto)> AdicionarProdutos(
        AppDbContext db, Dictionary<string, Categoria> categorias)
    {
        var ativos = new List<(ProdutoDemo Definicao, Produto Produto)>();

        foreach (var definicao in ProdutosDemo)
        {
            // A navegação Categoria basta: o EF preenche CategoriaId no SaveChanges,
            // inclusive para categoria nova, cujo Id ainda não existe neste momento.
            var produto = new Produto
            {
                Nome = definicao.Nome,
                Preco = definicao.Preco,
                Categoria = categorias[definicao.Categoria],
                Ativo = definicao.Ativo
            };

            db.Produtos.Add(produto);

            if (produto.Ativo)
            {
                ativos.Add((definicao, produto));
            }
        }

        return ativos;
    }

    /// <summary>Adiciona os 12 clientes fictícios.</summary>
    private static List<Cliente> AdicionarClientes(AppDbContext db)
    {
        var clientes = ClientesDemo
            .Select(c => new Cliente { Nome = c.Nome, Telefone = c.Telefone, Email = c.Email })
            .ToList();

        db.Clientes.AddRange(clientes);
        return clientes;
    }

    /// <summary>
    /// Monta um pedido coerente com as regras da API, exceto a data passada (exceção à R21).
    /// </summary>
    /// <remarks>
    /// <para><b>Linha do tempo</b> (horários pensados em São Paulo, gravados em UTC):</para>
    /// <list type="bullet">
    ///   <item><c>CriadoEm</c>: 1 a 6 dias antes da entrega, em horário comercial; se isso cair
    ///     no futuro, vira "algumas horas atrás". Nunca depois do dia da entrega (R21 no momento
    ///     da criação) e nunca no futuro.</item>
    ///   <item>Confirmado: mudança entre a criação e agora.</item>
    ///   <item>Entregue: no dia da entrega, entre 9h e 19h (sempre no passado: entrega ≤ ontem).</item>
    ///   <item>Cancelado: entre a criação e as 8h do dia da entrega.</item>
    /// </list>
    /// <para><b>Auditoria</b> (R40): criado pelo Operador ou pelo Admin. A última mudança de status
    /// pode ser de qualquer um dos dois: confirmar e entregar são livres, e um cancelamento feito pelo
    /// Operador é, por definição, de pedido Aberto (cancelar Confirmado é só do Admin, R30).</para>
    /// <para>Todos os sorteios acontecem sempre, na mesma ordem, mesmo quando o valor não é usado:
    /// assim a sequência de <see cref="Random"/> não depende do status e o seed continua reproduzível.</para>
    /// </remarks>
    private static Pedido MontarPedido(
        PedidoDemo definicao,
        DateOnly hoje,
        DateTime agoraUtc,
        Random sorteio,
        List<Cliente> clientes,
        List<(ProdutoDemo Definicao, Produto Produto)> produtosAtivos,
        Usuario admin,
        Usuario operador)
    {
        var dataEntrega = hoje.AddDays(definicao.DiasAteEntrega);

        // --- Sorteios (sempre todos, sempre nesta ordem) ---
        var cliente = clientes[sorteio.Next(clientes.Count)];
        var diasDeAntecedencia = sorteio.Next(1, 7);          // 1 a 6
        var horaCriacao = sorteio.Next(8, 18);                // 8h a 17h
        var minutoCriacao = sorteio.Next(0, 60);
        var minutosAtras = sorteio.Next(30, 600);             // usado se a criação cairia no futuro
        var fracaoDoIntervalo = sorteio.NextDouble();
        var horaEntrega = sorteio.Next(9, 19);                // 9h a 18h
        var minutoEntrega = sorteio.Next(0, 60);
        var criadoPeloAdmin = sorteio.Next(100) < 30;         // 30% Admin, 70% Operador
        var alteradoPeloAdmin = sorteio.Next(100) < 40;       // 40% Admin, 60% Operador
        var quantidadeDeItens = sorteio.Next(1, 6);           // 1 a 5 itens

        // --- Criação ---
        var criadoEm = HorarioLocalEmUtc(
            dataEntrega.AddDays(-diasDeAntecedencia), horaCriacao, minutoCriacao);

        // Pedido para hoje/amanhã: a antecedência poderia jogar a criação para depois de agora.
        var limite = agoraUtc.AddMinutes(-30);
        if (criadoEm > limite)
        {
            criadoEm = agoraUtc.AddMinutes(-minutosAtras);
        }

        // --- Última mudança de status ---
        DateTime? statusAlteradoEm = definicao.Status switch
        {
            StatusPedido.Aberto => null,
            StatusPedido.Confirmado => PontoEntre(criadoEm, agoraUtc, fracaoDoIntervalo),
            StatusPedido.Entregue => HorarioLocalEmUtc(dataEntrega, horaEntrega, minutoEntrega),
            StatusPedido.Cancelado => PontoEntre(criadoEm, HorarioLocalEmUtc(dataEntrega, 8, 0), fracaoDoIntervalo),
            _ => throw new ArgumentOutOfRangeException(nameof(definicao), definicao.Status, "Status desconhecido.")
        };

        // Aberto nunca mudou de status: as duas colunas de auditoria ficam NULL juntas (R40).
        var statusAlteradoPor = definicao.Status == StatusPedido.Aberto
            ? null
            : (alteradoPeloAdmin ? admin : operador);

        // --- Itens: produtos ATIVOS distintos (índice único PedidoId + ProdutoId) ---
        var embaralhados = produtosAtivos.ToArray();
        sorteio.Shuffle(embaralhados);

        var itens = embaralhados
            .Take(quantidadeDeItens)
            .Select(p => new ItemPedido
            {
                Produto = p.Produto,
                Quantidade = SortearQuantidade(p.Definicao, sorteio),
                // Preço congelado = preço atual do produto (R23).
                PrecoUnitario = p.Produto.Preco
            })
            .ToList();

        return new Pedido
        {
            Cliente = cliente,
            DataEntrega = dataEntrega,
            Status = definicao.Status,
            // Preenchido aqui (diferente da API, que deixa o DEFAULT now() do banco agir):
            // a demo precisa de criações no passado. DateTime com Kind = Utc, como o Npgsql exige.
            CriadoEm = criadoEm,
            CriadoPor = criadoPeloAdmin ? admin : operador,
            StatusAlteradoEm = statusAlteradoEm,
            StatusAlteradoPor = statusAlteradoPor,
            Itens = itens
        };
    }

    /// <summary>Quantidade dentro da faixa do produto, em múltiplos de <c>Passo</c> (1 a 999, CHECK do banco).</summary>
    private static int SortearQuantidade(ProdutoDemo produto, Random sorteio) =>
        sorteio.Next(produto.QuantidadeMinima / produto.Passo, produto.QuantidadeMaxima / produto.Passo + 1)
        * produto.Passo;

    /// <summary>
    /// Converte um horário de São Paulo (dia + hora) para UTC.
    /// </summary>
    /// <remarks>
    /// <c>DateOnly.ToDateTime</c> devolve <c>Kind = Unspecified</c>, que é o que
    /// <c>ConvertTimeToUtc</c> espera para interpretar o valor no fuso informado.
    /// O resultado sai com <c>Kind = Utc</c>, aceito pela coluna <c>timestamp with time zone</c>.
    /// </remarks>
    private static DateTime HorarioLocalEmUtc(DateOnly dia, int hora, int minuto) =>
        TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(new TimeOnly(hora, minuto)), FusoHorario);

    /// <summary>
    /// Um instante entre <paramref name="inicio"/> e <paramref name="fim"/>, sem encostar nas pontas
    /// (fica entre 10% e 90% do intervalo).
    /// </summary>
    private static DateTime PontoEntre(DateTime inicio, DateTime fim, double fracao) =>
        inicio + (fim - inicio) * (0.1 + 0.8 * fracao);
}
