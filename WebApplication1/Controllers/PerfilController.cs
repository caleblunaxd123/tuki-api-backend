using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WebApplication1.Models;
using static WebApplication1.Models.DTOs;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/perfil")]
    public class PerfilController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PerfilController> _logger;

        public PerfilController(IConfiguration config, ILogger<PerfilController> logger)
        {
            _config = config;
            _logger = logger;
        }

        // =============================================
        // GET: api/perfil/{usuarioId}
        // Obtener perfil completo del usuario
        // =============================================
        [HttpGet("{usuarioId}")]
        public IActionResult ObtenerPerfil(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // Usar el procedimiento almacenado que creamos
                    using (SqlCommand cmd = new SqlCommand("SP_ObtenerPerfilCompleto", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var perfil = new PerfilCompletoResponse
                                {
                                    UsuarioId = (int)reader["UsuarioId"],
                                    Nombre = reader["Nombre"].ToString(),
                                    Telefono = reader["Telefono"].ToString(),
                                    Correo = reader.IsDBNull("Correo") ? null : reader["Correo"].ToString(),
                                    FechaRegistro = (DateTime)reader["FechaRegistro"],

                                    // Datos del perfil
                                    PerfilId = reader.IsDBNull("PerfilId") ? null : (int?)reader["PerfilId"],
                                    FotoPerfil = reader.IsDBNull("FotoPerfil") ? null : reader["FotoPerfil"].ToString(),
                                    Biografia = reader.IsDBNull("Biografia") ? null : reader["Biografia"].ToString(),
                                    FechaNacimiento = reader.IsDBNull("FechaNacimiento") ? null : (DateTime?)reader["FechaNacimiento"],
                                    Genero = reader.IsDBNull("Genero") ? null : reader["Genero"].ToString(),
                                    PerfilFechaCreacion = reader.IsDBNull("PerfilFechaCreacion") ? null : (DateTime?)reader["PerfilFechaCreacion"],
                                    PerfilFechaActualizacion = reader.IsDBNull("PerfilFechaActualizacion") ? null : (DateTime?)reader["PerfilFechaActualizacion"],

                                    // Configuración de pago
                                    ConfigPagoId = reader.IsDBNull("ConfigPagoId") ? null : (int?)reader["ConfigPagoId"],
                                    MetodoPago = reader.IsDBNull("MetodoPago") ? null : reader["MetodoPago"].ToString(),
                                    NombreTitular = reader.IsDBNull("NombreTitular") ? null : reader["NombreTitular"].ToString(),
                                    TelefonoPago = reader.IsDBNull("TelefonoPago") ? null : reader["TelefonoPago"].ToString(),
                                    ImagenQR = reader.IsDBNull("ImagenQR") ? null : reader["ImagenQR"].ToString(),
                                    ConfigPagoFechaCreacion = reader.IsDBNull("ConfigPagoFechaCreacion") ? null : (DateTime?)reader["ConfigPagoFechaCreacion"],
                                    ConfigPagoFechaActualizacion = reader.IsDBNull("ConfigPagoFechaActualizacion") ? null : (DateTime?)reader["ConfigPagoFechaActualizacion"],

                                    // Estados
                                    TieneMetodoPago = (bool)reader["TieneMetodoPago"],
                                    TienePerfil = (bool)reader["TienePerfil"]
                                };

                                // Evaluar si el perfil está completo
                                perfil.EsPerfilCompleto = EvaluarPerfilCompleto(perfil);
                                perfil.CamposFaltantes = ObtenerCamposFaltantes(perfil);

                                Console.WriteLine($"✅ Perfil obtenido para usuario {usuarioId}");
                                return Ok(perfil);
                            }
                            else
                            {
                                Console.WriteLine($"❌ Usuario {usuarioId} no encontrado");
                                return NotFound(new { error = "Usuario no encontrado" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo perfil: {ex.Message}");
                _logger.LogError(ex, "Error al obtener perfil del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // PUT: api/perfil/{usuarioId}
        // Actualizar perfil del usuario
        // =============================================
        [HttpPut("{usuarioId}")]
        public IActionResult ActualizarPerfil(int usuarioId, [FromBody] ActualizarPerfilRequest request)
        {
            try
            {
                // Validar datos
                var erroresValidacion = ValidacionesPeruanas.ValidarPerfilCompleto(request);
                if (erroresValidacion.Any())
                {
                    Console.WriteLine($"❌ Errores de validación: {string.Join(", ", erroresValidacion)}");
                    return BadRequest(new { error = "Datos inválidos", errores = erroresValidacion });
                }

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // 1. Actualizar datos básicos del usuario
                        string updateUsuario = @"UPDATE Usuarios 
                                               SET Nombre = @Nombre, 
                                                   Telefono = @Telefono, 
                                                   Correo = @Correo 
                                               WHERE Id = @UsuarioId";

                        using (SqlCommand cmd = new SqlCommand(updateUsuario, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                            cmd.Parameters.AddWithValue("@Nombre", request.Nombre);
                            cmd.Parameters.AddWithValue("@Telefono", ValidacionesPeruanas.LimpiarTelefono(request.Telefono));
                            cmd.Parameters.AddWithValue("@Correo", (object?)request.Correo ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }

                        // 2. Verificar si el perfil existe
                        bool perfilExiste = false;
                        string checkPerfil = "SELECT COUNT(1) FROM PerfilesUsuario WHERE UsuarioId = @UsuarioId AND Activo = 1";
                        using (SqlCommand cmd = new SqlCommand(checkPerfil, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                            perfilExiste = (int)cmd.ExecuteScalar() > 0;
                        }

                        if (perfilExiste)
                        {
                            // Actualizar perfil existente
                            string updatePerfil = @"UPDATE PerfilesUsuario 
                                                   SET FotoPerfil = @FotoPerfil,
                                                       Biografia = @Biografia,
                                                       FechaNacimiento = @FechaNacimiento,
                                                       Genero = @Genero,
                                                       FechaActualizacion = GETDATE()
                                                   WHERE UsuarioId = @UsuarioId AND Activo = 1";

                            using (SqlCommand cmd = new SqlCommand(updatePerfil, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                                cmd.Parameters.AddWithValue("@FotoPerfil", (object?)request.FotoPerfil ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Biografia", (object?)request.Biografia ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@FechaNacimiento", (object?)request.FechaNacimiento ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Genero", (object?)request.Genero ?? DBNull.Value);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Crear nuevo perfil
                            string insertPerfil = @"INSERT INTO PerfilesUsuario 
                                                   (UsuarioId, FotoPerfil, Biografia, FechaNacimiento, Genero, FechaCreacion, FechaActualizacion, Activo)
                                                   VALUES 
                                                   (@UsuarioId, @FotoPerfil, @Biografia, @FechaNacimiento, @Genero, GETDATE(), GETDATE(), 1)";

                            using (SqlCommand cmd = new SqlCommand(insertPerfil, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                                cmd.Parameters.AddWithValue("@FotoPerfil", (object?)request.FotoPerfil ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Biografia", (object?)request.Biografia ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@FechaNacimiento", (object?)request.FechaNacimiento ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Genero", (object?)request.Genero ?? DBNull.Value);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        Console.WriteLine($"✅ Perfil actualizado para usuario {usuarioId}");

                        // Obtener el perfil actualizado
                        return ObtenerPerfil(usuarioId);
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
                Console.WriteLine($"❌ Error actualizando perfil: {ex.Message}");
                _logger.LogError(ex, "Error al actualizar perfil del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // POST: api/perfil/{usuarioId}/foto
        // Subir foto de perfil
        // =============================================
        [HttpPost("{usuarioId}/foto")]
        public IActionResult SubirFotoPerfil(int usuarioId, [FromBody] FotoPerfilRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FotoBase64))
                {
                    return BadRequest(new { error = "La foto es requerida" });
                }

                // Validar que sea una imagen válida en base64
                if (!ValidacionesPeruanas.EsImagenBase64Valida(request.FotoBase64))
                {
                    return BadRequest(new { error = "Formato de imagen inválido" });
                }

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // Verificar si el perfil existe
                    bool perfilExiste = false;
                    string checkPerfil = "SELECT COUNT(1) FROM PerfilesUsuario WHERE UsuarioId = @UsuarioId AND Activo = 1";
                    using (SqlCommand cmd = new SqlCommand(checkPerfil, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        perfilExiste = (int)cmd.ExecuteScalar() > 0;
                    }

                    if (!perfilExiste)
                    {
                        // Crear perfil básico con la foto
                        string insertPerfil = @"INSERT INTO PerfilesUsuario 
                                               (UsuarioId, FotoPerfil, FechaCreacion, FechaActualizacion, Activo)
                                               VALUES 
                                               (@UsuarioId, @FotoPerfil, GETDATE(), GETDATE(), 1)";

                        using (SqlCommand cmd = new SqlCommand(insertPerfil, conn))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                            cmd.Parameters.AddWithValue("@FotoPerfil", request.FotoBase64);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Actualizar foto existente
                        string updateFoto = @"UPDATE PerfilesUsuario 
                                             SET FotoPerfil = @FotoPerfil, FechaActualizacion = GETDATE() 
                                             WHERE UsuarioId = @UsuarioId AND Activo = 1";

                        using (SqlCommand cmd = new SqlCommand(updateFoto, conn))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                            cmd.Parameters.AddWithValue("@FotoPerfil", request.FotoBase64);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    Console.WriteLine($"✅ Foto de perfil actualizada para usuario {usuarioId}");
                    return Ok(new { message = "Foto de perfil actualizada correctamente" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error subiendo foto: {ex.Message}");
                _logger.LogError(ex, "Error al subir foto de perfil del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // DELETE: api/perfil/{usuarioId}/foto
        // Eliminar foto de perfil
        // =============================================
        [HttpDelete("{usuarioId}/foto")]
        public IActionResult EliminarFotoPerfil(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    string updateFoto = @"UPDATE PerfilesUsuario 
                                         SET FotoPerfil = NULL, FechaActualizacion = GETDATE() 
                                         WHERE UsuarioId = @UsuarioId AND Activo = 1";

                    using (SqlCommand cmd = new SqlCommand(updateFoto, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            Console.WriteLine($"✅ Foto eliminada para usuario {usuarioId}");
                            return Ok(new { message = "Foto eliminada correctamente" });
                        }
                        else
                        {
                            return NotFound(new { error = "Perfil no encontrado" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error eliminando foto: {ex.Message}");
                _logger.LogError(ex, "Error al eliminar foto de perfil del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // GET: api/perfil/{usuarioId}/resumen
        // Obtener resumen básico del perfil
        // =============================================
        [HttpGet("{usuarioId}/resumen")]
        public IActionResult ObtenerResumenPerfil(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            u.Id, u.Nombre, u.Telefono, u.Correo,
                            p.FotoPerfil,
                            CASE WHEN cp.Id IS NOT NULL THEN 1 ELSE 0 END as TieneMetodoPago,
                            cp.MetodoPago
                        FROM Usuarios u
                        LEFT JOIN PerfilesUsuario p ON u.Id = p.UsuarioId AND p.Activo = 1
                        LEFT JOIN ConfiguracionesPago cp ON u.Id = cp.UsuarioId AND cp.Activo = 1
                        WHERE u.Id = @UsuarioId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var resumen = new
                                {
                                    usuarioId = (int)reader["Id"],
                                    nombre = reader["Nombre"].ToString(),
                                    telefono = reader["Telefono"].ToString(),
                                    correo = reader.IsDBNull("Correo") ? null : reader["Correo"].ToString(),
                                    fotoPerfil = reader.IsDBNull("FotoPerfil") ? null : reader["FotoPerfil"].ToString(),
                                    tieneMetodoPago = (bool)reader["TieneMetodoPago"],
                                    metodoPago = reader.IsDBNull("MetodoPago") ? null : reader["MetodoPago"].ToString(),
                                    perfilCompleto = !string.IsNullOrEmpty(reader["Nombre"].ToString()) &&
                                                   !string.IsNullOrEmpty(reader["Telefono"].ToString()) &&
                                                   (bool)reader["TieneMetodoPago"]
                                };

                                Console.WriteLine($"✅ Resumen obtenido para usuario {usuarioId}");
                                return Ok(resumen);
                            }
                            else
                            {
                                return NotFound(new { error = "Usuario no encontrado" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error obteniendo resumen: {ex.Message}");
                _logger.LogError(ex, "Error al obtener resumen del usuario {UsuarioId}", usuarioId);
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        // =============================================
        // POST: api/perfil/{usuarioId}/validar
        // Validar datos antes de guardar
        // =============================================
        [HttpPost("{usuarioId}/validar")]
        public IActionResult ValidarDatosPerfil([FromBody] ActualizarPerfilRequest request)
        {
            try
            {
                var errores = ValidacionesPeruanas.ValidarPerfilCompleto(request);

                var resultado = new
                {
                    esValido = errores.Count == 0,
                    errores = errores,
                    advertencias = new List<string>()
                };

                // Agregar advertencias opcionales
                if (string.IsNullOrEmpty(request.Correo))
                    resultado.advertencias.Add("Se recomienda agregar un correo electrónico");

                if (request.FechaNacimiento == null)
                    resultado.advertencias.Add("Se recomienda agregar la fecha de nacimiento");

                Console.WriteLine($"✅ Validación completada: {(resultado.esValido ? "Válido" : "Inválido")}");
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en validación: {ex.Message}");
                return BadRequest(new { error = "Error en validación", details = ex.Message });
            }
        }

        // =============================================
        // MÉTODOS PRIVADOS DE AYUDA
        // =============================================
        private static bool EvaluarPerfilCompleto(PerfilCompletoResponse perfil)
        {
            return !string.IsNullOrEmpty(perfil.Nombre) &&
                   !string.IsNullOrEmpty(perfil.Telefono) &&
                   perfil.TieneMetodoPago;
        }

        private static List<string> ObtenerCamposFaltantes(PerfilCompletoResponse perfil)
        {
            var faltantes = new List<string>();

            if (string.IsNullOrEmpty(perfil.Nombre))
                faltantes.Add("Nombre");

            if (string.IsNullOrEmpty(perfil.Telefono))
                faltantes.Add("Teléfono");

            if (string.IsNullOrEmpty(perfil.Correo))
                faltantes.Add("Correo");

            if (!perfil.TieneMetodoPago)
                faltantes.Add("Método de pago");

            if (string.IsNullOrEmpty(perfil.FotoPerfil))
                faltantes.Add("Foto de perfil");

            return faltantes;
        }
    }
}