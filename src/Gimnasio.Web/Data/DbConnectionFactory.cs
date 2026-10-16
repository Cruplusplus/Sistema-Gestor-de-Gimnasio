using System.Data;
using Microsoft.Data.SqlClient;

namespace Gimnasio.Web.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class SqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
