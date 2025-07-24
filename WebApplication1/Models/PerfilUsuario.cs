namespace WebApplication1.Models
{
    public class PerfilUsuario
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string? FotoPerfil { get; set; } // Base64
        public string? Biografia { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public string? Genero { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public bool Activo { get; set; }
    }
}
