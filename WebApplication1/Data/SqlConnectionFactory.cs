using Microsoft.Data.SqlClient;
using Npgsql;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace WebApplication1.Data
{
    public class SqlConnectionFactory
    {
        private readonly IConfiguration _configuration;

        public SqlConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IDbConnection GetConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            // Detectar si es PostgreSQL o SQL Server basado en la connection string
            if (connectionString.StartsWith("postgresql://") || connectionString.Contains("postgres"))
            {
                // PostgreSQL para producción
                return new NpgsqlConnection(connectionString);
            }
            else
            {
                // SQL Server para desarrollo local
                return new SqlConnection(connectionString);
            }
        }

        public string GetDatabaseType()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (connectionString.StartsWith("postgresql://") || connectionString.Contains("postgres"))
            {
                return "PostgreSQL";
            }
            else
            {
                return "SqlServer";
            }
        }
    }
}