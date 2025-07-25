using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class DTOs
    {
        // =============================================
        // DTOs PARA GRUPOS (existentes)
        // =============================================
        public class CrearPagoRequest
        {
            [Required]
            public int GrupoId { get; set; }

            [Required]
            public int UsuarioId { get; set; }
        }

        public class ParticipanteDTO
        {
            public int UsuarioId { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string Telefono { get; set; } = string.Empty;
            public decimal MontoIndividual { get; set; }
            public decimal MontoPagado { get; set; }
            public bool YaPago { get; set; }

            // 🆕 Campos para comprobantes
            public bool TieneComprobante { get; set; }
            public DateTime? FechaPago { get; set; }
            public string? MetodoPagoUsado { get; set; }
            public string? ComprobantePreview { get; set; }

            // 🆕 Campos para validación
            public bool? ComprobanteValidado { get; set; }
            public DateTime? FechaValidacion { get; set; }
            public string? ComentarioValidacion { get; set; }
            public string EstadoValidacion =>
                !TieneComprobante ? "Sin comprobante" :
                ComprobanteValidado == null ? "Pendiente validación" :
                ComprobanteValidado == true ? "Validado ✅" : "Rechazado ❌";
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



        // 🆕 DTO para respuesta de detalles del grupo con nuevos campos
        public class GrupoDetalleResponse
        {
            public int Id { get; set; }
            public string NombreGrupo { get; set; } = string.Empty;
            public decimal TotalMonto { get; set; }
            public decimal TotalPagado { get; set; }
            public string Categoria { get; set; } = string.Empty;
            public DateTime? FechaLimite { get; set; }
            public string? Descripcion { get; set; }
            public DateTime FechaCreacion { get; set; }
            public int CreadorId { get; set; }
            public List<ParticipanteDTO> Participantes { get; set; } = new();

            // Campos de urgencia existentes
            public int? DiasRestantes { get; set; }
            public bool EsUrgente { get; set; }
            public bool EstaVencido { get; set; }

            // 🆕 Estadísticas de comprobantes
            public object? EstadisticasComprobantes { get; set; }
        }

        public class ComprobanteDetalleDTO
        {
            public int GrupoId { get; set; }
            public int UsuarioId { get; set; }
            public string NombreUsuario { get; set; } = string.Empty;
            public string Telefono { get; set; } = string.Empty;
            public string NombreGrupo { get; set; } = string.Empty;
            public string? Comprobante { get; set; } // Base64 completo
            public DateTime? FechaPago { get; set; }
            public string? MetodoPagoUsado { get; set; }
            public decimal MontoIndividual { get; set; }
            public bool TieneComprobante { get; set; }

            // Información de validación
            public bool? ComprobanteValidado { get; set; }
            public DateTime? FechaValidacion { get; set; }
            public string? ComentarioValidacion { get; set; }
            public string? ValidadoPor { get; set; }
        }

        public class ResumenComprobantesDTO
        {
            public int GrupoId { get; set; }
            public string NombreGrupo { get; set; } = string.Empty;
            public int CreadorId { get; set; }
            public string NombreCreador { get; set; } = string.Empty;
            public int TotalPagos { get; set; }
            public int PagosConComprobante { get; set; }
            public int PagosSinComprobante { get; set; }
            public int ComprobantesValidados { get; set; }
            public int ComprobantesRechazados { get; set; }
            public int ComprobantesPendientes { get; set; }
            public List<ComprobanteResumenDTO> Comprobantes { get; set; } = new();
        }

        public class ComprobanteResumenDTO
        {
            public int UsuarioId { get; set; }
            public string NombreParticipante { get; set; } = string.Empty;
            public string Telefono { get; set; } = string.Empty;
            public decimal MontoIndividual { get; set; }
            public DateTime? FechaPago { get; set; }
            public string? MetodoPagoUsado { get; set; }
            public bool TieneComprobante { get; set; }
            public string? ComprobantePreview { get; set; }
            public string EstadoValidacion { get; set; } = string.Empty;
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

        // =============================================
        // DTOs PARA PERFIL
        // =============================================
        public class ActualizarPerfilRequest
        {
            [Required(ErrorMessage = "El nombre es requerido")]
            [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
            public string Nombre { get; set; } = string.Empty;

            [Required(ErrorMessage = "El teléfono es requerido")]
            [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
            public string Telefono { get; set; } = string.Empty;

            [EmailAddress(ErrorMessage = "Email inválido")]
            [StringLength(100, ErrorMessage = "El email no puede exceder 100 caracteres")]
            public string? Correo { get; set; }

            public string? FotoPerfil { get; set; }

            [StringLength(500, ErrorMessage = "La biografía no puede exceder 500 caracteres")]
            public string? Biografia { get; set; }

            public DateTime? FechaNacimiento { get; set; }

            [StringLength(20, ErrorMessage = "El género no puede exceder 20 caracteres")]
            public string? Genero { get; set; }
        }
        public class FotoPerfilResponse
        {
            public int UsuarioId { get; set; }
            public string? FotoBase64 { get; set; }
            public DateTime? FechaActualizacion { get; set; }
            public bool TieneFoto { get; set; }
        }
        public class FotoPerfilRequest
        {
            [Required(ErrorMessage = "La foto es requerida")]
            public string FotoBase64 { get; set; } = string.Empty;
        }

        // =============================================
        // DTOs PARA CONFIGURACIÓN DE PAGO
        // =============================================
        public class ConfigurarPagoRequest
        {
            [Required(ErrorMessage = "El método de pago es requerido")]
            public string MetodoPago { get; set; } = string.Empty;

            [Required(ErrorMessage = "El nombre del titular es requerido")]
            [StringLength(150, ErrorMessage = "El nombre no puede exceder 150 caracteres")]
            public string NombreTitular { get; set; } = string.Empty;

            [Required(ErrorMessage = "El número de teléfono es requerido")]
            [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
            public string NumeroTelefono { get; set; } = string.Empty;

            [Required(ErrorMessage = "La imagen QR es requerida")]
            public string ImagenQR { get; set; } = string.Empty;
        }

        public class ValidarQRRequest
        {
            [Required(ErrorMessage = "La imagen QR es requerida")]
            public string ImagenQR { get; set; } = string.Empty;
        }

        // =============================================
        // DTOs PARA RESPONSES
        // =============================================
        public class PerfilCompletoResponse
        {
            // Datos del usuario
            public int UsuarioId { get; set; }
            public string Nombre { get; set; } = string.Empty;
            public string Telefono { get; set; } = string.Empty;
            public string? Correo { get; set; }
            public DateTime FechaRegistro { get; set; }

            // Datos del perfil
            public int? PerfilId { get; set; }
            public string? FotoPerfil { get; set; }
            public string? Biografia { get; set; }
            public DateTime? FechaNacimiento { get; set; }
            public string? Genero { get; set; }
            public DateTime? PerfilFechaCreacion { get; set; }
            public DateTime? PerfilFechaActualizacion { get; set; }

            // Configuración de pago
            public int? ConfigPagoId { get; set; }
            public string? MetodoPago { get; set; }
            public string? NombreTitular { get; set; }
            public string? TelefonoPago { get; set; }
            public string? ImagenQR { get; set; }
            public DateTime? ConfigPagoFechaCreacion { get; set; }
            public DateTime? ConfigPagoFechaActualizacion { get; set; }

            // Estados
            public bool TieneMetodoPago { get; set; }
            public bool TienePerfil { get; set; }
            public bool EsPerfilCompleto { get; set; }
            public List<string> CamposFaltantes { get; set; } = new();
        }

        public class ConfiguracionPagoResponse
        {
            public int Id { get; set; }
            public string MetodoPago { get; set; } = string.Empty;
            public string NombreTitular { get; set; } = string.Empty;
            public string NumeroTelefono { get; set; } = string.Empty;
            public string? ImagenQR { get; set; }
            public DateTime FechaCreacion { get; set; }
            public DateTime FechaActualizacion { get; set; }
            public bool Activo { get; set; }
        }

        public class QRResponse
        {
            public string MetodoPago { get; set; } = string.Empty;
            public string NombreTitular { get; set; } = string.Empty;
            public string? ImagenQR { get; set; }
        }

        public class QRValidacionResponse
        {
            public bool EsValido { get; set; }
            public long TamanoBytes { get; set; }
            public string Formato { get; set; } = string.Empty;
            public List<string> Errores { get; set; } = new();
        }
    }

    // =============================================
    // RESPONSE GENÉRICO
    // =============================================
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Constructor por defecto
        public ApiResponse()
        {
            Errors = new List<string>();
            Timestamp = DateTime.Now;
        }

        public static ApiResponse<T> SuccessResult(T data, string message = "Operación exitosa")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Errors = new List<string>(),
                Timestamp = DateTime.Now
            };
        }

        public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default(T),
                Errors = errors ?? new List<string>(),
                Timestamp = DateTime.Now
            };
        }

        public static ApiResponse<T> ErrorResult(string message, string error)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default(T),
                Errors = new List<string> { error },
                Timestamp = DateTime.Now
            };
        }
    }

    // =============================================
    // RESPUESTA SIMPLE SIN DATOS ESPECÍFICOS
    // =============================================
    public class SimpleApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Constructor por defecto
        public SimpleApiResponse()
        {
            Errors = new List<string>();
            Timestamp = DateTime.Now;
        }

        public static SimpleApiResponse SuccessResult(string message = "Operación exitosa")
        {
            return new SimpleApiResponse
            {
                Success = true,
                Message = message,
                Errors = new List<string>(),
                Timestamp = DateTime.Now
            };
        }

        public static SimpleApiResponse ErrorResult(string message, List<string>? errors = null)
        {
            return new SimpleApiResponse
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>(),
                Timestamp = DateTime.Now
            };
        }
    }

    // =============================================
    // VALIDACIONES ESTÁTICAS
    // =============================================
    public static class ValidacionesPeruanas
    {
        public static bool EsTelefonoValido(string telefono)
        {
            if (string.IsNullOrEmpty(telefono)) return false;

            var telefonoLimpio = LimpiarTelefono(telefono);
            return telefonoLimpio.Length == 9 &&
                   telefonoLimpio.StartsWith("9") &&
                   telefonoLimpio.All(char.IsDigit);
        }

        public static bool EsMetodoPagoValido(string metodoPago)
        {
            if (string.IsNullOrEmpty(metodoPago)) return false;

            var metodo = metodoPago.ToLower().Trim();
            return metodo == "yape" || metodo == "plin";
        }

        public static string LimpiarTelefono(string telefono)
        {
            if (string.IsNullOrEmpty(telefono)) return string.Empty;

            return telefono.Replace(" ", "")
                          .Replace("-", "")
                          .Replace("(", "")
                          .Replace(")", "")
                          .Replace("+51", "")
                          .Trim();
        }

        public static List<string> ValidarPerfilCompleto(DTOs.ActualizarPerfilRequest request)
        {
            var errores = new List<string>();

            if (string.IsNullOrWhiteSpace(request.Nombre))
                errores.Add("El nombre es requerido");
            else if (request.Nombre.Length > 100)
                errores.Add("El nombre no puede exceder 100 caracteres");

            if (string.IsNullOrWhiteSpace(request.Telefono))
                errores.Add("El teléfono es requerido");
            else if (!EsTelefonoValido(request.Telefono))
                errores.Add("El formato del teléfono es inválido (debe ser 9XXXXXXXX)");

            if (!string.IsNullOrEmpty(request.Correo) && !EsEmailValido(request.Correo))
                errores.Add("El formato del email es inválido");

            if (!string.IsNullOrEmpty(request.Biografia) && request.Biografia.Length > 500)
                errores.Add("La biografía no puede exceder 500 caracteres");

            if (!string.IsNullOrEmpty(request.Genero) && request.Genero.Length > 20)
                errores.Add("El género no puede exceder 20 caracteres");

            return errores;
        }

        public static List<string> ValidarConfiguracionPago(DTOs.ConfigurarPagoRequest request)
        {
            var errores = new List<string>();

            if (!EsMetodoPagoValido(request.MetodoPago))
                errores.Add("El método de pago debe ser 'yape' o 'plin'");

            if (string.IsNullOrWhiteSpace(request.NombreTitular))
                errores.Add("El nombre del titular es requerido");
            else if (request.NombreTitular.Length > 150)
                errores.Add("El nombre del titular no puede exceder 150 caracteres");

            if (!EsTelefonoValido(request.NumeroTelefono))
                errores.Add("El número de teléfono es inválido");

            if (string.IsNullOrWhiteSpace(request.ImagenQR))
                errores.Add("La imagen QR es requerida");
            else if (!EsImagenBase64Valida(request.ImagenQR))
                errores.Add("El formato de la imagen QR es inválido");

            return errores;
        }

        public static bool EsEmailValido(string email)
        {
            if (string.IsNullOrEmpty(email)) return true;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public static bool EsImagenBase64Valida(string base64)
        {
            try
            {
                if (string.IsNullOrEmpty(base64)) return false;

                var base64Data = base64.Contains(',') ? base64.Split(',')[1] : base64;
                var bytes = Convert.FromBase64String(base64Data);

                const int minSize = 100;
                const int maxSize = 5 * 1024 * 1024;

                return bytes.Length >= minSize && bytes.Length <= maxSize;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string FormatearTelefono(string telefono)
        {
            var telefonoLimpio = LimpiarTelefono(telefono);

            if (telefonoLimpio.Length != 9) return telefono;

            return $"{telefonoLimpio.Substring(0, 3)} {telefonoLimpio.Substring(3, 3)} {telefonoLimpio.Substring(6, 3)}";
        }

        public static bool EsEdadValida(DateTime fechaNacimiento)
        {
            var edad = DateTime.Now.Year - fechaNacimiento.Year;
            if (fechaNacimiento.Date > DateTime.Now.AddYears(-edad)) edad--;

            return edad >= 13 && edad <= 120;
        }
    }
}