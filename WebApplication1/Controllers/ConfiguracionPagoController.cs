using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WebApplication1.Models;
using static WebApplication1.Models.DTOs;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/configuracion-pago")]
    public class ConfiguracionPagoController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ConfiguracionPagoController> _logger;

        public ConfiguracionPagoController(IConfiguration config, ILogger<ConfiguracionPagoController> logger)
        {
            _config = config;
            _logger = logger;
        }

        // =============================================
        // GET: api/configuracion-pago/{usuarioId}
        // Obtener configuración de pago del usuario
        // =============================================
        [HttpGet("{usuarioId}")]
        public IActionResult ObtenerConfiguracion(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    string query = @"SELECT Id, UsuarioId, MetodoPago, NombreTitular, NumeroTelefono, 
                                           ImagenQR, Activo, FechaCreacion, FechaActualizacion
                                    FROM ConfiguracionesPago 
                                    WHERE UsuarioId = @UsuarioId AND Activo = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var configuracion = new ConfiguracionPagoResponse
                                {
                                    Id = (int)reader["Id"],
                                    MetodoPago = reader["MetodoPago"].ToString(),
                                    NombreTitular = reader["NombreTitular"].ToString(),
                                    NumeroTelefono = reader["NumeroTelefono"].ToString(),
                                    ImagenQR = reader.IsDBNull("ImagenQR") ? null : reader["ImagenQR"].ToString(),
                                    Activo = (bool)reader["Activo"],
                                    FechaCreacion = (DateTime)reader["FechaCreacion"],
                                    FechaActualizacion = (DateTime)reader["FechaActualizacion"]
                                };

                                Console.WriteLine($"✅ Configuración obtenida para usuario {usuarioId}: {configuracion.MetodoPago}");
                                return Ok(configuracion);
                            }
                            else
                            {
                                Console.WriteLine($"❌ No se encontró configuración para usuario {usuarioId}");
                                return NotFound(new { error = "No se encontró configuración de pago para este usuario" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo configuración: {ex.Message}");
                _logger.LogError(ex, "Error al obtener configuración de pago del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // POST: api/configuracion-pago/{usuarioId}
        // Crear nueva configuración de pago
        // =============================================
        [HttpPost("{usuarioId}")]
        public IActionResult CrearConfiguracion(int usuarioId, [FromBody] ConfigurarPagoRequest request)
        {
            try
            {
                // Validar datos
                var erroresValidacion = ValidacionesPeruanas.ValidarConfiguracionPago(request);
                if (erroresValidacion.Any())
                {
                    Console.WriteLine($"❌ Errores de validación: {string.Join(", ", erroresValidacion)}");
                    return BadRequest(new { error = "Datos inválidos", errores = erroresValidacion });
                }

                // Verificar que el usuario existe
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // Verificar usuario
                    string checkUsuario = "SELECT COUNT(1) FROM Usuarios WHERE Id = @UsuarioId";
                    using (SqlCommand cmd = new SqlCommand(checkUsuario, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        if ((int)cmd.ExecuteScalar() == 0)
                        {
                            return NotFound(new { error = "Usuario no encontrado" });
                        }
                    }

                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // Desactivar configuraciones anteriores
                        string desactivarAnteriores = @"UPDATE ConfiguracionesPago 
                                                       SET Activo = 0, FechaActualizacion = GETDATE()
                                                       WHERE UsuarioId = @UsuarioId AND Activo = 1";

                        using (SqlCommand cmd = new SqlCommand(desactivarAnteriores, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                            cmd.ExecuteNonQuery();
                        }

                        // Crear nueva configuración
                        string insertConfig = @"INSERT INTO ConfiguracionesPago 
                                               (UsuarioId, MetodoPago, NombreTitular, NumeroTelefono, ImagenQR, Activo, FechaCreacion, FechaActualizacion)
                                               VALUES 
                                               (@UsuarioId, @MetodoPago, @NombreTitular, @NumeroTelefono, @ImagenQR, 1, GETDATE(), GETDATE());
                                               SELECT SCOPE_IDENTITY();";

                        int nuevaConfiguracionId;
                        using (SqlCommand cmd = new SqlCommand(insertConfig, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                            cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago.ToLower());
                            cmd.Parameters.AddWithValue("@NombreTitular", request.NombreTitular);
                            cmd.Parameters.AddWithValue("@NumeroTelefono", ValidacionesPeruanas.LimpiarTelefono(request.NumeroTelefono));
                            cmd.Parameters.AddWithValue("@ImagenQR", request.ImagenQR);

                            nuevaConfiguracionId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        transaction.Commit();

                        Console.WriteLine($"✅ Configuración creada para usuario {usuarioId}: {request.MetodoPago} (ID: {nuevaConfiguracionId})");

                        // Retornar la configuración creada
                        return Ok(new
                        {
                            Id = nuevaConfiguracionId,
                            UsuarioId = usuarioId,
                            MetodoPago = request.MetodoPago.ToLower(),
                            NombreTitular = request.NombreTitular,
                            NumeroTelefono = ValidacionesPeruanas.LimpiarTelefono(request.NumeroTelefono),
                            Activo = true,
                            FechaCreacion = DateTime.Now,
                            Message = "Configuración de pago creada correctamente"
                        });
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creando configuración: {ex.Message}");
                _logger.LogError(ex, "Error al crear configuración de pago para usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // PUT: api/configuracion-pago/{usuarioId}
        // Actualizar configuración de pago existente
        // =============================================
        [HttpPut("{usuarioId}")]
        public IActionResult ActualizarConfiguracion(int usuarioId, [FromBody] ConfigurarPagoRequest request)
        {
            try
            {
                // Validar datos
                var erroresValidacion = ValidacionesPeruanas.ValidarConfiguracionPago(request);
                if (erroresValidacion.Any())
                {
                    Console.WriteLine($"❌ Errores de validación: {string.Join(", ", erroresValidacion)}");
                    return BadRequest(new { error = "Datos inválidos", errores = erroresValidacion });
                }

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // Verificar que existe una configuración activa
                    string checkConfig = "SELECT COUNT(1) FROM ConfiguracionesPago WHERE UsuarioId = @UsuarioId AND Activo = 1";
                    using (SqlCommand cmd = new SqlCommand(checkConfig, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        if ((int)cmd.ExecuteScalar() == 0)
                        {
                            return NotFound(new { error = "No se encontró configuración de pago activa para este usuario" });
                        }
                    }

                    // Actualizar configuración
                    string updateConfig = @"UPDATE ConfiguracionesPago 
                                          SET MetodoPago = @MetodoPago,
                                              NombreTitular = @NombreTitular,
                                              NumeroTelefono = @NumeroTelefono,
                                              ImagenQR = @ImagenQR,
                                              FechaActualizacion = GETDATE()
                                          WHERE UsuarioId = @UsuarioId AND Activo = 1";

                    using (SqlCommand cmd = new SqlCommand(updateConfig, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago.ToLower());
                        cmd.Parameters.AddWithValue("@NombreTitular", request.NombreTitular);
                        cmd.Parameters.AddWithValue("@NumeroTelefono", ValidacionesPeruanas.LimpiarTelefono(request.NumeroTelefono));
                        cmd.Parameters.AddWithValue("@ImagenQR", request.ImagenQR);

                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            Console.WriteLine($"✅ Configuración actualizada para usuario {usuarioId}: {request.MetodoPago}");
                            return Ok(new { message = "Configuración actualizada correctamente" });
                        }
                        else
                        {
                            return BadRequest(new { error = "No se pudo actualizar la configuración" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error actualizando configuración: {ex.Message}");
                _logger.LogError(ex, "Error al actualizar configuración de pago del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // DELETE: api/configuracion-pago/{usuarioId}
        // Desactivar configuración de pago
        // =============================================
        [HttpDelete("{usuarioId}")]
        public IActionResult DesactivarConfiguracion(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    string desactivarConfig = @"UPDATE ConfiguracionesPago 
                                              SET Activo = 0, FechaActualizacion = GETDATE()
                                              WHERE UsuarioId = @UsuarioId AND Activo = 1";

                    using (SqlCommand cmd = new SqlCommand(desactivarConfig, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            Console.WriteLine($"✅ Configuración desactivada para usuario {usuarioId}");
                            return Ok(new { message = "Configuración de pago desactivada correctamente" });
                        }
                        else
                        {
                            return NotFound(new { error = "No se encontró configuración activa para desactivar" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error desactivando configuración: {ex.Message}");
                _logger.LogError(ex, "Error al desactivar configuración de pago del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message }); // ✅ ESTE ERA EL RETURN FALTANTE
            }
        }

        // =============================================
        // GET: api/configuracion-pago/{usuarioId}/verificar
        // Verificar si el usuario tiene método de pago configurado
        // =============================================
        [HttpGet("{usuarioId}/verificar")]
        public IActionResult VerificarConfiguracion(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // Usar función directa en lugar del procedimiento (si no existe la función)
                    string query = "SELECT COUNT(1) FROM ConfiguracionesPago WHERE UsuarioId = @UsuarioId AND Activo = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        bool tieneMetodo = (int)cmd.ExecuteScalar() > 0;

                        Console.WriteLine($"✅ Usuario {usuarioId} {(tieneMetodo ? "SÍ" : "NO")} tiene método de pago");
                        return Ok(new
                        {
                            usuarioId = usuarioId,
                            tieneMetodoPago = tieneMetodo,
                            mensaje = tieneMetodo ? "Usuario tiene método de pago configurado" : "Usuario no tiene método de pago configurado"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error verificando configuración: {ex.Message}");
                _logger.LogError(ex, "Error al verificar configuración de pago del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // GET: api/configuracion-pago/{usuarioId}/qr
        // Obtener solo el QR del usuario (para mostrar en grupos)
        // =============================================
        [HttpGet("{usuarioId}/qr")]
        public IActionResult ObtenerQR(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    string query = @"SELECT MetodoPago, NombreTitular, ImagenQR 
                                    FROM ConfiguracionesPago 
                                    WHERE UsuarioId = @UsuarioId AND Activo = 1";

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
                                    ImagenQR = reader.IsDBNull("ImagenQR") ? null : reader["ImagenQR"].ToString()
                                };

                                Console.WriteLine($"✅ QR obtenido para usuario {usuarioId}: {qrResponse.MetodoPago}");
                                return Ok(qrResponse);
                            }
                            else
                            {
                                return NotFound(new { error = "No se encontró configuración de pago" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo QR: {ex.Message}");
                _logger.LogError(ex, "Error al obtener QR del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // POST: api/configuracion-pago/validar-qr
        // Validar que el QR sea legible
        // =============================================
        [HttpPost("validar-qr")]
        public IActionResult ValidarQR([FromBody] ValidarQRRequest request)
        {
            try
            {
                var validacion = new QRValidacionResponse
                {
                    EsValido = ValidacionesPeruanas.EsImagenBase64Valida(request.ImagenQR),
                    TamanoBytes = CalcularTamanoBase64(request.ImagenQR),
                    Formato = DetectarFormatoImagen(request.ImagenQR)
                };

                if (!validacion.EsValido)
                {
                    validacion.Errores.Add("El formato de imagen no es válido");
                }

                if (validacion.TamanoBytes > 5 * 1024 * 1024) // 5MB
                {
                    validacion.EsValido = false;
                    validacion.Errores.Add("La imagen es demasiado grande (máximo 5MB)");
                }

                var mensaje = validacion.EsValido ? "QR válido" : "QR inválido";
                Console.WriteLine($"✅ Validación QR: {mensaje} ({validacion.TamanoBytes} bytes, {validacion.Formato})");

                return Ok(new
                {
                    validacion = validacion,
                    mensaje = mensaje
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error validando QR: {ex.Message}");
                _logger.LogError(ex, "Error al validar QR");
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // MÉTODOS PRIVADOS DE AYUDA
        // =============================================
        private static long CalcularTamanoBase64(string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64)) return 0;

                var base64Data = base64.Contains(',') ? base64.Split(',')[1] : base64;
                var bytes = Convert.FromBase64String(base64Data);
                return bytes.Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string DetectarFormatoImagen(string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64)) return "Desconocido";

                if (base64.Contains("data:image/"))
                {
                    var tipo = base64.Split(';')[0].Replace("data:image/", "").ToUpper();
                    return tipo;
                }

                var base64Data = base64.Contains(',') ? base64.Split(',')[1] : base64;
                var bytes = Convert.FromBase64String(base64Data);

                if (bytes.Length >= 4)
                {
                    // PNG
                    if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                        return "PNG";

                    // JPEG
                    if (bytes[0] == 0xFF && bytes[1] == 0xD8)
                        return "JPEG";

                    // GIF
                    if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
                        return "GIF";
                }

                return "Desconocido";
            }
            catch
            {
                return "Error";
            }
        }
    }
}