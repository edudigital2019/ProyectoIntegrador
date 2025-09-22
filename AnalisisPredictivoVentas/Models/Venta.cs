using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnalisisPredictivoVentas.Models
{
    [Index(nameof(Numero), nameof(AlmacenId), IsUnique = true)]
    public class VentaCab
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [StringLength(40)] public string? Numero { get; set; }

        public int? ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        public int? EmpleadoId { get; set; }
        public Empleado? Empleado { get; set; }

        public int AlmacenId { get; set; }
        public Almacen Almacen { get; set; } = null!;

        [Precision(18, 2)] public decimal Total { get; set; }
        public string? Observacion { get; set; }

        public ICollection<VentaDet> Detalles { get; set; } = new List<VentaDet>();
        public ICollection<VentaPago> Pagos { get; set; } = new List<VentaPago>();
    }

    public class VentaDet
    {
        public int Id { get; set; }

        public int VentaCabId { get; set; }
        public VentaCab VentaCab { get; set; } = null!;

        public int ProductoId { get; set; }
        public Producto Producto { get; set; } = null!;

        [Precision(18, 3)] public decimal Cantidad { get; set; }
        [Precision(18, 2)] public decimal PrecioUnitario { get; set; }
        [Precision(18, 2)] public decimal Descuento { get; set; }

        // ← este es el “neto de línea” que se guarda en la BD
        [Precision(18, 2)] public decimal Subtotal { get; set; }

        // Estas columnas no existen en la tabla VentasDet: ignóralas o elimínalas.
        public string? Marca { get; set; }
        public string? Categoria { get; set; }
        public string? Genero { get; set; }
        public string? Color { get; set; }
        public string? Referencia { get; set; }
        public string? Talla { get; set; }
    }

    public class VentaPago
    {
        public int Id { get; set; }
        public int VentaCabId { get; set; }
        public VentaCab VentaCab { get; set; } = null!;

        public int MetodoPagoId { get; set; }
        public MetodoPago MetodoPago { get; set; } = null!;

        [Precision(18, 2)] public decimal Monto { get; set; }
    }

}
