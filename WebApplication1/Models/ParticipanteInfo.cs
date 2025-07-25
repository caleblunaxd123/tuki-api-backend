namespace WebApplication1.Models
{
    public class ParticipanteInfo
    {
        public bool YaPago { get; set; }
        public decimal MontoIndividual { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string NombreGrupo { get; set; } = string.Empty;

        // 🆕 Nuevas propiedades para comprobantes
        public string? Comprobante { get; set; }
        public DateTime? FechaPago { get; set; }
        public string? MetodoPagoUsado { get; set; }
        public bool TieneComprobante => !string.IsNullOrEmpty(Comprobante);
    }
}
