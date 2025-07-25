using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication1.Models;
using static WebApplication1.Models.DTOs;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/grupo")]
    public class GrupoController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<GrupoController> _logger;

        public GrupoController(IConfiguration config, ILogger<GrupoController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpPost("crear")]
        public IActionResult CrearGrupo([FromBody] CrearGrupoRequest request)
        {
            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                conn.Open();
                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // 🆕 SQL actualizado con nuevos campos
                    string insertGrupo = @"
                        INSERT INTO GruposPago (NombreGrupo, CreadorId, TotalMonto, Categoria, FechaLimite, Descripcion)
                        VALUES (@Nombre, @CreadorId, @Monto, @Categoria, @FechaLimite, @Descripcion);
                        SELECT SCOPE_IDENTITY();";

                    SqlCommand cmdGrupo = new SqlCommand(insertGrupo, conn, transaction);
                    cmdGrupo.Parameters.AddWithValue("@Nombre", request.NombreGrupo);
                    cmdGrupo.Parameters.AddWithValue("@CreadorId", request.CreadorId);
                    cmdGrupo.Parameters.AddWithValue("@Monto", request.MontoTotal);

                    // 🆕 Nuevos parámetros
                    cmdGrupo.Parameters.AddWithValue("@Categoria", request.Categoria ?? "general");
                    cmdGrupo.Parameters.AddWithValue("@FechaLimite",
                        request.FechaLimite.HasValue ? (object)request.FechaLimite.Value : DBNull.Value);
                    cmdGrupo.Parameters.AddWithValue("@Descripcion",
                        string.IsNullOrEmpty(request.Descripcion) ? (object)DBNull.Value : request.Descripcion);

                    int grupoId = Convert.ToInt32(cmdGrupo.ExecuteScalar());

                    // 🆕 División manual o automática
                    if (request.DivisionManual && request.MontosIndividuales != null && request.MontosIndividuales.Count > 0)
                    {
                        // División manual - usar montos específicos
                        for (int i = 0; i < request.Participantes.Count; i++)
                        {
                            decimal montoIndividual = i < request.MontosIndividuales.Count
                                ? request.MontosIndividuales[i]
                                : 0;

                            string insertPart = @"
                                INSERT INTO ParticipantesGrupo (GrupoId, UsuarioId, MontoIndividual)
                                VALUES (@GrupoId, 
                                    (SELECT Id FROM Usuarios WHERE Telefono = @Telefono), 
                                    @Monto);";

                            SqlCommand cmdPart = new SqlCommand(insertPart, conn, transaction);
                            cmdPart.Parameters.AddWithValue("@GrupoId", grupoId);
                            cmdPart.Parameters.AddWithValue("@Telefono", request.Participantes[i]);
                            cmdPart.Parameters.AddWithValue("@Monto", montoIndividual);
                            cmdPart.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // División automática - igual para todos
                        decimal montoIndividual = request.MontoTotal / request.Participantes.Count;

                        foreach (var participante in request.Participantes)
                        {
                            string insertPart = @"
                                INSERT INTO ParticipantesGrupo (GrupoId, UsuarioId, MontoIndividual)
                                VALUES (@GrupoId, 
                                    (SELECT Id FROM Usuarios WHERE Telefono = @Telefono), 
                                    @Monto);";

                            SqlCommand cmdPart = new SqlCommand(insertPart, conn, transaction);
                            cmdPart.Parameters.AddWithValue("@GrupoId", grupoId);
                            cmdPart.Parameters.AddWithValue("@Telefono", participante);
                            cmdPart.Parameters.AddWithValue("@Monto", montoIndividual);
                            cmdPart.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();

                    // 🆕 Respuesta mejorada
                    return Ok(new
                    {
                        GrupoId = grupoId,
                        NombreGrupo = request.NombreGrupo,
                        Categoria = request.Categoria ?? "general",
                        FechaLimite = request.FechaLimite,
                        TotalParticipantes = request.Participantes.Count,
                        MontoTotal = request.MontoTotal,
                        Mensaje = "Grupo creado exitosamente"
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"❌ Error creando grupo: {ex.Message}");
                    return BadRequest(new
                    {
                        error = "Error al crear el grupo",
                        details = ex.Message
                    });
                }
            }
        }

        [HttpGet("detalle/{id}")]
        public IActionResult ObtenerDetalleGrupo(int id)
        {
            var response = new DTOs.GrupoDetalleResponse();
            var participantes = new List<DTOs.ParticipantePagoDTO>();

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                // 🆕 1. Obtener datos del grupo (con nuevos campos)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT Id, NombreGrupo, TotalMonto, Categoria, FechaLimite, Descripcion, FechaCreacion, CreadorId
                    FROM GruposPago
                    WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            response.Id = (int)reader["Id"];
                            response.NombreGrupo = reader["NombreGrupo"].ToString();
                            response.TotalMonto = (decimal)reader["TotalMonto"];
                            response.Categoria = reader["Categoria"]?.ToString() ?? "general";
                            response.FechaLimite = reader["FechaLimite"] as DateTime?;
                            response.Descripcion = reader["Descripcion"]?.ToString();
                            response.FechaCreacion = (DateTime)reader["FechaCreacion"];
                            response.CreadorId = (int)reader["CreadorId"];
                        }
                        else
                        {
                            return NotFound("Grupo no encontrado");
                        }
                    }
                }

                // 2. Obtener total pagado (sin cambios)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(MontoPagado), 0)
                    FROM PagosGrupo
                    WHERE GrupoId = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    response.TotalPagado = (decimal)cmd.ExecuteScalar();
                }

                // 3. Obtener participantes y su pago (sin cambios)
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT 
                        u.Id AS UsuarioId,
                        u.Nombre,
                        u.Telefono,
                        pg.MontoIndividual,
                        ISNULL(SUM(pg2.MontoPagado), 0) AS MontoPagado,
                        pg.YaPago
                    FROM ParticipantesGrupo pg
                    INNER JOIN Usuarios u ON u.Id = pg.UsuarioId
                    LEFT JOIN PagosGrupo pg2 ON pg2.UsuarioId = pg.UsuarioId AND pg2.GrupoId = pg.GrupoId
                    WHERE pg.GrupoId = @Id
                    GROUP BY u.Id, u.Nombre, u.Telefono, pg.MontoIndividual, pg.YaPago", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            participantes.Add(new DTOs.ParticipantePagoDTO
                            {
                                UsuarioId = (int)reader["UsuarioId"],
                                Nombre = reader["Nombre"].ToString(),
                                Telefono = reader["Telefono"].ToString(),
                                MontoIndividual = (decimal)reader["MontoIndividual"],
                                MontoPagado = (decimal)reader["MontoPagado"],
                                YaPago = (bool)reader["YaPago"]
                            });
                        }
                    }
                }
            }

            response.Participantes = participantes;

            // 🆕 Agregar información de urgencia
            response.DiasRestantes = response.FechaLimite.HasValue
                ? (int?)(response.FechaLimite.Value - DateTime.Now).TotalDays
                : null;
            response.EsUrgente = response.DiasRestantes.HasValue && response.DiasRestantes <= 3;
            response.EstaVencido = response.DiasRestantes.HasValue && response.DiasRestantes < 0;

            return Ok(response);
        }

        [HttpGet("mis-grupos/{usuarioId}")]
        public IActionResult ObtenerGruposPorUsuario(int usuarioId)
        {
            var grupos = new List<object>();

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                // 🆕 Query actualizada con nuevos campos
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT 
                        g.Id,
                        g.NombreGrupo,
                        g.TotalMonto,
                        g.FechaCreacion,
                        g.CreadorId,
                        g.Categoria,                    -- 🆕 Nuevo campo
                        g.FechaLimite,                  -- 🆕 Nuevo campo
                        g.Descripcion,                  -- 🆕 Nuevo campo
                        
                        -- Estadísticas del grupo
                        (SELECT COUNT(*) FROM ParticipantesGrupo p WHERE p.GrupoId = g.Id) AS TotalParticipantes,
                        (SELECT COUNT(*) FROM ParticipantesGrupo p WHERE p.GrupoId = g.Id AND p.YaPago = 1) AS ParticipantesPagaron,
                        
                        -- Información específica del usuario consultante
                        CASE 
                            WHEN g.CreadorId = @usuarioId THEN 'creador'
                            ELSE 'participante'
                        END as RolUsuario,
                        
                        -- Si es participante, obtener su monto
                        CASE 
                            WHEN pg.UsuarioId IS NOT NULL THEN pg.MontoIndividual
                            ELSE 0
                        END as MiMonto,
                        
                        -- Estado de pago del usuario
                        CASE 
                            WHEN pg.UsuarioId IS NOT NULL THEN pg.YaPago
                            ELSE CAST(0 as BIT)
                        END as YaPague
                        
                    FROM GruposPago g
                    LEFT JOIN ParticipantesGrupo pg ON g.Id = pg.GrupoId AND pg.UsuarioId = @usuarioId
                    WHERE 
                        g.CreadorId = @usuarioId          -- Grupos que creé
                        OR 
                        EXISTS (                          -- O grupos donde soy participante
                            SELECT 1 FROM ParticipantesGrupo pg2 
                            WHERE pg2.GrupoId = g.Id AND pg2.UsuarioId = @usuarioId
                        )
                    ORDER BY g.FechaCreacion DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@usuarioId", usuarioId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fechaLimite = reader["FechaLimite"] as DateTime?;
                            var diasRestantes = fechaLimite.HasValue
                                ? (int?)(fechaLimite.Value - DateTime.Now).TotalDays
                                : null;

                            var grupo = new
                            {
                                Id = (int)reader["Id"],
                                NombreGrupo = reader["NombreGrupo"].ToString(),
                                TotalMonto = (decimal)reader["TotalMonto"],
                                FechaCreacion = (DateTime)reader["FechaCreacion"],
                                CreadorId = (int)reader["CreadorId"],

                                // 🆕 Nuevos campos
                                Categoria = reader["Categoria"]?.ToString() ?? "general",
                                FechaLimite = fechaLimite,
                                Descripcion = reader["Descripcion"]?.ToString(),

                                // Estadísticas
                                TotalParticipantes = (int)reader["TotalParticipantes"],
                                ParticipantesPagaron = (int)reader["ParticipantesPagaron"],

                                // Información del usuario
                                RolUsuario = reader["RolUsuario"].ToString(),
                                EsCreador = reader["RolUsuario"].ToString() == "creador",
                                MiMonto = (decimal)reader["MiMonto"],
                                YaPague = (bool)reader["YaPague"],

                                // 🆕 Información de urgencia
                                DiasRestantes = diasRestantes,
                                EsUrgente = diasRestantes.HasValue && diasRestantes <= 3 && diasRestantes >= 0,
                                EstaVencido = diasRestantes.HasValue && diasRestantes < 0,
                                EstadoUrgencia = diasRestantes.HasValue
                                    ? (diasRestantes < 0 ? "vencido" : diasRestantes <= 1 ? "urgente" : diasRestantes <= 3 ? "proximo" : "normal")
                                    : "sin_fecha",

                                // Debug info
                                UsuarioConsultante = usuarioId
                            };

                            grupos.Add(grupo);
                        }
                    }
                }
            }

            Console.WriteLine($"✅ Usuario {usuarioId} tiene {grupos.Count} grupos");
            return Ok(grupos);
        }

        [HttpGet("debug-usuario/{usuarioId}")]
        public IActionResult DebugUsuario(int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // 1. Verificar usuario
                    var usuario = new { Id = 0, Nombre = "", Telefono = "" };
                    string queryUsuario = "SELECT Id, Nombre, Telefono FROM Usuarios WHERE Id = @Id";
                    using (SqlCommand cmd = new SqlCommand(queryUsuario, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", usuarioId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                usuario = new
                                {
                                    Id = (int)reader["Id"],
                                    Nombre = reader["Nombre"].ToString(),
                                    Telefono = reader["Telefono"].ToString()
                                };
                            }
                        }
                    }

                    // 2. Grupos creados por el usuario
                    var gruposCreados = new List<object>();
                    string queryCreados = @"
                SELECT Id, NombreGrupo, TotalMonto, FechaCreacion 
                FROM GruposPago 
                WHERE CreadorId = @usuarioId 
                ORDER BY FechaCreacion DESC";

                    using (SqlCommand cmd = new SqlCommand(queryCreados, conn))
                    {
                        cmd.Parameters.AddWithValue("@usuarioId", usuarioId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                gruposCreados.Add(new
                                {
                                    Id = (int)reader["Id"],
                                    NombreGrupo = reader["NombreGrupo"].ToString(),
                                    TotalMonto = (decimal)reader["TotalMonto"],
                                    FechaCreacion = (DateTime)reader["FechaCreacion"]
                                });
                            }
                        }
                    }

                    // 3. Grupos donde es participante
                    var gruposParticipante = new List<object>();
                    string queryParticipante = @"
                SELECT g.Id, g.NombreGrupo, g.CreadorId, g.TotalMonto, 
                       pg.MontoIndividual, pg.YaPago
                FROM GruposPago g
                INNER JOIN ParticipantesGrupo pg ON g.Id = pg.GrupoId
                WHERE pg.UsuarioId = @usuarioId
                ORDER BY g.FechaCreacion DESC";

                    using (SqlCommand cmd = new SqlCommand(queryParticipante, conn))
                    {
                        cmd.Parameters.AddWithValue("@usuarioId", usuarioId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                gruposParticipante.Add(new
                                {
                                    Id = (int)reader["Id"],
                                    NombreGrupo = reader["NombreGrupo"].ToString(),
                                    CreadorId = (int)reader["CreadorId"],
                                    TotalMonto = (decimal)reader["TotalMonto"],
                                    MontoIndividual = (decimal)reader["MontoIndividual"],
                                    YaPago = (bool)reader["YaPago"]
                                });
                            }
                        }
                    }

                    return Ok(new
                    {
                        usuario = usuario,
                        gruposCreados = gruposCreados,
                        gruposParticipante = gruposParticipante,
                        totalGrupos = gruposCreados.Count + gruposParticipante.Count,
                        resumen = new
                        {
                            comoCreador = gruposCreados.Count,
                            comoParticipante = gruposParticipante.Count
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("verificar-creador/{grupoId}/usuario/{usuarioId}")]
        public IActionResult VerificarCreador(int grupoId, int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    g.Id,
                    g.NombreGrupo,
                    g.CreadorId,
                    u.Nombre as NombreCreador,
                    CASE WHEN g.CreadorId = @usuarioId THEN 1 ELSE 0 END as EsCreador
                FROM GruposPago g
                INNER JOIN Usuarios u ON g.CreadorId = u.Id
                WHERE g.Id = @grupoId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@grupoId", grupoId);
                        cmd.Parameters.AddWithValue("@usuarioId", usuarioId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return Ok(new
                                {
                                    grupoId = grupoId,
                                    usuarioId = usuarioId,
                                    nombreGrupo = reader["NombreGrupo"].ToString(),
                                    creadorId = (int)reader["CreadorId"],
                                    nombreCreador = reader["NombreCreador"].ToString(),
                                    esCreador = (int)reader["EsCreador"] == 1,
                                    mensaje = (int)reader["EsCreador"] == 1
                                        ? "Usuario ES el creador del grupo"
                                        : "Usuario NO es el creador del grupo"
                                });
                            }
                            else
                            {
                                return NotFound(new { error = "Grupo no encontrado" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("mis-pagos-pendientes/{usuarioId}")]
        public IActionResult ObtenerPagosPendientes(int usuarioId)
        {
            var pagosPendientes = new List<object>();

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(@"
            SELECT 
                g.Id as GrupoId,
                g.NombreGrupo,
                g.TotalMonto as MontoTotalGrupo,
                pg.MontoIndividual as MiMonto,
                pg.YaPago,
                g.FechaCreacion,
                
                -- Información del creador
                u_creador.Nombre as NombreCreador,
                u_creador.Telefono as TelefonoCreador,
                
                -- Estadísticas
                (SELECT COUNT(*) FROM ParticipantesGrupo p WHERE p.GrupoId = g.Id) AS TotalParticipantes,
                (SELECT COUNT(*) FROM ParticipantesGrupo p WHERE p.GrupoId = g.Id AND p.YaPago = 1) AS ParticipantesPagaron
                
            FROM ParticipantesGrupo pg
            INNER JOIN GruposPago g ON pg.GrupoId = g.Id
            INNER JOIN Usuarios u_creador ON g.CreadorId = u_creador.Id
            WHERE pg.UsuarioId = @usuarioId 
            AND pg.YaPago = 0  -- Solo pagos pendientes
            ORDER BY g.FechaCreacion DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@usuarioId", usuarioId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            pagosPendientes.Add(new
                            {
                                GrupoId = (int)reader["GrupoId"],
                                NombreGrupo = reader["NombreGrupo"].ToString(),
                                MontoTotalGrupo = (decimal)reader["MontoTotalGrupo"],
                                MiMonto = (decimal)reader["MiMonto"],
                                FechaCreacion = (DateTime)reader["FechaCreacion"],

                                // Info del creador
                                NombreCreador = reader["NombreCreador"].ToString(),
                                TelefonoCreador = reader["TelefonoCreador"].ToString(),

                                // Estadísticas
                                TotalParticipantes = (int)reader["TotalParticipantes"],
                                ParticipantesPagaron = (int)reader["ParticipantesPagaron"],
                                PorcentajePagado = (int)reader["TotalParticipantes"] > 0
                                    ? ((int)reader["ParticipantesPagaron"] * 100 / (int)reader["TotalParticipantes"])
                                    : 0,

                                // UI helpers
                                Urgencia = DateTime.Now.Subtract((DateTime)reader["FechaCreacion"]).Days > 7 ? "alta" : "normal",
                                TiempoCreacion = DateTime.Now.Subtract((DateTime)reader["FechaCreacion"]).Days + " días"
                            });
                        }
                    }
                }
            }

            Console.WriteLine($"✅ Usuario {usuarioId} tiene {pagosPendientes.Count} pagos pendientes");

            return Ok(pagosPendientes);
        }

        [HttpDelete("eliminar/{id}")]
        public async Task<IActionResult> EliminarGrupo(int id, [FromBody] EliminarGrupoRequest request)
        {
            try
            {
                Console.WriteLine($"🗑️ Solicitud eliminación grupo {id} por usuario {request?.UsuarioId}");

                // Validación básica
                if (request == null || request.UsuarioId <= 0)
                {
                    return BadRequest(new { error = "ID de usuario requerido para eliminar grupo" });
                }

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // 1. Verificar que el grupo existe y obtener información
                        string checkGrupo = @"
                    SELECT Id, NombreGrupo, CreadorId, TotalMonto 
                    FROM GruposPago 
                    WHERE Id = @Id";

                        int creadorId = 0;
                        string nombreGrupo = "";
                        decimal totalMonto = 0;

                        using (SqlCommand cmd = new SqlCommand(checkGrupo, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", id);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    creadorId = (int)reader["CreadorId"];
                                    nombreGrupo = reader["NombreGrupo"].ToString();
                                    totalMonto = (decimal)reader["TotalMonto"];
                                }
                                else
                                {
                                    transaction.Rollback();
                                    return NotFound(new { error = "El grupo no existe" });
                                }
                            }
                        }

                        Console.WriteLine($"✅ Grupo encontrado: {nombreGrupo} (Creador: {creadorId})");

                        // 2. VALIDACIÓN CRÍTICA: Solo el creador puede eliminar
                        if (creadorId != request.UsuarioId)
                        {
                            Console.WriteLine($"🚫 ACCESO DENEGADO: Usuario {request.UsuarioId} intentó eliminar grupo creado por {creadorId}");
                            transaction.Rollback();
                            return StatusCode(403, new
                            {
                                error = "Solo el creador del grupo puede eliminarlo",
                                grupoCreador = creadorId,
                                usuarioSolicitante = request.UsuarioId
                            });

                        }

                        Console.WriteLine($"✅ Validación de creador exitosa. Usuario {request.UsuarioId} puede eliminar grupo {id}");

                        // 3. Obtener estadísticas antes de eliminar (para logs)
                        var estadisticas = new
                        {
                            totalParticipantes = 0,
                            totalPagos = 0,
                            participantesPagaron = 0
                        };

                        string getEstadisticas = @"
                    SELECT 
                        (SELECT COUNT(*) FROM ParticipantesGrupo WHERE GrupoId = @Id) as TotalParticipantes,
                        (SELECT COUNT(*) FROM PagosGrupo WHERE GrupoId = @Id) as TotalPagos,
                        (SELECT COUNT(*) FROM ParticipantesGrupo WHERE GrupoId = @Id AND YaPago = 1) as ParticipantesPagaron";

                        using (SqlCommand cmd = new SqlCommand(getEstadisticas, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", id);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    estadisticas = new
                                    {
                                        totalParticipantes = (int)reader["TotalParticipantes"],
                                        totalPagos = (int)reader["TotalPagos"],
                                        participantesPagaron = (int)reader["ParticipantesPagaron"]
                                    };
                                }
                            }
                        }

                        Console.WriteLine($"📊 Estadísticas: {estadisticas.totalParticipantes} participantes, {estadisticas.totalPagos} pagos, {estadisticas.participantesPagaron} pagaron");

                        // 4. Validación adicional: Advertir si hay pagos realizados
                        if (estadisticas.totalPagos > 0)
                        {
                            Console.WriteLine($"⚠️ ADVERTENCIA: Eliminando grupo con {estadisticas.totalPagos} pagos realizados");
                        }

                        // 5. Eliminar en orden correcto (por foreign keys)

                        // 5a. Eliminar pagos
                        string deletePagos = "DELETE FROM PagosGrupo WHERE GrupoId = @Id";
                        int pagosEliminados = 0;
                        using (SqlCommand cmd = new SqlCommand(deletePagos, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", id);
                            pagosEliminados = cmd.ExecuteNonQuery();
                        }

                        // 5b. Eliminar participantes
                        string deleteParticipantes = "DELETE FROM ParticipantesGrupo WHERE GrupoId = @Id";
                        int participantesEliminados = 0;
                        using (SqlCommand cmd = new SqlCommand(deleteParticipantes, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", id);
                            participantesEliminados = cmd.ExecuteNonQuery();
                        }

                        // 5c. Eliminar grupo
                        string deleteGrupo = "DELETE FROM GruposPago WHERE Id = @Id";
                        int gruposEliminados = 0;
                        using (SqlCommand cmd = new SqlCommand(deleteGrupo, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Id", id);
                            gruposEliminados = cmd.ExecuteNonQuery();
                        }

                        // 6. Verificar que se eliminó correctamente
                        if (gruposEliminados == 0)
                        {
                            transaction.Rollback();
                            return BadRequest(new { error = "No se pudo eliminar el grupo" });
                        }

                        // 7. Confirmar transacción
                        transaction.Commit();

                        // 8. Log de auditoría
                        var logEliminacion = new
                        {
                            grupoId = id,
                            nombreGrupo = nombreGrupo,
                            creadorId = creadorId,
                            eliminadoPor = request.UsuarioId,
                            totalMonto = totalMonto,
                            participantesEliminados = participantesEliminados,
                            pagosEliminados = pagosEliminados,
                            fechaEliminacion = DateTime.Now
                        };

                        Console.WriteLine($"✅ GRUPO ELIMINADO EXITOSAMENTE:");
                        Console.WriteLine($"   📋 Grupo: {nombreGrupo} (ID: {id})");
                        Console.WriteLine($"   👤 Creador: {creadorId}");
                        Console.WriteLine($"   🗑️ Eliminado por: {request.UsuarioId}");
                        Console.WriteLine($"   💰 Monto total: S/ {totalMonto}");
                        Console.WriteLine($"   👥 Participantes eliminados: {participantesEliminados}");
                        Console.WriteLine($"   💳 Pagos eliminados: {pagosEliminados}");

                        return Ok(new
                        {
                            message = "Grupo eliminado exitosamente",
                            grupo = logEliminacion
                        });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"❌ Error en transacción de eliminación: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error eliminando grupo {id}: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");

                return BadRequest(new
                {
                    error = "Error interno del servidor al eliminar el grupo",
                    details = ex.Message
                });
            }
        }

        [HttpGet("puede-eliminar/{id}")]
        [HttpGet("puede-eliminar/{grupoId}/usuario/{usuarioId}")]
        public IActionResult PuedeEliminarGrupo(int grupoId, int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    string query = @"
                SELECT 
                    g.Id,
                    g.NombreGrupo,
                    g.CreadorId,
                    g.TotalMonto,
                    (SELECT COUNT(*) FROM ParticipantesGrupo pg WHERE pg.GrupoId = g.Id) as TotalParticipantes,
                    (SELECT COUNT(*) FROM PagosGrupo pag WHERE pag.GrupoId = g.Id) as TotalPagos,
                    (SELECT COUNT(*) FROM ParticipantesGrupo pg WHERE pg.GrupoId = g.Id AND pg.YaPago = 1) as ParticipantesPagaron,
                    u.Nombre as NombreCreador
                FROM GruposPago g
                INNER JOIN Usuarios u ON g.CreadorId = u.Id
                WHERE g.Id = @GrupoId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int creadorId = (int)reader["CreadorId"];
                                bool esCreador = creadorId == usuarioId;
                                int totalPagos = (int)reader["TotalPagos"];
                                int participantesPagaron = (int)reader["ParticipantesPagaron"];

                                var advertencias = new List<string>();

                                // Agregar advertencias
                                if (!esCreador)
                                {
                                    advertencias.Add("Solo el creador del grupo puede eliminarlo");
                                }

                                if (totalPagos > 0)
                                {
                                    advertencias.Add($"El grupo tiene {totalPagos} pagos realizados que se eliminarán");
                                }

                                if (participantesPagaron > 0)
                                {
                                    advertencias.Add($"{participantesPagaron} participantes ya realizaron pagos");
                                }

                                var resultado = new
                                {
                                    grupoId = grupoId,
                                    usuarioId = usuarioId,
                                    nombreGrupo = reader["NombreGrupo"].ToString(),
                                    creadorId = creadorId,
                                    nombreCreador = reader["NombreCreador"].ToString(),
                                    esCreador = esCreador,

                                    // Permisos
                                    puedeEliminar = esCreador,

                                    // Advertencias
                                    advertencias = advertencias,

                                    // Estadísticas
                                    totalParticipantes = (int)reader["TotalParticipantes"],
                                    totalPagos = totalPagos,
                                    participantesPagaron = participantesPagaron,
                                    totalMonto = (decimal)reader["TotalMonto"]
                                };

                                return Ok(resultado);
                            }
                            else
                            {
                                return NotFound(new { error = "Grupo no encontrado" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // 🆕 NUEVO ENDPOINT: Obtener estadísticas por categoría
        [HttpGet("estadisticas-categoria/{usuarioId}")]
        public IActionResult ObtenerEstadisticasPorCategoria(int usuarioId)
        {
            var estadisticas = new List<object>();

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT 
                        g.Categoria,
                        COUNT(g.Id) as CantidadGrupos,
                        SUM(g.TotalMonto) as MontoTotal,
                        AVG(g.TotalMonto) as MontoPromedio,
                        COUNT(CASE WHEN pg.YaPago = 1 THEN 1 END) as GruposPagados,
                        SUM(CASE WHEN pg.YaPago = 1 THEN pg.MontoIndividual ELSE 0 END) as MontoPagado,
                        SUM(CASE WHEN pg.YaPago = 0 THEN pg.MontoIndividual ELSE 0 END) as MontoPendiente
                    FROM GruposPago g
                    LEFT JOIN ParticipantesGrupo pg ON g.Id = pg.GrupoId AND pg.UsuarioId = @usuarioId
                    WHERE g.CreadorId = @usuarioId OR EXISTS (
                        SELECT 1 FROM ParticipantesGrupo pg2 
                        WHERE pg2.GrupoId = g.Id AND pg2.UsuarioId = @usuarioId
                    )
                    GROUP BY g.Categoria
                    ORDER BY SUM(g.TotalMonto) DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@usuarioId", usuarioId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            estadisticas.Add(new
                            {
                                categoria = reader["Categoria"]?.ToString() ?? "general",
                                cantidadGrupos = (int)reader["CantidadGrupos"],
                                montoTotal = (decimal)reader["MontoTotal"],
                                montoPromedio = (decimal)reader["MontoPromedio"],
                                gruposPagados = (int)reader["GruposPagados"],
                                montoPagado = (decimal)reader["MontoPagado"],
                                montoPendiente = (decimal)reader["MontoPendiente"]
                            });
                        }
                    }
                }
            }

            return Ok(estadisticas);
        }

        // 🆕 NUEVO ENDPOINT: Próximos vencimientos
        [HttpGet("proximos-vencimientos/{usuarioId}")]
        public IActionResult ObtenerProximosVencimientos(int usuarioId)
        {
            var vencimientos = new List<object>();

            using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT 
                        g.Id,
                        g.NombreGrupo,
                        g.Categoria,
                        g.FechaLimite,
                        pg.MontoIndividual,
                        pg.YaPago,
                        DATEDIFF(day, GETDATE(), g.FechaLimite) as DiasRestantes
                    FROM GruposPago g
                    INNER JOIN ParticipantesGrupo pg ON g.Id = pg.GrupoId
                    WHERE pg.UsuarioId = @usuarioId 
                        AND pg.YaPago = 0 
                        AND g.FechaLimite IS NOT NULL
                        AND g.FechaLimite >= DATEADD(day, -7, GETDATE())  -- Incluir vencidos hasta 7 días atrás
                    ORDER BY g.FechaLimite ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@usuarioId", usuarioId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int diasRestantes = (int)reader["DiasRestantes"];

                            vencimientos.Add(new
                            {
                                grupoId = (int)reader["Id"],
                                nombreGrupo = reader["NombreGrupo"].ToString(),
                                categoria = reader["Categoria"]?.ToString() ?? "general",
                                fechaLimite = (DateTime)reader["FechaLimite"],
                                montoIndividual = (decimal)reader["MontoIndividual"],
                                diasRestantes = diasRestantes,
                                urgencia = diasRestantes < 0 ? "vencido" :
                                          diasRestantes <= 1 ? "urgente" :
                                          diasRestantes <= 3 ? "proximo" : "normal",
                                esVencido = diasRestantes < 0,
                                esUrgente = diasRestantes >= 0 && diasRestantes <= 3
                            });
                        }
                    }
                }
            }

            return Ok(vencimientos);
        }

        [HttpPost("registrar-eliminacion")]
        public IActionResult RegistrarEliminacion([FromBody] object datosEliminacion)
        {
            // Implementar si necesitas auditoría de eliminaciones
            return Ok(new { message = "Eliminación registrada en auditoría" });
        }

        [HttpGet("auditoria-eliminaciones")]
        public IActionResult ObtenerAuditoriaEliminaciones()
        {
            // Si implementaste tabla de auditoría, aquí puedes consultar
            // las eliminaciones realizadas para fines de auditoría
            return Ok(new
            {
                message = "Auditoría no implementada",
                sugerencia = "Implementar tabla GruposEliminados para tracking"
            });
        }
        [HttpPost("enviar-recordatorio")]
        public IActionResult RegistrarRecordatorioEnviado([FromBody] RegistrarRecordatorioRequest request)
        {
            try
            {
                Console.WriteLine($"📧 Registrando recordatorio enviado:");
                Console.WriteLine($"   Grupo: {request.GrupoId}");
                Console.WriteLine($"   Enviado por: {request.EnviadoPor}");
                Console.WriteLine($"   Para: {request.UsuarioDestino}");
                Console.WriteLine($"   Método: {request.MetodoEnvio}");

                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // Verificar que el remitente sea el creador del grupo
                    string verificarCreador = @"
                SELECT CreadorId, NombreGrupo 
                FROM GruposPago 
                WHERE Id = @GrupoId";

                    int creadorId = 0;
                    string nombreGrupo = "";

                    using (SqlCommand cmd = new SqlCommand(verificarCreador, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", request.GrupoId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                creadorId = (int)reader["CreadorId"];
                                nombreGrupo = reader["NombreGrupo"].ToString();
                            }
                            else
                            {
                                return NotFound(new { error = "Grupo no encontrado" });
                            }
                        }
                    }

                    // ✅ CORRECCIÓN: Usar StatusCode en lugar de Forbid con objeto
                    if (creadorId != request.EnviadoPor)
                    {
                        return StatusCode(403, new { error = "Solo el creador puede enviar recordatorios" });
                    }

                    // Opcional: Crear tabla de auditoría de notificaciones
                    // Si tienes una tabla NotificacionesEnviadas, puedes registrar ahí

                    // Por ahora solo retornamos confirmación
                    var resultado = new
                    {
                        grupoId = request.GrupoId,
                        nombreGrupo = nombreGrupo,
                        enviadoPor = request.EnviadoPor,
                        usuarioDestino = request.UsuarioDestino,
                        metodoEnvio = request.MetodoEnvio,
                        fechaEnvio = DateTime.Now,
                        mensaje = "Recordatorio registrado exitosamente"
                    };

                    Console.WriteLine($"✅ Recordatorio registrado exitosamente");

                    return Ok(resultado);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error registrando recordatorio: {ex.Message}");
                return BadRequest(new { error = "Error interno del servidor", details = ex.Message });
            }
        }

        [HttpGet("recordatorios-enviados/{grupoId}")]
        public IActionResult ObtenerRecordatoriosEnviados(int grupoId, int usuarioId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();

                    // Verificar que el usuario sea el creador
                    string verificarCreador = @"
                SELECT CreadorId, NombreGrupo 
                FROM GruposPago 
                WHERE Id = @GrupoId";

                    using (SqlCommand cmd = new SqlCommand(verificarCreador, conn))
                    {
                        cmd.Parameters.AddWithValue("@GrupoId", grupoId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int creadorId = (int)reader["CreadorId"];

                                // ✅ CORRECCIÓN: Usar StatusCode en lugar de Forbid con objeto
                                if (creadorId != usuarioId)
                                {
                                    return StatusCode(403, new { error = "Solo el creador puede ver los recordatorios enviados" });
                                }
                            }
                            else
                            {
                                return NotFound(new { error = "Grupo no encontrado" });
                            }
                        }
                    }

                    // Por ahora retornar estructura mock
                    // En una implementación real, consultar tabla NotificacionesEnviadas
                    var recordatoriosMock = new[]
                    {
                new
                {
                    fechaEnvio = DateTime.Now.AddHours(-2),
                    usuarioDestino = "Juan Pérez",
                    metodoEnvio = "WhatsApp",
                    estado = "Enviado"
                }
            };

                    return Ok(new
                    {
                        grupoId = grupoId,
                        totalRecordatorios = recordatoriosMock.Length,
                        recordatorios = recordatoriosMock,
                        mensaje = "Implementar tabla NotificacionesEnviadas para datos reales"
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // Clase para la request
        public class RegistrarRecordatorioRequest
        {
            public int GrupoId { get; set; }
            public int EnviadoPor { get; set; }
            public int UsuarioDestino { get; set; }
            public string MetodoEnvio { get; set; } = ""; // "WhatsApp", "SMS", "Email", etc.
            public string? Mensaje { get; set; }
        }

        // CLASES DE REQUEST
        public class CrearGrupoRequest
        {
            public string NombreGrupo { get; set; }
            public int CreadorId { get; set; }
            public decimal MontoTotal { get; set; }
            public List<string> Participantes { get; set; }
            public string? Categoria { get; set; } = "general";
            public DateTime? FechaLimite { get; set; }
            public string? Descripcion { get; set; }
            public bool DivisionManual { get; set; } = false;
            public List<decimal>? MontosIndividuales { get; set; }
        }

        public class EliminarGrupoRequest
        {
            public int UsuarioId { get; set; }
        }
    }
}