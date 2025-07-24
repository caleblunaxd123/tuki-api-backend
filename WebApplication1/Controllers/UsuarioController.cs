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

        [HttpPost("configurar-pago")]
        public IActionResult ConfigurarMetodoPago([FromBody] ConfigurarPagoRequest request)
        {
            try
            {
                Console.WriteLine($"📱 Configurando método de pago para usuario {request.UsuarioId}");
                Console.WriteLine($"📝 Método: {request.MetodoPago}");
                Console.WriteLine($"👤 Titular: {request.NombreTitular}");
                Console.WriteLine($"📞 Teléfono: {request.NumeroTelefono}");
                Console.WriteLine($"🖼️ Tiene QR: {!string.IsNullOrEmpty(request.QrImageBase64)}");

                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // Verificar si el usuario existe
                        string checkUserQuery = "SELECT COUNT(*) FROM Usuarios WHERE Id = @UsuarioId";
                        using (SqlCommand cmd = new SqlCommand(checkUserQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            int userCount = (int)cmd.ExecuteScalar();

                            if (userCount == 0)
                            {
                                transaction.Rollback();
                                return NotFound(new { error = "Usuario no encontrado" });
                            }
                        }

                        // Verificar si ya tiene configuración de pago
                        string checkPaymentQuery = @"
                    SELECT COUNT(*) FROM MetodosPago 
                    WHERE UsuarioId = @UsuarioId";

                        bool tieneConfiguracion = false;
                        using (SqlCommand cmd = new SqlCommand(checkPaymentQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            tieneConfiguracion = (int)cmd.ExecuteScalar() > 0;
                        }

                        if (tieneConfiguracion)
                        {
                            // Actualizar configuración existente
                            string updateQuery = @"
                        UPDATE MetodosPago 
                        SET MetodoPago = @MetodoPago,
                            NombreTitular = @NombreTitular,
                            NumeroTelefono = @NumeroTelefono,
                            QrImage = @QrImage,
                            FechaActualizacion = GETDATE()
                        WHERE UsuarioId = @UsuarioId";

                            using (SqlCommand cmd = new SqlCommand(updateQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                                cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago);
                                cmd.Parameters.AddWithValue("@NombreTitular", request.NombreTitular);
                                cmd.Parameters.AddWithValue("@NumeroTelefono", request.NumeroTelefono);
                                cmd.Parameters.AddWithValue("@QrImage",
                                    string.IsNullOrEmpty(request.QrImageBase64) ? (object)DBNull.Value : request.QrImageBase64);

                                cmd.ExecuteNonQuery();
                            }

                            Console.WriteLine("✅ Configuración de pago actualizada");
                        }
                        else
                        {
                            // Crear nueva configuración
                            string insertQuery = @"
                        INSERT INTO MetodosPago (UsuarioId, MetodoPago, NombreTitular, NumeroTelefono, QrImage, FechaCreacion)
                        VALUES (@UsuarioId, @MetodoPago, @NombreTitular, @NumeroTelefono, @QrImage, GETDATE())";

                            using (SqlCommand cmd = new SqlCommand(insertQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                                cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago);
                                cmd.Parameters.AddWithValue("@NombreTitular", request.NombreTitular);
                                cmd.Parameters.AddWithValue("@NumeroTelefono", request.NumeroTelefono);
                                cmd.Parameters.AddWithValue("@QrImage",
                                    string.IsNullOrEmpty(request.QrImageBase64) ? (object)DBNull.Value : request.QrImageBase64);

                                cmd.ExecuteNonQuery();
                            }

                            Console.WriteLine("✅ Nueva configuración de pago creada");
                        }

                        transaction.Commit();

                        return Ok(new
                        {
                            message = tieneConfiguracion ? "Método de pago actualizado correctamente" : "Método de pago configurado correctamente",
                            metodoPago = request.MetodoPago,
                            nombreTitular = request.NombreTitular,
                            numeroTelefono = request.NumeroTelefono,
                            tieneQR = !string.IsNullOrEmpty(request.QrImageBase64),
                            fechaConfiguracion = DateTime.Now
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"❌ Error en transacción: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error configurando método de pago: {ex.Message}");
                return BadRequest(new
                {
                    error = "Error interno del servidor",
                    details = ex.Message
                });
            }
        }

        [HttpGet("metodo-pago/{usuarioId}")]
        public IActionResult ObtenerMetodoPago(int usuarioId)
        {
            try
            {
                Console.WriteLine($"🔍 Obteniendo método de pago para usuario {usuarioId}");

                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    mp.MetodoPago,
                    mp.NombreTitular,
                    mp.NumeroTelefono,
                    mp.QrImage,
                    mp.FechaCreacion,
                    mp.FechaActualizacion,
                    u.Nombre as NombreUsuario,
                    u.Telefono as TelefonoUsuario
                FROM MetodosPago mp
                INNER JOIN Usuarios u ON mp.UsuarioId = u.Id
                WHERE mp.UsuarioId = @UsuarioId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var metodoPago = new
                                {
                                    metodoPago = reader["MetodoPago"].ToString(),
                                    nombreTitular = reader["NombreTitular"].ToString(),
                                    numeroTelefono = reader["NumeroTelefono"].ToString(),
                                    qrImage = reader["QrImage"] == DBNull.Value ? null : reader["QrImage"].ToString(),
                                    fechaCreacion = (DateTime)reader["FechaCreacion"],
                                    fechaActualizacion = reader["FechaActualizacion"] as DateTime?,
                                    nombreUsuario = reader["NombreUsuario"].ToString(),
                                    telefonoUsuario = reader["TelefonoUsuario"].ToString(),
                                    configurado = true
                                };

                                Console.WriteLine($"✅ Método de pago encontrado: {metodoPago.metodoPago}");
                                return Ok(metodoPago);
                            }
                            else
                            {
                                Console.WriteLine($"ℹ️ Usuario {usuarioId} no tiene método de pago configurado");
                                return NotFound(new
                                {
                                    message = "Usuario no tiene método de pago configurado",
                                    configurado = false
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo método de pago: {ex.Message}");
                return BadRequest(new
                {
                    error = "Error interno del servidor",
                    details = ex.Message
                });
            }
        }

        // Clase para el request de configurar pago
        public class ConfigurarPagoRequest
        {
            public int UsuarioId { get; set; }
            public string MetodoPago { get; set; }  // "yape" o "plin"
            public string NombreTitular { get; set; }
            public string NumeroTelefono { get; set; }
            public string? QrImageBase64 { get; set; }
        }
        public class LoginRequest
        {
            public string Telefono { get; set; }
            public string Contrasena { get; set; }
        }
    }
}