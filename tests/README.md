# Testes de integração

Sobem a API real em memória (`WebApplicationFactory`) contra um PostgreSQL 17 em contêiner (Testcontainers).

## Como rodar

Requisito: **Docker** em execução (Docker Desktop no Windows/macOS).

```bash
dotnet test MyCRUD.slnx
```

Um único contêiner sobe por execução; as migrations são aplicadas uma vez. O CI (job `testes`) é a fonte de verdade.

## Padrão

- **Uma regra por teste** (R1, R8...). Nome: `Regra_Cenario_Resultado`, ex.: `R8_CategoriaComNomeRepetido_Retorna409`.
- **Dados com sufixo único** (`Cenario.Sufixo()`); cada teste cria o que usa e só verifica o que criou. Nada de contagens globais.
- Classes com `[Collection("Api")]` recebem `ApiFactory` no construtor. Ambiente `Testing`, limite de login 1000/min.
- Precisa de Development (cadastro, Scalar)? `IClassFixture<ApiFactoryDevelopment>`. Limite de 5 (429)? `IClassFixture<ApiFactoryLimite5>`.
- Helpers em `Infra/Cenario.cs`; o que muda com o ambiente está documentado no topo de `Infra/ApiFactory.cs`.
