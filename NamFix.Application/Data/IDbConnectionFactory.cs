using System.Data;
using Microsoft.Data.SqlClient;

namespace NamFix.Application.Data;

/// <summary>
/// Creates open ADO.NET connections for the Dapper repositories. The connection string is supplied
/// by the host from "ConnectionStrings:DefaultConnection" in appsettings.json.
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
