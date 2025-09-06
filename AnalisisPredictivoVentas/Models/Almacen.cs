using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AnalisisPredictivoVentas.Models
{
    [Index(nameof(Codigo), IsUnique = true)]
    public class Almacen
    {
        public int Id { get; set; }
        [Required, StringLength(20)] public string Codigo { get; set; } = null!;
        [Required, StringLength(120)] public string Nombre { get; set; } = null!;
        public string? Direccion { get; set; }
    }

}
