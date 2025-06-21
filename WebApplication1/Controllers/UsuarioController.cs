using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/usuario")]
    public class UsuarioController : ControllerBase
    {
        private readonly UsuarioRepository _repo;

        public UsuarioController(SqlConnectionFactory factory)
        {
            _repo = new UsuarioRepository(factory);
        }

        [HttpPost("registro")]
        public IActionResult Registrar([FromBody] Usuario usuario)
        {
            _repo.RegistrarUsuario(usuario);
            return Ok(new { mensaje = "Usuario registrado con éxito" });
        }
        private readonly string connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=TukiDB;Trusted_Connection=True;";

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string sql = "SELECT Id, Nombre, Telefono FROM Usuarios WHERE Telefono = @telefono AND Contrasena = @contrasena";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@telefono", request.Telefono);
                cmd.Parameters.AddWithValue("@contrasena", request.Contrasena);

                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var usuario = new
                    {
                        Id = reader["Id"],
                        Nombre = reader["Nombre"],
                        Telefono = reader["Telefono"]
                    };
                    return Ok(usuario);
                }
                else
                {
                    return Unauthorized("Credenciales incorrectas");
                }
            }
        }

    

        public class LoginRequest
        {
            public string Telefono { get; set; }
            public string Contrasena { get; set; }
        }

    }
}
