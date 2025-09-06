using System.ComponentModel.DataAnnotations;

namespace AnalisisPredictivoVentas.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        [Required, StringLength(120)] public string Nombre { get; set; } = null!;
        [StringLength(20)] public string? Identificacion { get; set; }
        [StringLength(120)] public string? Email { get; set; }
        [StringLength(20)] public string? Telefono { get; set; }
    }

}
