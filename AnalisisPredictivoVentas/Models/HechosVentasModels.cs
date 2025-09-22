using System.ComponentModel.DataAnnotations;

namespace AnalisisPredictivoVentas.Models
{
    public class HechosVentasModels
    {
        [Key] public long Id { get; set; }
        public int VentaCabId { get; set; }
        public string Numero { get; set; } = null!;
        public DateTime Fecha { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public int? Semana { get; set; }
        public int AlmacenId { get; set; }
        public int ProductoId { get; set; }
        public int? ClienteId { get; set; }
        public int? EmpleadoId { get; set; }
        public string? Marca { get; set; }
        public string? Categoria { get; set; }
        public string? Genero { get; set; }
        public string? Color { get; set; }
        public string? Referencia { get; set; }
        public string? Talla { get; set; }
        public string MetodoPago { get; set; } = null!;
        public decimal Cantidad { get; set; }
        public decimal Neto { get; set; }
        public decimal Descuento { get; set; }
    }
}
