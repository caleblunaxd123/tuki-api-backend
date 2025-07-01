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
        private readonly SqlConnectionFactory _connectionFactory;
        private readonly ILogger<UsuarioController> _logger;

        public UsuarioController(SqlConnectionFactory factory, ILogger<UsuarioController> logger)
        {
            _repo = new UsuarioRepository(factory);
            _connectionFactory = factory;
            _logger = logger;
        }

        [HttpPost("registro")]
        public IActionResult Registrar([FromBody] Usuario usuario)
        {
            try
            {
                _repo.RegistrarUsuario(usuario);
                return Ok(new { mensaje = "Usuario registrado con éxito" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error en registro: {ex.Message}");
                return StatusCode(500, new { mensaje = "Error interno del servidor" });
            }
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation($"🔍 Login attempt for phone: {request?.Telefono}");

                if (request == null || string.IsNullOrEmpty(request.Telefono) || string.IsNullOrEmpty(request.Contrasena))
                {
                    _logger.LogWarning("❌ Invalid request data");
                    return BadRequest(new { message = "Teléfono y contraseña son requeridos" });
                }

                // ✅ USAR SqlConnectionFactory - ahora devuelve SqlConnection directamente
                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();
                    _logger.LogInformation("✅ Database connection opened successfully");

                    string sql = "SELECT Id, Nombre, Telefono FROM Usuarios WHERE Telefono = @telefono AND Contrasena = @contrasena";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@telefono", request.Telefono);
                        cmd.Parameters.AddWithValue("@contrasena", request.Contrasena);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var usuario = new
                                {
                                    id = Convert.ToInt32(reader["Id"]),
                                    nombre = reader["Nombre"].ToString(),
                                    telefono = reader["Telefono"].ToString()
                                };

                                _logger.LogInformation($"✅ Login successful for user: {usuario.nombre}");
                                return Ok(usuario);
                            }
                            else
                            {
                                _logger.LogWarning($"❌ Invalid credentials for phone: {request.Telefono}");
                                return Unauthorized(new { message = "Credenciales incorrectas" });
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError($"❌ SQL Error: {sqlEx.Message}");
                return StatusCode(500, new { message = "Error de base de datos", error = sqlEx.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ General Error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        public class LoginRequest
        {
            public string Telefono { get; set; }
            public string Contrasena { get; set; }
        }
    }
}