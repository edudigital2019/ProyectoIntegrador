using System.ComponentModel.DataAnnotations;

namespace AnalisisPredictivoVentas.Models
{
    public class Empleado
    {
        public int Id { get; set; }
        [Required] public string UsuarioIdIdentity { get; set; } = null!; // si usas Identity
        [Required, StringLength(120)] public string Nombres { get; set; } = null!;
        public int? AlmacenId { get; set; }
        public Almacen? Almacen { get; set; }
    }

}
