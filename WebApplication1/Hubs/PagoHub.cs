// ================================
// PagoHub.cs - SignalR Hub para Tiempo Real
// ================================

using Microsoft.AspNetCore.SignalR;

namespace WebApplication1.Controllers
{
    public class PagoHub : Hub
    {
        private readonly ILogger<PagoHub> _logger;

        public PagoHub(ILogger<PagoHub> logger)
        {
            _logger = logger;
        }

        // ================================
        // MÉTODOS PARA UNIRSE A GRUPOS
        // ================================

        /// <summary>
        /// El cliente se une a un grupo específico para recibir notificaciones
        /// </summary>
        public async Task JoinGrupo(string grupoId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"grupo_{grupoId}");
                _logger.LogInformation($"✅ Cliente {Context.ConnectionId} se unió al grupo {grupoId}");

                // Notificar al cliente que se unió exitosamente
                await Clients.Caller.SendAsync("JoinedGrupo", new
                {
                    GrupoId = grupoId,
                    Message = $"Te uniste al grupo {grupoId}",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error al unir cliente al grupo {grupoId}: {ex.Message}");
                await Clients.Caller.SendAsync("Error", new
                {
                    Message = "Error al unirse al grupo",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// El cliente sale de un grupo específico
        /// </summary>
        public async Task LeaveGrupo(string grupoId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"grupo_{grupoId}");
                _logger.LogInformation($"👋 Cliente {Context.ConnectionId} salió del grupo {grupoId}");

                await Clients.Caller.SendAsync("LeftGrupo", new
                {
                    GrupoId = grupoId,
                    Message = $"Saliste del grupo {grupoId}",
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error al remover cliente del grupo {grupoId}: {ex.Message}");
            }
        }

        // ================================
        // MÉTODOS DE CONEXIÓN/DESCONEXIÓN
        // ================================

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

            _logger.LogInformation($"🔌 Cliente conectado: {Context.ConnectionId} desde {userAgent}");

            // Enviar mensaje de bienvenida
            await Clients.Caller.SendAsync("Connected", new
            {
                ConnectionId = Context.ConnectionId,
                Message = "Conectado al hub de pagos Tuki",
                Timestamp = DateTime.Now
            });

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogError($"❌ Cliente {Context.ConnectionId} desconectado con error: {exception.Message}");
            }
            else
            {
                _logger.LogInformation($"👋 Cliente {Context.ConnectionId} desconectado normalmente");
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ================================
        // MÉTODOS PARA TESTING
        // ================================

        /// <summary>
        /// Método para probar la conexión desde el cliente
        /// </summary>
        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", new
            {
                Message = "Conexión activa",
                Timestamp = DateTime.Now,
                ConnectionId = Context.ConnectionId
            });
        }

        /// <summary>
        /// Simular un pago para testing (solo desarrollo)
        /// </summary>
        public async Task SimularPago(string grupoId, string usuarioId, decimal monto)
        {
            try
            {
                _logger.LogInformation($"🧪 Simulando pago: Grupo {grupoId}, Usuario {usuarioId}, Monto {monto}");

                // Enviar notificación simulada al grupo
                await Clients.Group($"grupo_{grupoId}").SendAsync("PagoRealizado", new
                {
                    GrupoId = grupoId,
                    UsuarioId = usuarioId,
                    Monto = monto,
                    Usuario = new
                    {
                        Nombre = "Usuario Test",
                        MontoIndividual = monto
                    },
                    EsSimulacion = true,
                    Timestamp = DateTime.Now
                });

                await Clients.Caller.SendAsync("SimulacionCompleta", new
                {
                    Message = "Pago simulado enviado correctamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error en simulación: {ex.Message}");
                await Clients.Caller.SendAsync("Error", new
                {
                    Message = "Error en simulación",
                    Details = ex.Message
                });
            }
        }

        // ================================
        // MÉTODOS AUXILIARES PARA NOTIFICACIONES
        // ================================

        /// <summary>
        /// Notificar a todos los miembros de un grupo
        /// </summary>
        public async Task NotificarGrupo(string grupoId, string evento, object data)
        {
            try
            {
                await Clients.Group($"grupo_{grupoId}").SendAsync(evento, data);
                _logger.LogInformation($"📢 Notificación enviada al grupo {grupoId}: {evento}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error notificando grupo {grupoId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtener información de conexiones activas (solo para admin/debug)
        /// </summary>
        public async Task GetEstadisticasConexion()
        {
            try
            {
                // En una implementación real, mantendrías un registro de conexiones
                await Clients.Caller.SendAsync("EstadisticasConexion", new
                {
                    ConnectionId = Context.ConnectionId,
                    ConectadoEn = DateTime.Now,
                    TotalConexiones = "N/A", // Implementar contador si es necesario
                    Message = "Estadísticas básicas de conexión"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error obteniendo estadísticas: {ex.Message}");
            }
        }
    }

    // ================================
    // EXTENSIONES PARA EL HUB (OPCIONAL)
    // ================================
    public static class PagoHubExtensions
    {
        /// <summary>
        /// Método estático para enviar notificaciones desde otros controladores
        /// </summary>
        public static async Task NotificarPagoRealizado(
            IHubContext<PagoHub> hubContext,
            int grupoId,
            int usuarioId,
            object datosCompletos)
        {
            await hubContext.Clients.Group($"grupo_{grupoId}")
                .SendAsync("PagoRealizado", datosCompletos);
        }

        /// <summary>
        /// Notificar cambios en el grupo
        /// </summary>
        public static async Task NotificarCambioGrupo(
            IHubContext<PagoHub> hubContext,
            int grupoId,
            string tipoEvento,
            object datos)
        {
            await hubContext.Clients.Group($"grupo_{grupoId}")
                .SendAsync("CambioGrupo", new
                {
                    Evento = tipoEvento,
                    Datos = datos,
                    Timestamp = DateTime.Now
                });
        }
    }
}