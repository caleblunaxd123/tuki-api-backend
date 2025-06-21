using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class DTOs
    {
        public class CrearPagoRequest
        {
            [Required]
            public int GrupoId { get; set; }

            [Required]
            public int UsuarioId { get; set; }
        }

        public class ParticipanteInfo
        {
            public bool YaPago { get; set; }
            public decimal MontoIndividual { get; set; }
            public string NombreUsuario { get; set; } = "";
            public string NombreGrupo { get; set; } = "";
        }

        public class GrupoCompleto
        {
            public int Id { get; set; }
            public string NombreGrupo { get; set; } = "";
            public decimal TotalMonto { get; set; }
            public decimal TotalPagado { get; set; }
            public List<ParticipanteDTO> Participantes { get; set; } = new();
        }

        public class ParticipanteDTO
        {
            public int UsuarioId { get; set; }
            public string Nombre { get; set; } = "";
            public string Telefono { get; set; } = "";
            public decimal MontoIndividual { get; set; }
            public decimal MontoPagado { get; set; }
            public bool YaPago { get; set; }
        }

        public class SimularPagoRequest
        {
            public int GrupoId { get; set; }
            public int UsuarioId { get; set; }
            public decimal Monto { get; set; }
        }
        public class EliminarGrupoRequest
        {
            [Required]
            public int UsuarioId { get; set; }

            public string Motivo { get; set; } = ""; // Opcional: razón de eliminación
        }
    }
}
