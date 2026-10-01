# CRUD Confeitaria - Web API em C#

Uma Web API simples desenvolvida em C# (.NET) com Minimal APIs e Entity Framework Core, conectada ao banco de dados PostgreSQL do Supabase.

Este projeto tem como objetivo gerenciar os processos básicos de uma confeitaria ficticia (produtos, categorias, clientes e pedidos) utilizando uma arquitetura enxuta e moderna.

---

## Objetivos e Características do Projeto

* **Estrutura Enxuta:** Desenvolvido com Minimal APIs do .NET para código simples e direto .
* **ORM Moderno:** Uso do Entity Framework Core para mapeamento e manipulação de dados sem necessidade de escrever SQL puro(gosto de SQL mas isso aqui é bom d+).
* **Banco na Nuvem:** Conexão nativa com PostgreSQL hospedado no Supabase.
* **Gerenciamento de Pacotes:** Restauração automática de dependências (sem necessidade de ambientes virtuais como no Python).

---

## Mapeamento de Tabelas (Banco de Dados)

O sistema conta com 4 tabelas relacionais simples:

1. **Categorias**: Tipos de produtos comercializados.
   * `Id` (int), `Nome` (string) - Ex: Bolos, Doces Finos, Tortas, Salgados.
2. **Produtos**: Itens do cardápio da confeitaria.
   * `Id` (int), `Nome` (string), `Preco` (decimal), `CategoriaId` (int), `Ativo` (bool).
3. **Clientes**: Cadastro de clientes para encomendas.
   * `Id` (int), `Nome` (string), `Telefone` (string), `Endereco` (string).
4. **Pedidos**: Registro das vendas e entregas.
   * `Id` (int), `ClienteId` (int), `DataEntrega` (DateTime), `Status` (string), `ValorTotal` (decimal).

---

## Pré-requisitos

Antes de começar, você precisará das seguintes ferramentas instaladas em sua máquina:

* [SDK do .NET (versão 8.0 ou superior)](https://dotnet.microsoft.com/download)
* [Git](https://git-scm.com/)
* Uma conta ativa no [Supabase](https://supabase.com/) (para o banco de dados PostgreSQL)

---

## Passo a Passo de Configuração do Ambiente

### 1. Clonar e Acessar o Repositório

```bash
# Clonar este repositório
git clone [https://github.com/MatheusRamos1233/CRUDConfeitaria.git](https://github.com/MatheusRamos1233/CRUDConfeitaria.git)

# Acessar a pasta do repositório
cd CRUDConfeitaria
