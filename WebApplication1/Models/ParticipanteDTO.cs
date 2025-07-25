namespace WebApplication1.Models
{
    public class ParticipanteDTO
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; }
        public string Telefono { get; set; }
        public decimal MontoIndividual { get; set; }
        public decimal MontoPagado { get; set; }
        public bool YaPago { get; set; }
    }

    public class ParticipantePagoConComprobanteDTO
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public decimal MontoIndividual { get; set; }
        public decimal MontoPagado { get; set; }
        public bool YaPago { get; set; }

        // 🆕 Información del comprobante
        public bool TieneComprobante { get; set; }
        public DateTime? FechaPago { get; set; }
        public string? MetodoPagoUsado { get; set; }
        public string? ComprobantePreview { get; set; } // Solo primeros caracteres para preview
    }

    // Clase para respuesta de comprobante completo
    public class ComprobanteResponse
    {
        public int GrupoId { get; set; }
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Comprobante { get; set; } = string.Empty; // Base64 completo
        public DateTime? FechaPago { get; set; }
        public string? MetodoPagoUsado { get; set; }
        public decimal MontoIndividual { get; set; }
        public bool TieneComprobante { get; set; }
    }

    // Clase para estadísticas de grupo con comprobantes
    public class EstadisticasGrupoConComprobantes
    {
        public int TotalParticipantes { get; set; }
        public int ParticipantesPagaron { get; set; }
        public int ParticipantesConComprobante { get; set; }
        public int ParticipantesSinComprobante { get; set; }
        public decimal TotalMonto { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal PorcentajePagado { get; set; }
        public decimal PorcentajeConComprobante { get; set; }
    }

    // Enum para tipos de método de pago
    public enum MetodoPago
    {
        Yape,
        Plin,
        MercadoPago,
        Efectivo,
        Transferencia,
        Otro
    }
}
