using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly string connectionString;

        public DashboardController(IConfiguration configuration)
        {
            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("stats/{userId}")]
        public IActionResult GetDashboardStats(int userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Obtener estadísticas del dashboard
                    var stats = new
                    {
                        gruposActivos = GetGruposActivos(conn, userId),
                        porCobrar = GetMontoPorCobrar(conn, userId),
                        porPagar = GetMontoPorPagar(conn, userId),
                        gruposCompletados = GetGruposCompletados(conn, userId),
                        totalTransacciones = GetTotalTransacciones(conn, userId)
                    };

                    return Ok(stats);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener estadísticas: {ex.Message}");
            }
        }

        [HttpGet("activity/{userId}")]
        public IActionResult GetDashboardActivity(int userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var activities = new List<object>();

                    // Obtener actividad reciente combinada (últimas 20 actividades)
                    string activityQuery = @"
                    WITH ActividadCombinada AS (
                        -- Grupos creados
                        SELECT 
                            'grupo_creado' as tipo,
                            'Creaste el grupo ''' + NombreGrupo + '''' as descripcion,
                            FechaCreacion as fecha,
                            TotalMonto as monto,
                            Id as grupoId,
                            NULL as pagoId
                        FROM GruposPago 
                        WHERE CreadorId = @UserId

                        UNION ALL

                        -- Pagos recibidos (como creador del grupo)
                        SELECT 
                            'pago_recibido' as tipo,
                            u.Nombre + ' pagó en el grupo ''' + gp.NombreGrupo + '''' as descripcion,
                            pg.FechaPago as fecha,
                            pg.MontoPagado as monto,
                            pg.GrupoId as grupoId,
                            pg.Id as pagoId
                        FROM PagosGrupo pg
                        INNER JOIN GruposPago gp ON pg.GrupoId = gp.Id
                        INNER JOIN Usuarios u ON pg.UsuarioId = u.Id
                        WHERE gp.CreadorId = @UserId AND pg.UsuarioId != @UserId

                        UNION ALL

                        -- Pagos realizados (como participante)
                        SELECT 
                            'pago_realizado' as tipo,
                            'Pagaste en el grupo ''' + gp.NombreGrupo + '''' as descripcion,
                            pg.FechaPago as fecha,
                            pg.MontoPagado as monto,
                            pg.GrupoId as grupoId,
                            pg.Id as pagoId
                        FROM PagosGrupo pg
                        INNER JOIN GruposPago gp ON pg.GrupoId = gp.Id
                        WHERE pg.UsuarioId = @UserId

                        UNION ALL

                        -- Grupos completados
                        SELECT 
                            'grupo_completado' as tipo,
                            'Se completó el grupo ''' + gp.NombreGrupo + '''' as descripcion,
                            MAX(pg.FechaPago) as fecha,
                            gp.TotalMonto as monto,
                            gp.Id as grupoId,
                            NULL as pagoId
                        FROM GruposPago gp
                        INNER JOIN ParticipantesGrupo ptg ON gp.Id = ptg.GrupoId
                        LEFT JOIN PagosGrupo pg ON gp.Id = pg.GrupoId
                        WHERE (gp.CreadorId = @UserId OR ptg.UsuarioId = @UserId)
                        AND NOT EXISTS (
                            SELECT 1 FROM ParticipantesGrupo ptg2 
                            WHERE ptg2.GrupoId = gp.Id 
                            AND ptg2.YaPago = 0
                        )
                        GROUP BY gp.Id, gp.NombreGrupo, gp.TotalMonto
                    )
                    SELECT TOP 10 
                        tipo, descripcion, fecha, monto, grupoId, pagoId,
                        ROW_NUMBER() OVER (ORDER BY fecha DESC) as id
                    FROM ActividadCombinada
                    ORDER BY fecha DESC";

                    using (SqlCommand cmd = new SqlCommand(activityQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                activities.Add(new
                                {
                                    id = reader["id"],
                                    tipo = reader["tipo"].ToString(),
                                    descripcion = reader["descripcion"].ToString(),
                                    fecha = reader["fecha"],
                                    monto = reader["monto"] != DBNull.Value ? reader["monto"] : null,
                                    grupoId = reader["grupoId"] != DBNull.Value ? reader["grupoId"] : null
                                });
                            }
                        }
                    }

                    return Ok(activities);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener actividad: {ex.Message}");
            }
        }

        #region Métodos Auxiliares para Estadísticas

        private int GetGruposActivos(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT COUNT(DISTINCT gp.Id) 
            FROM GruposPago gp
            INNER JOIN ParticipantesGrupo pg ON gp.Id = pg.GrupoId
            WHERE (gp.CreadorId = @UserId OR pg.UsuarioId = @UserId)
            AND EXISTS (
                SELECT 1 FROM ParticipantesGrupo pg2 
                WHERE pg2.GrupoId = gp.Id AND pg2.YaPago = 0
            )";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                return (int)cmd.ExecuteScalar();
            }
        }

        private decimal GetMontoPorCobrar(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT ISNULL(SUM(pg.MontoIndividual), 0)
            FROM GruposPago gp
            INNER JOIN ParticipantesGrupo pg ON gp.Id = pg.GrupoId
            WHERE gp.CreadorId = @UserId 
            AND pg.UsuarioId != @UserId
            AND pg.YaPago = 0";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? (decimal)result : 0;
            }
        }

        private decimal GetMontoPorPagar(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT ISNULL(SUM(pg.MontoIndividual), 0)
            FROM ParticipantesGrupo pg
            INNER JOIN GruposPago gp ON pg.GrupoId = gp.Id
            WHERE pg.UsuarioId = @UserId 
            AND pg.YaPago = 0";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? (decimal)result : 0;
            }
        }

        private int GetGruposCompletados(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT COUNT(DISTINCT gp.Id)
            FROM GruposPago gp
            INNER JOIN ParticipantesGrupo pg ON gp.Id = pg.GrupoId
            WHERE (gp.CreadorId = @UserId OR pg.UsuarioId = @UserId)
            AND NOT EXISTS (
                SELECT 1 FROM ParticipantesGrupo pg2 
                WHERE pg2.GrupoId = gp.Id AND pg2.YaPago = 0
            )";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                return (int)cmd.ExecuteScalar();
            }
        }

        private int GetTotalTransacciones(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT COUNT(*)
            FROM PagosGrupo pg
            INNER JOIN GruposPago gp ON pg.GrupoId = gp.Id
            WHERE pg.UsuarioId = @UserId OR gp.CreadorId = @UserId";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                return (int)cmd.ExecuteScalar();
            }
        }

        #endregion

        #region Endpoints Adicionales para el Dashboard

        [HttpGet("groups/{userId}")]
        public IActionResult GetUserGroups(int userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                    SELECT DISTINCT
                        gp.Id,
                        gp.NombreGrupo,
                        gp.TotalMonto,
                        gp.FechaCreacion,
                        gp.CreadorId,
                        u.Nombre as CreadorNombre,
                        CASE 
                            WHEN EXISTS (
                                SELECT 1 FROM ParticipantesGrupo pg2 
                                WHERE pg2.GrupoId = gp.Id AND pg2.YaPago = 0
                            ) THEN 'activo'
                            ELSE 'completado'
                        END as Estado,
                        (
                            SELECT COUNT(*) FROM ParticipantesGrupo 
                            WHERE GrupoId = gp.Id
                        ) as TotalParticipantes,
                        (
                            SELECT COUNT(*) FROM ParticipantesGrupo 
                            WHERE GrupoId = gp.Id AND YaPago = 1
                        ) as ParticipantesPagaron
                    FROM GruposPago gp
                    INNER JOIN Usuarios u ON gp.CreadorId = u.Id
                    INNER JOIN ParticipantesGrupo pg ON gp.Id = pg.GrupoId
                    WHERE gp.CreadorId = @UserId OR pg.UsuarioId = @UserId
                    ORDER BY gp.FechaCreacion DESC";

                    var grupos = new List<object>();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                grupos.Add(new
                                {
                                    id = reader["Id"],
                                    nombreGrupo = reader["NombreGrupo"].ToString(),
                                    totalMonto = reader["TotalMonto"],
                                    fechaCreacion = reader["FechaCreacion"],
                                    creadorId = reader["CreadorId"],
                                    creadorNombre = reader["CreadorNombre"].ToString(),
                                    estado = reader["Estado"].ToString(),
                                    totalParticipantes = reader["TotalParticipantes"],
                                    participantesPagaron = reader["ParticipantesPagaron"]
                                });
                            }
                        }
                    }

                    return Ok(grupos);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener grupos: {ex.Message}");
            }
        }

        [HttpGet("payments/{userId}")]
        public IActionResult GetUserPayments(int userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                    SELECT 
                        pg.Id,
                        pg.MontoPagado,
                        pg.FechaPago,
                        gp.NombreGrupo,
                        gp.Id as GrupoId,
                        u.Nombre as UsuarioNombre
                    FROM PagosGrupo pg
                    INNER JOIN GruposPago gp ON pg.GrupoId = gp.Id
                    INNER JOIN Usuarios u ON pg.UsuarioId = u.Id
                    WHERE pg.UsuarioId = @UserId OR gp.CreadorId = @UserId
                    ORDER BY pg.FechaPago DESC";

                    var pagos = new List<object>();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                pagos.Add(new
                                {
                                    id = reader["Id"],
                                    montoPagado = reader["MontoPagado"],
                                    fechaPago = reader["FechaPago"],
                                    nombreGrupo = reader["NombreGrupo"].ToString(),
                                    grupoId = reader["GrupoId"],
                                    usuarioNombre = reader["UsuarioNombre"].ToString()
                                });
                            }
                        }
                    }

                    return Ok(pagos);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener pagos: {ex.Message}");
            }
        }

        [HttpGet("summary/{userId}")]
        public IActionResult GetUserSummary(int userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var summary = new
                    {
                        totalGastado = GetTotalGastado(conn, userId),
                        totalRecibido = GetTotalRecibido(conn, userId),
                        promedioGrupoPorMes = GetPromedioGruposPorMes(conn, userId),
                        categoriaGastoMayor = GetCategoriaGastoMayor(conn, userId),
                        balanceGeneral = GetBalanceGeneral(conn, userId)
                    };

                    return Ok(summary);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener resumen: {ex.Message}");
            }
        }

        #endregion

        #region Métodos para Summary Adicional

        private decimal GetTotalGastado(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT ISNULL(SUM(pg.MontoPagado), 0)
            FROM PagosGrupo pg
            WHERE pg.UsuarioId = @UserId";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? (decimal)result : 0;
            }
        }

        private decimal GetTotalRecibido(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT ISNULL(SUM(pg.MontoPagado), 0)
            FROM PagosGrupo pg
            INNER JOIN GruposPago gp ON pg.GrupoId = gp.Id
            WHERE gp.CreadorId = @UserId AND pg.UsuarioId != @UserId";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? (decimal)result : 0;
            }
        }

        private decimal GetPromedioGruposPorMes(SqlConnection conn, int userId)
        {
            string query = @"
            SELECT 
                CASE 
                    WHEN COUNT(DISTINCT YEAR(gp.FechaCreacion) * 12 + MONTH(gp.FechaCreacion)) = 0 THEN 0
                    ELSE CAST(COUNT(gp.Id) AS DECIMAL) / COUNT(DISTINCT YEAR(gp.FechaCreacion) * 12 + MONTH(gp.FechaCreacion))
                END
            FROM GruposPago gp
            WHERE gp.CreadorId = @UserId
            AND gp.FechaCreacion >= DATEADD(YEAR, -1, GETDATE())";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                var result = cmd.ExecuteScalar();
                return result != DBNull.Value ? (decimal)result : 0;
            }
        }

        private string GetCategoriaGastoMayor(SqlConnection conn, int userId)
        {
            // Por ahora retornamos un valor por defecto ya que no tienes categorías en tu BD
            // Puedes expandir esto cuando agregues categorías a tus grupos
            return "Gastos generales";
        }

        private decimal GetBalanceGeneral(SqlConnection conn, int userId)
        {
            decimal porCobrar = GetMontoPorCobrar(conn, userId);
            decimal porPagar = GetMontoPorPagar(conn, userId);
            return porCobrar - porPagar;
        }

        #endregion
    }
}
