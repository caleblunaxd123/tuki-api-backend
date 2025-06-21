using Microsoft.Data.SqlClient;
using System.Data;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    public class UsuarioRepository
    {
        private readonly IDbConnection _connection;

        public UsuarioRepository(SqlConnectionFactory factory)
        {
            _connection = factory.GetConnection();
        }

        public void RegistrarUsuario(Usuario usuario)
        {
            var command = _connection.CreateCommand();
            command.CommandText = "INSERT INTO Usuarios (Nombre, Telefono,Correo,Contrasena,FechaRegistro) VALUES (@nombre, @telefono,@correo,@contrasena,@fechaRegistro)";
            command.Parameters.Add(new SqlParameter("@nombre", usuario.Nombre));
            command.Parameters.Add(new SqlParameter("@telefono", usuario.Telefono));
            command.Parameters.Add(new SqlParameter("@correo", usuario.Correo));
            command.Parameters.Add(new SqlParameter("@contrasena", usuario.Contrasena));
            command.Parameters.Add(new SqlParameter("@fechaRegistro", DateTime.Now));

            _connection.Open();
            command.ExecuteNonQuery();
            _connection.Close();
        }
    }
}
