# Registro de decisões de arquitetura (ADRs)

Cada decisão importante fica registrada com o contexto, a escolha e o custo dela.
Formato: **Contexto → Decisão → Consequências**.

---

<a id="adr-001"></a>

## ADR-001 — Minimal APIs em vez de Controllers

**Contexto.** O projeto tem poucas rotas e um objetivo didático. Controllers MVC exigem classes base, atributos de rota e mais arquivos por recurso.

**Decisão.** Usar Minimal APIs com `MapGroup` por funcionalidade (`/login`, `/produtos`).

**Consequências.**
- ✅ Menos código; a rota e seu comportamento ficam juntos.
- ✅ É o modelo recomendado pela Microsoft para APIs novas.
- ⚠️ Validação automática de modelo não vem pronta — resolvida com `ValidacaoFiltro` ([ADR-004](#adr-004)).

---

<a id="adr-002"></a>

## ADR-002 — Autenticação por cookie em vez de JWT

**Contexto.** O único cliente é o próprio front-end, servido pelo mesmo servidor (mesma origem).

**Decisão.** Cookie de autenticação do ASP.NET (`HttpOnly`, `SameSite=Lax`).

**Consequências.**
- ✅ O navegador envia o cookie sozinho; o JavaScript nunca toca no token.
- ✅ `HttpOnly` impede roubo do token por XSS — com JWT em `localStorage` isso não acontece.
- ✅ Logout real: o servidor remove o cookie.
- ⚠️ Para um app mobile ou outro domínio consumir a API, seria preciso adicionar JWT.

---

<a id="adr-003"></a>

## ADR-003 — Supabase apenas como PostgreSQL hospedado

**Contexto.** A disciplina pede C#. O Supabase oferece Auth, REST automático e RLS, mas tudo isso substituiria justamente o código C# que queremos escrever.

**Decisão.** Conectar via Npgsql + Entity Framework Core usando o *Session pooler*. Não usar Supabase Auth, REST nem RLS.

**Consequências.**
- ✅ Toda regra de negócio e segurança fica na API C#, testável e versionada.
- ✅ Trocar de provedor PostgreSQL exige mudar só a string de conexão.
- ⚠️ A API é a única barreira: se a senha do banco vazar, não há RLS como segunda camada.

---

<a id="adr-004"></a>

## ADR-004 — Validação com DataAnnotations + filtro de endpoint

**Contexto.** As regras estavam em `if`s repetidos dentro de cada endpoint, com mensagens em texto solto.

**Decisão.** Declarar regras nos DTOs com atributos (`[Required]`, `[MaxLength]`, `[Range]`, `[EmailAddress]`) e aplicá-las com `ValidacaoFiltro<T>` antes do endpoint.

**Alternativas consideradas.**
- *FluentValidation*: mais poderoso, mas adiciona dependência e sintaxe nova para o grupo.
- *Validação nativa do .NET 10 (`AddValidation`)*: depende de gerador de código; o filtro explícito deixa visível como a validação funciona, o que tem valor didático.

**Consequências.**
- ✅ Regra declarada uma vez, perto do dado.
- ✅ Erros por campo no formato `ValidationProblem`.
- ⚠️ Regras que dependem do banco (ex.: e-mail já existe) continuam no endpoint.

---

<a id="adr-005"></a>

## ADR-005 — Problem Details (RFC 9457) para todos os erros

**Contexto.** Cada erro voltava com um formato diferente: texto, corpo vazio, página HTML.

**Decisão.** `AddProblemDetails` + `TratadorGlobalDeExcecoes` + `UseStatusCodePages`.

**Consequências.**
- ✅ O front tem uma única função para ler erros (`mensagemDeErro` em `js/api.js`).
- ✅ Padrão aberto, entendido por ferramentas como Scalar e Postman.
- ✅ Produção nunca exibe stack trace.

---

<a id="adr-006"></a>

## ADR-006 — Tamanhos máximos nas colunas de texto

**Contexto.** Colunas `text` aceitam qualquer tamanho; um nome de produto de 1 MB seria salvo.

**Decisão.** `varchar(120)` para nomes e `varchar(254)` para e-mail (limite da RFC 5321), com as mesmas constantes no DTO.

**Consequências.**
- ✅ O banco garante a regra mesmo se a aplicação falhar.
- ⚠️ Migration `PadronizarTamanhosEEmail` falha se houver dados maiores que o limite ou e-mails duplicados por maiúsculas — ver instruções no próprio arquivo.

---

<a id="adr-007"></a>

## ADR-007 — Remoção da migration vazia `CriarUsuarios`

**Contexto.** `CriarUsuarios` foi gerada antes de o `AppDbContext` ser salvo e ficou com `Up()` e `Down()` vazios. A tabela é criada de fato em `AdicionarTabelaUsuarios`.

**Decisão.** Remover os arquivos da migration vazia.

**Consequências.**
- ✅ Histórico de migrations legível.
- ✅ Seguro para bancos onde ela já foi aplicada: o EF só executa migrations que **não** estão em `__EFMigrationsHistory`; uma linha a mais na tabela de histórico é ignorada.
- ⚠️ Não use `CriarUsuarios` como alvo de `dotnet ef database update <migration>`.

---

<a id="adr-008"></a>

## ADR-008 — Papéis Admin/Operador definidos por configuração

**Contexto.** A Fase 2 separa quem opera o balcão (clientes, pedidos) de quem administra o cadastro (categorias, desativar produtos, excluir clientes, cancelar pedido confirmado). Não há tela de gestão de usuários, e o cadastro aberto só existe em Development.

**Decisão.** Enum `Papel { Operador, Admin }` salvo como texto (`varchar(20)` + `CHECK`), padrão `Operador`. O Admin é quem entra com o e-mail de `Admin:Email` (User Secrets / variável `Admin__Email`): no login, se o e-mail confere, o papel vira `Admin` antes de emitir o cookie. O papel viaja na claim `ClaimTypes.Role`; as rotas usam a política `Politicas.SomenteAdmin` (`RequireRole("Admin")`).

**Consequências.**
- ✅ Ninguém vira Admin por requisição HTTP: o cadastro não tem campo de papel e o e-mail do Admin não fica no repositório.
- ✅ Política declarada na rota → 403 automático e documentado no OpenAPI.
- ⚠️ Mudança de papel só vale no próximo login (o cookie guarda o papel; até 2 h).
- ⚠️ Nunca rebaixa automaticamente (D8): trocar `Admin:Email` não tira o acesso do Admin antigo; rebaixar é SQL manual.
- ⚠️ Em Development, quem se cadastrar com o e-mail do Admin vira Admin no login.

---

<a id="adr-009"></a>

## ADR-009 — Produto não é apagado: `DELETE` desativa (soft delete)

**Contexto.** Itens de pedido apontam para produtos. Apagar um produto quebraria pedidos antigos ou exigiria copiar todos os dados do produto no item.

**Decisão.** `DELETE /produtos/{id}` faz `Ativo = false` e `POST /produtos/{id}/reativar` desfaz; ambos só Admin e idempotentes (`ExecuteUpdateAsync`, 204 mesmo se já estava no estado). `PUT` deixou de receber `Ativo` (D3). A listagem filtra por situação (padrão: ativos); pedido novo só aceita produto ativo (R22).

**Consequências.**
- ✅ Pedidos antigos continuam íntegros e mostram o nome atual do produto (R32).
- ✅ Um Operador não consegue desativar pelo formulário de edição.
- ⚠️ Contrato quebrado em relação à Fase 1: `DELETE` não remove e `PUT` ignora `ativo` (README atualizado).
- ⚠️ Produtos inativos ocupam a tabela para sempre (aceitável no porte do projeto).

---

<a id="adr-010"></a>

## ADR-010 — Preço congelado no item; total sempre calculado

**Contexto.** O preço do cardápio muda. Um pedido já combinado com o cliente não pode mudar de valor depois.

**Decisão.** `ItemPedido.PrecoUnitario` é copiado de `Produto.Preco` **no servidor**, na criação (o DTO de entrada nem tem campo de preço). Não existe coluna de total: `Subtotal = Quantidade × PrecoUnitario` e `Total = Σ subtotais` são calculados na consulta (`SUM` no SQL). Pedido e itens são gravados num único `SaveChangesAsync`.

**Consequências.**
- ✅ Alterar preço não altera pedidos antigos (R17, R23).
- ✅ Sem total armazenado, não há duas cópias da mesma informação que possam divergir (R25).
- ✅ O cliente da API não consegue forjar preço.
- ⚠️ Toda listagem soma os itens; com índices e paginação, irrelevante neste porte.

---

<a id="adr-011"></a>

## ADR-011 — Envelope `ResultadoPaginado<T>` em todas as listagens

**Contexto.** As telas têm barra de filtro e paginador. Devolver um array solto obrigaria o front a pedir o total em outra rota, e mudar o formato depois quebraria os clientes duas vezes.

**Decisão.** Toda rota `GET /<recurso>` de lista devolve `{ itens, pagina, tamanhoPagina, total, totalPaginas }` (`Compartilhado/Paginacao.cs`). Filtros chegam por `[AsParameters]` em records validados pelo mesmo `ValidacaoFiltro` dos DTOs: `pagina ≥ 1`, `tamanho` 1–100 (padrão 20). Toda ordenação termina em `Id`. Filtros de texto usam `ILIKE` com curingas escapados (`ConsultaTexto`).

**Consequências.**
- ✅ Um único formato para as quatro telas; o front usa um só `montarPaginador`.
- ✅ `tamanho` máximo impede pedir a tabela inteira de uma vez.
- ⚠️ `GET /produtos` deixou de devolver array (contrato quebrado em relação à Fase 1).
- ⚠️ Paginação por `OFFSET`: páginas muito distantes ficam mais lentas (irrelevante neste porte).

---

<a id="adr-012"></a>

## ADR-012 — Transição de status por UPDATE condicional

**Contexto.** Dois usuários podem agir no mesmo pedido ao mesmo tempo ("confirmar" numa tela e "cancelar" em outra). "Ler, conferir o status em C# e salvar" deixa uma janela em que a segunda ação sobrescreve a primeira.

**Decisão.** Rotas de ação (`/confirmar`, `/entregar`, `/cancelar`) em vez de `PATCH status` (D2). Cada uma executa `UPDATE "Pedidos" SET "Status" = @destino WHERE "Id" = @id AND "Status" IN (@origens)` (`ExecuteUpdateAsync`). 1 linha = sucesso; 0 linhas → uma leitura curta decide entre 404 (não existe), 403 (Operador tentando cancelar Confirmado) e 409 ("Pedido Entregue não pode ser cancelado."). O papel entra na própria lista de origens.

**Consequências.**
- ✅ Atômico sem transação explícita nem coluna de versão: o banco decide quem chegou primeiro.
- ✅ Mensagem 409 clara por transição; repetir a ação também dá 409 (de propósito: o usuário fica sabendo que alguém agiu antes).
- ⚠️ A regra de transição fica no endpoint, não na entidade (o UPDATE não passa pelo objeto `Pedido`).
