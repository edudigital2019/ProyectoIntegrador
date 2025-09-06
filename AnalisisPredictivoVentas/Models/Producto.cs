using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AnalisisPredictivoVentas.Models
{
    [Index(nameof(Codigo), IsUnique = true)]
    public class Producto
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Codigo { get; set; } = null!;

        [Required, StringLength(120)]
        public string Nombre { get; set; } = null!;

        [StringLength(60)] public string? Categoria { get; set; }

        [Precision(18, 2)] public decimal PrecioCosto { get; set; }
        [Precision(18, 2)] public decimal PrecioVenta { get; set; }

        public bool Activo { get; set; } = true;
    }

}
