namespace AnalisisPredictivoVentas.ViewModels
{
    public class ImportResultVm
    {
        public string NombreArchivo { get; set; } = "";
        public int VentasInsertadas { get; set; }
        public int VentasOmitidasDuplicadas { get; set; }
        public int ProductosUpsert { get; set; }
        public int ClientesUpsert { get; set; }
        public int AlmacenesUpsert { get; set; }
        public int MetodosPagoUpsert { get; set; }
        public List<string> Errores { get; set; } = new();
    }

}
