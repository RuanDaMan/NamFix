using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NamFix.Application.Data;

/// <summary>
/// Creates open ADO.NET connections for the Dapper repositories. The connection string is
/// resolved from configuration (appsettings + environment variables) under "ConnectionStrings:NamFix".
/// </summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("NamFix")
            ?? throw new InvalidOperationException(
                "Connection string 'NamFix' is not configured. Set ConnectionStrings:NamFix in appsettings or the NAMFIX_DB env var.");
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
