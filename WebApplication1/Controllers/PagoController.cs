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
        private readonly ILogger<PagoController> _logger;

        public PagoController(IConfiguration config, IHubContext<PagoHub> hubContext, ILogger<PagoController> logger)
        {
            _config = config;
            _hubContext = hubContext;
            _logger = logger;
        }

        // ✅ Método para obtener connection string de forma segura
        private string GetConnectionString()
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found");
            }
            return connectionString;
        }

        // ================================
        // ENDPOINT CORREGIDO
        // ================================
        [HttpPost("crear-preferencia")]
        public async Task<IActionResult> CrearPreferenciaPago([FromBody] CrearPagoRequest request)
        {
            try
            {
                _logger.LogInformation($"📥 === INICIO CrearPreferenciaPago ===");
                _logger.LogInformation($"📥 GrupoId recibido: {request?.GrupoId}");
                _logger.LogInformation($"📥 UsuarioId recibido: {request?.UsuarioId}");

                // Validación básica
                if (request == null)
                {
                    _logger.LogWarning($"❌ Request es null");
                    return BadRequest(new { error = "Request no puede ser null" });
                }

                if (request.GrupoId <= 0)
                {
                    _logger.LogWarning($"❌ GrupoId inválido: {request.GrupoId}");
                    return BadRequest(new { error = $"GrupoId debe ser mayor a 0, recibido: {request.GrupoId}" });
                }

                if (request.UsuarioId <= 0)
                {
                    _logger.LogWarning($"❌ UsuarioId inválido: {request.UsuarioId}");
                    return BadRequest(new { error = $"UsuarioId debe ser mayor a 0, recibido: {request.UsuarioId}" });
                }

                _logger.LogInformation($"✅ Validación básica pasada");

                // 1. Validar que el usuario pertenece al grupo
                _logger.LogInformation($"🔍 Validando participante...");
                var participante = await ValidarParticipante(request.GrupoId, request.UsuarioId);

                if (participante == null)
                {
                    _logger.LogWarning($"❌ Participante no encontrado");
                    _logger.LogInformation($"🔍 Verificando si el grupo {request.GrupoId} existe...");

                    // Debug: verificar si el grupo existe
                    using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                    {
                        conn.Open();
                        string checkGrupo = "SELECT COUNT(*) FROM GruposPago WHERE Id = @GrupoId";
                        using (SqlCommand cmd = new SqlCommand(checkGrupo, conn))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            int grupoCount = (int)cmd.ExecuteScalar();
                            _logger.LogInformation($"🔍 Grupos encontrados con ID {request.GrupoId}: {grupoCount}");
                        }

                        string checkUsuario = "SELECT COUNT(*) FROM Usuarios WHERE Id = @UsuarioId";
                        using (SqlCommand cmd = new SqlCommand(checkUsuario, conn))
                        {
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            int usuarioCount = (int)cmd.ExecuteScalar();
                            _logger.LogInformation($"🔍 Usuarios encontrados con ID {request.UsuarioId}: {usuarioCount}");
                        }

                        string checkParticipante = "SELECT COUNT(*) FROM ParticipantesGrupo WHERE GrupoId = @GrupoId AND UsuarioId = @UsuarioId";
                        using (SqlCommand cmd = new SqlCommand(checkParticipante, conn))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            int participanteCount = (int)cmd.ExecuteScalar();
                            _logger.LogInformation($"🔍 Participantes encontrados: {participanteCount}");
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

                _logger.LogInformation($"✅ Participante encontrado: {participante.NombreUsuario}");

                if (participante.YaPago)
                {
                    _logger.LogWarning($"❌ Usuario ya pagó");
                    return BadRequest(new { error = "Este usuario ya ha pagado" });
                }

                _logger.LogInformation($"✅ Usuario no ha pagado, procediendo...");

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

                _logger.LogInformation($"✅ === ÉXITO CrearPreferenciaPago ===");
                _logger.LogInformation($"✅ PreferenceId: {preferenceId}");
                _logger.LogInformation($"✅ Monto: {participante.MontoIndividual}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ === ERROR CrearPreferenciaPago ===");
                _logger.LogError($"❌ Message: {ex.Message}");
                _logger.LogError($"❌ StackTrace: {ex.StackTrace}");
                _logger.LogError($"❌ InnerException: {ex.InnerException?.Message}");

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
                _logger.LogInformation($"🧪 Simulando pago exitoso: Grupo {request.GrupoId}, Usuario {request.UsuarioId}, Monto {request.Monto}");

                // Validar participante
                var participante = await ValidarParticipante(request.GrupoId, request.UsuarioId);
                if (participante == null)
                {
                    return BadRequest(new { error = "Usuario no encontrado en este grupo" });
                }

                // Simular pago exitoso
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
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

                        _logger.LogInformation($"✅ Pago simulado procesado exitosamente");

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
                _logger.LogError($"❌ Error simulando pago: {ex.Message}");
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
                _logger.LogInformation($"🔍 === VALIDANDO PARTICIPANTE ===");
                _logger.LogInformation($"🔍 GrupoId: {grupoId}, UsuarioId: {usuarioId}");

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    _logger.LogInformation($"✅ Conexión a BD abierta");

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

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

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

                                _logger.LogInformation($"✅ Participante encontrado:");
                                _logger.LogInformation($"✅ NombreUsuario: {participante.NombreUsuario}");
                                _logger.LogInformation($"✅ NombreGrupo: {participante.NombreGrupo}");
                                _logger.LogInformation($"✅ MontoIndividual: {participante.MontoIndividual}");
                                _logger.LogInformation($"✅ YaPago: {participante.YaPago}");

                                return participante;
                            }
                            else
                            {
                                _logger.LogWarning($"❌ No se encontró participante con GrupoId={grupoId}, UsuarioId={usuarioId}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error en ValidarParticipante: {ex.Message}");
                _logger.LogError($"❌ StackTrace: {ex.StackTrace}");
            }
            return null;
        }

        // ================================
        // ENDPOINTS DE PRUEBA ADICIONALES
        // ================================
        [HttpGet("test")]
        public IActionResult TestEndpoint()
        {
            try
            {
                var connectionString = GetConnectionString();
                return Ok(new
                {
                    message = "PagoController funcionando correctamente",
                    timestamp = DateTime.Now,
                    connectionString = connectionString.Contains("TukiDB") ? "BD Configurada ✅" : "BD NO configurada ❌",
                    isAzureSQL = connectionString.Contains("database.windows.net") ? "Azure SQL ✅" : "Local DB ❌"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
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
        // WEBHOOKS Y OTROS MÉTODOS
        // ================================
        [HttpPost("webhook")]
        public async Task<IActionResult> WebhookMercadoPago([FromBody] object notification)
        {
            try
            {
                _logger.LogInformation($"📥 Webhook recibido: {JsonConvert.SerializeObject(notification)}");
                return Ok(new { message = "Webhook recibido correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error en webhook: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        // Métodos auxiliares existentes
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

        [HttpPost("marcar-pagado-con-comprobante")]
        public async Task<IActionResult> MarcarPagadoConComprobante([FromBody] MarcarPagoConComprobanteRequest request)
        {
            try
            {
                _logger.LogInformation($"📸 Marcando pago con comprobante: Grupo {request.GrupoId}, Usuario {request.UsuarioId}");

                // Validación básica
                if (request == null)
                {
                    return BadRequest(new { error = "Request no puede ser null" });
                }

                if (request.GrupoId <= 0 || request.UsuarioId <= 0)
                {
                    return BadRequest(new { error = "GrupoId y UsuarioId deben ser mayor a 0" });
                }

                if (string.IsNullOrEmpty(request.Comprobante))
                {
                    return BadRequest(new { error = "Comprobante es requerido" });
                }

                // Validar que el usuario pertenece al grupo
                var participante = await ValidarParticipante(request.GrupoId, request.UsuarioId);
                if (participante == null)
                {
                    return BadRequest(new { error = "Usuario no encontrado en este grupo" });
                }

                if (participante.YaPago)
                {
                    return BadRequest(new { error = "Este usuario ya ha pagado" });
                }

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // 1. Actualizar ParticipantesGrupo con comprobante
                        string updateParticipante = @"
                    UPDATE ParticipantesGrupo 
                    SET YaPago = 1,
                        Comprobante = @Comprobante,
                        FechaPago = @FechaPago,
                        MetodoPagoUsado = @MetodoPago
                    WHERE GrupoId = @GrupoId AND UsuarioId = @UsuarioId";

                        using (SqlCommand cmd = new SqlCommand(updateParticipante, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            cmd.Parameters.AddWithValue("@Comprobante", request.Comprobante);
                            cmd.Parameters.AddWithValue("@FechaPago", request.FechaPago ?? DateTime.Now);
                            cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago ?? "yape");

                            int rowsAffected = await cmd.ExecuteNonQueryAsync();
                            if (rowsAffected == 0)
                            {
                                throw new Exception("No se pudo actualizar el participante");
                            }
                        }

                        // 2. Registrar en PagosGrupo (historial)
                        string insertPago = @"
                    INSERT INTO PagosGrupo (GrupoId, UsuarioId, MontoPagado, FechaPago)
                    VALUES (@GrupoId, @UsuarioId, @MontoPagado, @FechaPago)";

                        using (SqlCommand cmd = new SqlCommand(insertPago, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            cmd.Parameters.AddWithValue("@MontoPagado", request.Monto);
                            cmd.Parameters.AddWithValue("@FechaPago", request.FechaPago ?? DateTime.Now);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        // 3. Notificar al creador del grupo via SignalR
                        await NotificarPagoConComprobante(request.GrupoId, request.UsuarioId, participante, request.Comprobante);

                        _logger.LogInformation($"✅ Pago con comprobante registrado exitosamente");

                        return Ok(new
                        {
                            message = "Pago registrado con comprobante exitosamente",
                            grupoId = request.GrupoId,
                            usuarioId = request.UsuarioId,
                            monto = request.Monto,
                            fechaPago = request.FechaPago ?? DateTime.Now,
                            tieneComprobante = true
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
                _logger.LogError($"❌ Error marcando pago con comprobante: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        private async Task NotificarPagoConComprobante(int grupoId, int usuarioId, ParticipanteInfo participante, string comprobante)
        {
            try
            {
                await _hubContext.Clients.Group($"grupo_{grupoId}")
                    .SendAsync("PagoRealizadoConComprobante", new
                    {
                        GrupoId = grupoId,
                        UsuarioId = usuarioId,
                        Monto = participante.MontoIndividual,
                        Usuario = new
                        {
                            Nombre = participante.NombreUsuario,
                            MontoIndividual = participante.MontoIndividual
                        },
                        TieneComprobante = true,
                        ComprobantePreview = comprobante?.Substring(0, Math.Min(100, comprobante.Length)) + "...",
                        Timestamp = DateTime.Now
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error enviando notificación con comprobante: {ex.Message}");
            }
        }

        private async Task NotificarPagoSinComprobante(int grupoId, int usuarioId, ParticipanteInfo participante)
        {
            try
            {
                await _hubContext.Clients.Group($"grupo_{grupoId}")
                    .SendAsync("PagoRealizadoSinComprobante", new
                    {
                        GrupoId = grupoId,
                        UsuarioId = usuarioId,
                        Monto = participante.MontoIndividual,
                        Usuario = new
                        {
                            Nombre = participante.NombreUsuario,
                            MontoIndividual = participante.MontoIndividual
                        },
                        TieneComprobante = false,
                        Timestamp = DateTime.Now
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error enviando notificación sin comprobante: {ex.Message}");
            }
        }

        [HttpPost("marcar-pagado-sin-comprobante")]
        public async Task<IActionResult> MarcarPagadoSinComprobante([FromBody] MarcarPagoSinComprobanteRequest request)
        {
            try
            {
                _logger.LogInformation($"✅ Marcando pago sin comprobante: Grupo {request.GrupoId}, Usuario {request.UsuarioId}");

                // Validaciones básicas
                if (request == null || request.GrupoId <= 0 || request.UsuarioId <= 0)
                {
                    return BadRequest(new { error = "Datos inválidos" });
                }

                // Validar que el usuario pertenece al grupo
                var participante = await ValidarParticipante(request.GrupoId, request.UsuarioId);
                if (participante == null)
                {
                    return BadRequest(new { error = "Usuario no encontrado en este grupo" });
                }

                if (participante.YaPago)
                {
                    return BadRequest(new { error = "Este usuario ya ha pagado" });
                }

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // 1. Actualizar ParticipantesGrupo sin comprobante
                        string updateParticipante = @"
                    UPDATE ParticipantesGrupo 
                    SET YaPago = 1,
                        FechaPago = @FechaPago,
                        MetodoPagoUsado = @MetodoPago
                    WHERE GrupoId = @GrupoId AND UsuarioId = @UsuarioId";

                        using (SqlCommand cmd = new SqlCommand(updateParticipante, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            cmd.Parameters.AddWithValue("@FechaPago", DateTime.Now);
                            cmd.Parameters.AddWithValue("@MetodoPago", request.MetodoPago ?? "yape");

                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. Registrar en PagosGrupo (historial)
                        string insertPago = @"
                    INSERT INTO PagosGrupo (GrupoId, UsuarioId, MontoPagado, FechaPago)
                    VALUES (@GrupoId, @UsuarioId, @MontoPagado, @FechaPago)";

                        using (SqlCommand cmd = new SqlCommand(insertPago, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                            cmd.Parameters.AddWithValue("@UsuarioId", request.UsuarioId);
                            cmd.Parameters.AddWithValue("@MontoPagado", participante.MontoIndividual);
                            cmd.Parameters.AddWithValue("@FechaPago", DateTime.Now);

                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        // 3. Notificar sin comprobante
                        await NotificarPagoSinComprobante(request.GrupoId, request.UsuarioId, participante);

                        _logger.LogInformation($"✅ Pago sin comprobante registrado exitosamente");

                        return Ok(new
                        {
                            message = "Pago registrado exitosamente",
                            grupoId = request.GrupoId,
                            usuarioId = request.UsuarioId,
                            monto = participante.MontoIndividual,
                            fechaPago = DateTime.Now,
                            tieneComprobante = false
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
                _logger.LogError($"❌ Error marcando pago sin comprobante: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("comprobante/{grupoId}/{usuarioId}")]
        public async Task<IActionResult> ObtenerComprobante(int grupoId, int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    pg.Comprobante,
                    pg.FechaPago,
                    pg.MetodoPagoUsado,
                    pg.MontoIndividual,
                    u.Nombre as NombreUsuario
                FROM ParticipantesGrupo pg
                INNER JOIN Usuarios u ON u.Id = pg.UsuarioId
                WHERE pg.GrupoId = @GrupoId AND pg.UsuarioId = @UsuarioId AND pg.YaPago = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (reader.Read())
                            {
                                var comprobante = new
                                {
                                    grupoId = grupoId,
                                    usuarioId = usuarioId,
                                    nombreUsuario = reader["NombreUsuario"]?.ToString(),
                                    comprobante = reader["Comprobante"]?.ToString(),
                                    fechaPago = reader["FechaPago"] as DateTime?,
                                    metodoPagoUsado = reader["MetodoPagoUsado"]?.ToString(),
                                    montoIndividual = reader["MontoIndividual"] as decimal?,
                                    tieneComprobante = !string.IsNullOrEmpty(reader["Comprobante"]?.ToString())
                                };

                                return Ok(comprobante);
                            }
                            else
                            {
                                return NotFound(new { error = "Comprobante no encontrado o usuario no ha pagado" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error obteniendo comprobante: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("comprobantes-grupo/{grupoId}")]
        public async Task<IActionResult> ObtenerComprobantesGrupo(int grupoId)
        {
            try
            {
                var comprobantes = new List<object>();

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    pg.UsuarioId,
                    pg.Comprobante,
                    pg.FechaPago,
                    pg.MetodoPagoUsado,
                    pg.MontoIndividual,
                    u.Nombre as NombreUsuario,
                    u.Telefono
                FROM ParticipantesGrupo pg
                INNER JOIN Usuarios u ON u.Id = pg.UsuarioId
                WHERE pg.GrupoId = @GrupoId AND pg.YaPago = 1
                ORDER BY pg.FechaPago DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (reader.Read())
                            {
                                comprobantes.Add(new
                                {
                                    usuarioId = reader["UsuarioId"],
                                    nombreUsuario = reader["NombreUsuario"]?.ToString(),
                                    telefono = reader["Telefono"]?.ToString(),
                                    comprobante = reader["Comprobante"]?.ToString(),
                                    fechaPago = reader["FechaPago"] as DateTime?,
                                    metodoPagoUsado = reader["MetodoPagoUsado"]?.ToString(),
                                    montoIndividual = reader["MontoIndividual"] as decimal?,
                                    tieneComprobante = !string.IsNullOrEmpty(reader["Comprobante"]?.ToString())
                                });
                            }
                        }
                    }
                }

                return Ok(new
                {
                    grupoId = grupoId,
                    totalComprobantes = comprobantes.Count,
                    comprobantes = comprobantes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error obteniendo comprobantes del grupo: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("debug/grupo/{grupoId}/usuario/{usuarioId}")]
        public async Task<IActionResult> DebugParticipante(int grupoId, int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
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
                        connectionInfo = "Using Azure SQL Database"
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }



        // Clases auxiliares
        public class CrearPagoRequest
        {
            public int GrupoId { get; set; }
            public int UsuarioId { get; set; }
        }

        public class SimularPagoRequest
        {
            public int GrupoId { get; set; }
            public int UsuarioId { get; set; }
            public decimal Monto { get; set; }
        }
        public class MarcarPagoConComprobanteRequest
        {
            public int GrupoId { get; set; }
            public int UsuarioId { get; set; }
            public string Comprobante { get; set; } = string.Empty; // Base64 de la imagen
            public decimal Monto { get; set; }
            public DateTime? FechaPago { get; set; }
            public string? MetodoPago { get; set; } = "yape";
        }

        public class MarcarPagoSinComprobanteRequest
        {
            public int GrupoId { get; set; }
            public int UsuarioId { get; set; }
            public string? MetodoPago { get; set; } = "yape";
        }

        // SignalR Hub
        public class PagoHub : Hub
        {
            public async Task JoinGroup(string groupName)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            }

            public async Task LeaveGroup(string groupName)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            }
        }
    }
}