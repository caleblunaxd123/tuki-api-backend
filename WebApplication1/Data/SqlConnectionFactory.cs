using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Data
{
    public class SqlConnectionFactory
    {
        private readonly IConfiguration _configuration;

        public SqlConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ✅ Devuelve SqlConnection específicamente para SQL Server
        public SqlConnection GetConnection()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            }

            // Solo SQL Server
            return new SqlConnection(connectionString);
        }

        public string GetDatabaseType()
        {
            return "SqlServer";
        }
    }
}