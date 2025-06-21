namespace WebApplication1.Models
{
    public class GrupoDetalleResponse
    {
        public int Id { get; set; }
        public string NombreGrupo { get; set; }
        public decimal TotalMonto { get; set; }
        public decimal TotalPagado { get; set; }
        public decimal MontoRestante => TotalMonto - TotalPagado;
        public List<ParticipantePagoDTO> Participantes { get; set; }
        public string Categoria { get; set; }
        public DateTime? FechaLimite { get; set; }
        public string? Descripcion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int CreadorId { get; set; }
        public int? DiasRestantes { get; set; }
        public bool EsUrgente { get; set; }
        public bool EstaVencido { get; set; }
    }

    public class ParticipantePagoDTO
    {
        public int UsuarioId { get; set; }
        public string Nombre { get; set; }
        public string Telefono { get; set; }
        public decimal MontoIndividual { get; set; }
        public decimal MontoPagado { get; set; }
        public bool YaPago { get; set; }
    }

}
