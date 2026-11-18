// Atalhos para rodar SQL cru no banco de testes (fora do EF, para provar o que o PostgreSQL recusa).

using Microsoft.EntityFrameworkCore;
using Npgsql;
using MyCRUD.Testes.Infra;

namespace MyCRUD.Testes.Seguranca;

internal static class BancoSql
{
    /// <summary>Executa um comando (INSERT/UPDATE) e devolve o número de linhas afetadas.</summary>
    public static async Task<int> ExecutarAsync(
        this ApiFactory fabrica, string sql, params NpgsqlParameter[] parametros)
    {
        await using var conexao = await AbrirAsync(fabrica);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddRange(parametros);
        return await comando.ExecuteNonQueryAsync();
    }

    /// <summary>Executa uma consulta e devolve a primeira coluna da primeira linha (null se não houver linha).</summary>
    public static async Task<object?> EscalarAsync(
        this ApiFactory fabrica, string sql, params NpgsqlParameter[] parametros)
    {
        await using var conexao = await AbrirAsync(fabrica);
        await using var comando = new NpgsqlCommand(sql, conexao);
        comando.Parameters.AddRange(parametros);
        var resultado = await comando.ExecuteScalarAsync();
        return resultado is DBNull ? null : resultado;
    }

    private static async Task<NpgsqlConnection> AbrirAsync(ApiFactory fabrica)
    {
        var stringDeConexao = await fabrica.ComBancoAsync(db =>
            Task.FromResult(db.Database.GetConnectionString()!));

        var conexao = new NpgsqlConnection(stringDeConexao);
        await conexao.OpenAsync();
        return conexao;
    }
}
