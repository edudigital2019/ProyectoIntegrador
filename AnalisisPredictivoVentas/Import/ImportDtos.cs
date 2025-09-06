namespace AnalisisPredictivoVentas.Import
{
    public class ArchivoVentasDto
    {
        public string? ArchivoId { get; set; }
        public AlmacenDto? Almacen { get; set; }
        public EmpleadoDto? Responsable { get; set; }
        public List<VentaDto> Ventas { get; set; } = new();
    }

    public class VentaDto
    {
        public string? Numero { get; set; }
        public DateTime Fecha { get; set; }
        public ClienteDto? Cliente { get; set; }
        public AlmacenDto? Almacen { get; set; }
        public EmpleadoDto? Empleado { get; set; }
        public string? Observacion { get; set; }
        public List<VentaDetDto> Detalles { get; set; } = new();
        public List<VentaPagoDto> Pagos { get; set; } = new();
    }

    public class VentaDetDto
    {
        public ProductoDto Producto { get; set; } = new();
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal Subtotal { get; set; }
        public string? Marca { get; set; }
        public string? Categoria { get; set; }   // si viene null, puedes caer a Producto.Categoria
        public string? Genero { get; set; }
        public string? Color { get; set; }
        public string? Referencia { get; set; }
        public string? Talla { get; set; }
    }

    public class VentaPagoDto
    {
        public string Metodo { get; set; } = "Efectivo";
        public decimal Monto { get; set; }
    }

    public class ProductoDto
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Categoria { get; set; }
        public decimal? PrecioVenta { get; set; }
    }

    public class ClienteDto
    {
        public string? Identificacion { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Email { get; set; }
        public string? Telefono { get; set; }
    }

    public class AlmacenDto
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
    }

    public class EmpleadoDto
    {
        public string? UsuarioIdIdentity { get; set; }
        public string Nombre { get; set; } = null!;
    }

    // ---- CSV row (una fila = un detalle de venta) ----
    public class CsvVentaRow
    {
        public string? Numero { get; set; }
        public DateTime Fecha { get; set; }

        public string? AlmacenCodigo { get; set; }
        public string? AlmacenNombre { get; set; }

        public string? ClienteIdentificacion { get; set; }
        public string? ClienteNombre { get; set; }
        public string? ClienteEmail { get; set; }
        public string? ClienteTelefono { get; set; }

        public string? EmpleadoUsuarioId { get; set; }
        public string? EmpleadoNombre { get; set; }

        public string? ProductoCodigo { get; set; }
        public string? ProductoNombre { get; set; }
        public string? Categoria { get; set; }

        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal Subtotal { get; set; }

        // Pagos: dos modos
        public string? Pagos { get; set; }          // "Efectivo:50|Tarjeta:25"
        public string? MetodoPago { get; set; }     // alternativo si no usas Pagos
        public decimal? MontoPago { get; set; }

        public string? Observacion { get; set; }
    }

}
