using System.Data;
using Npgsql;

namespace MultiSiteIkas.Data.Connections;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    Task<IDbConnection> CreateConnectionAsync();
}

public class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public Task<IDbConnection> CreateConnectionAsync()
    {
        IDbConnection connection = new NpgsqlConnection(_connectionString);
        return Task.FromResult(connection);
    }
}
