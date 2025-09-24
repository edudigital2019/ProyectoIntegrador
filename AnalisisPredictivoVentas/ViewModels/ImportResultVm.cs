namespace AnalisisPredictivoVentas.ViewModels
{
    public class ImportResultVm
    {
        public string NombreArchivo { get; set; } = "";

        public int TotalProcesadas { get; set; }
        public int VentasInsertadas { get; set; }
        public int VentasActualizadas { get; set; }
        public int LineasDetalle { get; set; }
        public decimal TotalCantidad { get; set; }
        public decimal TotalBruto { get; set; }
        public decimal TotalDescuento { get; set; }
        public decimal TotalNeto { get; set; } 
        public decimal TotalPagos { get; set; }

        public DateTime? MinFecha { get; set; }
        public DateTime? MaxFecha { get; set; }
        public int SucursalesAfectadas { get; set; }

        public int ProductosUpsert { get; set; }
        public int ClientesUpsert { get; set; }
        public int AlmacenesUpsert { get; set; }
        public int MetodosPagoUpsert { get; set; }

        public Dictionary<string, decimal> PagosPorMetodo { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Errores { get; set; } = new();
    }
}
