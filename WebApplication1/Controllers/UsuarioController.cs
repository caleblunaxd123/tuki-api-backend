using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Data;
using WebApplication1.Models;
using static WebApplication1.Models.DTOs;

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
                // ✅ Validación usando tu clase de validaciones existente
                if (request == null)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("Datos requeridos"));
                }

                // ✅ Usar las validaciones que ya tienes definidas
                var erroresValidacion = ValidacionesPeruanas.ValidarConfiguracionPago(request);
                if (erroresValidacion.Any())
                {
                    Console.WriteLine($"❌ Errores de validación: {string.Join(", ", erroresValidacion)}");
                    return BadRequest(ApiResponse<object>.ErrorResult("Datos inválidos", erroresValidacion));
                }

                // ✅ Necesitamos obtener el UsuarioId del contexto o agregarlo al request
                // Por ahora, lo obtendremos del teléfono
                int usuarioId = 0;
                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();

                    // Buscar usuario por teléfono
                    string getUserQuery = "SELECT Id FROM Usuarios WHERE Telefono = @Telefono";
                    using (SqlCommand cmd = new SqlCommand(getUserQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Telefono", ValidacionesPeruanas.LimpiarTelefono(request.NumeroTelefono));
                        var result = cmd.ExecuteScalar();

                        if (result == null)
                        {
                            return NotFound(ApiResponse<object>.ErrorResult("Usuario no encontrado con ese número de teléfono"));
                        }

                        usuarioId = Convert.ToInt32(result);
                    }
                }

                Console.WriteLine($"📱 Configurando método de pago para usuario {usuarioId}");
                Console.WriteLine($"📝 Método: {request.MetodoPago}");
                Console.WriteLine($"👤 Titular: {request.NombreTitular}");
                Console.WriteLine($"📞 Teléfono: {request.NumeroTelefono}");
                Console.WriteLine($"🖼️ Tiene QR: {!string.IsNullOrEmpty(request.ImagenQR)}");

                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // Verificar si ya tiene configuración de pago
                        string checkPaymentQuery = "SELECT COUNT(*) FROM MetodosPago WHERE UsuarioId = @UsuarioId";
                        bool tieneConfiguracion = false;

                        using (SqlCommand cmd = new SqlCommand(checkPaymentQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
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
                                cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                                cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago.ToLower());
                                cmd.Parameters.AddWithValue("@NombreTitular", request.NombreTitular.Trim());
                                cmd.Parameters.AddWithValue("@NumeroTelefono", ValidacionesPeruanas.LimpiarTelefono(request.NumeroTelefono));
                                cmd.Parameters.AddWithValue("@QrImage", request.ImagenQR);

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
                                cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                                cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago.ToLower());
                                cmd.Parameters.AddWithValue("@NombreTitular", request.NombreTitular.Trim());
                                cmd.Parameters.AddWithValue("@NumeroTelefono", ValidacionesPeruanas.LimpiarTelefono(request.NumeroTelefono));
                                cmd.Parameters.AddWithValue("@QrImage", request.ImagenQR);

                                cmd.ExecuteNonQuery();
                            }

                            Console.WriteLine("✅ Nueva configuración de pago creada");
                        }

                        transaction.Commit();

                        // ✅ Respuesta usando tu estructura de DTOs
                        var response = new ConfiguracionPagoResponse
                        {
                            MetodoPago = request.MetodoPago,
                            NombreTitular = request.NombreTitular,
                            NumeroTelefono = ValidacionesPeruanas.FormatearTelefono(request.NumeroTelefono),
                            FechaCreacion = DateTime.Now,
                            FechaActualizacion = DateTime.Now,
                            Activo = true
                        };

                        Console.WriteLine("✅ Configuración guardada exitosamente");
                        return Ok(ApiResponse<ConfiguracionPagoResponse>.SuccessResult(
                            response,
                            tieneConfiguracion ? "Método de pago actualizado correctamente" : "Método de pago configurado correctamente"
                        ));
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
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                return BadRequest(ApiResponse<object>.ErrorResult("Error interno del servidor", ex.Message));
            }
        }

        [HttpGet("metodo-pago/{usuarioId}")]
        public IActionResult ObtenerMetodoPago(int usuarioId)
        {
            try
            {
                Console.WriteLine($"🔍 Obteniendo método de pago para usuario {usuarioId}");

                if (usuarioId <= 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("ID de usuario inválido"));
                }

                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            mp.Id,
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
                                var metodoPago = new ConfiguracionPagoResponse
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    MetodoPago = reader["MetodoPago"].ToString(),
                                    NombreTitular = reader["NombreTitular"].ToString(),
                                    NumeroTelefono = ValidacionesPeruanas.FormatearTelefono(reader["NumeroTelefono"].ToString()),
                                    ImagenQR = reader["QrImage"]?.ToString(),
                                    FechaCreacion = (DateTime)reader["FechaCreacion"],
                                    FechaActualizacion = reader["FechaActualizacion"] as DateTime? ?? (DateTime)reader["FechaCreacion"],
                                    Activo = true
                                };

                                Console.WriteLine($"✅ Método de pago encontrado: {metodoPago.MetodoPago}");
                                return Ok(ApiResponse<ConfiguracionPagoResponse>.SuccessResult(
                                    metodoPago,
                                    "Método de pago obtenido correctamente"
                                ));
                            }
                            else
                            {
                                Console.WriteLine($"ℹ️ Usuario {usuarioId} no tiene método de pago configurado");
                                return NotFound(ApiResponse<object>.ErrorResult("Usuario no tiene método de pago configurado"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo método de pago: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                return BadRequest(ApiResponse<object>.ErrorResult("Error interno del servidor", ex.Message));
            }
        }

        [HttpGet("qr/{usuarioId}")]
        public IActionResult ObtenerQR(int usuarioId)
        {
            try
            {
                Console.WriteLine($"🔍 Obteniendo QR para usuario {usuarioId}");

                if (usuarioId <= 0)
                {
                    return BadRequest(ApiResponse<object>.ErrorResult("ID de usuario inválido"));
                }

                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();

                    string query = @"
                        SELECT MetodoPago, NombreTitular, QrImage
                        FROM MetodosPago
                        WHERE UsuarioId = @UsuarioId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var qrResponse = new QRResponse
                                {
                                    MetodoPago = reader["MetodoPago"].ToString(),
                                    NombreTitular = reader["NombreTitular"].ToString(),
                                    ImagenQR = reader["QrImage"]?.ToString()
                                };

                                Console.WriteLine($"✅ QR encontrado para método: {qrResponse.MetodoPago}");
                                return Ok(ApiResponse<QRResponse>.SuccessResult(qrResponse, "QR obtenido correctamente"));
                            }
                            else
                            {
                                Console.WriteLine($"ℹ️ Usuario {usuarioId} no tiene QR configurado");
                                return NotFound(ApiResponse<object>.ErrorResult("Usuario no tiene QR configurado"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo QR: {ex.Message}");
                return BadRequest(ApiResponse<object>.ErrorResult("Error interno del servidor", ex.Message));
            }
        }

        [HttpGet("{id}")]
        public IActionResult ObtenerUsuario(int id)
        {
            try
            {
                Console.WriteLine($"🔍 Obteniendo datos del usuario ID: {id}");

                if (id <= 0)
                {
                    return BadRequest(new { error = "ID de usuario inválido" });
                }

                using (SqlConnection conn = _connectionFactory.GetConnection())
                {
                    conn.Open();

                    // Obtener datos básicos del usuario
                    string queryUsuario = @"
                SELECT Id, Nombre, Telefono, Correo
                FROM Usuarios 
                WHERE Id = @Id";

                    using (SqlCommand cmd = new SqlCommand(queryUsuario, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var usuario = new
                                {
                                    id = (int)reader["Id"],
                                    nombre = reader["Nombre"]?.ToString(),
                                    telefono = reader["Telefono"]?.ToString(),
                                    email = reader["Correo"]?.ToString()
                                };

                                // Cerrar el reader antes de la siguiente consulta
                                reader.Close();

                                // Ahora obtener datos del método de pago
                                string queryMetodoPago = @"
                            SELECT MetodoPago, NombreTitular, NumeroTelefono, QrImage
                            FROM MetodosPago 
                            WHERE UsuarioId = @Id";

                                using (SqlCommand cmdPago = new SqlCommand(queryMetodoPago, conn))
                                {
                                    cmdPago.Parameters.AddWithValue("@Id", id);

                                    using (SqlDataReader readerPago = cmdPago.ExecuteReader())
                                    {
                                        if (readerPago.Read())
                                        {
                                            // Usuario CON método de pago configurado
                                            var usuarioCompleto = new
                                            {
                                                id = usuario.id,
                                                nombre = usuario.nombre,
                                                telefono = usuario.telefono,
                                                email = usuario.email,
                                                metodoPago = readerPago["MetodoPago"]?.ToString(),
                                                nombreTitular = readerPago["NombreTitular"]?.ToString(),
                                                telefonoPago = readerPago["NumeroTelefono"]?.ToString(),
                                                qrImage = readerPago["QrImage"]?.ToString()
                                            };

                                            Console.WriteLine($"✅ Usuario encontrado CON método de pago: {usuarioCompleto.nombre} ({usuarioCompleto.metodoPago})");
                                            return Ok(usuarioCompleto);
                                        }
                                        else
                                        {
                                            // Usuario SIN método de pago configurado
                                            var usuarioSinPago = new
                                            {
                                                id = usuario.id,
                                                nombre = usuario.nombre,
                                                telefono = usuario.telefono,
                                                email = usuario.email,
                                                metodoPago = (string?)null,
                                                nombreTitular = (string?)null,
                                                telefonoPago = (string?)null,
                                                qrImage = (string?)null
                                            };

                                            Console.WriteLine($"✅ Usuario encontrado SIN método de pago: {usuarioSinPago.nombre}");
                                            return Ok(usuarioSinPago);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"❌ Usuario con ID {id} no encontrado en base de datos");
                                return NotFound(new
                                {
                                    error = "Usuario no encontrado",
                                    id = id
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                Console.WriteLine($"❌ SQL Error obteniendo usuario {id}: {sqlEx.Message}");
                return StatusCode(500, new
                {
                    error = "Error de base de datos",
                    details = sqlEx.Message,
                    id = id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error general obteniendo usuario {id}: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                return StatusCode(500, new
                {
                    error = "Error interno del servidor",
                    details = ex.Message,
                    id = id
                });
            }
        }


        // Clases para compatibilidad con el login existente
        public class LoginRequest
        {
            public string Telefono { get; set; } = string.Empty;
            public string Contrasena { get; set; } = string.Empty;
        }
    }
}