namespace WebApplication1.Models
{
    public class GrupoCompleto
    {
        public int Id { get; set; }
        public string NombreGrupo { get; set; }
        public decimal TotalMonto { get; set; }
        public decimal TotalPagado { get; set; }
        public List<ParticipanteDTO> Participantes { get; set; } = new();
    }
}
