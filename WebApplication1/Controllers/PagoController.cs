using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/pago")]
    public class PagoController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHubContext<PagoHub> _hubContext;
        private readonly string connectionString;

        public PagoController(IConfiguration config, IHubContext<PagoHub> hubContext)
        {
            _config = config;
            _hubContext = hubContext;
            connectionString = _config.GetConnectionString("DefaultConnection") ??
                             "Server=(localdb)\\MSSQLLocalDB;Database=TukiDB;Trusted_Connection=True;";
        }

        // ================================
        // ENDPOINT CORREGIDO
        // ================================
        [HttpPost("crear-preferencia")]
        public async Task<IActionResult> CrearPreferenciaPago([FromBody] CrearPagoRequest request)
        {
            try
            {
                Console.WriteLine($"📥 === INICIO CrearPreferenciaPago ===");
                Console.WriteLine($"📥 GrupoId recibido: {request?.GrupoId}");
                Console.WriteLine($"📥 UsuarioId recibido: {request?.UsuarioId}");

                // Validación básica
                if (request == null)
                {
                    Console.WriteLine($"❌ Request es null");
                    return BadRequest(new { error = "Request no puede ser null" });
                }

                if (request.GrupoId <= 0)
                {
                    Console.WriteLine($"❌ GrupoId inválido: {request.GrupoId}");
                    return BadRequest(new { error = $"GrupoId debe ser mayor a 0, recibido: {request.GrupoId}" });
                }

                if (request.UsuarioId <= 0)
                {
                    Console.WriteLine($"❌ UsuarioId inválido: {request.UsuarioId}");
                    return BadRequest(new { error = $"UsuarioId debe ser mayor a 0, recibido: {request.UsuarioId}" });
                }

                Console.WriteLine($"✅ Validación básica pasada");

                // 1. Validar que el usuario pertenece al grupo
                Console.WriteLine($"🔍 Validando participante...");
                var participante = await ValidarParticipante(request.GrupoId, request.UsuarioId);

                if (participante == null)
                {
                    Console.WriteLine($"❌ Participante no encontrado");
                    Console.WriteLine($"🔍 Verificando si el grupo {request.GrupoId} existe...");

                    // Debug: verificar si el grupo existe
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string checkGrupo = "SELECT COUNT(*) FROM GruposPago WHERE Id = @GrupoId";
                        using (SqlCommand cmd = new SqlCommand(checkGrupo, conn))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            int grupoCount = (int)cmd.ExecuteScalar();
                            Console.WriteLine($"🔍 Grupos encontrados con ID {request.GrupoId}: {grupoCount}");
                        }

                        string checkUsuario = "SELECT COUNT(*) FROM Usuarios WHERE Id = @UsuarioId";
                        using (SqlCommand cmd = new SqlCommand(checkUsuario, conn))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            int usuarioCount = (int)cmd.ExecuteScalar();
                            Console.WriteLine($"🔍 Usuarios encontrados con ID {request.UsuarioId}: {usuarioCount}");
                        }

                        string checkParticipante = "SELECT COUNT(*) FROM ParticipantesGrupo WHERE GrupoId = @GrupoId AND UsuarioId = @UsuarioId";
                        using (SqlCommand cmd = new SqlCommand(checkParticipante, conn))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            int participanteCount = (int)cmd.ExecuteScalar();
                            Console.WriteLine($"🔍 Participantes encontrados: {participanteCount}");
                        }
                    }

                    return BadRequest(new
                    {
                        error = "Usuario no encontrado en este grupo",
                        grupoId = request.GrupoId,
                        usuarioId = request.UsuarioId,
                        debug = "Verifica que el usuario esté en ParticipantesGrupo"
                    });
                }

                Console.WriteLine($"✅ Participante encontrado: {participante.NombreUsuario}");

                if (participante.YaPago)
                {
                    Console.WriteLine($"❌ Usuario ya pagó");
                    return BadRequest(new { error = "Este usuario ya ha pagado" });
                }

                Console.WriteLine($"✅ Usuario no ha pagado, procediendo...");

                // 2. Crear respuesta simulada de MercadoPago
                var preferenceId = $"TUKI-{request.GrupoId}-{request.UsuarioId}-{DateTime.Now.Ticks}";
                var externalReference = $"{request.GrupoId}_{request.UsuarioId}_{DateTime.Now.Ticks}";

                var response = new
                {
                    PreferenceId = preferenceId,
                    InitPoint = $"https://www.mercadopago.com.pe/checkout/v1/redirect?pref_id={preferenceId}",
                    SandboxInitPoint = $"https://sandbox.mercadopago.com.pe/checkout/v1/redirect?pref_id={preferenceId}",
                    Monto = participante.MontoIndividual,
                    Descripcion = $"Pago {participante.NombreGrupo} - {participante.NombreUsuario}",
                    ExternalReference = externalReference,
                    Status = "created"
                };

                Console.WriteLine($"✅ === ÉXITO CrearPreferenciaPago ===");
                Console.WriteLine($"✅ PreferenceId: {preferenceId}");
                Console.WriteLine($"✅ Monto: {participante.MontoIndividual}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ === ERROR CrearPreferenciaPago ===");
                Console.WriteLine($"❌ Message: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                Console.WriteLine($"❌ InnerException: {ex.InnerException?.Message}");

                return BadRequest(new
                {
                    error = ex.Message,
                    type = ex.GetType().Name,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // ================================
        // ENDPOINT PARA SIMULAR PAGO (SOLO PRUEBAS)
        // ================================
        [HttpPost("simular-pago-exitoso")]
        public async Task<IActionResult> SimularPagoExitoso([FromBody] SimularPagoRequest request)
        {
            try
            {
                Console.WriteLine($"🧪 Simulando pago exitoso: Grupo {request.GrupoId}, Usuario {request.UsuarioId}, Monto {request.Monto}");

                // Validar participante
                var participante = await ValidarParticipante(request.GrupoId, request.UsuarioId);
                if (participante == null)
                {
                    return BadRequest(new { error = "Usuario no encontrado en este grupo" });
                }

                // Simular pago exitoso
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // 1. Registrar el pago
                        string insertPago = @"
                            INSERT INTO PagosGrupo (GrupoId, UsuarioId, MontoPagado, FechaPago)
                            VALUES (@GrupoId, @UsuarioId, @MontoPagado, GETDATE())";

                        using (SqlCommand cmd = new SqlCommand(insertPago, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            cmd.Parameters.AddWithValue("@MontoPagado", request.Monto);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. Marcar como pagado
                        string updateParticipante = @"
                            UPDATE ParticipantesGrupo 
                            SET YaPago = 1 
                            WHERE GrupoId = @GrupoId AND UsuarioId = @UsuarioId";

                        using (SqlCommand cmd = new SqlCommand(updateParticipante, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        // 3. Notificar via SignalR
                        await _hubContext.Clients.Group($"grupo_{request.GrupoId}")
                            .SendAsync("PagoRealizado", new
                            {
                                GrupoId = request.GrupoId,
                                UsuarioId = request.UsuarioId,
                                Monto = request.Monto,
                                Usuario = new
                                {
                                    Nombre = participante.NombreUsuario,
                                    MontoIndividual = request.Monto
                                },
                                EsSimulacion = true,
                                Timestamp = DateTime.Now
                            });

                        Console.WriteLine($"✅ Pago simulado procesado exitosamente");

                        return Ok(new
                        {
                            message = "Pago simulado exitosamente",
                            grupoId = request.GrupoId,
                            usuarioId = request.UsuarioId,
                            monto = request.Monto
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error simulando pago: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        // ================================
        // MÉTODO VALIDAR PARTICIPANTE CORREGIDO
        // ================================
        private async Task<ParticipanteInfo?> ValidarParticipante(int grupoId, int usuarioId)
        {
            try
            {
                Console.WriteLine($"🔍 === VALIDANDO PARTICIPANTE ===");
                Console.WriteLine($"🔍 GrupoId: {grupoId}, UsuarioId: {usuarioId}");
                Console.WriteLine($"🔍 ConnectionString: {connectionString.Substring(0, 50)}...");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    Console.WriteLine($"✅ Conexión a BD abierta");

                    string query = @"
                SELECT 
                    pg.YaPago,
                    pg.MontoIndividual,
                    u.Nombre as NombreUsuario,
                    g.NombreGrupo
                FROM ParticipantesGrupo pg
                INNER JOIN Usuarios u ON u.Id = pg.UsuarioId
                INNER JOIN GruposPago g ON g.Id = pg.GrupoId
                WHERE pg.GrupoId = @GrupoId AND pg.UsuarioId = @UsuarioId";

                    Console.WriteLine($"🔍 Query: {query}");

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

                        Console.WriteLine($"🔍 Ejecutando query...");

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (reader.Read())
                            {
                                var participante = new ParticipanteInfo
                                {
                                    YaPago = (bool)reader["YaPago"],
                                    MontoIndividual = (decimal)reader["MontoIndividual"],
                                    NombreUsuario = reader["NombreUsuario"]?.ToString() ?? "",
                                    NombreGrupo = reader["NombreGrupo"]?.ToString() ?? ""
                                };

                                Console.WriteLine($"✅ Participante encontrado:");
                                Console.WriteLine($"✅ NombreUsuario: {participante.NombreUsuario}");
                                Console.WriteLine($"✅ NombreGrupo: {participante.NombreGrupo}");
                                Console.WriteLine($"✅ MontoIndividual: {participante.MontoIndividual}");
                                Console.WriteLine($"✅ YaPago: {participante.YaPago}");

                                return participante;
                            }
                            else
                            {
                                Console.WriteLine($"❌ No se encontró participante con GrupoId={grupoId}, UsuarioId={usuarioId}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en ValidarParticipante: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
            }
            return null;
        }

        // ================================
        // ENDPOINTS DE PRUEBA ADICIONALES
        // ================================
        [HttpGet("test")]
        public IActionResult TestEndpoint()
        {
            return Ok(new
            {
                message = "PagoController funcionando correctamente",
                timestamp = DateTime.Now,
                connectionString = connectionString.Contains("TukiDB") ? "BD Configurada ✅" : "BD NO configurada ❌"
            });
        }

        [HttpPost("test-request")]
        public IActionResult TestRequest([FromBody] CrearPagoRequest request)
        {
            return Ok(new
            {
                message = "Request recibida correctamente",
                grupoId = request.GrupoId,
                usuarioId = request.UsuarioId,
                timestamp = DateTime.Now
            });
        }

        // ================================
        // WEBHOOKS Y OTROS MÉTODOS (SIN CAMBIOS)
        // ================================
        [HttpPost("webhook")]
        public async Task<IActionResult> WebhookMercadoPago([FromBody] object notification)
        {
            try
            {
                Console.WriteLine($"📥 Webhook recibido: {JsonConvert.SerializeObject(notification)}");
                return Ok(new { message = "Webhook recibido correctamente" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en webhook: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        // Métodos auxiliares existentes (sin cambios)...
        private async Task RegistrarPago(SqlConnection conn, SqlTransaction transaction, int grupoId, int usuarioId, dynamic paymentDetails)
        {
            string insertPago = @"
                INSERT INTO PagosGrupo (GrupoId, UsuarioId, MontoPagado, FechaPago)
                VALUES (@GrupoId, @UsuarioId, @MontoPagado, GETDATE())";

            using (SqlCommand cmd = new SqlCommand(insertPago, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                cmd.Parameters.AddWithValue("@MontoPagado", (decimal)paymentDetails.TransactionAmount);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task MarcarComoPagado(SqlConnection conn, SqlTransaction transaction, int grupoId, int usuarioId)
        {
            string updateParticipante = @"
                UPDATE ParticipantesGrupo 
                SET YaPago = 1 
                WHERE GrupoId = @GrupoId AND UsuarioId = @UsuarioId";

            using (SqlCommand cmd = new SqlCommand(updateParticipante, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task<dynamic> CrearPreferenciaMercadoPago(object preference)
        {
            return new
            {
                Id = $"preference_{Guid.NewGuid()}",
                InitPoint = "https://www.mercadopago.com.pe/checkout/v1/redirect?pref_id=xxx",
                SandboxInitPoint = "https://sandbox.mercadopago.com.pe/checkout/v1/redirect?pref_id=xxx"
            };
        }

        private async Task<dynamic> ObtenerDetallePago(string paymentId)
        {
            return new
            {
                Status = "approved",
                TransactionAmount = 30.00m,
                ExternalReference = "1_2_123456789"
            };
        }

        private async Task<GrupoCompleto> ObtenerDatosGrupo(SqlConnection conn, SqlTransaction transaction, int grupoId)
        {
            return new GrupoCompleto();
        }

        [HttpGet("debug/grupo/{grupoId}/usuario/{usuarioId}")]
        public async Task<IActionResult> DebugParticipante(int grupoId, int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Verificar grupo
                    string checkGrupo = "SELECT Id, NombreGrupo, TotalMonto FROM GruposPago WHERE Id = @GrupoId";
                    object grupo = null;
                    using (SqlCommand cmd = new SqlCommand(checkGrupo, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                grupo = new
                                {
                                    Id = reader["Id"],
                                    NombreGrupo = reader["NombreGrupo"],
                                    TotalMonto = reader["TotalMonto"]
                                };
                            }
                        }
                    }

                    // Verificar usuario
                    string checkUsuario = "SELECT Id, Nombre, Telefono FROM Usuarios WHERE Id = @UsuarioId";
                    object usuario = null;
                    using (SqlCommand cmd = new SqlCommand(checkUsuario, conn))
                    {
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                usuario = new
                                {
                                    Id = reader["Id"],
                                    Nombre = reader["Nombre"],
                                    Telefono = reader["Telefono"]
                                };
                            }
                        }
                    }

                    // Verificar participante
                    string checkParticipante = @"
                SELECT pg.GrupoId, pg.UsuarioId, pg.MontoIndividual, pg.YaPago
                FROM ParticipantesGrupo pg
                WHERE pg.GrupoId = @GrupoId AND pg.UsuarioId = @UsuarioId";
                    object participante = null;
                    using (SqlCommand cmd = new SqlCommand(checkParticipante, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                participante = new
                                {
                                    GrupoId = reader["GrupoId"],
                                    UsuarioId = reader["UsuarioId"],
                                    MontoIndividual = reader["MontoIndividual"],
                                    YaPago = reader["YaPago"]
                                };
                            }
                        }
                    }

                    return Ok(new
                    {
                        grupoId = grupoId,
                        usuarioId = usuarioId,
                        grupo = grupo,
                        usuario = usuario,
                        participante = participante,
                        connectionString = connectionString.Substring(0, 50) + "..."
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}