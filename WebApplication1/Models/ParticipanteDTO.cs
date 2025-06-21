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
}
