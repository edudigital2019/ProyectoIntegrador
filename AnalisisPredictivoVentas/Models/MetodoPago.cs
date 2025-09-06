using System.ComponentModel.DataAnnotations;

namespace AnalisisPredictivoVentas.Models
{
    public class MetodoPago
    {
        public int Id { get; set; }
        [Required, StringLength(60)] public string Nombre { get; set; } = null!;
        public bool PermiteVuelto { get; set; } = true;
        public bool Activo { get; set; } = true;
    }

}
