namespace WebApplication1.Models
{
    public class ConfiguracionPago
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string MetodoPago { get; set; } = string.Empty; // "yape" o "plin"
        public string NombreTitular { get; set; } = string.Empty;
        public string NumeroTelefono { get; set; } = string.Empty;
        public string? ImagenQR { get; set; } // Base64
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }
}
