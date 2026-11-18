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
